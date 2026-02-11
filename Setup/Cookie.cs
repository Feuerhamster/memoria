using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Memoria.Models.Config;

namespace Memoria.Setup;

public class ConfigureCookieOptions(IOptions<CookieConfig> cookieConfig): IConfigureNamedOptions<CookieAuthenticationOptions>
{
	public void Configure(CookieAuthenticationOptions options) => this.Configure(CookieAuthenticationDefaults.AuthenticationScheme, options);

	public void Configure(string? name, CookieAuthenticationOptions options)
	{
		options.SlidingExpiration = true;
		options.ExpireTimeSpan = cookieConfig.Value.Lifetime;
		options.Cookie.Name = cookieConfig.Value.CookieName;
		
		options.Events = new CookieAuthenticationEvents
		{
			OnRedirectToLogin = context =>
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				return Task.CompletedTask;
			},
			OnRedirectToAccessDenied = context =>
			{
				context.Response.StatusCode = StatusCodes.Status403Forbidden;
				return Task.CompletedTask;
			}
		};
	}
}