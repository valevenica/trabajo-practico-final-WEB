using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using StockManufactura.Application.Backups;
using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Infrastructure.Cloud
{
    /// <summary>
    /// Sincronizacion bidireccional de copias de seguridad contra un servicio en la nube,
    /// usando como punto de intercambio la carpeta que el cliente de sincronización del proveedor
    /// (Google Drive, OneDrive) replica automaticamente contra el servidor remoto.
    ///
    /// Este enfoque mantiene el sistema operativo sin conexion: si la carpeta no esta disponible
    /// la sincronizacion se omite y la aplicacion sigue funcionando con las copias locales.
    /// </summary>
    public sealed class FolderCloudBackupSyncService : ICloudBackupSyncService
    {
        private const string BackupPattern = "backup-*.zip";

        public string ProviderName => "Carpeta sincronizada en la nube";

        public Task<bool> IsAvailableAsync(BackupSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TryResolveCloudFolder(settings, out _));
        }

        public Task<bool> TryUploadAsync(BackupRecord backupRecord, BackupSettings settings, CancellationToken cancellationToken = default)
        {
            if (!TryResolveCloudFolder(settings, out var cloudFolder))
            {
                return Task.FromResult(false);
            }

            if (string.IsNullOrWhiteSpace(backupRecord.RutaArchivo) || !File.Exists(backupRecord.RutaArchivo))
            {
                return Task.FromResult(false);
            }

            try
            {
                var destination = Path.Combine(cloudFolder, Path.GetFileName(backupRecord.RutaArchivo));
                File.Copy(backupRecord.RutaArchivo, destination, overwrite: true);
                return Task.FromResult(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Task.FromResult(false);
            }
        }

        public Task<IReadOnlyList<CloudBackupItem>> ListRemoteAsync(BackupSettings settings, CancellationToken cancellationToken = default)
        {
            if (!TryResolveCloudFolder(settings, out var cloudFolder))
            {
                return Task.FromResult<IReadOnlyList<CloudBackupItem>>(Array.Empty<CloudBackupItem>());
            }

            var items = Directory.GetFiles(cloudFolder, BackupPattern)
                .Select(Describe)
                .OrderByDescending(item => item.FechaUtc)
                .ToArray();

            return Task.FromResult<IReadOnlyList<CloudBackupItem>>(items);
        }

        public Task<CloudSyncResult> SynchronizeAsync(BackupSettings settings, CancellationToken cancellationToken = default)
        {
            if (!settings.GoogleDriveHabilitado)
            {
                return Task.FromResult(CloudSyncResult.NoEjecutada("La sincronización con la nube está deshabilitada."));
            }

            if (!TryResolveCloudFolder(settings, out var cloudFolder))
            {
                return Task.FromResult(CloudSyncResult.NoEjecutada(
                    "No se encontró la carpeta de la nube. Verifique que el cliente de sincronización esté instalado y la ruta configurada sea correcta."));
            }

            var localFolder = settings.CarpetaLocal;
            if (string.IsNullOrWhiteSpace(localFolder))
            {
                return Task.FromResult(CloudSyncResult.NoEjecutada("No hay una carpeta local de backups configurada."));
            }

            Directory.CreateDirectory(localFolder);

            var subidos = new List<string>();
            var descargados = new List<string>();
            var conflictos = new List<string>();

            var localItems = Directory.GetFiles(localFolder, BackupPattern).Select(Describe)
                .ToDictionary(item => item.NombreArchivo, StringComparer.OrdinalIgnoreCase);
            var remoteItems = Directory.GetFiles(cloudFolder, BackupPattern).Select(Describe)
                .ToDictionary(item => item.NombreArchivo, StringComparer.OrdinalIgnoreCase);

            // Sentido local -> nube: se suben las copias que el equipo genero y la nube todavia no tiene.
            foreach (var local in localItems.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!remoteItems.TryGetValue(local.NombreArchivo, out var remote))
                {
                    if (TryCopy(local.RutaCompleta, Path.Combine(cloudFolder, local.NombreArchivo)))
                    {
                        subidos.Add(local.NombreArchivo);
                    }
                    continue;
                }

                if (!string.Equals(local.Hash, remote.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    // Mismo nombre y contenido distinto: no se pisa ninguna de las dos copias.
                    // Se conserva la remota bajo un nombre alternativo y se reporta el conflicto.
                    var preserved = BuildConflictName(remote.NombreArchivo);
                    if (TryCopy(remote.RutaCompleta, Path.Combine(cloudFolder, preserved))
                        && TryCopy(local.RutaCompleta, Path.Combine(cloudFolder, local.NombreArchivo)))
                    {
                        conflictos.Add($"{local.NombreArchivo} (la copia de la nube se conservó como {preserved})");
                    }
                }
            }

            // Sentido nube -> local: se traen las copias que otros equipos subieron.
            if (settings.SincronizacionBidireccional)
            {
                foreach (var remote in remoteItems.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (localItems.ContainsKey(remote.NombreArchivo))
                    {
                        continue;
                    }

                    if (TryCopy(remote.RutaCompleta, Path.Combine(localFolder, remote.NombreArchivo)))
                    {
                        descargados.Add(remote.NombreArchivo);
                    }
                }
            }

            var mensaje = settings.SincronizacionBidireccional
                ? $"Sincronización bidireccional completada: {subidos.Count} subida(s), {descargados.Count} descarga(s), {conflictos.Count} conflicto(s)."
                : $"Sincronización de subida completada: {subidos.Count} subida(s), {conflictos.Count} conflicto(s).";

            return Task.FromResult(new CloudSyncResult
            {
                Ejecutada = true,
                Proveedor = string.IsNullOrWhiteSpace(settings.ProveedorNube) ? ProviderName : settings.ProveedorNube,
                Subidos = subidos,
                Descargados = descargados,
                Conflictos = conflictos,
                Mensaje = mensaje
            });
        }

        private static bool TryResolveCloudFolder(BackupSettings settings, out string cloudFolder)
        {
            cloudFolder = settings.CarpetaNube?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cloudFolder))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(cloudFolder);
                return Directory.Exists(cloudFolder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                cloudFolder = string.Empty;
                return false;
            }
        }

        private static bool TryCopy(string source, string destination)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string BuildConflictName(string fileName)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            return $"{stem}-conflicto-{Environment.MachineName}-{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
        }

        private static CloudBackupItem Describe(string path)
        {
            var info = new FileInfo(path);
            return new CloudBackupItem
            {
                NombreArchivo = info.Name,
                RutaCompleta = info.FullName,
                TamanoBytes = info.Length,
                FechaUtc = info.LastWriteTimeUtc,
                Hash = ComputeHash(path)
            };
        }

        private static string ComputeHash(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var sha = SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(stream));
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }
    }
}
