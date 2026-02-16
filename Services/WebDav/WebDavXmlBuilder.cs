using System.Xml.Linq;
using Memoria.Models.Database;

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

	public static XElement CreateFile(string href, FileMetadata file)
	{
		return new XElement(Dav + "response",
			new XElement(Dav + "href", href),
			new XElement(Dav + "propstat",
				new XElement(Dav + "prop",
					new XElement(Dav + "displayname", file.FileName),
					new XElement(Dav + "resourcetype"),
					new XElement(Dav + "getcontentlength", file.SizeInBytes),
					new XElement(Dav + "getcontenttype", file.ContentType),
					new XElement(Dav + "creationdate", file.UploadedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")),
					new XElement(Dav + "getlastmodified", file.UploadedAt.ToString("R")),
					new XElement(Dav + "getetag", $"\"{file.FileHash}\"")
				),
				new XElement(Dav + "status", "HTTP/1.1 200 OK")
			)
		);
	}
}
