using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using EFCoreSecondLevelCacheInterceptor;
using Memoria.Extensions;
using Memoria.Models.Database;
using Memoria.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoria.Authentication;

public class BasicAuthHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder,
	AppDbContext db)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string SchemeName = "AppAccessToken";

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var authHeader = Request.Headers.Authorization.ToString();

		if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
		{
			return AuthenticateResult.NoResult();
		}

		string[] credentials;
		try
		{
			var base64 = authHeader.Split(" ")[1].Trim();
			var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
			credentials = decoded.Split(":");
			
			if (credentials.Length != 2) throw new FormatException("Invalid Basic auth format");
		}
		catch
		{
			return AuthenticateResult.Fail("Invalid Basic auth encoding");
		}

		var username = credentials[0];
		var password = credentials[1];
		
		if (!Guid.TryParse(username, out var userId))
		{
			return AuthenticateResult.Fail("Invalid user ID format");
		}

		var token = await db.AppAccessTokens
			.Cacheable()
			.AsNoTracking()
			.FirstOrDefaultAsync(t => t.UserId == userId && t.AccessToken == password);
			
		if (token == null)
		{
			return AuthenticateResult.Fail("Invalid access token");
		}

		var claims = new[]
		{
			new Claim(CustomClaimNames.UserId, userId.ToString()),
			new Claim(CustomClaimNames.TokenPermissions, ((int)token.Permissions).ToString()),
		};

		var identity = new ClaimsIdentity(claims, SchemeName);
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, SchemeName);

		return AuthenticateResult.Success(ticket);
	}

	protected override Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		Response.StatusCode = StatusCodes.Status401Unauthorized;
		Response.Headers.WWWAuthenticate = "Basic realm=\"Memoria WebDAV\"";
		return Task.CompletedTask;
	}
}
