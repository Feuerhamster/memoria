using System.ComponentModel.DataAnnotations;
using Memoria.Models.Database;

namespace Memoria.Models.Request;

public class JwtClaimsParsed(Guid userId, Guid sessionId, DateTime issuedAtUtc) {
	public Guid UserId { get; } = userId;
	public Guid SessionId { get; } = sessionId;
	public DateTime IssuedAtUtc { get; } = issuedAtUtc;
}

public class OAuthLogin {
	[Required]
	public string Token { get; set; }
}

public class CreateAccessTokenRequest
{
	[StringLength(64, MinimumLength = 1)]
	public string Name { get; set; }
	
	[Required]
	public EUserAppAccessTokenPermissions Permissions { get; set; }
}