using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Memoria.Exceptions;
using Memoria.Extensions;
using Memoria.Models;
using Memoria.Models.Config;
using Memoria.Models.Database;
using Memoria.Utils;
using System.Security.Claims;
using Memoria.Models.Response;

namespace Memoria.Services;

public interface IAuthenticationService {
	public Task<AuthCreationInfo> LogInUser(Guid userId, string userAgent);
	
	Result<OidcProfileData> DecodeAndVerifyOidcTransferToken(string token);
	AuthCreationInfo CreateCookieAuthData(string userId, string sessionId);
}

public class AuthenticationService : IAuthenticationService {
	private readonly OAuthConfig _oauthConfig;
	private readonly CookieConfig _cookieConfig;

	private readonly ISessionService _sessionService;
	
	private readonly byte[] _transferTokenSigningKey;
	
	public AuthenticationService(ISessionService sessionService, IKeyService keyService, IOptions<OAuthConfig> oauthConfig, IOptions<CookieConfig> cookieConfig) {
		this._sessionService = sessionService;
		this._oauthConfig = oauthConfig.Value;
		this._cookieConfig = cookieConfig.Value;
		
		this._transferTokenSigningKey = keyService.OidcTransferTokenKey;
	}
	
	/// <summary>
	/// Log in a user. This creates a jwt and a refresh session.
	/// </summary>
	/// <param name="userId"></param>
	/// <param name="userAgent"></param>
	/// <returns>Result containing a jwt and session token</returns>
	public async Task<AuthCreationInfo> LogInUser(Guid userId, string userAgent) {
		var session = await this._sessionService.CreateSession(userId, userAgent);

		return this.CreateCookieAuthData(userId.ToString(), session.Id.ToString());
	}

	public AuthCreationInfo CreateCookieAuthData(string userId, string sessionId)
	{
		var identity = new ClaimsIdentity([
			new Claim(CustomClaimNames.UserId, userId),
			new Claim(CustomClaimNames.SessionId, sessionId),
			new Claim(CustomClaimNames.IssuedAt, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
		], CookieAuthenticationDefaults.AuthenticationScheme);

		var authProps = new AuthenticationProperties
		{
			IsPersistent = true, IssuedUtc = DateTime.UtcNow, ExpiresUtc = DateTime.UtcNow.Add(this._cookieConfig.Lifetime), AllowRefresh = true
		};

		return new AuthCreationInfo(identity, authProps);
	}

	public Result<OidcProfileData> DecodeAndVerifyOidcTransferToken(string token)
	{
		var transferObjectRes = TrustedDataProvider.DecodeAndVerifyData<OidcProfileData>(token, this._transferTokenSigningKey);

		if (transferObjectRes.IsFailed)
		{
			return new Result<OidcProfileData>(transferObjectRes.Exception);
		}
		
		var expiry = transferObjectRes.Value.IssuedAt.Add(_oauthConfig.LoginTokenExpiry);
		
		if (DateTime.UtcNow > expiry)
		{
			return new Result<OidcProfileData>(new TokenExpiryReached());
		}

		return new Result<OidcProfileData>(transferObjectRes.Value);
	}
}