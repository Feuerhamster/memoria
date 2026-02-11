namespace Memoria.Models.Database;

public class User {
	public User()
	{
		
	}

	public User(OidcProfileData oidcProfile)
	{
		this.Username = oidcProfile.Username;
		this.Nickname = oidcProfile.Nickname;
		this.Image = oidcProfile.Image;
		this.OidcSub = oidcProfile.Subject;
		this.OidcProvider = oidcProfile.Provider;
		this.EMail = oidcProfile.EMail;
	}
	
	public Guid Id { get; set; }
	
	public DateTime RegisterDate { get; set; } = DateTime.Now;
	
	public string Username { get; set; }
	public string Nickname { get; set; }
	public string? Image { get; set; }
	
	public string OidcSub {  get; set; }
	public string OidcProvider { get; set; }

	public string? EMail { get; set; }
}

public class UserRefreshSession {
	public UserRefreshSession()
	{
		
	}

	public UserRefreshSession(Guid userId, string uaHash)
	{
		this.UserId = userId;
		this.UserAgentHash = uaHash;
	}
	
	public Guid Id { get; set; }
	
	public Guid UserId { get; set; }
	
	public string? UserAgentHash { get; set; }
	
	public DateTime CreatedTime { get; set; } = DateTime.Now;
	
	public DateTime UpdatedTime { get; set; } = DateTime.Now;
}