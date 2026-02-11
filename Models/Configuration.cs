namespace Memoria.Models.Config;

public class DatabaseConfig {
	public const string ConfigKey = "Database";

	public string ConnectionString { get; set; } = String.Empty;
}

public class CookieConfig
{
	public const string ConfigKey = "Cookie";
	
	public string CookieName { get; set; } = "memoria-authentication";
	public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(90);
	public TimeSpan ValidationInterval { get; set; } = TimeSpan.FromMinutes(1);
}

public class OAuthIdentityProvider {
	public string Identifier { get; set; } = String.Empty;
	public string Authority { get; set; } = String.Empty;
	public string ClientId { get; set; } = String.Empty;
	public string ClientSecret { get; set; } = String.Empty;
	public string RedirectUri { get; set; } = String.Empty;
	public string CallbackPath { get; set; } = String.Empty;
}

public class OAuthConfig {
	public const string ConfigKey = "OAuth";
	public List<OAuthIdentityProvider> IdentityProviders { get; set; } = new();
	public TimeSpan LoginTokenExpiry { get; set; } = TimeSpan.FromMinutes(90);
}

public class SessionConfig {
	public const string ConfigKey = "Session";
	public TimeSpan Expiry { get; set; }
	public int TokenLength { get; set; }
}