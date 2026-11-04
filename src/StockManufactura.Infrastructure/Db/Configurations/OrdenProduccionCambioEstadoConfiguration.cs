using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Infrastructure.Db.Configurations
{
    public sealed class OrdenProduccionCambioEstadoConfiguration : IEntityTypeConfiguration<OrdenProduccionCambioEstado>
    {
        public void Configure(EntityTypeBuilder<OrdenProduccionCambioEstado> builder)
        {
            builder.ToTable("OrdenesProduccionCambiosEstado");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.EstadoAnterior).HasConversion<int?>();
            builder.Property(x => x.EstadoNuevo).HasConversion<int>().IsRequired();
            builder.Property(x => x.Fecha).IsRequired();
            builder.Property(x => x.Usuario).IsRequired().HasMaxLength(100);

            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.UpdatedAt).IsRequired();
            builder.Property(x => x.IsDeleted).HasDefaultValue(false);

            builder.HasIndex(x => new { x.OrdenProduccionId, x.Fecha });

            // Si se elimina una orden (solo en borrador, planificada o cancelada), se va con su historial.
            builder.HasOne<OrdenProduccion>()
                .WithMany()
                .HasForeignKey(x => x.OrdenProduccionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
