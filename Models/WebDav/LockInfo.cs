namespace Memoria.Models.WebDav;

/// <summary>
/// Represents the scope of a WebDAV lock (RFC 4918 Section 6.1)
/// </summary>
public enum LockScope
{
	/// <summary>
	/// Exclusive lock - only one principal may modify the resource
	/// </summary>
	Exclusive,

	/// <summary>
	/// Shared lock - multiple principals may hold locks
	/// </summary>
	Shared
}

/// <summary>
/// Represents the type of WebDAV lock (RFC 4918 Section 6.2)
/// </summary>
public enum LockType
{
	/// <summary>
	/// Write lock - prevents modifications
	/// </summary>
	Write
}

/// <summary>
/// Represents an active WebDAV lock on a file
/// </summary>
public class LockInfo
{
	/// <summary>
	/// Unique lock token (opaquelocktoken:uuid)
	/// </summary>
	public string LockToken { get; set; } = string.Empty;

	/// <summary>
	/// File ID that is locked
	/// </summary>
	public Guid FileId { get; set; }

	/// <summary>
	/// User who created the lock
	/// </summary>
	public Guid OwnerUserId { get; set; }

	/// <summary>
	/// Optional owner information (free-form, typically username or href)
	/// </summary>
	public string? OwnerInfo { get; set; }

	/// <summary>
	/// Lock scope (exclusive or shared)
	/// </summary>
	public LockScope Scope { get; set; }

	/// <summary>
	/// Lock type (write)
	/// </summary>
	public LockType Type { get; set; }

	/// <summary>
	/// Lock depth (0 or infinity) - for collections
	/// </summary>
	public string Depth { get; set; } = "0";

	/// <summary>
	/// When the lock was created
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// When the lock expires (null = infinite)
	/// </summary>
	public DateTime? ExpiresAt { get; set; }

	/// <summary>
	/// Timeout in seconds (null = infinite)
	/// </summary>
	public int? TimeoutSeconds { get; set; }

	/// <summary>
	/// Check if the lock has expired
	/// </summary>
	public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

	/// <summary>
	/// Check if a user owns this lock
	/// </summary>
	public bool IsOwnedBy(Guid userId) => OwnerUserId == userId;
}
