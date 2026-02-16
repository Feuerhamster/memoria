using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Memoria.Attributes;

/// <summary>
/// Ensures that 401 Unauthorized responses include the WWW-Authenticate header.
/// Required for WebDAV clients to know which authentication scheme to use.
/// </summary>
public class EnsureWwwAuthenticateAttribute : ActionFilterAttribute
{
	private readonly string _realm;

	public EnsureWwwAuthenticateAttribute(string realm = "Memoria WebDAV")
	{
		_realm = realm;
	}

	public override void OnResultExecuting(ResultExecutingContext context)
	{
		if (context.HttpContext.Response.StatusCode == 401)
		{
			// Only set if not already present (don't override BasicAuthHandler)
			if (!context.HttpContext.Response.Headers.ContainsKey("WWW-Authenticate"))
			{
				context.HttpContext.Response.Headers.WWWAuthenticate = $"Basic realm=\"{_realm}\"";
			}
		}

		base.OnResultExecuting(context);
	}
}
