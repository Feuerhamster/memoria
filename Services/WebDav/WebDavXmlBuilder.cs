using System.Xml.Linq;
using Memoria.Models.Database;
using Memoria.Models.WebDav;

namespace Memoria.Services.WebDav;

/// <summary>
/// Simple XML builder for WebDAV responses
/// </summary>
public static class WebDavXmlBuilder
{
	private static readonly XNamespace Dav = "DAV:";

	/// <summary>
	/// Builds a WebDAV href path from segments.
	/// Collections end with a trailing slash, files do not (RFC 4918 Section 8.3).
	/// </summary>
	/// <param name="isCollection">True for collections (adds trailing /), false for files</param>
	/// <param name="segments">Path segments to join</param>
	public static string BuildHref(bool isCollection, params string[] segments)
	{
		if (segments.Length == 0) return "/webdav/";
		var joined = string.Join("/", segments.Select(Uri.EscapeDataString));
		return isCollection ? $"/webdav/{joined}/" : $"/webdav/{joined}";
	}

	/// <summary>
	/// Builds a collection href (with trailing slash). Convenience overload.
	/// </summary>
	public static string BuildHref(params string[] segments) => BuildHref(true, segments);

	public static XElement CreateCollection(string href, string name, DateTime? created = null)
	{
		var createdAt = created ?? DateTime.UtcNow;

		return new XElement(Dav + "response",
			new XElement(Dav + "href", href),
			new XElement(Dav + "propstat",
				new XElement(Dav + "prop",
					new XElement(Dav + "displayname", name),
					new XElement(Dav + "resourcetype", new XElement(Dav + "collection")),
					new XElement(Dav + "creationdate", createdAt.ToString("yyyy-MM-ddTHH:mm:ssZ")),
					new XElement(Dav + "getlastmodified", createdAt.ToString("R"))
				),
				new XElement(Dav + "status", "HTTP/1.1 200 OK")
			)
		);
	}

	public static XElement CreateFile(string href, FileMetadata file, List<LockInfo>? activeLocks = null)
	{
		var props = new List<XElement>
		{
			new XElement(Dav + "displayname", file.FileName),
			new XElement(Dav + "resourcetype"),
			new XElement(Dav + "getcontentlength", file.SizeInBytes),
			new XElement(Dav + "getcontenttype", file.ContentType),
			new XElement(Dav + "creationdate", file.UploadedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")),
			new XElement(Dav + "getlastmodified", file.UploadedAt.ToString("R")),
			new XElement(Dav + "getetag", $"\"{file.FileHash}\""),
			CreateSupportedLockProperty(),
			CreateLockDiscoveryProperty(activeLocks, href)
		};

		return new XElement(Dav + "response",
			new XElement(Dav + "href", href),
			new XElement(Dav + "propstat",
				new XElement(Dav + "prop", props),
				new XElement(Dav + "status", "HTTP/1.1 200 OK")
			)
		);
	}

	/// <summary>
	/// Creates the supportedlock property (RFC 4918 Section 15.10)
	/// </summary>
	private static XElement CreateSupportedLockProperty()
	{
		return new XElement(Dav + "supportedlock",
			new XElement(Dav + "lockentry",
				new XElement(Dav + "lockscope", new XElement(Dav + "exclusive")),
				new XElement(Dav + "locktype", new XElement(Dav + "write"))
			),
			new XElement(Dav + "lockentry",
				new XElement(Dav + "lockscope", new XElement(Dav + "shared")),
				new XElement(Dav + "locktype", new XElement(Dav + "write"))
			)
		);
	}

	/// <summary>
	/// Creates the lockdiscovery property (RFC 4918 Section 15.8)
	/// </summary>
	private static XElement CreateLockDiscoveryProperty(List<LockInfo>? locks, string href)
	{
		if (locks == null || !locks.Any())
			return new XElement(Dav + "lockdiscovery");

		var activeLocks = locks.Select(lockInfo =>
		{
			var timeout = lockInfo.TimeoutSeconds.HasValue
				? $"Second-{lockInfo.TimeoutSeconds.Value}"
				: "Infinite";

			return new XElement(Dav + "activelock",
				new XElement(Dav + "locktype", new XElement(Dav + "write")),
				new XElement(Dav + "lockscope",
					lockInfo.Scope == LockScope.Exclusive
						? new XElement(Dav + "exclusive")
						: new XElement(Dav + "shared")
				),
				new XElement(Dav + "depth", lockInfo.Depth),
				lockInfo.OwnerInfo != null
					? new XElement(Dav + "owner", lockInfo.OwnerInfo)
					: null,
				new XElement(Dav + "timeout", timeout),
				new XElement(Dav + "locktoken",
					new XElement(Dav + "href", lockInfo.LockToken)
				),
				new XElement(Dav + "lockroot",
					new XElement(Dav + "href", href)
				)
			);
		});

		return new XElement(Dav + "lockdiscovery", activeLocks);
	}
	
	/// <summary>
	/// Builds a LOCK response XML (RFC 4918 Section 9.10.2)
	/// </summary>
	public static string BuildLockResponse(LockInfo lockInfo, string href)
	{
		var davNs = XNamespace.Get("DAV:");

		var timeout = lockInfo.TimeoutSeconds.HasValue
			? $"Second-{lockInfo.TimeoutSeconds.Value}"
			: "Infinite";

		var doc = new XDocument(
			new XDeclaration("1.0", "utf-8", null),
			new XElement(davNs + "prop",
				new XAttribute(XNamespace.Xmlns + "D", "DAV:"),
				new XElement(davNs + "lockdiscovery",
					new XElement(davNs + "activelock",
						new XElement(davNs + "locktype", new XElement(davNs + "write")),
						new XElement(davNs + "lockscope",
							lockInfo.Scope == LockScope.Exclusive
								? new XElement(davNs + "exclusive")
								: new XElement(davNs + "shared")
						),
						new XElement(davNs + "depth", lockInfo.Depth),
						lockInfo.OwnerInfo != null
							? new XElement(davNs + "owner", lockInfo.OwnerInfo)
							: null,
						new XElement(davNs + "timeout", timeout),
						new XElement(davNs + "locktoken",
							new XElement(davNs + "href", lockInfo.LockToken)
						),
						new XElement(davNs + "lockroot",
							new XElement(davNs + "href", href)
						)
					)
				)
			)
		);

		return doc.Declaration + Environment.NewLine + doc.Root!.ToString(SaveOptions.DisableFormatting);
	}
}
