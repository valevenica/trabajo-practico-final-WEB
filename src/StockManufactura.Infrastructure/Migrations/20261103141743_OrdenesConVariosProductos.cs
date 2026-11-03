using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockManufactura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrdenesConVariosProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrdenesProduccionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrdenProduccionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenesProduccionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdenesProduccionItems_OrdenesProduccion_OrdenProduccionId",
                        column: x => x.OrdenProduccionId,
                        principalTable: "OrdenesProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrdenesProduccionItems_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesProduccionItems_OrdenProduccionId",
                table: "OrdenesProduccionItems",
                column: "OrdenProduccionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesProduccionItems_ProductoId",
                table: "OrdenesProduccionItems",
                column: "ProductoId");

            // Cada orden existente pasa a tener una línea con su producto y cantidad. El Id sigue el formato de Guid
            // (mayúsculas con guiones) con el que EF guarda las claves en SQLite.
            migrationBuilder.Sql(@"
INSERT INTO OrdenesProduccionItems (Id, OrdenProduccionId, ProductoId, Cantidad, CreatedAt, UpdatedAt, IsDeleted)
SELECT upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-'
             || substr('89AB', 1 + (abs(random()) % 4), 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
       Id, ProductoId, CantidadPlaneada, CreatedAt, UpdatedAt, 0
FROM OrdenesProduccion;");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesProduccion_Productos_ProductoId",
                table: "OrdenesProduccion");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesProduccion_ProductoId",
                table: "OrdenesProduccion");

            migrationBuilder.DropColumn(
                name: "CantidadPlaneada",
                table: "OrdenesProduccion");

            migrationBuilder.DropColumn(
                name: "ProductoId",
                table: "OrdenesProduccion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CantidadPlaneada",
                table: "OrdenesProduccion",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductoId",
                table: "OrdenesProduccion",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesProduccion_ProductoId",
                table: "OrdenesProduccion",
                column: "ProductoId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesProduccion_Productos_ProductoId",
                table: "OrdenesProduccion",
                column: "ProductoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Al volver atrás, cada orden conserva su primer producto y la cantidad total.
            migrationBuilder.Sql(@"
UPDATE OrdenesProduccion SET
    ProductoId = (SELECT i.ProductoId FROM OrdenesProduccionItems i WHERE i.OrdenProduccionId = OrdenesProduccion.Id ORDER BY i.CreatedAt LIMIT 1),
    CantidadPlaneada = COALESCE((SELECT SUM(i.Cantidad) FROM OrdenesProduccionItems i WHERE i.OrdenProduccionId = OrdenesProduccion.Id), 0);");

            migrationBuilder.DropTable(
                name: "OrdenesProduccionItems");
        }
    }
}
