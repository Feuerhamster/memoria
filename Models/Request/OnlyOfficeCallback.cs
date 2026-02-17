using System.Text.Json.Serialization;

namespace Memoria.Models.Request;

public class OnlyOfficeCallbackRequest
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("users")]
    public List<string>? Users { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

public class OnlyOfficeCallbackResponse
{
    [JsonPropertyName("error")]
    public int Error { get; set; }
}

public static class OnlyOfficeCallbackStatus
{
    public const int NotFound = 0;
    public const int BeingEdited = 1;
    public const int ReadyForSaving = 2;
    public const int SaveError = 3;
    public const int ClosedWithoutChanges = 4;
    public const int BeingEditedButSaveAnyway = 6;
    public const int ForceSaveError = 7;
}
