// File: Infrastructure/Serialization/DateTimeAsIsoUtcConverter.cs
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Minedu.VC.Issuer.Serialization
{
    // Forces DateTime to serialize in UTC with trailing 'Z'
    public sealed class DateTimeAsIsoUtcConverter : JsonConverter<DateTime>
    {
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        }

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            if (DateTime.TryParse(s, null,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            // Fallback: parse and normalize to UTC
            return DateTime.SpecifyKind(DateTime.Parse(s), DateTimeKind.Utc).ToUniversalTime();
        }
    }
}
