using System.Security.Cryptography;
using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Config;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Models.Response;
using Memoria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoria.Controllers;

[ApiController]
[Route("/onlyoffice")]
public class OnlyOfficeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOnlyOfficeService _onlyOfficeService;
    private readonly IFileStorageService _fileService;
    private readonly IAccessPolicyHelperService _accessHelper;
    private readonly ILogger<OnlyOfficeController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FileConfig _fileConfig;
    private readonly OnlyOfficeConfig _onlyOfficeConfig;

    public OnlyOfficeController(
        AppDbContext db,
        IOnlyOfficeService onlyOfficeService,
        IFileStorageService fileService,
        IAccessPolicyHelperService accessHelper,
        ILogger<OnlyOfficeController> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<FileConfig> fileConfig,
        IOptions<OnlyOfficeConfig> onlyOfficeConfig)
    {
        _db = db;
        _onlyOfficeService = onlyOfficeService;
        _fileService = fileService;
        _accessHelper = accessHelper;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _fileConfig = fileConfig.Value;
        _onlyOfficeConfig = onlyOfficeConfig.Value;
    }

    /// <summary>
    /// Opens the OnlyOffice editor UI for a file
    /// </summary>
    /// <param name="fileId">The ID of the file to edit</param>
    /// <returns>HTML page with embedded OnlyOffice editor</returns>
    [HttpGet("edit/{fileId:guid}")]
    [Authorize]
    public IActionResult EditFile(Guid fileId)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var html = $@"
<!DOCTYPE html>
<html lang='de'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Dokument bearbeiten</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            overflow: hidden;
            height: 100vh;
        }}
        #editor {{
            width: 100vw;
            height: 100vh;
        }}
        #loading {{
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            display: flex;
            align-items: center;
            justify-content: center;
            background: #f5f5f5;
            z-index: 1000;
        }}
        #loading.hidden {{
            display: none;
        }}
        .spinner {{
            width: 50px;
            height: 50px;
            border: 4px solid #e0e0e0;
            border-top: 4px solid #2196F3;
            border-radius: 50%;
            animation: spin 1s linear infinite;
        }}
        @keyframes spin {{
            0% {{ transform: rotate(0deg); }}
            100% {{ transform: rotate(360deg); }}
        }}
        .error {{
            max-width: 600px;
            padding: 20px;
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }}
        .error h2 {{
            color: #d32f2f;
            margin-bottom: 10px;
        }}
        .error p {{
            color: #666;
            line-height: 1.6;
        }}
    </style>
</head>
<body>
    <div id='editor'></div>

    <script>
        const fileId = '{fileId}';
        const apiUrl = '{baseUrl}/onlyoffice/editor/' + fileId;

        async function initEditor() {{
            try {{
                const response = await fetch(apiUrl, {{
                    credentials: 'include'
                }});

                if (!response.ok) {{
                    throw new Error('Fehler beim Laden der Editor-Konfiguration: ' + response.status);
                }}

                const config = await response.json();

                // OnlyOffice Document Server Script laden
                const script = document.createElement('script');
                script.src = config.documentServerUrl + '/web-apps/apps/api/documents/api.js';
                script.onload = () => {{
                    // Editor initialisieren
                    new DocsAPI.DocEditor('editor', {{
                        documentType: config.documentType,
                        document: {{
                            fileType: config.document.fileType,
                            key: config.document.key,
                            title: config.document.title,
                            url: config.document.url,
                            permissions: config.document.permissions
                        }},
                        editorConfig: {{
                            callbackUrl: config.editorConfig.callbackUrl,
                            user: config.editorConfig.user,
                            mode: config.editorConfig.mode,
                            lang: config.editorConfig.lang,
                            customization: {{
                                autosave: true,
                                forcesave: true,
                                compactHeader: false,
                                toolbarNoTabs: false
                            }}
                        }},
                        token: config.token,
                        events: {{
                            onReady: () => {{
                                document.getElementById('loading').classList.add('hidden');
                            }},
                            onError: (event) => {{
                                console.error('OnlyOffice Error:', event);
                                showError('Editor-Fehler', 'Ein Fehler ist beim Laden des Editors aufgetreten.');
                            }}
                        }}
                    }});
                }};
                script.onerror = () => {{
                    showError('Verbindungsfehler', 'Der OnlyOffice Document Server konnte nicht erreicht werden.');
                }};
                document.head.appendChild(script);

            }} catch (error) {{
                console.error('Error:', error);
                showError('Fehler', error.message);
            }}
        }}

        function showError(title, message) {{
            const loading = document.getElementById('loading');
            loading.innerHTML = `
                <div class='error'>
                    <h2>${{title}}</h2>
                    <p>${{message}}</p>
                </div>
            `;
        }}

        // Editor initialisieren
        initEditor();
    </script>
</body>
</html>";

        return Content(html, "text/html");
    }

    /// <summary>
    /// Gets the OnlyOffice editor configuration for a file (API endpoint)
    /// </summary>
    /// <param name="fileId">The ID of the file to edit</param>
    /// <param name="cancellationToken"></param>
    /// <returns>OnlyOffice editor configuration</returns>
    [HttpGet("editor/{fileId:guid}")]
    [Authorize]
    public async Task<ActionResult<OnlyOfficeEditorConfig>> GetEditorConfig(
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var user = this.User.GetAuthClaimsData();

        var file = await _db.Files.FindAsync(fileId, cancellationToken);
        if (file == null)
        {
            return new NotFoundApiException(new FileNotFoundException());
        }

        var dbUser = await _db.Users.FindAsync(user.UserId, cancellationToken);
        if (dbUser == null)
        {
            return new NotFoundApiException(new Exception("User not found"));
        }

        // Check read access
        var hasReadAccess = await _accessHelper.CheckAccessPolicy(
            file.AccessPolicy,
            AccessIntent.Read,
            file.OwnerUserId,
            this.User,
            file.SpaceId);

        if (!hasReadAccess)
        {
            return new AccessDeniedApiException();
        }

        // Check write access for edit permissions
        var hasWriteAccess = await _accessHelper.CheckAccessPolicy(
            file.AccessPolicy,
            AccessIntent.Write,
            file.OwnerUserId,
            this.User,
            file.SpaceId);

        // Generate file download URL with JWT token
        var baseUrl = !string.IsNullOrEmpty(_onlyOfficeConfig.ExternalApiUrl)
            ? _onlyOfficeConfig.ExternalApiUrl.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host}";

        // Generate download token (valid for 24 hours)
        var downloadToken = _onlyOfficeService.GenerateDownloadToken(fileId, TimeSpan.FromHours(24));
        var fileDownloadUrl = $"{baseUrl}/onlyoffice/download/{fileId}?token={downloadToken}";

        var editorConfig = _onlyOfficeService.GenerateEditorConfig(
            file,
            dbUser,
            fileDownloadUrl,
            hasWriteAccess);

        return Ok(editorConfig);
    }

    /// <summary>
    /// Download endpoint for OnlyOffice Document Server (JWT token authentication)
    /// </summary>
    /// <param name="fileId">The ID of the file to download</param>
    /// <param name="token">JWT download token</param>
    /// <param name="cancellationToken"></param>
    /// <returns>File stream</returns>
    [HttpGet("download/{fileId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadFile(Guid fileId, string? token, CancellationToken cancellationToken)
    {
        // Validate JWT token
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Download attempt without token for file {FileId}", fileId);
            return Unauthorized(new { Error = "Download token required" });
        }

        if (!_onlyOfficeService.ValidateDownloadToken(token, out var tokenFileId))
        {
            _logger.LogWarning("Invalid download token for file {FileId}", fileId);
            return Unauthorized(new { Error = "Invalid or expired download token" });
        }

        // Ensure the token is for this specific file
        if (tokenFileId != fileId)
        {
            _logger.LogWarning("Token fileId mismatch: expected {Expected}, got {Actual}", fileId, tokenFileId);
            return Unauthorized(new { Error = "Token does not match requested file" });
        }

        var file = await _db.Files.FindAsync(fileId, cancellationToken);

        if (file == null)
        {
            return NotFound();
        }

        var result = await _fileService.GetFile(fileId, cancellationToken);

        if (result.IsFailed)
        {
            return NotFound();
        }

        return File(
            result.Value.FileStream,
            result.Value.ContentType,
            enableRangeProcessing: true);
    }

    /// <summary>
    /// Callback endpoint for OnlyOffice Document Server to save changes
    /// </summary>
    /// <param name="fileId">The ID of the file being edited</param>
    /// <param name="request">OnlyOffice callback request</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Callback response</returns>
    [HttpPost("callback/{fileId:guid}")]
    public async Task<ActionResult<OnlyOfficeCallbackResponse>> Callback(
        Guid fileId,
        [FromBody] OnlyOfficeCallbackRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "OnlyOffice callback for file {FileId}, status: {Status}",
            fileId,
            request.Status);

        // Validate JWT token if present
        if (!string.IsNullOrEmpty(request.Token))
        {
            if (!_onlyOfficeService.ValidateJwtToken(request.Token, out _))
            {
                _logger.LogWarning("Invalid JWT token in callback for file {FileId}", fileId);
                return Unauthorized(new OnlyOfficeCallbackResponse { Error = 1 });
            }
        }

        var file = await _db.Files.FindAsync(fileId, cancellationToken);
        if (file == null)
        {
            _logger.LogWarning("File not found in callback: {FileId}", fileId);
            return NotFound(new OnlyOfficeCallbackResponse { Error = 1 });
        }

        // Handle different statuses
        switch (request.Status)
        {
            case OnlyOfficeCallbackStatus.BeingEdited:
                // Document is being edited, no action needed
                return Ok(new OnlyOfficeCallbackResponse { Error = 0 });

            case OnlyOfficeCallbackStatus.ReadyForSaving:
            case OnlyOfficeCallbackStatus.BeingEditedButSaveAnyway:
                // Document is ready to be saved
                if (string.IsNullOrEmpty(request.Url))
                {
                    _logger.LogError("No download URL provided in callback for file {FileId}", fileId);
                    return BadRequest(new OnlyOfficeCallbackResponse { Error = 1 });
                }

                await SaveDocumentFromOnlyOffice(file, request.Url, cancellationToken);
                return Ok(new OnlyOfficeCallbackResponse { Error = 0 });

            case OnlyOfficeCallbackStatus.SaveError:
            case OnlyOfficeCallbackStatus.ForceSaveError:
                _logger.LogError("OnlyOffice reported save error for file {FileId}", fileId);
                return Ok(new OnlyOfficeCallbackResponse { Error = 1 });

            case OnlyOfficeCallbackStatus.ClosedWithoutChanges:
                // Document closed without changes
                return Ok(new OnlyOfficeCallbackResponse { Error = 0 });

            default:
                _logger.LogWarning("Unknown callback status {Status} for file {FileId}", request.Status, fileId);
                return Ok(new OnlyOfficeCallbackResponse { Error = 0 });
        }
    }

    private async Task SaveDocumentFromOnlyOffice(
        FileMetadata file,
        string downloadUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();

            // Download the updated document from OnlyOffice
            using var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // Use UpdateFile to update the existing file (keeps same ID)
            var result = await _fileService.UpdateFile(file, stream, cancellationToken);

            if (result.IsFailed)
            {
                _logger.LogError(result.Exception, "Failed to save updated file {FileId}", file.Id);
                throw new Exception("Failed to save document");
            }

            _logger.LogInformation("Successfully saved updated document for file {FileId}", file.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving document from OnlyOffice for file {FileId}", file.Id);
            throw;
        }
    }
}
