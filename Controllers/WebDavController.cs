using System.Xml.Linq;
using EFCoreSecondLevelCacheInterceptor;
using Memoria.Attributes;
using Memoria.Authentication;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.WebDav;
using Memoria.Services;
using Memoria.Services.WebDav;
using Memoria.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Memoria.Controllers;

public enum EEntityTypes { Spaces, Users }
public enum EEntityPolicy { Public, Shared, Private }

[Route("webdav")]
[Authorize(AuthenticationSchemes = BasicAuthHandler.SchemeName, Policy = "WebDavFiles")]
[EnsureWwwAuthenticate]
public class WebDavController(
	AppDbContext db,
	IFileStorageService fileService,
	IAccessPolicyHelperService accessControl,
	ISpaceService spaceService,
	IStringLocalizer<WebDavController> localizer) : ControllerBase
{
	private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

	[AcceptVerbs("OPTIONS")]
	[AllowAnonymous]
	public IActionResult Options()
	{
		Response.Headers["DAV"] = "1";
		Response.Headers.Allow = "OPTIONS, PROPFIND, GET, PUT, DELETE, MOVE, COPY";
		return Ok();
	}

	[AcceptVerbs("PROPFIND")]
	[ValidateWebDavDepth]
	[Route("")]
	public async Task<IActionResult> RootDirectory()
	{
		var depth = GetDepth();

		var responses = new List<XElement> { WebDavXmlBuilder.CreateCollection("/webdav/", "WebDAV") };
		if (depth < 1) return MultiStatus(responses);
		responses.Add(WebDavXmlBuilder.CreateCollection("/webdav/users/", localizer["Users"]));
		responses.Add(WebDavXmlBuilder.CreateCollection("/webdav/spaces/", localizer["Spaces"]));
		
		return MultiStatus(responses);
	}

	[AcceptVerbs("PROPFIND")]
	[ValidateWebDavDepth]
	[Route("users")]
	public async Task<IActionResult> ListUsers(CancellationToken ct)
	{
		var depth = GetDepth();
		return MultiStatus(await ListUsers(depth, ct));
	}
	
	[AcceptVerbs("PROPFIND")]
	[ValidateWebDavDepth]
	[Route("spaces")]
	public async Task<IActionResult> ListSpaces(CancellationToken ct)
	{
		var depth = GetDepth();
		return MultiStatus(await ListSpaces(depth, ct));
	}
	
	[AcceptVerbs("PROPFIND")]
	[ValidateWebDavDepth]
	[Route("{entityType}/{entityName}")]
	public async Task<IActionResult> PolicyDirectory(EEntityTypes entityType, string entityName, CancellationToken ct)
	{
		var depth = GetDepth();
		var res = await ShowEntityFolder(entityType, entityName, depth, ct);
		return res == null ? NotFound() : MultiStatus(res);
	}
	
	[AcceptVerbs("PROPFIND")]
	[ValidateWebDavDepth]
	[Route("{entityType}/{entityName}/{policy}")]
	public async Task<IActionResult> GetEntityFileListByPolicy(EEntityTypes entityType, string entityName, EEntityPolicy policy, CancellationToken ct)
	{
		var depth = GetDepth();
		var res = await ListEntityFiles(entityType, entityName, policy, depth, ct);
		return res == null ? NotFound() : MultiStatus(res);
	}

	[HttpGet("{entityType}/{entityName}/{policy}/{fileName}")]
	public async Task<IActionResult> Get(EEntityTypes entityType, string entityName, EEntityPolicy policy, string fileName, CancellationToken ct)
	{
		var resolved = await this.ResolveFile(entityType, entityName, policy, fileName, ct);
		if (resolved?.File == null) return NotFound();

		// Validate ETag preconditions
		var etagError = ValidateETag(resolved.File.FileHash, isModificationRequest: false);
		if (etagError != null) return etagError;

		// Check read access
		if (!await accessControl.CheckAccessPolicy(resolved.File.AccessPolicy, AccessIntent.Read, resolved.File.OwnerUserId, User, resolved.File.SpaceId))
			return Forbid();

		var result = await fileService.GetFile(resolved.File.Id, ct);
		if (result.IsFailed) return NotFound();

		// Set ETag header in response
		Response.Headers.ETag = $"\"{resolved.File.FileHash}\"";

		return File(result.Value.FileStream, resolved.File.ContentType, enableRangeProcessing: true);
	}
	
	[AcceptVerbs("PROPFIND")]
	[ValidateWebDavDepth]
	[Route("{entityType}/{entityName}/{policy}/{fileName}")]
	public async Task<IActionResult> GetFileMetadata(EEntityTypes entityType, string entityName, EEntityPolicy policy, string fileName, CancellationToken ct)
	{
		var resolved = await this.ResolveFile(entityType, entityName, policy, fileName, ct);

		if (resolved?.File == null) return NotFound();

		var href = WebDavXmlBuilder.BuildHref(false, entityType.ToString().ToLower(), entityName, policy.ToString().ToLower(), fileName);
		return MultiStatus(new List<XElement>([WebDavXmlBuilder.CreateFile(href, resolved.File)]));
	}
	
	[HttpPut("{entityType}/{entityName}/{policyFolder}/{fileName}")]
	public async Task<IActionResult> UploadFile(EEntityTypes entityType, string entityName, EEntityPolicy policyFolder, string fileName, CancellationToken ct)
	{
		var userId = User.GetUserId();

		var resolved = await this.ResolveFile(entityType, entityName, policyFolder, fileName, ct);
		
		if (resolved == null) return NotFound();

		var (ctx, policy, existingFile) = resolved;
		
		if (existingFile != null)
		{
			// Check write access
			if (!await accessControl.CheckAccessPolicy(existingFile.AccessPolicy, AccessIntent.Write, existingFile.OwnerUserId, User,
				    existingFile.SpaceId))
			{
				return Forbid();
			}

			// Validate ETag preconditions (If-Match)
			var etagError = ValidateETag(existingFile.FileHash, isModificationRequest: true);
			if (etagError != null) return etagError;

			await fileService.DeleteFile(existingFile, ct);
		}
		
		// Store new file
		var owner = new RessourceOwnerHelper { UserId = ctx.OwnerId, SpaceId = ctx.SpaceId };

		var contentType = DetermineContentType(fileName);
		
		var result = await fileService.StoreFile(Request.Body, fileName, contentType, owner, policy, ct);

		return result.IsFailed ? StatusCode(500) : (existingFile != null ? NoContent() : StatusCode(201));
	}

	[AcceptVerbs("DELETE")]
	[Route("{entityType}/{entityName}/{policy}/{fileName}")]
	public async Task<IActionResult> Delete(EEntityTypes entityType, string entityName, EEntityPolicy policy, string fileName, CancellationToken ct)
	{
		var resolved = await ResolveFile(entityType, entityName, policy, fileName, ct);
		if (resolved?.File == null) return NotFound();

		// Check write access
		if (!await accessControl.CheckAccessPolicy(resolved.File.AccessPolicy, AccessIntent.Write, resolved.File.OwnerUserId, User, resolved.File.SpaceId))
			return Forbid();

		// Validate ETag preconditions (If-Match)
		var etagError = ValidateETag(resolved.File.FileHash, isModificationRequest: true);
		if (etagError != null) return etagError;

		await fileService.DeleteFile(resolved.File, ct);
		return NoContent();
	}

	/// <summary>
	/// MKCOL is not supported because this WebDAV server uses a fixed virtual folder structure.
	/// All collections (users, spaces, policy folders) are pre-defined and managed by the application.
	/// </summary>
	[AcceptVerbs("MKCOL")]
	[Route("{**path}")]
	public IActionResult MkCol(string? path = null)
	{
		// RFC 4918 Section 9.3: MKCOL creates a collection
		// Since we have a fixed virtual structure, creating arbitrary collections is not allowed
		return StatusCode(405, new
		{
			Error = "Method Not Allowed",
			Detail = "This WebDAV server uses a fixed folder structure. Collections cannot be created by clients."
		});
	}

	[AcceptVerbs("MOVE")]
	[Route("{entityType}/{entityName}/{policy}/{fileName}")]
	public async Task<IActionResult> MoveFile(EEntityTypes entityType, string entityName, EEntityPolicy policy, string fileName, CancellationToken ct)
	{
		// Get source file
		var resolved = await ResolveFile(entityType, entityName, policy, fileName, ct);
		if (resolved == null) return NotFound();

		var (sourceCtx, sourcePolicy, sourceFile) = resolved;
		if (sourceFile == null) return NotFound();

		// Check write access on source
		if (!await accessControl.CheckAccessPolicy(sourceFile.AccessPolicy, AccessIntent.Write, sourceFile.OwnerUserId, User, sourceFile.SpaceId))
			return Forbid();

		// Parse destination
		var destSegments = ParseDestinationHeader();
		if (destSegments == null) return BadRequest();

		var destParams = ParseDestinationSegments(destSegments);
		if (destParams == null) return BadRequest();

		// Resolve destination context
		var destCtx = await ResolveEntity(destParams.Value.entityType, destParams.Value.entityName, ct);
		if (destCtx == null) return NotFound();

		// Map destination policy
		var destPolicy = WebDavHelpers.MapPolicyFolder(destParams.Value.policy, destCtx.IsSpaceContext);
		if (destPolicy == null) return BadRequest();

		// Check write access on destination
		var userId = User.GetUserId();
		if (destParams.Value.entityType == EEntityTypes.Users && destCtx.OwnerId != userId) return Forbid();
		if (destParams.Value.entityType == EEntityTypes.Spaces && !await accessControl.CheckSpaceAccess(destCtx.SpaceId!.Value, userId, ct))
			return Forbid();

		// Check if destination exists
		var destFile = await WebDavHelpers.FindFile(db, destCtx.SpaceId, destCtx.OwnerId, destPolicy.Value, destParams.Value.fileName, ct);
		if (destFile != null)
		{
			var overwrite = Request.Headers["Overwrite"].FirstOrDefault() != "F";
			if (!overwrite) return StatusCode(412); // Precondition Failed
			await fileService.DeleteFile(destFile, ct);
		}

		// Update file metadata
		// Note: OwnerUserId is preserved during MOVE - only location (SpaceId) and policy change
		sourceFile.FileName = destParams.Value.fileName;
		sourceFile.SpaceId = destCtx.SpaceId;
		sourceFile.AccessPolicy = destPolicy.Value;
		await db.SaveChangesAsync(ct);

		return NoContent();
	}

	[AcceptVerbs("COPY")]
	[Route("{entityType}/{entityName}/{policy}/{fileName}")]
	public async Task<IActionResult> CopyFile(EEntityTypes entityType, string entityName, EEntityPolicy policy, string fileName, CancellationToken ct)
	{
		// Get source file stream
		var sourceResult = await GetFile(entityType, entityName, policy, fileName, ct);
		if (sourceResult == null) return NotFound();

		// Parse destination
		var destSegments = ParseDestinationHeader();
		if (destSegments == null) return BadRequest();

		var destParams = ParseDestinationSegments(destSegments);
		if (destParams == null) return BadRequest();

		// Resolve destination context
		var destCtx = await ResolveEntity(destParams.Value.entityType, destParams.Value.entityName, ct);
		if (destCtx == null) return NotFound();

		// Map destination policy
		var destPolicy = WebDavHelpers.MapPolicyFolder(destParams.Value.policy, destCtx.IsSpaceContext);
		if (destPolicy == null) return BadRequest();

		// Check write access on destination
		var userId = User.GetUserId();
		if (destParams.Value.entityType == EEntityTypes.Users && destCtx.OwnerId != userId) return Forbid();
		if (destParams.Value.entityType == EEntityTypes.Spaces && !await accessControl.CheckSpaceAccess(destCtx.SpaceId!.Value, userId, ct))
			return Forbid();

		// Check if destination exists
		var destFile = await WebDavHelpers.FindFile(db, destCtx.SpaceId, destCtx.OwnerId, destPolicy.Value, destParams.Value.fileName, ct);
		bool created = destFile == null;

		if (destFile != null)
		{
			var overwrite = Request.Headers["Overwrite"].FirstOrDefault() != "F";
			if (!overwrite) return StatusCode(412); // Precondition Failed
			await fileService.DeleteFile(destFile, ct);
		}

		// Copy file (streams directly from source to destination without loading into memory)
		await using (sourceResult.Value.stream)
		{
			var owner = new RessourceOwnerHelper { UserId = destCtx.OwnerId, SpaceId = destCtx.SpaceId };
			var result = await fileService.StoreFile(sourceResult.Value.stream, destParams.Value.fileName, sourceResult.Value.contentType, owner, destPolicy.Value, ct);
			if (result.IsFailed) return StatusCode(500);
		}

		return created ? StatusCode(201) : NoContent();
	}

	// --- PROPFIND Handlers ---

	private async Task<List<XElement>> ListUsers(int depth, CancellationToken ct)
	{
		var responses = new List<XElement> { WebDavXmlBuilder.CreateCollection("/webdav/users/", "Users") };
		if (depth < 1) return responses;

		var userId = this.User.GetUserId();

		var users = await db.Users.Cacheable().AsNoTracking().ToListAsync(ct);

		responses.AddRange(users.Select(user => WebDavXmlBuilder.CreateCollection(
			WebDavXmlBuilder.BuildHref("users", user.Username),
			user.Id.Equals(userId) ? $"{user.Nickname} ({localizer["MyFiles"]})" : user.Nickname,
			user.RegisterDate)));

		return responses;
	}

	private async Task<List<XElement>> ListSpaces(int depth, CancellationToken ct)
	{
		var responses = new List<XElement> { WebDavXmlBuilder.CreateCollection("/webdav/spaces/", "Spaces") };
		if (depth < 1) return responses;

		var userId = this.User.GetUserId();

		var memberSpaces = await spaceService.GetMemberSpaces(userId);
		var publicSpaces = await spaceService.GetPublicSpaces(RessourceAccessPolicy.Members);

		var spaces = new List<Space>([..memberSpaces, ..publicSpaces])
			.GroupBy(s => s.Id)
			.Select(g => g.First())
			.ToList();

		responses.AddRange(spaces.Select(space => WebDavXmlBuilder.CreateCollection(
			WebDavXmlBuilder.BuildHref("spaces", space.Name),
			space.Name,
			space.CreatedAt)));

		return responses;
	}

	private async Task<List<XElement>?> ShowEntityFolder(EEntityTypes entityType, string name, int depth, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return null;

		var responses = new List<XElement>
		{
			WebDavXmlBuilder.CreateCollection(
				WebDavXmlBuilder.BuildHref(entityType.ToString().ToLower(), name),
				ctx.FancyName ?? name,
				ctx.CreatedAt)
		};

		if (depth < 1) return responses;

		var userId =  this.User.GetUserId();

		var canSeePrivate = ctx.IsSpaceContext ? await accessControl.CheckSpaceAccess((Guid)ctx.SpaceId, userId, ct) : ctx.OwnerId == userId;

		responses.AddRange(WebDavHelpers.CreatePolicyFolderResponses(entityType, name, ctx.CreatedAt, canSeePrivate, localizer));

		return responses;
	}

	private async Task<List<XElement>?> ListEntityFiles(EEntityTypes entityType, string name, EEntityPolicy policyFolder, int depth, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return null;

		var policy = WebDavHelpers.MapPolicyFolder(policyFolder, ctx.IsSpaceContext);
		if (policy == null) return null;

		var responses = new List<XElement>
		{
			WebDavXmlBuilder.CreateCollection(
				WebDavXmlBuilder.BuildHref(entityType.ToString().ToLower(), name, policyFolder.ToString().ToLower()),
				policyFolder.ToString(),
				ctx.CreatedAt)
		};

		if (depth < 1) return responses;

		// Get all files in the policy folder
		var allFiles = await WebDavHelpers.ListFilesInPolicyFolder(db, ctx.SpaceId, ctx.SpaceId.HasValue ? null : ctx.OwnerId, policy.Value, ct);

		// Filter by read access permissions
		var accessibleFiles = new List<FileMetadata>();
		foreach (var file in allFiles)
		{
			if (await accessControl.CheckAccessPolicy(file.AccessPolicy, AccessIntent.Read, file.OwnerUserId, User, file.SpaceId))
			{
				accessibleFiles.Add(file);
			}
		}

		// Create XML responses for accessible files
		var fileResponses = WebDavHelpers.CreateFileResponses(accessibleFiles, entityType.ToString().ToLower(), name, policyFolder);
		responses.AddRange(fileResponses);

		return responses;
	}

	private async Task<(Stream stream, string contentType, string fileName)?> GetFile(EEntityTypes entityType, string name, EEntityPolicy policyFolder, string fileName, CancellationToken ct)
	{
		var resolved = await this.ResolveFile(entityType, name, policyFolder, fileName, ct);

		if (resolved == null) return null;

		var (ctx, policy, file) = resolved;
		
		if (file == null) return null;
		
		if (!await accessControl.CheckAccessPolicy(file.AccessPolicy, AccessIntent.Read, file.OwnerUserId, User, file.SpaceId))
			return null;

		var result = await fileService.GetFile(file.Id, ct);
		return result.IsFailed ? null : (result.Value.FileStream, file.ContentType, file.FileName);
	}

	private async Task<ResolvedFile?> ResolveFile(EEntityTypes entityType, string name, EEntityPolicy policyFolder, string fileName, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return null;

		var policy = WebDavHelpers.MapPolicyFolder(policyFolder, ctx.IsSpaceContext);
		if (policy == null) return null;
		
		var file = await WebDavHelpers.FindFile(db, ctx.SpaceId, ctx.OwnerId, policy.Value, fileName, ct);
		
		return new ResolvedFile((EntityContext)ctx, (RessourceAccessPolicy)policy, file);
	}

	// --- Entity Resolution ---

	private async Task<EntityContext?> ResolveEntity(EEntityTypes entityType, string name, CancellationToken ct)
	{
		return entityType switch
		{
			EEntityTypes.Users => await GetUserEntityContextCached(name, ct),
			EEntityTypes.Spaces => await GetSpaceEntityContextCached(name, ct),
			_ => null
		};
	}
	
	private async Task<EntityContext?> GetUserEntityContextCached(string username, CancellationToken ct)
	{
		var user = await db.Users.Cacheable().AsNoTracking().Select(u => new {u.Id, u.Username, u.RegisterDate, u.Nickname}).Where(u => u.Username == username).FirstOrDefaultAsync(ct);
		return user != null ? new EntityContext(user.Id, null, user.RegisterDate, user.Nickname) : null;
	}

	private async Task<EntityContext?> GetSpaceEntityContextCached(string spaceName, CancellationToken ct)
	{
		var space = await db.Spaces.Cacheable().AsNoTracking().Select(s => new { s.Id, s.Name, s.CreatedAt, s.OwnerUserId }).Where(s => s.Name == spaceName).FirstOrDefaultAsync(ct);
		return space != null ? new EntityContext(space.OwnerUserId, space.Id, space.CreatedAt, space.Name) : null;
	}

	// --- Helper Methods ---

	/// <summary>
	/// Validates ETag preconditions (If-Match, If-None-Match) for a file.
	/// Returns an error result if validation fails, otherwise null.
	/// </summary>
	/// <param name="fileETag">The file's current ETag (typically the file hash)</param>
	/// <param name="isModificationRequest">True for PUT/DELETE (uses If-Match), false for GET (uses If-None-Match)</param>
	private IActionResult? ValidateETag(string fileETag, bool isModificationRequest)
	{
		var ifMatch = Request.Headers["If-Match"].FirstOrDefault();
		var ifNoneMatch = Request.Headers["If-None-Match"].FirstOrDefault();

		// Normalize ETag format (with or without quotes)
		var normalizedFileETag = fileETag.StartsWith('"') ? fileETag : $"\"{fileETag}\"";

		if (isModificationRequest && !string.IsNullOrEmpty(ifMatch))
		{
			// PUT/DELETE with If-Match: only proceed if ETag matches
			if (ifMatch != "*" && ifMatch != normalizedFileETag)
			{
				return StatusCode(412, new { Error = "Precondition Failed", Detail = "ETag mismatch" });
			}
		}

		if (!isModificationRequest && !string.IsNullOrEmpty(ifNoneMatch))
		{
			// GET with If-None-Match: return 304 if ETag matches (client has current version)
			if (ifNoneMatch == "*" || ifNoneMatch == normalizedFileETag)
			{
				return StatusCode(304); // Not Modified
			}
		}

		return null; // Validation passed
	}

	private static string[] ParsePath(string? path) =>
		string.IsNullOrWhiteSpace(path) ? [] : path.Split('/', StringSplitOptions.RemoveEmptyEntries);

	/// <summary>
	/// Parses the Depth header. Defaults to 1 if not specified.
	/// Note: "infinity" is rejected by ValidateWebDavDepthAttribute before reaching this method.
	/// </summary>
	private int GetDepth() => Request.Headers["Depth"].FirstOrDefault() switch
	{
		"0" => 0,
		"1" => 1,
		_ => 1  // Default to 1 if header is missing
	};

	private string[]? ParseDestinationHeader()
	{
		var dest = Request.Headers["Destination"].FirstOrDefault();
		if (string.IsNullOrEmpty(dest)) return null;

		if (Uri.TryCreate(dest, UriKind.Absolute, out var uri))
			dest = uri.AbsolutePath;

		const string prefix = "/webdav/";
		if (dest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			dest = dest[prefix.Length..];

		return ParsePath(dest);
	}

	private (EEntityTypes entityType, string entityName, EEntityPolicy policy, string fileName)? ParseDestinationSegments(string[] segments)
	{
		if (segments.Length != 4) return null;

		// Expected: ["users"|"spaces", entityName, policy, fileName]
		if (!Enum.TryParse<EEntityTypes>(segments[0], true, out var entityType)) return null;
		if (!Enum.TryParse<EEntityPolicy>(segments[2], true, out var policy)) return null;

		// Decode URL-encoded segments (handles spaces and special characters)
		var entityName = Uri.UnescapeDataString(segments[1]);
		var fileName = Uri.UnescapeDataString(segments[3]);

		return (entityType, entityName, policy, fileName);
	}

	/// <summary>
	/// Determines the content type for a file being uploaded via WebDAV PUT.
	/// In WebDAV, the Content-Type header of the PUT request represents the content type
	/// of the file being uploaded, not the request body format.
	/// Falls back to file extension detection if not provided or generic.
	/// </summary>
	private string DetermineContentType(string fileName)
	{
		// In WebDAV PUT, Request.ContentType is the actual file's content type
		var contentType = Request.ContentType;
		if (string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream")
		{
			// Fallback: Try to determine from file extension
			if (!ContentTypeProvider.TryGetContentType(fileName, out contentType))
				contentType = "application/octet-stream";
		}
		return contentType;
	}

	private IActionResult MultiStatus(List<XElement> responses)
	{
		var doc = new XDocument(
			new XDeclaration("1.0", "utf-8", null),
			new XElement(XNamespace.Get("DAV:") + "multistatus",
				new XAttribute(XNamespace.Xmlns + "D", "DAV:"),
				responses
			)
		);

		Response.StatusCode = 207;
		Response.ContentType = "application/xml; charset=utf-8";
		return Content(doc.Declaration + Environment.NewLine + doc.Root!.ToString(SaveOptions.DisableFormatting), "application/xml; charset=utf-8");
	}
}
