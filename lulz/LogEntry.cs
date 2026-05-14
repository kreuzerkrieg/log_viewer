namespace lulz;

/// <summary>One parsed line from a Scylla/Seastar log file.</summary>
public record LogEntry(
    string Node,
    string Timestamp,   // normalized ISO: YYYY-MM-DD HH:MM:SS.mmm
    int    Level,       // 0=error 1=warn 2=info 3=debug 4=trace  (matches Scylla log_level enum)
    int?   Shard,       // null for entries that have no [shard N] prefix
    string Group,
    string Facility,
    string Message);
