using System.Xml.Linq;
using EFCoreSecondLevelCacheInterceptor;
using Memoria.Controllers;
using Memoria.Models;
using Memoria.Models.Database;
using Memoria.Models.WebDav;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Memoria.Services.WebDav;

/// <summary>
/// Simple helper methods for common WebDAV operations - no complex abstractions!
/// </summary>
public static class WebDavHelpers
{
	/// <summary>
	/// Creates the three policy folders (private, shared, public) as XML responses
	/// </summary>
	public static List<XElement> CreatePolicyFolderResponses(EEntityTypes basePath, string entityName, DateTime createdAt, bool includePrivate, IStringLocalizer<WebDavController> localizer)
	{
		var responses = new List<XElement>();

		if (includePrivate)
		{
			responses.Add(WebDavXmlBuilder.CreateCollection(
				WebDavXmlBuilder.BuildHref(basePath.ToString().ToLower(), entityName, "private"),
				localizer["Policy.Private"],
				createdAt
			));
		}

		responses.Add(WebDavXmlBuilder.CreateCollection(
			WebDavXmlBuilder.BuildHref(basePath.ToString().ToLower(), entityName, "shared"),
			localizer["Policy.Shared"],
			createdAt
		));

		responses.Add(WebDavXmlBuilder.CreateCollection(
			WebDavXmlBuilder.BuildHref(basePath.ToString().ToLower(), entityName, "public"),
			localizer["Policy.Public"],
			createdAt
		));

		return responses;
	}

	/// <summary>
	/// Lists files in a policy folder.
	/// Note: Does not perform access control checks - caller must filter results.
	/// </summary>
	public static async Task<List<FileMetadata>> ListFilesInPolicyFolder(
		AppDbContext db,
		Guid? spaceId,
		Guid? ownerId,
		RessourceAccessPolicy policy,
		CancellationToken ct)
	{
		var filesQuery = db.Files.Cacheable().AsNoTracking().Where(f => f.AccessPolicy == policy);

		if (spaceId.HasValue)
		{
			filesQuery = filesQuery.Where(f => f.SpaceId == spaceId);
		}
		else if (ownerId.HasValue)
		{
			filesQuery = filesQuery.Where(f => f.OwnerUserId == ownerId && f.SpaceId == null);
		}

		return await filesQuery.ToListAsync(ct);
	}

	/// <summary>
	/// Creates WebDAV XML responses for a list of files.
	/// </summary>
	public static List<XElement> CreateFileResponses(
		IEnumerable<FileMetadata> files,
		string basePath,
		string entityName,
		EEntityPolicy policyFolder,
		Func<Guid, List<LockInfo>>? getLocksForFile = null)
	{
		return (from file in files
			let href = WebDavXmlBuilder.BuildHref(false, basePath, entityName, policyFolder.ToString().ToLower(), file.FileName)
			let locks = getLocksForFile?.Invoke(file.Id)
			select WebDavXmlBuilder.CreateFile(href, file, locks)).ToList();
	}
	
	public static async Task<FileMetadata?> FindFile(
		AppDbContext db,
		Guid? spaceId,
		Guid ownerId,
		RessourceAccessPolicy policy,
		string fileName,
		CancellationToken ct)
	{
		var query = db.Files.Cacheable()
			.Where(f => f.FileName == fileName && f.AccessPolicy == policy && f.OwnerUserId == ownerId);

		if (spaceId.HasValue)
		{
			query = query.Where(f => f.SpaceId == spaceId);
		}

		return await query.FirstOrDefaultAsync(ct);
	}

	/// <summary>
	/// Maps policy folder name to RessourceAccessPolicy enum
	/// </summary>
	public static RessourceAccessPolicy? MapPolicyFolder(EEntityPolicy policy, bool isSpaceContext) => policy switch
	{
		EEntityPolicy.Private => isSpaceContext ? RessourceAccessPolicy.Members : RessourceAccessPolicy.Private,
		EEntityPolicy.Shared  => RessourceAccessPolicy.Shared,
		EEntityPolicy.Public  => RessourceAccessPolicy.Public,
		_ => null
	};
}
