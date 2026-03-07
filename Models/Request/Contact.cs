namespace Memoria.Models.Request;

public class CreateContactRequest
{
    public string FormattedName { get; set; } = "";
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Organization { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Notes { get; set; }
}

public class UpdateContactRequest
{
    public string? FormattedName { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Organization { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Notes { get; set; }
}
