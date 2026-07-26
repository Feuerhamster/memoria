using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers;

[ApiController]
[Route("/files")]
public class FileController(AppDbContext db, IFileStorageService fileService, IAccessPolicyHelperService accessHelper, ISpaceService spaceService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public Task<List<FileMetadata>> ListUserFiles(CancellationToken cancellationToken)
    {
        var user = this.User.GetAuthClaimsData();

        return db.Files.Where(f => f.OwnerUserId.Equals(user.UserId)).ToListAsync(cancellationToken);
    }

    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> GetFile(Guid fileId, bool download, CancellationToken ct)
    {
        var file = await fileService.GetFileMetadata(fileId, ct);

        if (file == null)
        {
            return new NotFoundApiException("file not found");
        }

        var hasAccess = await accessHelper.CheckAccessPolicy(file, AccessIntent.Read, this.User);

        if (!hasAccess)
        {
            return new AccessDeniedApiException();
        }

        var result = await fileService.GetFile(fileId, ct);

        if (result.IsFailed)
        {
            return result.SelectApiError(
                expected: new NotFoundApiException(result.FailureDetails),
                unexpected: new OperationFailedApiException(result.FailureDetails)
            );
        }
        
        return File(
            result.Value.FileStream,
            result.Value.ContentType,
            fileDownloadName: download ? result.Value.FileName : null,
            enableRangeProcessing: true);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<FileMetadata>> UploadFile(FileUploadRequest upload, CancellationToken ct)
    {
        var user = this.User.GetAuthClaimsData();

        if (upload.SpaceId != null)
        {
            var spaceExists = await spaceService.SpaceExists(upload.SpaceId.Value);

            if (!spaceExists)
            {
                return new NotFoundApiException("Space not found");
            }

            var hasAccess = await accessHelper.CheckSpaceMembership(user.UserId, upload.SpaceId.Value, ct);

            if (!hasAccess) return new AccessDeniedApiException("No space member");
        }

        var owner = new RessourceOwnerHelper
        {
            UserId = user.UserId,
            SpaceId = upload.SpaceId
        };

        var accessPolicy = RessourceAccessPolicy.Private;

        if (upload.AccessPolicy != null)
        {
            accessPolicy = upload.AccessPolicy.Value;
        }
        
        await using var stream = upload.File.OpenReadStream();
                
        var fileMeta = await fileService.StoreFile(
            stream,
            upload.File.FileName,
            upload.File.ContentType,
            owner,
            accessPolicy,
            ct);
        
        return fileMeta.IsOk ? fileMeta.Value : new OperationFailedApiException(fileMeta.FailureDetails);
    }

    [HttpPatch("{fileId:guid}")]
    public async Task<ActionResult<FileMetadata>> UpdateFileMeta(Guid fileId, FileUpdateRequest update, CancellationToken ct)
    {
        var file = await fileService.GetFileMetadata(fileId, ct);

        if (file == null)
        {
            return new NotFoundApiException("file not found");
        }

        var hasAccess = await accessHelper.CheckAccessPolicy(file, AccessIntent.Write, this.User);
        if (!hasAccess) return new AccessDeniedApiException();
        
        update.Apply(file);
        var changed = await db.SaveChangesAsync(ct);
        
        return changed > 0 ? file : new OperationFailedApiException();
    }

    [HttpDelete("{fileId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteFile(Guid fileId, CancellationToken ct)
    {
        var file = await fileService.GetFileMetadata(fileId, ct);

        if (file == null)
        {
            return new NotFoundApiException("file not found");
        }

        var hasAccess = await accessHelper.CheckAccessPolicy(file, AccessIntent.Write, this.User);
        if (!hasAccess) return new AccessDeniedApiException();
        
        var deleted = await fileService.DeleteFile(file, ct);
        
        return deleted ? new OkResult() : new OperationFailedApiException();
    }
}