using System.Globalization;
using System.Text;

namespace Memoria.Utils;

/// <summary>
/// Opaque keyset-pagination cursor for timeline queries. Encodes the <c>CreatedAt</c> of the last
/// item on the previous page so the next page can filter with <c>WHERE CreatedAt &lt; cursor</c>
/// instead of an offset/skip, which stays fast no matter how deep the client pages.
/// </summary>
public readonly record struct PostCursor(DateTime CreatedAt)
{
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(CreatedAt.ToString("O", CultureInfo.InvariantCulture)));

    public static bool TryDecode(string? raw, out PostCursor cursor)
    {
        cursor = default;

        if (string.IsNullOrEmpty(raw)) return false;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
            var createdAt = DateTime.Parse(decoded, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            cursor = new PostCursor(createdAt);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
