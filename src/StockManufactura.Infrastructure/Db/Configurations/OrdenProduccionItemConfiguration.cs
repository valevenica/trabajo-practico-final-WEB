using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Infrastructure.Db.Configurations
{
    public sealed class OrdenProduccionItemConfiguration : IEntityTypeConfiguration<OrdenProduccionItem>
    {
        public void Configure(EntityTypeBuilder<OrdenProduccionItem> builder)
        {
            builder.ToTable("OrdenesProduccionItems");
            builder.HasKey(x => x.Id);

            // El Id lo genera el dominio: así EF da de alta como nuevas las líneas que se agregan a una orden existente.
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Cantidad).IsRequired().HasColumnType("decimal(18,4)");

            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.UpdatedAt).IsRequired();
            builder.Property(x => x.IsDeleted).HasDefaultValue(false);

            builder.HasIndex(x => x.OrdenProduccionId);
            builder.HasIndex(x => x.ProductoId);

            builder.HasOne<Producto>()
                .WithMany()
                .HasForeignKey(x => x.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
