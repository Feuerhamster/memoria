namespace Memoria.Models.Response;

public class OnlyOfficeEditorConfig
{
    public string DocumentServerUrl { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public OnlyOfficeDocument Document { get; set; } = new();
    public OnlyOfficeEditorSettings EditorConfig { get; set; } = new();
    public string Token { get; set; } = string.Empty;
}

public class OnlyOfficeDocument
{
    public string FileType { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public OnlyOfficePermissions Permissions { get; set; } = new();
}

public class OnlyOfficePermissions
{
    public bool Edit { get; set; }
    public bool Download { get; set; }
    public bool Review { get; set; }
    public bool Comment { get; set; }
}

public class OnlyOfficeEditorSettings
{
    public string CallbackUrl { get; set; } = string.Empty;
    public OnlyOfficeUser User { get; set; } = new();
    public string Mode { get; set; } = "edit";
    public string Lang { get; set; } = "de";
}

public class OnlyOfficeUser
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
