using Blockfrost.Api.Extensions;
using Blockfrost.Api.Http;
using Blockfrost.Api.Options;
using Blockfrost.Api.Services;
using Microsoft.EntityFrameworkCore;
using Minedu.VC.Issuer.Data;
using Minedu.VC.Issuer.Data.Repositories;
using Minedu.VC.Issuer.Models;
using Minedu.VC.Issuer.Serialization;
using Minedu.VC.Issuer.Services;
using Minedu.VC.Issuer.Services.Auth;
using Minedu.VC.Issuer.Services.Cardano;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ===========================================
// 🔧 SERILOG CONFIG ROBUSTA (con fallback)
// ===========================================
// Cargar configuraciones desde appsettings.json
var logConfig = builder.Configuration.GetSection("Logging");

// Obtener ruta de logs desde la configuración
var logPath = logConfig.GetValue<string>("LogPath");

// Intentar crear el directorio de logs
try
{
    // Crear el directorio si no existe
    Directory.CreateDirectory(logPath);

    // Test de escritura
    var testFile = Path.Combine(logPath, "write-test.txt");
    File.WriteAllText(testFile, $"Write OK at {DateTime.Now}");

    Log.Information("✔ Escritura exitosa en la ruta de logs: {LogPath}", logPath);
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ No se pudo escribir en la ruta de logs configurada: {LogPath}", logPath);
    throw new InvalidOperationException("No se puede escribir en la ruta de logs. Verifica los permisos.", ex);
}

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logPath, "issuer-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true)
    .CreateLogger();

Log.Information("✔ Serilog inicializado OK. Carpeta Log final = {Dir}", logPath);
Log.Information("✔ AppBaseDirectory = {BaseDir}", AppContext.BaseDirectory);

builder.Host.UseSerilog();

builder.Services.AddDbContext<MineduDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

Log.Information("✔ Cadena de conexión PostgreSQL = {DefaultConnection}", builder.Configuration.GetConnectionString("DefaultConnection"));

var network = builder.Configuration["Blockfrost:Network"] ?? "preprod";
var apiKey = builder.Configuration["Blockfrost:ApiKey"];

Log.Information("✔ Carga variables network y apiKey de Blockfrost. | network={network} | apiKey={apiKey} ", network, apiKey);

builder.Services.AddSingleton<string>(apiKey);

builder.Services.AddTransient<BlockfrostAuthorizationHandler>(sp =>
{
    var key = sp.GetRequiredService<string>();
    return new BlockfrostAuthorizationHandler(key);
});

builder.Services.AddBlockfrost(network, apiKey);

Log.Information("✔ Carga servicio de Blockfrost.");

builder.Services.AddScoped<CardanoTxGenerator>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var mnemonic = config["Cardano:Mnemonic"];
    var cardano = sp.GetRequiredService<ICardanoService>();
    var logger = sp.GetRequiredService<ILogger<CardanoTxGenerator>>();
    return new CardanoTxGenerator(mnemonic, cardano, logger);
});

Log.Information("✔ Carga servicio CardanoTxGenerator.");

// --- Registrar dependencias ---
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<StatusListRepository>();
builder.Services.AddScoped<RequestService>();
builder.Services.AddScoped<StatusListService>();
builder.Services.AddScoped<VCBuilder>();
builder.Services.AddScoped<DidWebResolver>();
builder.Services.AddScoped<SignatureService>();
builder.Services.AddScoped<CardanoTxSubmitter>();
builder.Services.AddScoped<CardanoAnchorService>();
builder.Services.AddScoped<IVerifiableCredentialRepository, VerifiableCredentialRepository>();
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddSingleton<AuthorizationService>();
builder.Services.AddSingleton<CredentialOfferService>();
Log.Information("✔ Pasa registro de dependencias.");

// Add services to the container.
builder.Services.AddControllers();
Log.Information("✔ Pasa registro de controllers.");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
Log.Information("✔ Pasa registro de SwaggerGen.");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new DateTimeAsIsoUtcConverter());
});

// Cargar clave Ed25519 desde archivo local
//var privateKey = Convert.FromBase64String(File.ReadAllText("Keys/issuer-key.json"));

// Leer y parsear el archivo JSON
var keyJson = File.ReadAllText("Keys/issuer-key.json");
var keyData = JsonSerializer.Deserialize<IssuerKey>(keyJson);
Log.Information("✔ Pasa carga de llaves criptograficas en Keys/issuer-key.json. | keyJson={keyJson}", keyJson);

// Quitar el prefijo 'z' del formato multibase (z = base58btc)
var multibase = keyData!.PrivateKeyMultibase;
if (multibase.StartsWith("z"))
    multibase = multibase.Substring(1);

// Decodificar Base58 a bytes
var privateKey = SimpleBase.Base58.Bitcoin.Decode(multibase).ToArray();


//builder.Services.AddSingleton(new SignatureService(privateKey));
Log.Information("✔ Previo a realizar builder.Build()");
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<BlockfrostAuthorizationHandler>();
    Log.Information("Handler creado OK!");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

Log.Information("✔ Pasa configuración del pipeline de solicitudes HTTP");

// ===========================================================
// ANCLAR LISTA DE ESTADOS VACÍA AL INICIAR EL API
// ===========================================================
// Si se desea, podrías solo verificar existencia sin anclar
using (var scope = app.Services.CreateScope())
{
    Log.Information("✔ Previo a inicializar la LISTA DE ESTADOS con metodo EnsureListExistsAsync de StatusListService.");
    var statusSvc = scope.ServiceProvider.GetRequiredService<StatusListService>();
    await statusSvc.EnsureListExistsAsync();
    Log.Information("✔ Se asegura la existencia de la LISTA DE ESTADOS.");
}

try
{
    app.Run();
    Log.Information("✔ Issuer API iniciado. Entorno: {Env}", app.Environment.EnvironmentName);
}
catch (Exception ex)
{
    Log.Fatal(ex, "El API del Issuer falló durante el inicio");
}
finally
{
    Log.CloseAndFlush();
}