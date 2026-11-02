using Microsoft.EntityFrameworkCore;
using StockManufactura.Domain.Entities;
using StockManufactura.Infrastructure.Db;

namespace StockManufactura.Web.Infrastructure
{
    /// <summary>
    /// Restablece la base de datos a su estado inicial: vacía todas las tablas y vuelve a sembrar los roles, el
    /// usuario administrador base y los datos de ejemplo, como en una instalación nueva. La página que lo invoca genera antes una copia de seguridad.
    /// </summary>
    public sealed class DatabaseResetService
    {
        public const string FraseConfirmacion = "BORRAR TODO";

        private readonly string _databasePath;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _services;

        public DatabaseResetService(string databasePath, IConfiguration configuration, IServiceProvider services)
        {
            _databasePath = databasePath;
            _configuration = configuration;
            _services = services;
        }

        public void Restablecer(string usuario)
        {
            var opciones = new DbContextOptionsBuilder<StockManufacturaDbContext>().UseSqlite($"Data Source={_databasePath}").Options;
            using (var context = new StockManufacturaDbContext(opciones))
            {
                context.Database.Migrate();

                // Se vacían las tablas en lugar de borrar el archivo: con la aplicación en uso, Windows no elimina un archivo
                // abierto y la base «nueva» seguiría siendo la anterior. El historial de migraciones se conserva.
                var conexion = context.Database.GetDbConnection();
                conexion.Open();
                var tablas = new List<string>();
                using (var consulta = conexion.CreateCommand())
                {
                    consulta.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory'";
                    using var lector = consulta.ExecuteReader();
                    while (lector.Read())
                    {
                        tablas.Add(lector.GetString(0));
                    }
                }

                using (var borrado = conexion.CreateCommand())
                {
                    borrado.CommandText = "PRAGMA foreign_keys = OFF; "
                        + string.Concat(tablas.Select(t => $"DELETE FROM \"{t}\"; "))
                        + "PRAGMA foreign_keys = ON;";
                    borrado.ExecuteNonQuery();
                }

                DatabaseSeeder.EnsureSeed(context);
                // La bitácora se vació con el resto: el restablecimiento queda como su primer registro.
                context.AuditLogs.Add(new AuditLog
                {
                    FechaHora = DateTime.UtcNow,
                    Usuario = usuario,
                    Modulo = "Administracion",
                    Accion = "ResetearBaseDatos",
                    Entidad = "Sistema",
                    IdEntidad = "-",
                    Descripcion = "Se restableció la base de datos a su estado inicial (borrado completo de datos de negocio; se recrearon los roles y el usuario administrador base).",
                    Equipo = WebSession.Equipo
                });
                context.SaveChanges();
            }

            if (DemoSetup.Habilitada(_configuration))
            {
                DemoSetup.Preparar(_services);
            }
        }
    }
}
