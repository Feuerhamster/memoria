using System.Security.Cryptography;
using System.Text;
using Memoria;
using Memoria.Models.Database;
using Microsoft.Extensions.Options;
using Memoria.Services;
using MyCSharp.HttpUserAgentParser;
using Memoria.Exceptions;
using Memoria.Models.Config;
using Memoria.Utils;

public interface ISessionService {
	Task<UserRefreshSession> CreateSession(Guid userId, string? userAgent);
	Task<Result<UserRefreshSession>> RenewSession(Guid sessionId, string? userAgent);
	Task<Result<bool>> LogoutSession(Guid sessionId, string? userAgent);
	public string UserAgentHash(string userAgent);
	public bool CheckSessionUserAgentAuth(string? uaSessionHash, string? userAgent = null);
}

public class SessionService : ISessionService {
	private readonly SessionConfig _sessionOptions;
	private readonly AppDbContext _db;
	private readonly IKeyService _keyService;

	public SessionService(IOptions<SessionConfig> sessionOptions, AppDbContext db, IKeyService keyService) {
		this._sessionOptions = sessionOptions.Value;
		this._db = db;
		this._keyService = keyService;
	}

	/// <summary>
	/// Creates a new user session by generating a unique token, calculating user agent hash,
	/// and storing the session in the database.
	/// </summary>
	/// <param name="userId">The unique identifier of the user for whom the session is being created.</param>
	/// <param name="userAgent">The user agent string of the client initiating the session. Optional.</param>
	/// <returns>The created <see cref="UserRefreshSession"/> containing session details.</returns>
	public async Task<UserRefreshSession> CreateSession(Guid userId, string? userAgent) {
		var token = _keyService.GenerateRandomSecureUniqueString(this._sessionOptions.TokenLength);
		var uaHash = userAgent != null ? UserAgentHash(userAgent) : "unknown";

		var session = new UserRefreshSession(userId, uaHash);
		
		this._db.Sessions.Add(session);
		await _db.SaveChangesAsync();

		return session;
	}

	/// <summary>
	/// Renews an active session by generating a new token and updating relevant session details.
	/// </summary>
	/// <param name="token">The token of the session to be renewed.</param>
	/// <param name="userAgent">The user agent of the client requesting the session renewal. Used for validating session authenticity. Optional.</param>
	/// <returns>A result containing the updated session information if the renewal is successful; otherwise, an error encapsulated in the result.</returns>
	public async Task<Result<UserRefreshSession>> RenewSession(Guid sessionId, string? userAgent = null) {
		var session = await this._db.Sessions.FindAsync(sessionId);
		
		if (session == null) {
			return new Result<UserRefreshSession>(new SessionNotFoundException());
		}

		if (!CheckSessionUserAgentAuth(session.UserAgentHash, userAgent)) {
			return new Result<UserRefreshSession>(new SessionInvalidClientException());
		}
		
		session.UpdatedTime = DateTime.UtcNow;

		int updated = await this._db.SaveChangesAsync();

		if (updated < 1) {
			return new Result<UserRefreshSession>(new SessionNotRenewedException());
		}
		
		return new Result<UserRefreshSession>(session);
	}

	/// <summary>
	/// Terminates the session associated with the provided token if the session exists and is valid.
	/// </summary>
	/// <param name="sessionId">Session Id</param>
	/// <param name="userAgent">The user agent of the client requesting the logout. This is used to validate the session's authenticity.</param>
	/// <returns>True if the session was successfully terminated; otherwise, false.</returns>
	public async Task<Result<bool>> LogoutSession(Guid sessionId, string? userAgent = null) {
		var session = await this._db.Sessions.FindAsync(sessionId);

		if (session == null) {
			return new Result<bool>(new SessionNotFoundException());
		}
		
		if (!CheckSessionUserAgentAuth(session.UserAgentHash, userAgent)) {
			return new Result<bool>(new SessionInvalidClientException());
		}

		this._db.Sessions.Remove(session);
		int deleted = await this._db.SaveChangesAsync();
		
		return new Result<bool>(deleted > 0);
	}

	/// <summary>
	/// Validates whether the provided user agent matches the user agent hash for a session.
	/// </summary>
	/// <param name="uaSessionHash">The hashed representation of the session's user agent</param>
	/// <param name="userAgent">The current user agent string of the client making the request</param>
	/// <returns>True if the user agent matches the stored hash, otherwise false.</returns>
	public bool CheckSessionUserAgentAuth(string? uaSessionHash, string? userAgent = null) {
		if (string.IsNullOrEmpty(uaSessionHash)) return false;
		if (string.IsNullOrEmpty(userAgent)) return false;

		var uaHash = UserAgentHash(userAgent);
		return CryptographicOperations.FixedTimeEquals(
			Encoding.UTF8.GetBytes(uaHash),
			Encoding.UTF8.GetBytes(uaSessionHash)
		);
	}

	/// <summary>
	/// Generates a hashed representation of a user agent string. The hash is based on properties of the user agent such as its type, name, platform, and platform type.
	/// </summary>
	/// <param name="userAgent">The user agent string to be hashed.</param>
	/// <returns>A SHA-256 hash string representation of the parsed user agent information.</returns>
	public string UserAgentHash(string userAgent) {
		var ua = HttpUserAgentParser.Parse(userAgent);
		var platform = "default";
		var type = HttpUserAgentPlatformType.Unknown;

		if (ua.Platform != null) {
			var p = (HttpUserAgentPlatformInformation)ua.Platform;
			platform = p.Name;
			type = p.PlatformType;
		}

		var str = $"{ua.Type};{ua.Name};{platform};{type}";
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(str));
		return Convert.ToHexString(hash);
	}


}
