using Memoria.Models.WebDav;
using Microsoft.Extensions.Caching.Memory;

namespace Memoria.Services.WebDav;

public interface IWebDavLockService
{
	/// <summary>
	/// Creates a new lock on a file
	/// </summary>
	LockInfo CreateLock(Guid fileId, Guid ownerUserId, string? ownerInfo, LockScope scope, LockType type, string depth, int? timeoutSeconds);

	/// <summary>
	/// Removes a lock by its token
	/// </summary>
	bool RemoveLock(string lockToken);

	/// <summary>
	/// Gets a lock by its token
	/// </summary>
	LockInfo? GetLockByToken(string lockToken);

	/// <summary>
	/// Gets all active locks for a file
	/// </summary>
	List<LockInfo> GetLocksForFile(Guid fileId);

	/// <summary>
	/// Validates if a user can perform an operation on a locked file
	/// </summary>
	bool ValidateLockAccess(Guid fileId, Guid userId, string? providedLockToken);

	/// <summary>
	/// Refreshes a lock's timeout
	/// </summary>
	bool RefreshLock(string lockToken, int? newTimeoutSeconds);
}

/// <summary>
/// WebDAV lock management service using ASP.NET Core MemoryCache
/// </summary>
public class WebDavLockService : IWebDavLockService
{
	private readonly IMemoryCache _cache;
	private readonly ILogger<WebDavLockService> _logger;

	// Cache key prefix for lock tokens
	private const string LockTokenPrefix = "webdav_lock_token_";

	// Cache key prefix for file -> lock tokens mapping
	private const string FileLockPrefix = "webdav_file_locks_";

	// Default timeout: 3 hours
	private const int DefaultTimeoutSeconds = 3 * 60 * 60;

	// Maximum timeout: 24 hours
	private const int MaxTimeoutSeconds = 24 * 60 * 60;

	public WebDavLockService(IMemoryCache cache, ILogger<WebDavLockService> logger)
	{
		_cache = cache;
		_logger = logger;

		_logger.LogInformation("WebDAV Lock Service initialized (ASP.NET Core MemoryCache)");
	}

	public LockInfo CreateLock(Guid fileId, Guid ownerUserId, string? ownerInfo, LockScope scope, LockType type, string depth, int? timeoutSeconds)
	{
		// Validate and cap timeout
		int? timeout = timeoutSeconds switch
		{
			null => null,
			< 0 => DefaultTimeoutSeconds,
			> MaxTimeoutSeconds => MaxTimeoutSeconds,
			_ => timeoutSeconds.Value
		};

		// Check for exclusive lock conflicts
		if (scope == LockScope.Exclusive)
		{
			var existingLocks = GetLocksForFile(fileId);
			if (existingLocks.Any())
			{
				throw new InvalidOperationException($"File {fileId} already has active locks");
			}
		}

		// Generate lock token (opaquelocktoken:uuid format)
		var lockToken = $"opaquelocktoken:{Guid.NewGuid()}";

		var lockInfo = new LockInfo
		{
			LockToken = lockToken,
			FileId = fileId,
			OwnerUserId = ownerUserId,
			OwnerInfo = ownerInfo,
			Scope = scope,
			Type = type,
			Depth = depth,
			CreatedAt = DateTime.UtcNow,
			TimeoutSeconds = timeout,
			ExpiresAt = timeout.HasValue ? DateTime.UtcNow.AddSeconds(timeout.Value) : null
		};

		// Store lock with automatic expiration
		var cacheOptions = new MemoryCacheEntryOptions();
		if (timeout.HasValue)
		{
			cacheOptions.SetAbsoluteExpiration(TimeSpan.FromSeconds(timeout.Value));
		}

		// Store lock by token
		_cache.Set(LockTokenPrefix + lockToken, lockInfo, cacheOptions);

		// Add token to file's lock list
		var fileLocksKey = FileLockPrefix + fileId;
		var fileLocks = _cache.GetOrCreate(fileLocksKey, entry =>
		{
			if (timeout.HasValue)
			{
				entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(timeout.Value));
			}
			return new HashSet<string>();
		})!;

		fileLocks.Add(lockToken);
		_cache.Set(fileLocksKey, fileLocks, cacheOptions);

		_logger.LogInformation("Lock created: {LockToken} on file {FileId} by user {UserId} (expires in {Timeout}s)",
			lockToken, fileId, ownerUserId, timeout?.ToString() ?? "infinite");

		return lockInfo;
	}

	public bool RemoveLock(string lockToken)
	{
		var lockInfo = _cache.Get<LockInfo>(LockTokenPrefix + lockToken);
		if (lockInfo == null)
			return false;

		// Remove from cache
		_cache.Remove(LockTokenPrefix + lockToken);

		// Remove from file's lock list
		var fileLocksKey = FileLockPrefix + lockInfo.FileId;
		var fileLocks = _cache.Get<HashSet<string>>(fileLocksKey);
		if (fileLocks != null)
		{
			fileLocks.Remove(lockToken);
			if (fileLocks.Any())
			{
				_cache.Set(fileLocksKey, fileLocks);
			}
			else
			{
				_cache.Remove(fileLocksKey);
			}
		}

		_logger.LogInformation("Lock removed: {LockToken} from file {FileId}", lockToken, lockInfo.FileId);
		return true;
	}

	public LockInfo? GetLockByToken(string lockToken)
	{
		return _cache.Get<LockInfo>(LockTokenPrefix + lockToken);
	}

	public List<LockInfo> GetLocksForFile(Guid fileId)
	{
		var fileLocksKey = FileLockPrefix + fileId;
		var lockTokens = _cache.Get<HashSet<string>>(fileLocksKey);

		if (lockTokens == null || !lockTokens.Any())
			return new List<LockInfo>();

		var locks = new List<LockInfo>();
		foreach (var token in lockTokens.ToList()) // ToList to avoid modification during iteration
		{
			var lockInfo = _cache.Get<LockInfo>(LockTokenPrefix + token);
			if (lockInfo != null)
			{
				locks.Add(lockInfo);
			}
			else
			{
				// Lock expired, remove from file's lock list
				lockTokens.Remove(token);
			}
		}

		// Update file's lock list if tokens were removed
		if (lockTokens.Any())
		{
			_cache.Set(fileLocksKey, lockTokens);
		}
		else
		{
			_cache.Remove(fileLocksKey);
		}

		return locks;
	}

	public bool ValidateLockAccess(Guid fileId, Guid userId, string? providedLockToken)
	{
		var activeLocks = GetLocksForFile(fileId);

		// No locks = access granted
		if (!activeLocks.Any())
			return true;

		// Check if user owns any lock
		if (activeLocks.Any(l => l.IsOwnedBy(userId)))
			return true;

		// Check if provided lock token is valid
		if (!string.IsNullOrEmpty(providedLockToken))
		{
			var lockInfo = GetLockByToken(providedLockToken);
			if (lockInfo != null && lockInfo.FileId == fileId)
				return true;
		}

		// Access denied
		return false;
	}

	public bool RefreshLock(string lockToken, int? newTimeoutSeconds)
	{
		var lockInfo = GetLockByToken(lockToken);
		if (lockInfo == null)
			return false;

		// Validate and cap timeout
		int? timeout = newTimeoutSeconds switch
		{
			null => null,
			< 0 => DefaultTimeoutSeconds,
			> MaxTimeoutSeconds => MaxTimeoutSeconds,
			_ => newTimeoutSeconds.Value
		};

		// Update lock info
		lockInfo.TimeoutSeconds = timeout;
		lockInfo.ExpiresAt = timeout.HasValue ? DateTime.UtcNow.AddSeconds(timeout.Value) : null;

		// Re-store with new expiration
		var cacheOptions = new MemoryCacheEntryOptions();
		if (timeout.HasValue)
		{
			cacheOptions.SetAbsoluteExpiration(TimeSpan.FromSeconds(timeout.Value));
		}

		_cache.Set(LockTokenPrefix + lockToken, lockInfo, cacheOptions);

		// Also update file's lock list expiration
		var fileLocksKey = FileLockPrefix + lockInfo.FileId;
		var fileLocks = _cache.Get<HashSet<string>>(fileLocksKey);
		if (fileLocks != null)
		{
			_cache.Set(fileLocksKey, fileLocks, cacheOptions);
		}

		_logger.LogInformation("Lock refreshed: {LockToken} with timeout {Timeout}s", lockToken, timeout?.ToString() ?? "infinite");

		return true;
	}
}
