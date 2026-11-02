using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StockManufactura.Domain.Entities;
using StockManufactura.Infrastructure.Db;

namespace StockManufactura.Web.Infrastructure
{
    /// <summary>
    /// Restablece la base de datos a su estado inicial: borra el archivo, lo vuelve a crear con las migraciones y siembra los
    /// roles y el usuario administrador base. La página que lo invoca genera antes una copia de seguridad.
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
            // Libera el archivo aunque otras páginas hayan abierto conexiones antes.
            SqliteConnection.ClearAllPools();
            foreach (var sufijo in new[] { string.Empty, "-wal", "-shm", "-journal" })
            {
                var ruta = _databasePath + sufijo;
                if (File.Exists(ruta))
                {
                    File.Delete(ruta);
                }
            }

            var opciones = new DbContextOptionsBuilder<StockManufacturaDbContext>().UseSqlite($"Data Source={_databasePath}").Options;
            using (var context = new StockManufacturaDbContext(opciones))
            {
                context.Database.Migrate();
                DatabaseSeeder.EnsureSeed(context);
                // La bitácora se borró con la base: el restablecimiento queda como su primer registro.
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
