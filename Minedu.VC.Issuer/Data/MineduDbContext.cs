using Microsoft.EntityFrameworkCore;
using Minedu.VC.Issuer.Data.Entities;

namespace Minedu.VC.Issuer.Data
{
    public class MineduDbContext : DbContext
    {
        public MineduDbContext(DbContextOptions<MineduDbContext> options)
            : base(options) { }

        public DbSet<StudentEntity> Estudiantes { get; set; } = null!;
        public DbSet<RequestEntity> Solicitudes { get; set; } = null!;
        public DbSet<EnrollmentEntity> Grados { get; set; } = null!;
        public DbSet<GradeEntity> Notas { get; set; } = null!;
        public DbSet<ObservationEntity> Observaciones { get; set; } = null!;
        public DbSet<VerifiableCredentialEntity> AnchoredCredentials { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Relaciones: un Estudiante tiene muchas Solicitudes
            modelBuilder.Entity<RequestEntity>()
                .HasOne(r => r.Estudiante)
                .WithMany(e => e.Solicitudes)
                .HasForeignKey(r => r.EstudianteId);

            // Una Solicitud tiene muchos Grados
            modelBuilder.Entity<EnrollmentEntity>()
                .HasOne(e => e.Solicitud)
                .WithMany(r => r.Grados)
                .HasForeignKey(e => e.IdSolicitud);

            // Clave alterna en CERT_GRADO para poder colgar las notas por (Solicitud, Año, CodMod)
            modelBuilder.Entity<EnrollmentEntity>()
                .HasAlternateKey(e => new { e.IdSolicitud, e.Anio, e.CodigoModular });

            // Un Grado tiene muchas Notas
            modelBuilder.Entity<GradeEntity>()
                .HasOne(n => n.Grado)
                .WithMany(g => g.Notas)
                .HasForeignKey(n => new { n.SolicitudId, n.Anio, n.CodigoModular })
                .HasPrincipalKey(g => new { g.IdSolicitud, g.Anio, g.CodigoModular}); 

            // Una Solicitud puede tener muchas Observaciones
            modelBuilder.Entity<ObservationEntity>()
                .HasOne(o => o.Solicitud)
                .WithMany(r => r.Observaciones)
                .HasForeignKey(o => o.SolicitudId);

            base.OnModelCreating(modelBuilder);
        }
    }
}

