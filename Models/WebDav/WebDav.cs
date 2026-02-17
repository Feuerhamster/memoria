using Memoria.Models.Database;

namespace Memoria.Models.WebDav;

/// <summary>
/// Represents a resolved file with its space
/// </summary>
public record ResolvedFile(Space Space, FileMetadata? File);