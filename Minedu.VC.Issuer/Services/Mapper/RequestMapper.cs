using Humanizer;
using Minedu.VC.Issuer.Data.Entities;
using Minedu.VC.Issuer.Models.Dto;
using System.Text.RegularExpressions;

namespace Minedu.VC.Issuer.Services.Mapper
{
    public class RequestMapper
    {
        /// <summary>
        /// Maps the persistence aggregate into a clean DTO aggregate for the VC builder.
        /// </summary>
        public static RequestAggregateDto ToAggregate(RequestEntity src)
        {
            if (src is null) throw new ArgumentNullException(nameof(src));

            // ---- Student ----
            var studentDto = src.Estudiante == null ? null : new StudentDto
            {
                Id = src.Estudiante.Id,
                TipoDocumento = src.Estudiante.TipoDocumento,
                NumeroDocumento = src.Estudiante.NumeroDocumento,
                ApellidoPaterno = src.Estudiante.ApellidoPaterno,
                ApellidoMaterno = src.Estudiante.ApellidoMaterno,
                Nombres = src.Estudiante.Nombres
            };

            // ---- Enrollments (each with its own notes) ----
            var enrollments = (src.Grados ?? Array.Empty<EnrollmentEntity>())
                .OrderBy(g => g.Anio ?? 0)
                .ThenBy(g => ParseGrado(g.IdGrado, g.GradoDescripcion))
                .Select(g => new EnrollmentDto
                {
                    IdSolicitud = g.IdSolicitud,
                    IdNivel = g.IdNivel,
                    IdGrado = g.IdGrado,
                    GradoDescripcion = g.GradoDescripcion,
                    Anio = g.Anio,
                    CodigoModular = g.CodigoModular,
                    Anexo = g.Anexo,
                    SituacionFinal = g.SituacionFinal,

                    // Parsed numeric grade to sort and feed the VC (1..6, etc.)
                    GradoNumero = ParseGrado(g.IdGrado, g.GradoDescripcion),

                    // Notes ONLY for this enrollment (thanks to the composite FK in EF)
                    Notas = (g.Notas ?? new List<GradeEntity>())
                        .OrderBy(n => n.Area)
                        .Select(n => new GradeDto
                        {
                            Id = n.Id,
                            // Entity names vs DTO names:
                            //  - Entity.Competencia (DSC_ASIGNATURA) → DTO.Asignatura
                            //  - Entity.Area (DSC_AREA) → DTO.Area
                            Competencia = n.Competencia,
                            Area = n.Area,
                            TipoArea = n.TipoArea,
                            Calificacion = n.Calificacion,
                            SolicitudId = n.SolicitudId,

                            // Grouping keys on the note (useful for diagnostics/UI and future joins)
                            Anio = n.Anio,
                            CodigoModular = n.CodigoModular,
                            IdGrado = g.IdGrado,
                            GradoDescripcion = g.GradoDescripcion
                        })
                        .ToList()
                })
                .ToList();

            // ---- Observations ----
            var observations = (src.Observaciones ?? Array.Empty<ObservationEntity>())
                .Select(o => new ObservationDto
                {
                    Id = o.Id,
                    SolicitudId = o.SolicitudId,
                    Resolucion = o.Resolucion,
                    Motivo = o.Motivo,
                    TipoSolicitud = o.TipoSolicitud,
                    Anio = o.ID_ANIO
                })
                .ToList();

            var dto = new RequestAggregateDto
            {
                // CERT_SOLICITUD
                SolicitudId = src.Id,
                EstudianteId = src.EstudianteId,
                IdModalidad = src.IdModalidad,
                Modalidad = src.Modalidad,
                IdNivel = src.IdNivel,
                Nivel = src.Nivel,
                IdGrado = src.IdGrado,
                GradoDescripcion = src.GradoDescripcion,
                CodigoVirtual = src.CodigoVirtual,
                Director = src.Director,

                // CERT_ESTUDIANTE
                Student = studentDto,

                // CERT_GRADO y CERT_NOTA
                Enrollments = enrollments,

                Observations = observations
            };

            return dto;
        }

        /// <summary>
        /// Extracts a numeric grade from IdGrado or GradoDescripcion.
        /// Priority: parse IdGrado int → first digits in description → Spanish ordinals.
        /// Returns 0 if nothing matches.
        /// </summary>
        private static int ParseGrado(string? idGrado, string? gradoDescripcion)
        {
            if (!string.IsNullOrWhiteSpace(idGrado) && int.TryParse(idGrado.Trim(), out var g1))
                return g1;

            if (!string.IsNullOrWhiteSpace(gradoDescripcion))
            {
                var match = Regex.Match(gradoDescripcion, @"\d+");
                if (match.Success && int.TryParse(match.Value, out var g2))
                    return g2;

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
    }
}
