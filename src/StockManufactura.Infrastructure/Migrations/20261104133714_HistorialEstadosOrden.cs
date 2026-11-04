using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockManufactura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HistorialEstadosOrden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstadoAntesDeCancelar",
                table: "OrdenesProduccion",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrdenesProduccionCambiosEstado",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrdenProduccionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EstadoAnterior = table.Column<int>(type: "INTEGER", nullable: true),
                    EstadoNuevo = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenesProduccionCambiosEstado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdenesProduccionCambiosEstado_OrdenesProduccion_OrdenProduccionId",
                        column: x => x.OrdenProduccionId,
                        principalTable: "OrdenesProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesProduccionCambiosEstado_OrdenProduccionId_Fecha",
                table: "OrdenesProduccionCambiosEstado",
                columns: new[] { "OrdenProduccionId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdenesProduccionCambiosEstado");

            migrationBuilder.DropColumn(
                name: "EstadoAntesDeCancelar",
                table: "OrdenesProduccion");
        }
    }
}
