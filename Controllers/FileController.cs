using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Config;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoria.Controllers;

[ApiController]
[Route("/files")]
public class FileController(AppDbContext db, IFileStorageService fileService, IAccessPolicyHelperService accessHelper, IOptions<FileConfig> fileConfig) : ControllerBase
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

        var uploadLimitBytes = fileConfig.Value.UploadLimitMb * 1024 * 1024;

        if (upload.File.Length > uploadLimitBytes)
        {
            return new ValidationErrorApiException($"Maximum allowed file size is {fileConfig.Value.UploadLimitMb} MB.");
        }

        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id.Equals(upload.PostId), ct);

        if (post == null)
        {
            return new NotFoundApiException("Post not found");
        }

        var hasAccess = await accessHelper.CheckAccessPolicy(post, AccessIntent.Write, this.User);

        if (!hasAccess) return new AccessDeniedApiException("No write access to the target post");

        var accessPolicy = upload.AccessPolicy ?? RessourceAccessPolicy.Private;

        // A file may be more restrictive than the post it's attached to (e.g. a private file on a
        // public post), but never less restrictive than the post itself.
        if (accessPolicy < post.AccessPolicy)
        {
            return new ValidationErrorApiException("File access policy must be at least as restrictive as the post it is attached to");
        }

        var owner = new RessourceOwnerHelper
        {
            UserId = user.UserId,
            SpaceId = post.SpaceId
        };

        await using var stream = upload.File.OpenReadStream();

        var fileMeta = await fileService.StoreFile(
            stream,
            upload.File.FileName,
            upload.File.ContentType,
            owner,
            accessPolicy,
            post.Id,
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

        if (update.AccessPolicy != null && file.PostId != null)
        {
            var post = await db.Posts.FirstOrDefaultAsync(p => p.Id.Equals(file.PostId), ct);

            // A file may be more restrictive than the post it's attached to, but never less.
            if (post != null && update.AccessPolicy.Value < post.AccessPolicy)
            {
                return new ValidationErrorApiException("File access policy must be at least as restrictive as the post it is attached to");
            }
        }

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