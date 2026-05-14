namespace lulz;

using System.Text.RegularExpressions;

/// <summary>
/// Parses Scylla / Seastar text log files.
///
/// Supported formats:
///   INFO  2024-01-01 12:00:00,123 [shard  0] storage - message
///   WARN  2024-01-01T12:00:00.000 [shard  1:ioq] raft - message
///   DEBUG 2024-01-01 12:00:00,123 [shard  0] database - message
/// </summary>
public static class LogParser
{
    // LEVEL  TIMESTAMP  [shard N(:group)?]  facility  -  message
    private static readonly Regex LineRegex = new(
        @"^(\w+)\s+([\d-]+[T ][\d:,\.]+(?:[+-]\d{2}:?\d{2}|Z)?)\s+\[shard\s*(\d+)(?::(\w+))?\]\s+([\w_:\.]+)\s+-\s+(.+)$",
        RegexOptions.Compiled);

    /// <summary>Lazily parses every matching line in the file.</summary>
    public static IEnumerable<LogEntry> Parse(string filePath)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            var m = LineRegex.Match(line.TrimEnd());
            if (!m.Success)
                continue;

            yield return new LogEntry(
                Timestamp: m.Groups[2].Value,
                Level:     m.Groups[1].Value,
                Shard:     m.Groups[3].Value,
                Group:     m.Groups[4].Value,
                Facility:  m.Groups[5].Value,
                Message:   m.Groups[6].Value);
        }
    }
}

