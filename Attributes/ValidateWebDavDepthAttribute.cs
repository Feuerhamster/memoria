using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Memoria.Attributes;

/// <summary>
/// Validates the WebDAV Depth header and rejects "infinity" requests.
/// WebDAV servers may refuse "infinity" to prevent DoS attacks (RFC 4918).
/// </summary>
public class ValidateWebDavDepthAttribute : ActionFilterAttribute
{
	public override void OnActionExecuting(ActionExecutingContext context)
	{
		var depthHeader = context.HttpContext.Request.Headers["Depth"].FirstOrDefault();

		if (string.Equals(depthHeader, "infinity", StringComparison.OrdinalIgnoreCase))
		{
			context.Result = new ObjectResult(new { Error = "Depth 'infinity' is not supported" })
			{
				StatusCode = 403
			};
		}

		base.OnActionExecuting(context);
	}
}
