namespace Memoria.Models.Response;

public class TimelinePageResponse
{
    public List<PostResponse> Items { get; set; } = new();

    /// <summary>Pass back as the `cursor` query param to fetch the next page. Null once there are no more items.</summary>
    public string? NextCursor { get; set; }
}
