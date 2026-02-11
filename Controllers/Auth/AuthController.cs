using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Memoria.Exceptions;
using Memoria.Models;
using Memoria.Models.Config;
using Memoria.Models.Database;
using Memoria.Models.Request;
using Memoria.Services;
using System.Security.Claims;
using Memoria.Models.Response;
using Microsoft.EntityFrameworkCore;
using IAuthenticationService = Memoria.Services.IAuthenticationService;

namespace Memoria.Controllers;

[ApiController]
[Route("auth/oidc")]
public class AuthController(IAuthenticationService authService, AppDbContext db, IOptions<OAuthConfig> oauthConfig) : ControllerBase {
	
	[HttpGet("flow/{provider}")]
	public async Task<ActionResult> OAuthIdpInitFlow(string provider)
	{
		var idpConfig = oauthConfig.Value.IdentityProviders.FirstOrDefault(idp => idp.Identifier == provider);

		if (idpConfig == null)
		{
			return new LoginFailedApiException();
		}
		
		return this.Challenge(new AuthenticationProperties() { RedirectUri = idpConfig.RedirectUri }, idpConfig.Identifier);
	}
	
	[HttpPost("login")]
	public async Task<ActionResult<AuthResponse>> OAuthLogin([FromBody] OAuthLogin login) {
		var userAgent = this.HttpContext.Request.Headers["User-Agent"].ToString();
		var oidc = authService.DecodeAndVerifyOidcTransferToken(login.Token);
		
		if (oidc.IsFailed) {
			return new LoginFailedApiException(oidc.Exception);
		}

		var user = await db.Users.Where(u => u.OidcProvider == oidc.Value.Provider && u.OidcSub == oidc.Value.Subject)
			.FirstOrDefaultAsync();

		var loginType = ELoginType.Login;

		// register new user account with current OAuth provider
		if (user == null) {
			user = new User(oidc.Value);
			
			db.Users.Add(user);
			await db.SaveChangesAsync();
			
			loginType = ELoginType.Register;
		}
		else
		{
			user.EMail = oidc.Value.EMail;
			user.Nickname = oidc.Value.Nickname;
			user.Username = oidc.Value.Username;
			user.Image =  oidc.Value.Image;
			await db.SaveChangesAsync();
		}
		
		var authCreationInfo = await authService.LogInUser(user.Id, userAgent);

		await this.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(authCreationInfo.Identity), authCreationInfo.AuthProps);
		
		return Ok(new AuthResponse(new PublicUser(user), loginType));
	}
}