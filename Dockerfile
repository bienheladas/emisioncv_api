# ── Stage 1: build ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar dependencias (cacheado si el .csproj no cambia)
COPY Minedu.VC.Issuer/Minedu.VC.Issuer.csproj Minedu.VC.Issuer/
RUN dotnet restore Minedu.VC.Issuer/Minedu.VC.Issuer.csproj

# Copiar el resto del proyecto y publicar
COPY Minedu.VC.Issuer/ Minedu.VC.Issuer/
RUN dotnet publish Minedu.VC.Issuer/Minedu.VC.Issuer.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: runtime ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Crear carpetas necesarias en tiempo de imagen
RUN mkdir -p /app/logs/Data

# Copiar artefactos publicados
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Minedu.VC.Issuer.dll"]
