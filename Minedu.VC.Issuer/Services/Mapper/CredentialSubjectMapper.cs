using Minedu.VC.Issuer.Models;
using Minedu.VC.Issuer.Models.Dto;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;

namespace Minedu.VC.Issuer.Services.Mapper
{
    public class CredentialSubjectMapper
    {
        /// <summary>
        /// Maps the clean aggregate (DB-facing) to the VC-facing CredentialSubject.
        /// Keep only what truly belongs to the subject. 
        /// Non-subject fields (Director, CodigoVirtual, Observaciones) are returned in IssueContext.
        /// </summary>
        public static CredentialSubject ToSubject(RequestAggregateDto a)
        {
            if (a is null) throw new ArgumentNullException(nameof(a));

            // --- Identity (Titular) ---
            var titular = new Titular
            {
                Nombres = a.Student?.Nombres ?? string.Empty,
                Apellidos = BuildApellidos(a.Student?.ApellidoPaterno, a.Student?.ApellidoMaterno),
                TipoDocumento = MapTipoDocumento(a.Student?.TipoDocumento ?? string.Empty),
                NumeroDocumento = a.Student?.NumeroDocumento ?? string.Empty
            };

            // --- Grade (GradoConcluido) ---
            // Current issuance flow expects one enrollment per request, but CredentialSubject supports many.
            // We build a single-item list if Enrollment is present.
            var gradosConcluidos = (a.Enrollments ?? Enumerable.Empty<EnrollmentDto>())
                .OrderBy(e => e.Anio ?? 0)
                .ThenBy(e => e.GradoNumero)
                .Select(e => new GradoConcluido 
                { 
                    Grado       = ParseGrado(e.IdGrado, e.GradoDescripcion),
                    Anio        = e.Anio ?? 0,
                    CodigoModular = e.CodigoModular?.Trim() ?? string.Empty,
                    Anexo       = e.Anexo ?? string.Empty,
                    SituacionFinal = e.SituacionFinal?.Trim() ?? string.Empty,
                    Notas          = (e.Notas ?? Enumerable.Empty<GradeDto>()).Select(MapNota).ToList()
                })
                .ToList();

            // --- Build CredentialSubject ---
            var subject = new CredentialSubject
            {
                Modalidad = a.IdModalidad ?? string.Empty,
                Nivel = a.IdNivel ?? string.Empty,
                CodigoVirtual = a.CodigoVirtual ?? Guid.NewGuid(),
                Titular = titular,
                GradosConcluidos = gradosConcluidos
            };

            return subject;
        }

        // ---------- helpers ----------

        private static string BuildApellidos(string? apPaterno, string? apMaterno)
        {
            var p = (apPaterno ?? string.Empty).Trim();
            var m = (apMaterno ?? string.Empty).Trim();
            var full = $"{p} {m}".Trim();
            return string.IsNullOrWhiteSpace(full) ? string.Empty : full;
        }

        /// <summary>
        /// Attempts to parse a numeric grade from IdGrado or GradoDescripcion.
        /// Fallback: returns 0 if not found.
        /// </summary>
        private static int ParseGrado(string? idGrado, string? gradoDescripcion)
        {

            // 2) extract first integer from description (e.g., "Quinto grado" -> 5)
            if (!string.IsNullOrWhiteSpace(gradoDescripcion))
            {
                var match = Regex.Match(gradoDescripcion, @"\d+");
                if (match.Success && int.TryParse(match.Value, out var g2))
                    return g2;

                // optional: map Spanish ordinals if needed (e.g., "PRIMERO", "SEGUNDO", etc.)
                var norm = gradoDescripcion.Trim().ToLowerInvariant();
                if (norm.Contains("primero")) return 1;
                if (norm.Contains("segundo")) return 2;
                if (norm.Contains("tercero")) return 3;
                if (norm.Contains("cuarto")) return 4;
                if (norm.Contains("quinto")) return 5;
                if (norm.Contains("sexto")) return 6;
            }
            
            return 0;
        }

        private static string MapTipoDocumento(string raw) 
        {
            if (!int.TryParse(raw?.Trim(), out var code))
                throw new ArgumentOutOfRangeException(nameof(raw), raw, "El tipo de documento debe ser un numero: 1, 2, 3, o 4.");

            return code switch
            {
                1 => "CodigoEstudiante",
                2 => "DNI",
                3 => "Pasaporte",
                4 => "CarnetExtranjeria",
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Tipo de documento no soportado.")
            };
        }

        private static Nota MapNota(GradeDto n)
        {
            // Normalize inputs
            var area = NullIfWhite(n.Area);
            var competencia = NullIfWhite(n.Competencia);
            var tipoArea = NullIfWhite(n.TipoArea);

            // Case-insensitive match helpers
            static bool Eq(string? a, string b) =>
                !string.IsNullOrWhiteSpace(a) &&
                a.Trim().Equals(b, StringComparison.OrdinalIgnoreCase);

            var nota = new Nota
            {
                // Always keep a sane grade string
                Calificacion = string.IsNullOrWhiteSpace(n.Calificacion) ? "-" : n.Calificacion.Trim()
            };

            // 1) Transversal competencies: put subject into CompetenciaTransversal
            if (Eq(tipoArea, "COMPETENCIAS TRANSVERSALES"))
            {
                // Area is not specified by the rule; keep it if present (harmless for downstream)
                nota.Area = null;
                nota.Competencia = null;
                nota.CompetenciaTransversal = competencia;
                return nota;
            }

            // 2) Official area: Asignatura -> Competencia, Area -> Area
            if (Eq(tipoArea, "AREA OFICIAL"))
            {
                nota.Area = area;
                nota.Competencia = competencia;
                nota.CompetenciaTransversal = null;
                return nota;
            }

            // 3) Fallback (legacy heuristic): keep previous mapping logic
            nota.Area = area ?? competencia;
            nota.Competencia = competencia;
            nota.CompetenciaTransversal = tipoArea;
            return nota;
        }

        private static string? NullIfWhite(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}