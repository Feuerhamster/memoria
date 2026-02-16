using System.Xml.Linq;
using EFCoreSecondLevelCacheInterceptor;
using Memoria.Authentication;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.WebDav;
using Memoria.Services;
using Memoria.Services.WebDav;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Controllers;

public enum EEntityTypes { Space, User }

[Route("webdav/{*path}")]
[Authorize(AuthenticationSchemes = BasicAuthHandler.SchemeName, Policy = "WebDavFiles")]
public class WebDavController(
	AppDbContext db,
	IFileStorageService fileService,
	IAccessPolicyHelperService accessControl,
	ISpaceService spaceService) : ControllerBase
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
	public async Task<IActionResult> PropFind(string? path, CancellationToken ct)
	{
		var segments = ParsePath(path);
		var depth = GetDepth();
		var userId = User.GetUserId();

		var responses = segments switch
		{
			["users"] => await ListUsers(depth, ct),
			["users", var name] => await ShowEntityFolder("users", name, depth, ct),
			["users", var name, var policy] => await ListEntityFiles("users", name, policy, depth, ct),
			["spaces"] => await ListSpaces(depth, ct),
			["spaces", var name] => await ShowEntityFolder("spaces", name, depth, ct),
			["spaces", var name, var policy] => await ListEntityFiles("spaces", name, policy, depth, ct),
			_ => null
		};

		return responses == null ? NotFound() : MultiStatus(responses);
	}

	[AcceptVerbs("PROPFIND")]
	[Route("/")]
	public async Task<IActionResult> RootDirectory()
	{
		var depth = GetDepth();
		
		var responses = new List<XElement> { WebDavXmlBuilder.CreateCollection("/webdav/", "WebDAV") };
		if (depth < 1) return MultiStatus(responses);
		responses.Add(WebDavXmlBuilder.CreateCollection("/webdav/users/", "Users"));
		responses.Add(WebDavXmlBuilder.CreateCollection("/webdav/spaces/", "Spaces"));
		return MultiStatus(responses);
	}

	[HttpGet("/{entityType}/{entityName}/{policy}/{fileName}")]
	public async Task<IActionResult> Get(EEntityTypes entityType, string entityName, CancellationToken ct)
	{
		var segments = ParsePath(path);
		var result = segments switch
		{
			["users", var name, var policy, var file] => await GetFile("users", name, policy, file, ct),
			["spaces", var name, var policy, var file] => await GetFile("spaces", name, policy, file, ct),
			_ => null
		};

		if (result == null) return NotFound();

		return File(result.Value.stream, result.Value.contentType, enableRangeProcessing: true);
	}

	[AcceptVerbs("PUT")]
	public async Task<IActionResult> Put(string? path, CancellationToken ct)
	{
		var segments = ParsePath(path);
		var userId = User.GetUserId();

		var destination = segments switch
		{
			["users", var name, var policyFolder, var file] => await ResolvePut("users", name, policyFolder, file, userId, ct),
			["spaces", var name, var policyFolder, var file] => await ResolvePut("spaces", name, policyFolder, file, userId, ct),
			_ => null
		};

		if (destination == null) return StatusCode(403);

		var (ownerId, spaceId, fileName, policy) = destination.Value;
		var contentType = DetermineContentType(fileName);

		// Check if file exists and delete it
		var existingFile = await db.Files
			.FirstOrDefaultAsync(f => f.FileName == fileName &&
			                         (spaceId.HasValue ? f.SpaceId == spaceId : f.OwnerUserId == ownerId && f.SpaceId == null) &&
			                         f.AccessPolicy == policy, ct);

		if (existingFile != null)
			await fileService.DeleteFile(existingFile, ct);

		// Store new file
		var owner = new RessourceOwnerHelper { UserId = ownerId, SpaceId = spaceId };
		var result = await fileService.StoreFile(Request.Body, fileName, contentType, owner, policy, ct);

		return result.IsFailed ? StatusCode(500) : (existingFile != null ? NoContent() : StatusCode(201));
	}

	[AcceptVerbs("DELETE")]
	public async Task<IActionResult> Delete(string? path, CancellationToken ct)
	{
		var segments = ParsePath(path);
		var success = segments switch
		{
			["users", var name, var policy, var file] => await DeleteFile("users", name, policy, file, ct),
			["spaces", var name, var policy, var file] => await DeleteFile("spaces", name, policy, file, ct),
			_ => false
		};

		return success ? NoContent() : NotFound();
	}

	[AcceptVerbs("MKCOL")]
	public IActionResult MkCol()
	{
		Response.Headers.Allow = "OPTIONS, PROPFIND, GET, PUT, DELETE, MOVE, COPY";
		return StatusCode(405, new { error = "Folder creation is not supported. Only file operations are allowed." });
	}

	[AcceptVerbs("MOVE")]
	public async Task<IActionResult> Move(string? path, CancellationToken ct)
	{
		var sourceSegments = ParsePath(path);
		var destPath = ParseDestination();
		if (destPath == null) return StatusCode(400);

		var destSegments = ParsePath(string.Join('/', destPath));
		var userId = User.GetUserId();

		// Get source file metadata
		var sourceFile = await GetFileMetadata(sourceSegments, ct);
		if (sourceFile == null) return NotFound();

		// Check write access
		if (!await CanWrite(sourceSegments, userId, ct))
			return StatusCode(403);

		// Resolve destination
		var destination = await ResolvePutFromSegments(destSegments, userId, ct);
		if (destination == null) return StatusCode(403);

		// Update file metadata
		var sourceFileName = sourceSegments[^1];
		var file = await db.Files.FirstOrDefaultAsync(f => f.FileName == sourceFileName && f.OwnerUserId == userId, ct);
		if (file == null) return StatusCode(500);

		file.FileName = destination.Value.fileName;
		file.SpaceId = destination.Value.spaceId;
		file.OwnerUserId = destination.Value.ownerId;
		file.AccessPolicy = destination.Value.policy;
		await db.SaveChangesAsync(ct);

		return NoContent();
	}

	[AcceptVerbs("COPY")]
	public async Task<IActionResult> Copy(string? path, CancellationToken ct)
	{
		var sourceSegments = ParsePath(path);
		var destPath = ParseDestination();
		if (destPath == null) return StatusCode(400);

		var destSegments = ParsePath(string.Join('/', destPath));
		var userId = User.GetUserId();

		// Get source file stream
		var sourceResult = sourceSegments switch
		{
			["users", var name, var policy, var file] => await GetFile("users", name, policy, file, ct),
			["spaces", var name, var policy, var file] => await GetFile("spaces", name, policy, file, ct),
			_ => null
		};

		if (sourceResult == null) return NotFound();

		// Resolve destination
		var destination = await ResolvePutFromSegments(destSegments, userId, ct);
		if (destination == null) return StatusCode(403);

		// Copy file content
		await using (sourceResult.Value.stream)
		{
			var owner = new RessourceOwnerHelper { UserId = destination.Value.ownerId, SpaceId = destination.Value.spaceId };
			var result = await fileService.StoreFile(sourceResult.Value.stream, destination.Value.fileName, sourceResult.Value.contentType, owner, destination.Value.policy, ct);
			if (result.IsFailed) return StatusCode(500);
		}

		return StatusCode(201);
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
			user.Id.Equals(userId) ? $"{user.Nickname} (My Files)" : user.Nickname,
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

		responses.AddRange(spaces.Select(space => WebDavXmlBuilder.CreateCollection(WebDavXmlBuilder.BuildHref("spaces", space.Name), space.Name, space.CreatedAt)));
		
		return responses;
	}

	private async Task<List<XElement>?> ShowEntityFolder(string entityType, string name, int depth, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return null;

		var responses = new List<XElement>();

		if (depth < 1) return responses;
		
		var userId =  this.User.GetUserId();
		
		var canSeePrivate = ctx.IsSpaceContext ? await accessControl.CheckSpaceAccess((Guid)ctx.SpaceId, userId, ct) : ctx.OwnerId == userId;

		responses.AddRange(WebDavHelpers.CreatePolicyFolderResponses(entityType, name, ctx.CreatedAt, canSeePrivate));

		return responses;
	}

	private async Task<List<XElement>?> ListEntityFiles(string entityType, string name, string policyFolder, int depth, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return null;

		var policy = WebDavHelpers.MapPolicyFolder(policyFolder, ctx.IsSpaceContext);
		if (policy == null) return null;

		var responses = new List<XElement>
		{
			WebDavXmlBuilder.CreateCollection(WebDavXmlBuilder.BuildHref(entityType, name, policyFolder), policyFolder, ctx.CreatedAt)
		};

		if (depth < 1) return responses;
		
		var files = await WebDavHelpers.ListFilesInPolicyFolder(db, ctx.SpaceId, ctx.SpaceId.HasValue ? null : ctx.OwnerId, policy.Value, entityType, name, policyFolder, ct);
		responses.AddRange(files);

		return responses;
	}

	// --- Unified File Operations ---

	private async Task<(Stream stream, string contentType, string fileName)?> GetFile(string entityType, string name, string policyFolder, string fileName, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return null;

		var policy = WebDavHelpers.MapPolicyFolder(policyFolder, ctx.IsSpaceContext);
		if (policy == null) return null;

		var file = await WebDavHelpers.FindFile(db, ctx.SpaceId, ctx.SpaceId.HasValue ? null : ctx.OwnerId, policy.Value, fileName, ct);
		if (file == null) return null;

		if (!await accessControl.CheckAccessPolicy(file.AccessPolicy, AccessIntent.Read, ctx.OwnerId, User, ctx.SpaceId))
			return null;

		var result = await fileService.GetFile(file.Id, ct);
		return result.IsFailed ? null : (result.Value.FileStream, file.ContentType, file.FileName);
	}

	private async Task<(Guid ownerId, Guid? spaceId, string fileName, RessourceAccessPolicy policy)?> ResolvePut(string entityType, string name, string policyStr, string fileName, Guid userId, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return null;

		// Check permissions
		if (entityType == "users" && ctx.OwnerId != userId) return null;
		if (entityType == "spaces" && !await db.Spaces.AnyAsync(s => s.Name == name && (s.OwnerUserId == userId || s.Members.Any(m => m.Id == userId)), ct))
			return null;

		var policy = WebDavHelpers.MapPolicyFolder(policyStr, ctx.IsSpaceContext);
		if (policy == null) return null;

		return (ctx.OwnerId, ctx.SpaceId, fileName, policy.Value);
	}

	private async Task<bool> DeleteFile(string entityType, string name, string policyFolder, string fileName, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return false;

		var policy = WebDavHelpers.MapPolicyFolder(policyFolder, ctx.IsSpaceContext);
		if (policy == null) return false;

		var file = await WebDavHelpers.FindFile(db, ctx.SpaceId, ctx.SpaceId.HasValue ? null : ctx.OwnerId, policy.Value, fileName, ct);
		if (file == null) return false;

		if (!await accessControl.CheckAccessPolicy(file.AccessPolicy, AccessIntent.Write, ctx.OwnerId, User, ctx.SpaceId))
			return false;

		await fileService.DeleteFile(file, ct);
		return true;
	}

	// --- Move/Copy Helpers ---

	private async Task<FileMetadata?> GetFileMetadata(string[] segments, CancellationToken ct)
	{
		return segments switch
		{
			["users", var name, var policy, var file] => await FindEntityFile("users", name, policy, file, ct),
			["spaces", var name, var policy, var file] => await FindEntityFile("spaces", name, policy, file, ct),
			_ => null
		};
	}

	private async Task<FileMetadata?> FindEntityFile(string entityType, string name, string policyFolder, string fileName, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return null;

		var policy = WebDavHelpers.MapPolicyFolder(policyFolder, ctx.IsSpaceContext);
		return policy == null ? null : await WebDavHelpers.FindFile(db, ctx.SpaceId, ctx.SpaceId.HasValue ? null : ctx.OwnerId, policy.Value, fileName, ct);
	}

	private async Task<bool> CanWrite(string[] segments, Guid userId, CancellationToken ct)
	{
		return segments switch
		{
			["users", var name, var policy, var file] => await CanWriteEntityFile("users", name, policy, file, userId, ct),
			["spaces", var name, var policy, var file] => await CanWriteEntityFile("spaces", name, policy, file, userId, ct),
			_ => false
		};
	}

	private async Task<bool> CanWriteEntityFile(string entityType, string name, string policyFolder, string fileName, Guid userId, CancellationToken ct)
	{
		var ctx = await ResolveEntity(entityType, name, ct);
		if (ctx == null) return false;

		var policy = WebDavHelpers.MapPolicyFolder(policyFolder, ctx.IsSpaceContext);
		if (policy == null) return false;

		var file = await WebDavHelpers.FindFile(db, ctx.SpaceId, ctx.SpaceId.HasValue ? null : ctx.OwnerId, policy.Value, fileName, ct);
		return file != null && await accessControl.CheckAccessPolicy(file.AccessPolicy, AccessIntent.Write, ctx.OwnerId, User, ctx.SpaceId);
	}

	private async Task<(Guid ownerId, Guid? spaceId, string fileName, RessourceAccessPolicy policy)?> ResolvePutFromSegments(string[] segments, Guid userId, CancellationToken ct)
	{
		return segments switch
		{
			["users", var name, var policy, var file] => await ResolvePut("users", name, policy, file, userId, ct),
			["spaces", var name, var policy, var file] => await ResolvePut("spaces", name, policy, file, userId, ct),
			_ => null
		};
	}

	// --- Entity Resolution ---

	private async Task<EntityContext?> ResolveEntity(string entityType, string name, CancellationToken ct)
	{
		return entityType switch
		{
			"users" => await GetUserEntityContextCached(name, ct),
			"spaces" => await GetSpaceEntityContextCached(name, ct),
			_ => null
		};
	}
	
	private async Task<EntityContext?> GetUserEntityContextCached(string username, CancellationToken ct)
	{
		var user = await db.Users.Cacheable().AsNoTracking().Select(u => new {u.Id, u.Username, u.RegisterDate}).Where(u => u.Username == username).FirstOrDefaultAsync(ct);
		return user != null ? new EntityContext(user.Id, null, user.RegisterDate) : null;
	}

	private async Task<EntityContext?> GetSpaceEntityContextCached(string spaceName, CancellationToken ct)
	{
		var space = await db.Spaces.Cacheable().AsNoTracking().Select(s => new { s.Id, s.Name, s.CreatedAt, s.OwnerUserId }).Where(s => s.Name == spaceName).FirstOrDefaultAsync(ct);
		return space != null ? new EntityContext(space.OwnerUserId, space.Id, space.CreatedAt) : null;
	}

	// --- Helper Methods ---

	private static string[] ParsePath(string? path) =>
		string.IsNullOrWhiteSpace(path) ? [] : path.Split('/', StringSplitOptions.RemoveEmptyEntries);

	private int GetDepth() => Request.Headers["Depth"].FirstOrDefault() switch
	{
		"0" => 0,
		"1" => 1,
		_ => 1
	};

	private string[]? ParseDestination()
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

	private string DetermineContentType(string fileName)
	{
		var contentType = Request.ContentType;
		if (string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream")
		{
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
