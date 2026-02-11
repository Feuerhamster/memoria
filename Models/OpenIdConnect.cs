namespace Memoria.Models;

public class OidcProfileData()
{
	public string Subject { get; set; }
	public string Username { get; set; }
	public string? Nickname { get; set; }
	public string? Image { get; set; }
	public string? EMail { get; set; }
	
	public string Provider { get; set; }
	public DateTime IssuedAt  { get; set; } = DateTime.UtcNow;
}