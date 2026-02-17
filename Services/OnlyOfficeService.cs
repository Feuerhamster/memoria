using System.Text;
using System.Text.Json;
using Jose;
using Memoria.Models.Config;
using Memoria.Models.Database;
using Memoria.Models.Response;
using Microsoft.Extensions.Options;

namespace Memoria.Services;

public interface IOnlyOfficeService
{
    OnlyOfficeEditorConfig GenerateEditorConfig(FileMetadata file, User user, string fileDownloadUrl, bool canEdit);
    bool ValidateJwtToken(string token, out Dictionary<string, object> payload);
    string GenerateDocumentKey(Guid fileId, DateTime lastModified, string Hash);
    string GenerateDownloadToken(Guid fileId, TimeSpan? expiry = null);
    bool ValidateDownloadToken(string token, out Guid fileId);
}

public class OnlyOfficeService : IOnlyOfficeService
{
    private readonly OnlyOfficeConfig _config;
    private readonly ILogger<OnlyOfficeService> _logger;

    public OnlyOfficeService(
        IOptions<OnlyOfficeConfig> config,
        ILogger<OnlyOfficeService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public OnlyOfficeEditorConfig GenerateEditorConfig(
        FileMetadata file,
        User user,
        string fileDownloadUrl,
        bool canEdit)
    {
        var documentType = GetDocumentType(file.FileName);
        var fileType = Path.GetExtension(file.FileName).TrimStart('.');
        var documentKey = GenerateDocumentKey(file.Id, file.UploadedAt, file.FileHash);

        var config = new OnlyOfficeEditorConfig
        {
            DocumentServerUrl = _config.DocumentServerUrl,
            DocumentType = documentType,
            Document = new OnlyOfficeDocument
            {
                FileType = fileType,
                Key = documentKey,
                Title = file.FileName,
                Url = fileDownloadUrl,
                Permissions = new OnlyOfficePermissions
                {
                    Edit = canEdit,
                    Download = true,
                    Review = canEdit,
                    Comment = canEdit
                }
            },
            EditorConfig = new OnlyOfficeEditorSettings
            {
                CallbackUrl = $"{_config.CallbackUrl}/onlyoffice/callback/{file.Id}",
                User = new OnlyOfficeUser
                {
                    Id = user.Id.ToString(),
                    Name = user.Nickname
                },
                Mode = canEdit ? "edit" : "view",
                Lang = "de"
            }
        };

        // Generate JWT token for the entire config
        config.Token = GenerateJwtToken(config);

        return config;
    }

    public string GenerateDocumentKey(Guid fileId, DateTime lastModified, string Hash)
    {
        // OnlyOffice uses this key to determine if the document has changed
        // We combine fileId and last modified timestamp
        var keyString = $"{fileId}_{lastModified:yyyyMMddHHmmss}_{Hash}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(keyString))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private string GenerateJwtToken(OnlyOfficeEditorConfig config)
    {
        if (string.IsNullOrEmpty(_config.JwtSecret))
        {
            _logger.LogWarning("OnlyOffice JWT Secret is not configured");
            return string.Empty;
        }

        // Serialize the entire config as the JWT payload
        var payload = new Dictionary<string, object>
        {
            ["documentType"] = config.DocumentType,
            ["document"] = new Dictionary<string, object>
            {
                ["fileType"] = config.Document.FileType,
                ["key"] = config.Document.Key,
                ["title"] = config.Document.Title,
                ["url"] = config.Document.Url,
                ["permissions"] = new Dictionary<string, object>
                {
                    ["edit"] = config.Document.Permissions.Edit,
                    ["download"] = config.Document.Permissions.Download,
                    ["review"] = config.Document.Permissions.Review,
                    ["comment"] = config.Document.Permissions.Comment
                }
            },
            ["editorConfig"] = new Dictionary<string, object>
            {
                ["callbackUrl"] = config.EditorConfig.CallbackUrl,
                ["user"] = new Dictionary<string, object>
                {
                    ["id"] = config.EditorConfig.User.Id,
                    ["name"] = config.EditorConfig.User.Name
                },
                ["mode"] = config.EditorConfig.Mode,
                ["lang"] = config.EditorConfig.Lang
            }
        };

        var secretBytes = Encoding.UTF8.GetBytes(_config.JwtSecret);
        var token = JWT.Encode(payload, secretBytes, JwsAlgorithm.HS256);

        return token;
    }

    public bool ValidateJwtToken(string token, out Dictionary<string, object> payload)
    {
        payload = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(_config.JwtSecret))
        {
            return false;
        }

        try
        {
            var secretBytes = Encoding.UTF8.GetBytes(_config.JwtSecret);
            payload = JWT.Decode<Dictionary<string, object>>(token, secretBytes, JwsAlgorithm.HS256);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed");
            return false;
        }
    }

    public string GenerateDownloadToken(Guid fileId, TimeSpan? expiry = null)
    {
        if (string.IsNullOrEmpty(_config.JwtSecret))
        {
            _logger.LogWarning("OnlyOffice JWT Secret is not configured");
            return string.Empty;
        }

        var expiryTime = expiry ?? TimeSpan.FromHours(24);
        var payload = new Dictionary<string, object>
        {
            ["fileId"] = fileId.ToString(),
            ["exp"] = DateTimeOffset.UtcNow.Add(expiryTime).ToUnixTimeSeconds()
        };

        var secretBytes = Encoding.UTF8.GetBytes(_config.JwtSecret);
        var token = JWT.Encode(payload, secretBytes, JwsAlgorithm.HS256);

        return token;
    }

    public bool ValidateDownloadToken(string token, out Guid fileId)
    {
        fileId = Guid.Empty;

        if (string.IsNullOrEmpty(_config.JwtSecret) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            var secretBytes = Encoding.UTF8.GetBytes(_config.JwtSecret);
            var payload = JWT.Decode<Dictionary<string, object>>(token, secretBytes, JwsAlgorithm.HS256);

            if (!payload.TryGetValue("fileId", out var fileIdObj))
            {
                _logger.LogWarning("Download token missing fileId claim");
                return false;
            }

            if (!Guid.TryParse(fileIdObj.ToString(), out fileId))
            {
                _logger.LogWarning("Invalid fileId in download token");
                return false;
            }

            // Check expiry
            if (payload.TryGetValue("exp", out var expObj))
            {
                var exp = Convert.ToInt64(expObj);
                var expiryTime = DateTimeOffset.FromUnixTimeSeconds(exp);

                if (DateTimeOffset.UtcNow > expiryTime)
                {
                    _logger.LogWarning("Download token expired for file {FileId}", fileId);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Download token validation failed");
            return false;
        }
    }

    private static string GetDocumentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".doc" or ".docx" or ".odt" or ".txt" or ".rtf" or ".pdf" => "word",
            ".xls" or ".xlsx" or ".ods" or ".csv" => "cell",
            ".ppt" or ".pptx" or ".odp" => "slide",
            _ => "word"
        };
    }
}
