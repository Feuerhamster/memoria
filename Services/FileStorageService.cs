using System.Security.Cryptography;
using Memoria.Exceptions;
using Memoria.Models;
using Memoria.Models.Config;
using Memoria.Models.Database;
using Memoria.Models.Response;
using Memoria.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoria.Services;

public interface IFileStorageService
{
    Task<Result<FileMetadata>> StoreFile(Stream fileStream, string originalFileName, string contentType, RessourceOwnerHelper owner, RessourceAccessPolicy accessPolicy, CancellationToken cancellationToken = default);

    Task<Result<FileMetadata>> UpdateFile(FileMetadata existingFile, Stream fileStream, CancellationToken cancellationToken = default);

    Task<Result<FileDownloadResult>> GetFile(Guid fileId, CancellationToken cancellationToken = default);
    
    Task<FileMetadata?> GetFileMetadata(Guid fileId, CancellationToken ct = default);

    Task<bool> DeleteFile(FileMetadata file, CancellationToken cancellationToken = default);
}

public class FileStorageService : IFileStorageService
{
    private readonly AppDbContext _dbContext;
    private readonly FileConfig _config;
    private readonly ILogger<FileStorageService> _logger;
    private const int BUFFER_SIZE = 4096;
    
    public FileStorageService(
        AppDbContext dbContext,
        ILogger<FileStorageService> logger, IOptions<FileConfig> config)
    {
        _dbContext = dbContext;
        _config =  config.Value;
        _logger = logger;
        
        EnsureStorageDirectoryExists();
    }
    
    public async Task<Result<FileMetadata>> StoreFile(
        Stream fileStream, 
        string originalFileName, 
        string contentType, 
        RessourceOwnerHelper owner,
        RessourceAccessPolicy accessPolicy,
        CancellationToken cancellationToken = default)
    {
        var fileId = Guid.NewGuid();
        var sanitizedFileName = this.SanitizeFileName(originalFileName);
        var extension = Path.GetExtension(sanitizedFileName);
        
        var relativePath = GenerateStoragePath(fileId, extension);
        var fullPath = Path.Combine(_config.StoragePath, relativePath);
        
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            string fileHash;
            long fileSize;
            
            await using (var fileStreamWriter = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, useAsync: true))
            {
                using var sha256 = SHA256.Create();
                await using var cryptoStream = new CryptoStream(fileStreamWriter, sha256, CryptoStreamMode.Write);
                
                await fileStream.CopyToAsync(cryptoStream, BUFFER_SIZE, cancellationToken);
                await cryptoStream.FlushFinalBlockAsync(cancellationToken);
                
                fileHash = Convert.ToHexString(sha256.Hash ?? []);
                fileSize = fileStreamWriter.Length;
            }
            
            var storedFile = new FileMetadata
            {
                Id = fileId,
                FileName = sanitizedFileName,
                ContentType = contentType,
                SizeInBytes = fileSize,
                FileHash = fileHash,
                OwnerUserId = owner.UserId,
                SpaceId = owner.SpaceId,
                AccessPolicy = accessPolicy,
                UploadedAt = DateTime.UtcNow
            };
            
            _dbContext.Files.Add(storedFile);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("File saved: {FileId}, Name: {FileName} Size: {Size} kilobytes", 
                fileId, storedFile.FileName, storedFile.SizeInBytes * 1000);

            return new Result<FileMetadata>(storedFile);
        }
        catch (Exception ex)
        {
            // Cleanup bei Fehler
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(deleteEx, "Failed to delete file on failed upload {Path}", fullPath);
                }
            }
            
            _logger.LogError(ex, "Failed to save file: {FileName}", originalFileName);
            return new Result<FileMetadata>(ex);
        }
    }
    
    public async Task<Result<FileMetadata>> UpdateFile(
        FileMetadata existingFile,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(existingFile.FileName);
        var fullPath = Path.Combine(_config.StoragePath, GenerateStoragePath(existingFile.Id, extension));

        try
        {
            string fileHash;
            long fileSize;

            // Replace the physical file with new content
            await using (var fileStreamWriter = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, useAsync: true))
            {
                using var sha256 = SHA256.Create();
                await using var cryptoStream = new CryptoStream(fileStreamWriter, sha256, CryptoStreamMode.Write);

                await fileStream.CopyToAsync(cryptoStream, BUFFER_SIZE, cancellationToken);
                await cryptoStream.FlushFinalBlockAsync(cancellationToken);

                fileHash = Convert.ToHexString(sha256.Hash ?? []);
                fileSize = fileStreamWriter.Length;
            }

            // Update metadata in database
            existingFile.FileHash = fileHash;
            existingFile.SizeInBytes = fileSize;
            existingFile.UploadedAt = DateTime.UtcNow;

            _dbContext.Files.Update(existingFile);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("File updated: {FileId}, Name: {FileName}, Size: {Size} kilobytes",
                existingFile.Id, existingFile.FileName, existingFile.SizeInBytes / 1000);

            return new Result<FileMetadata>(existingFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update file: {FileId}", existingFile.Id);
            return new Result<FileMetadata>(ex);
        }
    }

    public async Task<Result<FileDownloadResult>> GetFile(Guid fileId, CancellationToken cancellationToken = default)
    {
        var storedFile = await _dbContext.Files
            .FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken);

        if (storedFile == null)
        {
            _logger.LogWarning("Datei nicht gefunden: {FileId}", fileId);
            return new Result<FileDownloadResult>(new FileNotFoundException());
        }

        var fullPath = Path.Combine(_config.StoragePath, GenerateStoragePath(storedFile.Id, Path.GetExtension(storedFile.FileName)));

        if (!File.Exists(fullPath))
        {
            _logger.LogError("Physische Datei nicht gefunden: {Path}", fullPath);
            return new Result<FileDownloadResult>(new FileNotFoundException());
        }

        var fileStream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BUFFER_SIZE,
            useAsync: true);

        return new Result<FileDownloadResult>(new FileDownloadResult
        {
            FileStream = fileStream,
            FileName = storedFile.FileName,
            ContentType = storedFile.ContentType,
            FileSize = storedFile.SizeInBytes
        });
    }

    public Task<FileMetadata?> GetFileMetadata(Guid fileId, CancellationToken ct = default)
    {
        return _dbContext.Files.Where(f => f.Id.Equals(fileId)).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> DeleteFile(FileMetadata file, CancellationToken cancellationToken = default)
    {
        // Physische Datei löschen (asynchron im Hintergrund)
        var fullPath = Path.Combine(_config.StoragePath, GenerateStoragePath(file.Id, Path.GetExtension(file.FileName)));
        
        if (!File.Exists(fullPath)) return true;
        
        try
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: {FileName}, Pfad: {Path}", file.FileName, fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete physical file: {Path}", fullPath);
            return false;
        }
        
        _dbContext.Files.Remove(file);
        var deleted = await _dbContext.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public static string GenerateStoragePath(Guid fileId, string extension)
    {
        var guidString = fileId.ToString("N");
        return Path.Combine(
            guidString.Substring(0, 2),
            guidString.Substring(2, 2),
            $"{fileId}{extension}"
        );
    }
    
    private string SanitizeFileName(string fileName)
    {
        // Entferne ungültige Zeichen
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars));
        
        // Begrenze Länge
        if (sanitized.Length > 255)
        {
            var extension = Path.GetExtension(sanitized);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
            sanitized = nameWithoutExt.Substring(0, 255 - extension.Length) + extension;
        }
        
        return sanitized;
    }
    
    private void EnsureStorageDirectoryExists()
    {
        if (!Directory.Exists(_config.StoragePath))
        {
            Directory.CreateDirectory(_config.StoragePath);
            _logger.LogInformation("Storage path created: {Path}", _config.StoragePath);
        }
    }
}