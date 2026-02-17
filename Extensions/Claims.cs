using Memoria.Models.Database;
using Memoria.Models.Request;
using System.Globalization;
using System.Security.Claims;

namespace Memoria.Extensions;

public static class CustomClaimNames
{
	public const string UserId = "user_id";
	public const string SessionId = "session_id";
	public const string IssuedAt = "issued_at";
	public const string TokenPermissions = "token_permissions";
}

public static class ClaimsPrincipalExtensions
{
    public static JwtClaimsParsed GetAuthClaimsData(this ClaimsPrincipal principal)
    {
	    var rawUserId = principal.FindFirstValue(CustomClaimNames.UserId);
	    var rawSessionId = principal.FindFirstValue(CustomClaimNames.SessionId);
	    var issuedAt = DateTime.Parse(principal.FindFirstValue(CustomClaimNames.IssuedAt));

	    return new JwtClaimsParsed(Guid.Parse(rawUserId), Guid.Parse(rawSessionId), issuedAt);
    }

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
	    var rawUserId = principal.FindFirstValue(CustomClaimNames.UserId);
	    return Guid.Parse(rawUserId!);
    }

    public static EUserAppAccessTokenPermissions? GetTokenPermissions(this ClaimsPrincipal principal)
    {
	    var raw = principal.FindFirstValue(CustomClaimNames.TokenPermissions);
	    if (raw == null) return null;
	    return (EUserAppAccessTokenPermissions)int.Parse(raw);
    }
}
