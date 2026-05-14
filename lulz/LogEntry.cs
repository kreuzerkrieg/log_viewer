namespace lulz;

/// <summary>One parsed line from a Scylla/Seastar log file.</summary>
public record LogEntry(
    string Timestamp,
    string Level,
    string Shard,
    string Group,
    string Facility,
    string Message);

