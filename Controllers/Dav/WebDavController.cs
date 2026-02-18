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

[Route("dav/webdav")]
[Authorize(AuthenticationSchemes = BasicAuthHandler.SchemeName, Policy = "WebDavFiles")]
[EnsureWwwAuthenticate]
public class WebDavController(
	AppDbContext db,
	IFileStorageService fileService,
	IAccessPolicyHelperService accessControl,
	ISpaceService spaceService,
	IWebDavLockService lockService,
	IStringLocalizer<WebDavController> localizer) : ControllerBase
{
	private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

	[AcceptVerbs("OPTIONS")]
	[AllowAnonymous]
	public IActionResult Options()
	{
		Response.Headers["DAV"] = "1, 2"; // Class 1 and Class 2 support
		Response.Headers.Allow = "OPTIONS, PROPFIND, GET, PUT, DELETE, MOVE, COPY, LOCK, UNLOCK";
		return Ok();
	}

	[AcceptVerbs("PROPFIND")]
	[ValidateWebDavDepth]
	[Route("")]
	public async Task<IActionResult> RootDirectory(CancellationToken ct)
	{
		var depth = GetDepth();

		var responses = new List<XElement> { WebDavXmlBuilder.CreateCollection("/webdav/", "WebDAV") };
		if (depth < 1) return MultiStatus(responses);
		
		var userId = this.User.GetUserId();
		var memberSpaces = await spaceService.GetMemberSpaces(userId);
		var publicSpaces = await spaceService.GetPublicSpaces(RessourceAccessPolicy.Members);

		var spaces = new List<Space>([..memberSpaces, ..publicSpaces])
			.GroupBy(s => s.Id)
			.Select(g => g.First())
			.ToList();

		responses.AddRange(spaces.Select(space => WebDavXmlBuilder.CreateCollection(
			WebDavXmlBuilder.BuildHref(space.Name),
			space.Name,
			space.CreatedAt)));

		return MultiStatus(responses);
	}

	
	[AcceptVerbs("PROPFIND")]
	[ValidateWebDavDepth]
	[Route("{spaceName}")]
	public async Task<IActionResult> PolicyDirectory(string spaceName, CancellationToken ct)
	{
		var depth = GetDepth();
		var res = await ShowSpaceFolder(spaceName, depth, ct);
		return res == null ? NotFound() : MultiStatus(res);
	}
	
	[AcceptVerbs("PROPFIND")]
	[ValidateWebDavDepth]
	[Route("{spaceName}/{policy}")]
	public async Task<IActionResult> GetSpaceFileListByPolicy(string spaceName, RessourceAccessPolicy policy, CancellationToken ct)
	{
		var depth = GetDepth();
		var res = await ListSpaceFiles(spaceName, policy, depth, ct);
		return res == null ? NotFound() : MultiStatus(res);
	}

	[HttpGet("{spaceName}/{policy}/{fileName}")]
	public async Task<IActionResult> Get(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		var resolved = await this.ResolveFile(spaceName, policy, fileName, ct);
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
	[Route("{spaceName}/{policy}/{fileName}")]
	public async Task<IActionResult> GetFileMetadata(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		var resolved = await this.ResolveFile(spaceName, policy, fileName, ct);

		if (resolved?.File == null) return NotFound();

		var href = WebDavXmlBuilder.BuildHref(false, spaceName, policy.ToString().ToLower(), fileName);
		var locks = lockService.GetLocksForFile(resolved.File.Id);
		return MultiStatus(new List<XElement>([WebDavXmlBuilder.CreateFile(href, resolved.File, locks)]));
	}
	
	[HttpPut("{spaceName}/{policy}/{fileName}")]
	public async Task<IActionResult> UploadFile(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		var userId = User.GetUserId();

		var resolved = await ResolveFile(spaceName, policy, fileName, ct);
		if (resolved == null) return NotFound();

		if (resolved.File != null)
		{
			// File exists - update it
			// Check write access
			if (!await accessControl.CheckAccessPolicy(resolved.File, AccessIntent.Write, User))
			{
				return Forbid();
			}

			// Validate ETag preconditions (If-Match)
			var etagError = ValidateETag(resolved.File.FileHash, isModificationRequest: true);
			if (etagError != null) return etagError;

			// Validate lock access
			var lockError = ValidateLockAccess(resolved.File.Id, userId);
			if (lockError != null) return lockError;

			// Update existing file (keeps same ID)
			var updateResult = await fileService.UpdateFile(resolved.File, Request.Body, ct);

			return updateResult.IsFailed ? StatusCode(500) : NoContent();
		}

		// File doesn't exist - create new one
		var owner = new RessourceOwnerHelper { UserId = resolved.Space.OwnerUserId, SpaceId = resolved.Space.Id };
		var contentType = DetermineContentType(fileName);

		var result = await fileService.StoreFile(Request.Body, fileName, contentType, owner, policy, ct);

		return result.IsFailed ? StatusCode(500) : StatusCode(201);
	}

	[AcceptVerbs("DELETE")]
	[Route("{spaceName}/{policy}/{fileName}")]
	public async Task<IActionResult> Delete(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		var resolved = await ResolveFile(spaceName, policy, fileName, ct);
		if (resolved?.File == null) return NotFound();

		// Check write access
		if (!await accessControl.CheckAccessPolicy(resolved.File, AccessIntent.Write, User))
			return Forbid();

		// Validate ETag preconditions (If-Match)
		var etagError = ValidateETag(resolved.File.FileHash, isModificationRequest: true);
		if (etagError != null) return etagError;

		// Validate lock access
		var userId = User.GetUserId();
		var lockError = ValidateLockAccess(resolved.File.Id, userId);
		if (lockError != null) return lockError;

		await fileService.DeleteFile(resolved.File, ct);
		return NoContent();
	}

	[AcceptVerbs("LOCK")]
	[Route("{spaceName}/{policy}/{fileName}")]
	public async Task<IActionResult> LockFile(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		var resolved = await ResolveFile(spaceName, policy, fileName, ct);
		if (resolved?.File == null) return NotFound();

		var userId = User.GetUserId();

		// Check write access
		if (!await accessControl.CheckAccessPolicy(resolved.File, AccessIntent.Write, User))
			return Forbid();

		try
		{
			// Parse LOCK request body
			var lockRequest = await ParseLockRequest();
			if (lockRequest == null)
				return BadRequest(new { Error = "Invalid LOCK request" });

			var (scope, type, depth, ownerInfo, timeoutSeconds) = lockRequest.Value;

			// Create lock
			var lockInfo = lockService.CreateLock(
				resolved.File.Id,
				userId,
				ownerInfo,
				scope,
				type,
				depth,
				timeoutSeconds
			);

			// Build response
			var response = WebDavXmlBuilder.BuildLockResponse(lockInfo, Request.Path.Value!);

			Response.Headers["Lock-Token"] = $"<{lockInfo.LockToken}>";
			Response.StatusCode = 200;
			Response.ContentType = "application/xml; charset=utf-8";

			return Content(response, "application/xml; charset=utf-8");
		}
		catch (InvalidOperationException ex)
		{
			return StatusCode(423, new { Error = "Locked", Detail = ex.Message }); // 423 Locked
		}
	}

	[AcceptVerbs("UNLOCK")]
	[Route("{spaceName}/{policy}/{fileName}")]
	public async Task<IActionResult> UnlockFile(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		var resolved = await ResolveFile(spaceName, policy, fileName, ct);
		if (resolved?.File == null) return NotFound();

		// Get Lock-Token from header
		var lockTokenHeader = Request.Headers["Lock-Token"].FirstOrDefault();
		if (string.IsNullOrEmpty(lockTokenHeader))
			return BadRequest(new { Error = "Missing Lock-Token header" });

		// Parse token (format: <opaquelocktoken:uuid>)
		var lockToken = lockTokenHeader.Trim('<', '>');

		var lockInfo = lockService.GetLockByToken(lockToken);
		if (lockInfo == null)
			return StatusCode(409, new { Error = "Conflict", Detail = "Lock token not found" }); // 409 Conflict

		// Verify lock belongs to this file
		if (lockInfo.FileId != resolved.File.Id)
			return StatusCode(409, new { Error = "Conflict", Detail = "Lock token does not match this resource" });

		// Verify user owns the lock or has write access
		var userId = User.GetUserId();
		if (!lockInfo.IsOwnedBy(userId))
		{
			if (!await accessControl.CheckAccessPolicy(resolved.File, AccessIntent.Write, User))
				return Forbid();
		}

		// Remove lock
		if (lockService.RemoveLock(lockToken))
		{
			return NoContent();
		}

		return StatusCode(409, new { Error = "Conflict", Detail = "Failed to remove lock" });
	}

	/// <summary>
	/// MKCOL is not supported because this WebDAV server uses a fixed virtual folder structure.
	/// All collections (spaces, policy folders) are pre-defined and managed by the application.
	/// </summary>
	[AcceptVerbs("MKCOL")]
	[Route("{*path}")]
	public IActionResult MkCol(string? path = null)
	{
		// RFC 4918 Section 9.3: MKCOL creates a collection
		// Since we have a fixed virtual structure, creating arbitrary collections is not allowed
		return StatusCode(StatusCodes.Status403Forbidden, new
		{
			Error = "Method Not Allowed",
			Detail = "This WebDAV server uses a fixed folder structure. Collections cannot be created by clients."
		});
	}

	[AcceptVerbs("MOVE")]
	[Route("{spaceName}/{policy}/{fileName}")]
	public async Task<IActionResult> MoveFile(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		// Get source file
		var resolved = await ResolveFile(spaceName, policy, fileName, ct);
		if (resolved?.File == null) return NotFound();

		// Check write access on source
		if (!await accessControl.CheckAccessPolicy(resolved.File, AccessIntent.Write, User))
			return Forbid();

		// Validate lock access on source
		var userId = User.GetUserId();
		var lockError = ValidateLockAccess(resolved.File.Id, userId);
		if (lockError != null) return lockError;

		// Parse destination
		var destSegments = ParseDestinationHeader();
		if (destSegments == null) return BadRequest();

		var destParams = ParseDestinationSegments(destSegments);
		if (destParams == null) return BadRequest();

		// Resolve destination space
		var destSpace = await ResolveSpace(destParams.Value.spaceName, ct);
		if (destSpace == null) return NotFound();

		// Check write access on destination (must be space member)
		if (!await accessControl.CheckSpaceMembership(destSpace.Id, userId, ct))
			return Forbid();

		// Check if destination exists
		var destFile = await WebDavHelpers.FindFile(db, destSpace.Id, destSpace.OwnerUserId, destParams.Value.policy, destParams.Value.fileName, ct);
		if (destFile != null)
		{
			var overwrite = Request.Headers["Overwrite"].FirstOrDefault() != "F";
			if (!overwrite) return StatusCode(412); // Precondition Failed
			await fileService.DeleteFile(destFile, ct);
		}

		// Update file metadata
		// Note: OwnerUserId is preserved during MOVE - only location (SpaceId) and policy change
		resolved.File.FileName = destParams.Value.fileName;
		resolved.File.SpaceId = destSpace.Id;
		resolved.File.AccessPolicy = destParams.Value.policy;
		await db.SaveChangesAsync(ct);

		return NoContent();
	}

	[AcceptVerbs("COPY")]
	[Route("{spaceName}/{policy}/{fileName}")]
	public async Task<IActionResult> CopyFile(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		var userId = User.GetUserId();

		// Get source file stream
		var sourceResult = await GetFile(spaceName, policy, fileName, ct);
		if (sourceResult == null) return NotFound();

		// Parse destination
		var destSegments = ParseDestinationHeader();
		if (destSegments == null) return BadRequest();

		var destParams = ParseDestinationSegments(destSegments);
		if (destParams == null) return BadRequest();

		// Resolve destination space
		var destSpace = await ResolveSpace(destParams.Value.spaceName, ct);
		if (destSpace == null) return NotFound();

		// Check write access on destination (must be space member)
		if (!await accessControl.CheckSpaceMembership(destSpace.Id, userId, ct))
			return Forbid();

		// Check if destination exists
		var destFile = await WebDavHelpers.FindFile(db, destSpace.Id, destSpace.OwnerUserId, destParams.Value.policy, destParams.Value.fileName, ct);
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
			var owner = new RessourceOwnerHelper { UserId = destSpace.OwnerUserId, SpaceId = destSpace.Id };
			var result = await fileService.StoreFile(sourceResult.Value.stream, destParams.Value.fileName, sourceResult.Value.contentType, owner, destParams.Value.policy, ct);
			if (result.IsFailed) return StatusCode(500);
		}

		return created ? StatusCode(201) : NoContent();
	}

	// --- PROPFIND Handlers ---

	private async Task<List<XElement>?> ShowSpaceFolder(string spaceName, int depth, CancellationToken ct)
	{
		var space = await ResolveSpace(spaceName, ct);
		if (space == null) return null;

		var responses = new List<XElement>
		{
			WebDavXmlBuilder.CreateCollection(
				WebDavXmlBuilder.BuildHref(spaceName),
				space.Name,
				space.CreatedAt)
		};

		if (depth < 1) return responses;

		var userId = this.User.GetUserId();
		var isMember = await accessControl.CheckSpaceMembership(space.Id, userId, ct);

		responses.AddRange(WebDavHelpers.CreatePolicyFolderResponses(spaceName, space.CreatedAt, isMember, localizer));

		return responses;
	}

	private async Task<List<XElement>?> ListSpaceFiles(string spaceName, RessourceAccessPolicy policy, int depth, CancellationToken ct)
	{
		var space = await ResolveSpace(spaceName, ct);
		if (space == null) return null;

		var responses = new List<XElement>
		{
			WebDavXmlBuilder.CreateCollection(
				WebDavXmlBuilder.BuildHref(spaceName, policy.ToString().ToLower()),
				policy.ToString(),
				space.CreatedAt)
		};

		if (depth < 1) return responses;

		// Get all files in the policy folder
		var allFiles = await WebDavHelpers.ListFilesInPolicyFolder(db, space.Id, policy, ct);

		// Filter by read access permissions
		var accessibleFiles = new List<FileMetadata>();
		foreach (var file in allFiles)
		{
			if (await accessControl.CheckAccessPolicy(file, AccessIntent.Read, User))
			{
				accessibleFiles.Add(file);
			}
		}

		// Create XML responses for accessible files
		var fileResponses = WebDavHelpers.CreateFileResponses(
			accessibleFiles,
			spaceName,
			policy,
			fileId => lockService.GetLocksForFile(fileId));
		responses.AddRange(fileResponses);

		return responses;
	}

	private async Task<(Stream stream, string contentType, string fileName)?> GetFile(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		var resolved = await ResolveFile(spaceName, policy, fileName, ct);
		if (resolved?.File == null) return null;

		if (!await accessControl.CheckAccessPolicy(resolved.File, AccessIntent.Read, User))
			return null;

		var result = await fileService.GetFile(resolved.File.Id, ct);
		return result.IsFailed ? null : (result.Value.FileStream, resolved.File.ContentType, resolved.File.FileName);
	}

	private async Task<ResolvedFile?> ResolveFile(string spaceName, RessourceAccessPolicy policy, string fileName, CancellationToken ct)
	{
		var space = await ResolveSpace(spaceName, ct);
		if (space == null) return null;

		var file = await WebDavHelpers.FindFile(db, space.Id, space.OwnerUserId, policy, fileName, ct);

		return new ResolvedFile(space, file);
	}

	// --- Space Resolution ---

	private async Task<Space?> ResolveSpace(string spaceName, CancellationToken ct)
	{
		return await db.Spaces.Cacheable().AsNoTracking()
			.Where(s => s.Name == spaceName)
			.FirstOrDefaultAsync(ct);
	}

	// --- Helper Methods ---

	/// <summary>
	/// Validates lock access for a file. Returns error result if locked and user doesn't have access.
	/// </summary>
	private IActionResult? ValidateLockAccess(Guid fileId, Guid userId)
	{
		// Parse If header for lock tokens (RFC 4918 Section 10.4)
		var ifHeader = Request.Headers["If"].FirstOrDefault();
		string? lockToken = null;

		if (!string.IsNullOrEmpty(ifHeader))
		{
			// Simple parsing: look for (<opaquelocktoken:...>)
			var match = System.Text.RegularExpressions.Regex.Match(ifHeader, @"<(opaquelocktoken:[^>]+)>");
			if (match.Success)
				lockToken = match.Groups[1].Value;
		}

		// Validate lock access
		if (!lockService.ValidateLockAccess(fileId, userId, lockToken))
		{
			return StatusCode(423, new { Error = "Locked", Detail = "Resource is locked" }); // 423 Locked
		}

		return null;
	}

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

	/// <summary>
	/// Parses a LOCK request body (RFC 4918 Section 9.10)
	/// </summary>
	private async Task<(LockScope Scope, LockType Type, string Depth, string? OwnerInfo, int? TimeoutSeconds)?> ParseLockRequest()
	{
		try
		{
			Request.EnableBuffering();
			var doc = await XDocument.LoadAsync(Request.Body, LoadOptions.None, CancellationToken.None);
			Request.Body.Position = 0;

			var davNs = XNamespace.Get("DAV:");
			var root = doc.Root;
			if (root == null) return null;

			// Parse lockscope
			var scopeElement = root.Descendants(davNs + "lockscope").FirstOrDefault();
			var scope = scopeElement?.Elements().FirstOrDefault()?.Name.LocalName switch
			{
				"exclusive" => LockScope.Exclusive,
				"shared" => LockScope.Shared,
				_ => LockScope.Exclusive
			};

			// Parse locktype
			var typeElement = root.Descendants(davNs + "locktype").FirstOrDefault();
			var type = typeElement?.Elements().FirstOrDefault()?.Name.LocalName switch
			{
				"write" => LockType.Write,
				_ => LockType.Write
			};

			// Parse depth (from header, default to 0)
			var depth = Request.Headers["Depth"].FirstOrDefault() ?? "0";

			// Parse owner (optional)
			var ownerElement = root.Descendants(davNs + "owner").FirstOrDefault();
			var ownerInfo = ownerElement?.Value;

			// Parse timeout (from header)
			var timeoutHeader = Request.Headers["Timeout"].FirstOrDefault();
			int? timeoutSeconds = null;
			if (!string.IsNullOrEmpty(timeoutHeader))
			{
				// Format: "Second-3600" or "Infinite"
				if (timeoutHeader.StartsWith("Second-", StringComparison.OrdinalIgnoreCase))
				{
					if (int.TryParse(timeoutHeader[7..], out var seconds))
						timeoutSeconds = seconds;
				}
			}

			return (scope, type, depth, ownerInfo, timeoutSeconds);
		}
		catch
		{
			return null;
		}
	}

	private static string[] ParsePath(string? path) =>
		string.IsNullOrWhiteSpace(path) ? [] : path.Split('/', StringSplitOptions.RemoveEmptyEntries);
	
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

	private (string spaceName, RessourceAccessPolicy policy, string fileName)? ParseDestinationSegments(string[] segments)
	{
		if (segments.Length != 3) return null;
		
		if (!Enum.TryParse<RessourceAccessPolicy>(segments[1], true, out var policy)) return null;
		
		var spaceName = Uri.UnescapeDataString(segments[0]);
		var fileName = Uri.UnescapeDataString(segments[2]);

		return (spaceName, policy, fileName);
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
