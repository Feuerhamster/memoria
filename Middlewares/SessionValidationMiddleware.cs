using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Memoria.Extensions;
using Memoria.Models.Config;
using System.Security.Claims;
using IAuthenticationService = Memoria.Services.IAuthenticationService;

namespace Memoria.Middlewares;

public class SessionValidationMiddleware(ISessionService sessionService, IOptions<CookieConfig> cookieConfig, IAuthenticationService authService) : IMiddleware
{
	public async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		if (context.User.Identity?.IsAuthenticated != true || context.User.Identity.AuthenticationType != CookieAuthenticationDefaults.AuthenticationScheme)
		{
			await next(context);
			return;
		}

		var claims = context.User.GetAuthClaimsData();
		
		var expiry = claims.IssuedAtUtc.Add(cookieConfig.Value.ValidationInterval);
		
		// don't have to be validated, still fine
		if (DateTime.UtcNow < expiry)
		{
			await next(context);
			return;
		}

		var result = await sessionService.RenewSession(claims.SessionId, context.Request.Headers.UserAgent);

		if (result.IsFailed)
		{
			await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			await next(context);
			return;
		}

		var cookieAuthData = authService.CreateCookieAuthData(result.Value.UserId.ToString(), result.Value.Id.ToString());
		
		await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(cookieAuthData.Identity), cookieAuthData.AuthProps);
		
		await next(context);
	}
}

public static class SessionValidationMiddlewareExtensions
{
	public static IApplicationBuilder UseCustomSessionValidation(
		this IApplicationBuilder builder)
	{
		return builder.UseMiddleware<SessionValidationMiddleware>();
	}
}