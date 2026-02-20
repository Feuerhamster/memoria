using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models.Request;
using Memoria.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Memoria.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}")]
public class SpaceImageController(AppDbContext db, IImageService imageService, ISpaceService spaceService) : ControllerBase
{
    [HttpGet]
    public IActionResult GetSpaceImage(Guid spaceId)
    {
        var imageStream = imageService.GetImageFile(spaceId);

        if (imageStream == null)
        {
            return NotFound();
        }
        
        Response.Headers.Append("Cache-Control", "public, max-age=7884000");
        return File(imageStream, "image/avif");
    }
    
    [HttpPost("image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(Guid spaceId, [FromForm] ImageUploadRequest formBody, CancellationToken ct)
    {
        var claims = this.User.GetAuthClaimsData();

        var space = await spaceService.GetSpace(spaceId, ct);

        if (space == null)
        {
            return new NotFoundApiException();
        }

        if (!space.OwnerUserId.Equals(claims.UserId))
        {
            return new ActionNotAllowedApiException();
        }
  
        await imageService.ProcessAndSaveFile(formBody.Image, space.Id);

        return Ok();
    }
}