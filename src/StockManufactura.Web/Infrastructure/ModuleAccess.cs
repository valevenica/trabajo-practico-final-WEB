namespace StockManufactura.Web.Infrastructure
{
    /// <summary>
    /// Reglas de ingreso por módulo: basta con uno de los permisos listados; los
    /// módulos sin entrada son de acceso libre para cualquier usuario autenticado.
    /// </summary>
    public static class ModuleAccess
    {
        public const string Dashboard = "Dashboard";
        public const string BusinessIntelligence = "BusinessIntelligence";
        public const string Shortages = "Shortages";
        public const string Products = "Products";
        public const string Resources = "Resources";
        public const string Providers = "Providers";
        public const string Clients = "Clients";
        public const string ProductionOrders = "ProductionOrders";
        public const string StockIntake = "StockIntake";
        public const string SpecialResources = "SpecialResources";
        public const string PriceImport = "PriceImport";
        public const string Monetary = "Monetary";
        public const string CostHistory = "CostHistory";
        public const string Audit = "Audit";
        public const string Backups = "Backups";
        public const string Users = "Users";

        private static readonly Dictionary<string, (string Nombre, string[] Permisos)> Modulos = new()
        {
            [BusinessIntelligence] = ("el tablero de inteligencia de negocio", new[] { "COSTOS_VER" }),
            [Shortages] = ("la pantalla de faltantes", new[] { "STOCK_VER" }),
            [Products] = ("la gestión de productos", new[] { "PRODUCTOS_VER", "PRODUCTOS_CREAR", "PRODUCTOS_EDITAR" }),
            [Resources] = ("la gestión de insumos", new[] { "PRODUCTOS_EDITAR" }),
            [Providers] = ("la gestión de proveedores", new[] { "PRODUCTOS_EDITAR", "USUARIOS_ADMIN" }),
            [Clients] = ("la gestión de clientes", new[] { "PRODUCTOS_EDITAR", "USUARIOS_ADMIN" }),
            [ProductionOrders] = ("las órdenes de producción", new[] { "PRODUCTOS_EDITAR" }),
            [StockIntake] = ("los ingresos de stock", new[] { "STOCK_AJUSTAR" }),
            [SpecialResources] = ("los insumos especiales", new[] { "PRODUCTOS_EDITAR" }),
            [PriceImport] = ("la carga masiva de precios", new[] { "PRODUCTOS_EDITAR" }),
            [Monetary] = ("la configuración monetaria", new[] { "COSTOS_VER" }),
            [CostHistory] = ("el historial de costos", new[] { "COSTOS_VER" }),
            [Audit] = ("la bitácora de auditoría", new[] { "USUARIOS_ADMIN" }),
            [Backups] = ("las copias de seguridad", new[] { "USUARIOS_ADMIN" }),
            [Users] = ("la administración de usuarios", new[] { "USUARIOS_ADMIN" }),
        };

        public static string[]? Permisos(string modulo) => Modulos.TryGetValue(modulo, out var regla) ? regla.Permisos : null;

        public static string Nombre(string modulo) => Modulos.TryGetValue(modulo, out var regla) ? regla.Nombre : "este módulo";
    }
}
