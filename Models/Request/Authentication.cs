using System.ComponentModel.DataAnnotations;

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