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
	/// Creates the four access policy folders as XML responses
	/// </summary>
	public static List<XElement> CreatePolicyFolderResponses(string spaceName, DateTime createdAt, bool isMember, IStringLocalizer<WebDavController> localizer)
	{
		var responses = new List<XElement>
		{
			WebDavXmlBuilder.CreateCollection(
				WebDavXmlBuilder.BuildHref(spaceName, "public"),
				localizer["Policy.Public"],
				createdAt
			),
			WebDavXmlBuilder.CreateCollection(
				WebDavXmlBuilder.BuildHref(spaceName, "shared"),
				localizer["Policy.Shared"],
				createdAt
			)
		};
		
		if (!isMember) return responses;
		
		// Members and Private folders only visible to space members
		responses.Add(WebDavXmlBuilder.CreateCollection(
			WebDavXmlBuilder.BuildHref(spaceName, "members"),
			localizer["Policy.Members"],
			createdAt
		));

		responses.Add(WebDavXmlBuilder.CreateCollection(
			WebDavXmlBuilder.BuildHref(spaceName, "private"),
			localizer["Policy.Private"],
			createdAt
		));

		return responses;
	}

	/// <summary>
	/// Lists files in a policy folder.
	/// Note: Does not perform access control checks - caller must filter results.
	/// </summary>
	public static Task<List<FileMetadata>> ListFilesInPolicyFolder(
		AppDbContext db,
		Guid spaceId,
		RessourceAccessPolicy policy,
		CancellationToken ct)
	{
		return db.Files.Cacheable().AsNoTracking().Where(f => f.AccessPolicy == policy && f.SpaceId == spaceId).ToListAsync(ct);
	}

	/// <summary>
	/// Creates WebDAV XML responses for a list of files.
	/// </summary>
	public static List<XElement> CreateFileResponses(
		IEnumerable<FileMetadata> files,
		string spaceName,
		RessourceAccessPolicy policy,
		Func<Guid, List<LockInfo>>? getLocksForFile = null)
	{
		return (from file in files
			let href = WebDavXmlBuilder.BuildHref(false, spaceName, policy.ToString().ToLower(), file.FileName)
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
}
