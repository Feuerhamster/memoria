using System.Xml.Linq;
using Memoria.Models.Database;

namespace Memoria.Services.WebDav;

/// <summary>
/// Simple XML builder for WebDAV responses
/// </summary>
public static class WebDavXmlBuilder
{
	private static readonly XNamespace Dav = "DAV:";

	public static string BuildHref(params string[] segments)
	{
		if (segments.Length == 0) return "/webdav/";
		var joined = string.Join("/", segments.Select(Uri.EscapeDataString));
		return $"/webdav/{joined}/";
	}

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
