using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Memoria.Exceptions;
using Memoria.Extensions;

namespace Memoria.Controllers;

[ApiController]
[Route("auth/sessions")]
[Authorize]
public class SessionController(ISessionService sessionService)  : ControllerBase {
	
	[HttpDelete("logout")]
	public async Task<ActionResult> LogoutSession()
	{
		var claims = this.User.GetAuthClaimsData();

		var logout = await sessionService.LogoutSession(claims.SessionId, this.HttpContext.Request.Headers.UserAgent);

		if (logout.IsFailed) {
			return new LogoutFailedApiException(logout.Exception);
		}

		if (logout.Value == false) {
			return new LogoutFailedApiException(new Exception("unknown error"));
		}

		await this.HttpContext.SignOutAsync();
		
		return Ok();
	}
}