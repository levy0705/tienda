using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class BackupService : IBackupService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("TIENDA_MVP_BK1");
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Pbkdf2Iterations = 210_000;
    private readonly ILocalDatabase _database;
    private readonly IUserSession _session;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;

    public BackupService(ILocalDatabase database, IUserSession session, IPermissionService permissions, IAuditService audit)
    {
        _database = database;
        _session = session;
        _permissions = permissions;
        _audit = audit;
    }

    public IReadOnlyList<BackupInfo> GetBackups()
    {
        var directory = GetBackupDirectory();
        if (!Directory.Exists(directory))
            return Array.Empty<BackupInfo>();
        return Directory.EnumerateFiles(directory, "*.tbackup", SearchOption.TopDirectoryOnly)
            .Select(CreateInfo)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task<BackupInfo> CreateBackupAsync(string administratorPin, bool preventive = false, CancellationToken cancellationToken = default)
    {
        _permissions.Require(AppPermission.ManageBackups);
        var administrator = ValidateAdministratorPin(administratorPin);
        return await CreateEncryptedBackupAsync(administratorPin, administrator.PinHash, preventive, administrator.DisplayName, cancellationToken);
    }

    private async Task<BackupInfo> CreateEncryptedBackupAsync(string administratorPin, string administratorPinHash, bool preventive, string administratorName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var archive = await BuildArchiveAsync(administratorPinHash, cancellationToken);
        var encrypted = Encrypt(archive, administratorPin);
        var directory = GetBackupDirectory();
        Directory.CreateDirectory(directory);
        var fileName = $"tienda-{(preventive ? "pre-restauracion-" : string.Empty)}{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.tbackup";
        var path = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, encrypted, cancellationToken);
        var info = CreateInfo(path);
        _audit.Record("Crear respaldo", nameof(BackupInfo), null, $"{info.FileName}; {info.SizeLabel}; usuario administrador: {administratorName}");
        return info;
    }

    public async Task<BackupValidationResult> ValidateAsync(string filePath, string administratorPin, CancellationToken cancellationToken = default)
    {
        _permissions.Require(AppPermission.ManageBackups);
        try
        {
            var package = await ReadPackageAsync(filePath, administratorPin, cancellationToken);
            var info = CreateInfo(filePath);
            info.CreatedAtUtc = package.Manifest.CreatedAtUtc;
            return new BackupValidationResult
            {
                IsValid = true,
                Message = $"Respaldo válido: {package.Manifest.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}. Contiene {package.Images.Count} imagen(es).",
                Backup = info,
                ImageCount = package.Images.Count,
                SchemaVersion = package.Manifest.SchemaVersion
            };
        }
        catch (Exception exception) when (exception is InvalidDataException or CryptographicException or JsonException or IOException or UnauthorizedAccessException)
        {
            return new BackupValidationResult { IsValid = false, Message = $"El archivo no es válido: {exception.Message}" };
        }
    }

    public async Task<BackupRestoreResult> RestoreAsync(string filePath, string administratorPin, CancellationToken cancellationToken = default)
    {
        _permissions.Require(AppPermission.ManageBackups);
        var package = await ReadPackageAsync(filePath, administratorPin, cancellationToken);
        var preventive = await CreateEncryptedBackupAsync(
            administratorPin,
            package.Manifest.AdministratorPinHash,
            preventive: true,
            _session.CurrentUser?.DisplayName ?? "PIN que protegió el respaldo",
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var stagedImages = await StageImagesAsync(package.Images, cancellationToken);
        try
        {
            _database.ReplaceDatabase(package.DatabaseBytes);
            ReplaceImages(stagedImages);
            _audit.Record("Restaurar respaldo", nameof(BackupInfo), null, $"{Path.GetFileName(filePath)}; imágenes: {package.Images.Count}");
            _session.SignOut();
            return new BackupRestoreResult
            {
                PreventiveBackup = preventive,
                RestoredBackup = CreateInfo(filePath),
                ImageCount = package.Images.Count
            };
        }
        catch
        {
            if (Directory.Exists(stagedImages))
                Directory.Delete(stagedImages, true);
            throw;
        }
    }

    private async Task<byte[]> BuildArchiveAsync(string administratorPinHash, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var databaseBytes = await File.ReadAllBytesAsync(_database.DatabasePath, cancellationToken);
            AddEntry(archive, "database/tienda-mvp.db3", databaseBytes);
            var settings = _database.Connection.Find<StoreSettings>("store") ?? new StoreSettings();
            AddEntry(archive, "config/store-settings.json", JsonSerializer.SerializeToUtf8Bytes(settings));
            var imageRoot = Path.Combine(FileSystem.AppDataDirectory, "product-images");
            var imageEntries = Directory.Exists(imageRoot)
                ? Directory.EnumerateFiles(imageRoot, "*", SearchOption.AllDirectories).ToList()
                : new List<string>();
            foreach (var file in imageEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(imageRoot, file).Replace('\\', '/');
                AddEntry(archive, $"images/{relative}", await File.ReadAllBytesAsync(file, cancellationToken));
            }
            var manifest = new BackupManifest
            {
                CreatedAtUtc = DateTime.UtcNow,
                SchemaVersion = _database.SchemaVersion,
                DatabaseBytes = databaseBytes.Length,
                ImageCount = imageEntries.Count,
                AdministratorPinHash = administratorPinHash
            };
            AddEntry(archive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest));
        }
        return stream.ToArray();
    }

    private async Task<BackupPackage> ReadPackageAsync(string filePath, string administratorPin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new InvalidDataException("No se encontró el archivo seleccionado.");
        if (string.IsNullOrWhiteSpace(administratorPin))
            throw new InvalidDataException("Se requiere el PIN que protegió el respaldo.");
        var encrypted = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var archiveBytes = Decrypt(encrypted, administratorPin);
        using var stream = new MemoryStream(archiveBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("Falta el manifiesto del respaldo.");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await ReadEntryAsync(manifestEntry, cancellationToken))
            ?? throw new InvalidDataException("El manifiesto no es válido.");
        if (manifest.Format != BackupManifest.CurrentFormat || manifest.SchemaVersion <= 0)
            throw new InvalidDataException("La versión del respaldo no es compatible.");
        try
        {
            if (string.IsNullOrWhiteSpace(manifest.AdministratorPinHash) || !PinHasher.Verify(administratorPin.Trim(), manifest.AdministratorPinHash))
                throw new InvalidDataException("El PIN no corresponde al administrador que protegió esta copia.");
        }
        catch (FormatException)
        {
            throw new InvalidDataException("El respaldo no contiene una protección válida.");
        }
        if (manifest.SchemaVersion > _database.SchemaVersion)
            throw new InvalidDataException("El respaldo fue creado con una versión más reciente de la aplicación.");
        var databaseEntry = archive.GetEntry("database/tienda-mvp.db3") ?? throw new InvalidDataException("Falta la base de datos del respaldo.");
        var databaseBytes = await ReadEntryAsync(databaseEntry, cancellationToken);
        if (databaseBytes.Length < 16 || !Encoding.ASCII.GetString(databaseBytes, 0, 15).StartsWith("SQLite format 3", StringComparison.Ordinal))
            throw new InvalidDataException("La base de datos incluida no es válida.");
        if (manifest.DatabaseBytes != databaseBytes.Length)
            throw new InvalidDataException("El tamaño de la base de datos no coincide con el manifiesto.");
        var settingsEntry = archive.GetEntry("config/store-settings.json") ?? throw new InvalidDataException("Falta la configuración de la tienda.");
        _ = JsonSerializer.Deserialize<StoreSettings>(await ReadEntryAsync(settingsEntry, cancellationToken))
            ?? throw new InvalidDataException("La configuración de la tienda no es válida.");
        var imageEntries = archive.Entries.Where(entry => entry.FullName.StartsWith("images/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name)).ToList();
        if (manifest.ImageCount != imageEntries.Count)
            throw new InvalidDataException("La cantidad de imágenes no coincide con el manifiesto.");
        var images = new List<BackupImage>(imageEntries.Count);
        var imagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in imageEntries)
        {
            var relative = image.FullName["images/".Length..];
            EnsureSafeEntryPath(relative);
            if (!imagePaths.Add(relative))
                throw new InvalidDataException("El respaldo contiene imágenes duplicadas.");
            images.Add(new BackupImage(relative, await ReadEntryAsync(image, cancellationToken)));
        }
        return new BackupPackage(manifest, databaseBytes, images);
    }

    private async Task<string> StageImagesAsync(IReadOnlyList<BackupImage> entries, CancellationToken cancellationToken)
    {
        var staged = Path.Combine(FileSystem.AppDataDirectory, $"product-images-restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staged);
        try
        {
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = entry.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                EnsureSafeEntryPath(relative);
                var destination = Path.GetFullPath(Path.Combine(staged, relative));
                var stagedRoot = Path.GetFullPath(staged + Path.DirectorySeparatorChar);
                if (!destination.StartsWith(stagedRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("El respaldo contiene una ruta de imagen no permitida.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await File.WriteAllBytesAsync(destination, entry.Content, cancellationToken);
            }
            return staged;
        }
        catch
        {
            if (Directory.Exists(staged))
                Directory.Delete(staged, true);
            throw;
        }
    }

    private static void ReplaceImages(string stagedImages)
    {
        var imageRoot = Path.Combine(FileSystem.AppDataDirectory, "product-images");
        var previous = Path.Combine(FileSystem.AppDataDirectory, $"product-images-before-restore-{Guid.NewGuid():N}");
        if (Directory.Exists(imageRoot))
            Directory.Move(imageRoot, previous);
        try
        {
            Directory.Move(stagedImages, imageRoot);
            if (Directory.Exists(previous))
                Directory.Delete(previous, true);
        }
        catch
        {
            if (Directory.Exists(imageRoot))
                Directory.Delete(imageRoot, true);
            if (Directory.Exists(previous))
                Directory.Move(previous, imageRoot);
            throw;
        }
    }

    private User ValidateAdministratorPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            throw new InvalidOperationException("Se requiere el PIN de un administrador.");
        var administrators = _database.Connection.Table<User>().ToList().Where(user => user.IsActive && user.Role == UserRoles.Administrator);
        foreach (var user in administrators)
        {
            try
            {
                if (PinHasher.Verify(pin.Trim(), user.PinHash))
                    return user;
            }
            catch (FormatException)
            {
                // Se ignora un hash corrupto y se continúa con los demás administradores.
            }
        }
        throw new InvalidOperationException("El PIN no corresponde a un administrador activo.");
    }

    private static byte[] Encrypt(byte[] archive, string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(pin, salt);
        var cipher = new byte[archive.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, archive, cipher, tag);
        CryptographicOperations.ZeroMemory(key);
        using var output = new MemoryStream();
        output.Write(Magic);
        output.Write(salt);
        output.Write(nonce);
        output.Write(BitConverter.GetBytes(cipher.Length));
        output.Write(tag);
        output.Write(cipher);
        return output.ToArray();
    }

    private static byte[] Decrypt(byte[] encrypted, string pin)
    {
        var minimumLength = Magic.Length + SaltSize + NonceSize + sizeof(int) + TagSize;
        if (encrypted.Length < minimumLength || !encrypted.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("El archivo no es un respaldo de Mi tienda.");
        var offset = Magic.Length;
        var salt = encrypted.AsSpan(offset, SaltSize).ToArray(); offset += SaltSize;
        var nonce = encrypted.AsSpan(offset, NonceSize).ToArray(); offset += NonceSize;
        var cipherLength = BitConverter.ToInt32(encrypted, offset); offset += sizeof(int);
        if (cipherLength < 1 || encrypted.Length != offset + TagSize + cipherLength)
            throw new InvalidDataException("El contenido cifrado está incompleto.");
        var tag = encrypted.AsSpan(offset, TagSize).ToArray(); offset += TagSize;
        var cipher = encrypted.AsSpan(offset, cipherLength).ToArray();
        var plain = new byte[cipherLength];
        var key = DeriveKey(pin, salt);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return plain;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DeriveKey(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin.Trim()), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);

    private static void AddEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var target = entry.Open();
        target.Write(bytes);
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var source = entry.Open();
        using var target = new MemoryStream();
        await source.CopyToAsync(target, cancellationToken);
        return target.ToArray();
    }

    private static void EnsureSafeEntryPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException("El respaldo contiene una ruta no permitida.");
    }

    private static string GetBackupDirectory() => Path.Combine(FileSystem.AppDataDirectory, "backups");

    private static BackupInfo CreateInfo(string path)
    {
        var file = new FileInfo(path);
        return new BackupInfo
        {
            FilePath = file.FullName,
            FileName = file.Name,
            CreatedAtUtc = file.LastWriteTimeUtc,
            SizeBytes = file.Length,
            IsPreventive = file.Name.Contains("pre-restauracion", StringComparison.OrdinalIgnoreCase)
        };
    }

    private sealed class BackupManifest
    {
        public const string CurrentFormat = "TiendaMvpBackupV1";
        public string Format { get; set; } = CurrentFormat;
        public int SchemaVersion { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int DatabaseBytes { get; set; }
        public int ImageCount { get; set; }
        public string AdministratorPinHash { get; set; } = string.Empty;
    }

    private sealed record BackupPackage(BackupManifest Manifest, byte[] DatabaseBytes, IReadOnlyList<BackupImage> Images);

    private sealed record BackupImage(string RelativePath, byte[] Content);
}
