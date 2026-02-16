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
public class FileController(AppDbContext db, IFileStorageService fileService, IAccessPolicyHelperService accessHelper) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public Task<List<FileMetadata>> ListUserFiles(CancellationToken cancellationToken)
    {
        var user = this.User.GetAuthClaimsData();

        return db.Files.Where(f => f.OwnerUserId.Equals(user.UserId)).ToListAsync(cancellationToken);
    }

    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> GetFile(Guid fileId, bool download, CancellationToken cancellationToken)
    {
        var file = await db.Files.FindAsync(fileId, cancellationToken);

        if (file == null)
        {
            return new NotFoundApiException(new FileNotFoundException());
        }

        var hasAccess = await accessHelper.CheckAccessPolicy(file.AccessPolicy, AccessIntent.Write, file.OwnerUserId, this.User,
            file.SpaceId);

        if (!hasAccess)
        {
            return new AccessDeniedApiException();
        }
        
        var result = await fileService.GetFile(fileId, cancellationToken);

        if (result.IsFailed)
        {
            return new NotFoundApiException(result.Exception);
        }
        
        return File(
            result.Value.FileStream,
            result.Value.ContentType,
            fileDownloadName: download ? result.Value.FileName : null,
            enableRangeProcessing: true);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<FileMetadata>> UploadFile(FileUploadRequest upload, CancellationToken cancellationToken)
    {
        var user = this.User.GetAuthClaimsData();

        if (upload.SpaceId != null)
        {
            var spaceExists = await db.Spaces.AnyAsync(s => s.Id.Equals(upload.SpaceId), cancellationToken);

            if (!spaceExists)
            {
                return new NotFoundApiException(new Exception("Space not found"));
            }
        }

        var owner = new RessourceOwnerHelper
        {
            UserId = user.UserId,
            SpaceId = upload.SpaceId
        };

        var accessPolicy = RessourceAccessPolicy.Private;

        if (upload.AccessPolicy != null)
        {
            accessPolicy = (RessourceAccessPolicy)upload.AccessPolicy;
        } else if (upload.SpaceId != null)
        {
            accessPolicy = RessourceAccessPolicy.Members;
        }
        
        await using var stream = upload.File.OpenReadStream();
                
        var fileMeta = await fileService.StoreFile(
            stream,
            upload.File.FileName,
            upload.File.ContentType,
            owner,
            accessPolicy,
            cancellationToken);

        if (fileMeta.IsFailed)
        {
            return new OperationFailedApiException(fileMeta.Exception);
        }

        return fileMeta.Value;
    }

    [HttpDelete("{fileId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteFile(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await db.Files.FindAsync(fileId, cancellationToken);

        if (file == null)
        {
            return new NotFoundApiException(new FileNotFoundException());
        }

        var hasAccess = await accessHelper.CheckAccessPolicy(file.AccessPolicy, AccessIntent.Write, file.OwnerUserId, this.User,
            file.SpaceId);

        if (!hasAccess)
        {
            return new AccessDeniedApiException();
        }
        
        var deleted = await fileService.DeleteFile(file, cancellationToken);

        if (deleted)
        {
            return Ok();
        }
        else
        {
            return new OperationFailedApiException();
        }
    }
}