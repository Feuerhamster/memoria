using Microsoft.AspNetCore.Authentication;
using Memoria.Models.Database;
using System.Security.Claims;

namespace Memoria.Models.Response;

public enum ELoginType {
	Login,
	Register,
}

public class PublicUser(User user) {
	public string Id { get; set; } = user.Id.ToString();
	public string Username { get; set; } = user.Username;
	public string Nickname { get; set; } = user.Nickname;
	public string Image { get; set; } = user.Image;
}

public class AuthResponse(PublicUser user, ELoginType type) {
	public PublicUser User { get; set; } = user;
	public ELoginType Type { get; set; } = type;
}

public class AuthCreationInfo(ClaimsIdentity identity, AuthenticationProperties authProps) {
	public ClaimsIdentity Identity { get; set; } = identity;
	public AuthenticationProperties AuthProps { get; set; } = authProps;
}

public class AddAppAccessTokenResponse
{
	public UserAppAccessToken AppAccessToken { get; set; }
	public string Secret { get; set; }
}