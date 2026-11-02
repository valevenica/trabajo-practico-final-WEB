using System;
using System.IO;

namespace StockManufactura.Shared
{
    public static class AppPaths
    {
        public const string AppFolderName = "StockManufactura";

        // Datos del sistema web: Documentos\StockManufactura\Web.
        public static string DocumentsRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppFolderName, "Web");

        public static string DataDirectory => Path.Combine(DocumentsRoot, "Data");
        public static string LogsDirectory => Path.Combine(DocumentsRoot, "Logs");
        public static string BackupsDirectory => Path.Combine(DocumentsRoot, "Backups");
        public static string AssetsDirectory => Path.Combine(DocumentsRoot, "Assets");
        public static string DatabaseFilePath => Path.Combine(DataDirectory, "StockManufactura.db");
        public static string SplashLogoPath => Path.Combine(AssetsDirectory, "logo.png");
        public static string StartupHealthcheckLogPath => Path.Combine(LogsDirectory, "startup-healthcheck.log");
        public static string ApplicationLogPath => Path.Combine(LogsDirectory, "StockManufactura.log");
    }
}