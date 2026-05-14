namespace lulz;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Parses Scylla / Seastar text log files.
///
/// Supported formats:
///   With shard:    LEVEL  TIMESTAMP [shard N:group]  facility - message
///   Without shard: LEVEL  TIMESTAMP facility - message
///
/// Shard group variations handled:
///   [shard 0:main]   [shard 0: gms]   [shard 0:  mt]   [shard 0:sl:d]
///
/// Continuation lines (indented or otherwise non-matching) are appended
/// to the preceding entry's message.
/// </summary>
public static class LogParser
{
    // With [shard N(:group)?]
    private static readonly Regex WithShard = new(
        @"^(INFO|WARN|DEBUG|TRACE|ERROR)\s+([\d-]+ [\d:,]+)\s+\[shard\s*(\d+)(?::([\w:\s]+?))?\]\s+([\w_:\.]+)\s+-\s+(.+)$",
        RegexOptions.Compiled);

    // Without shard: LEVEL  TIMESTAMP  facility  -  message
    private static readonly Regex WithoutShard = new(
        @"^(INFO|WARN|DEBUG|TRACE|ERROR)\s+([\d-]+ [\d:,]+)\s+([\w_:\.]+)\s+-\s+(.+)$",
        RegexOptions.Compiled);

    // A line that starts a new log entry (level + date prefix)
    private static readonly Regex StartsEntry = new(
        @"^(?:INFO|WARN|DEBUG|TRACE|ERROR)\s+\d{4}-\d{2}-\d{2}",
        RegexOptions.Compiled);

    /// <summary>
    /// Lazily parses every matching line in the file.
    /// <paramref name="node"/> defaults to the filename stem (e.g. "scylla-gw10-1").
    /// </summary>
    public static IEnumerable<LogEntry> Parse(string filePath, string? node = null)
    {
        node ??= Path.GetFileNameWithoutExtension(filePath);

        // Pending entry being built (may span multiple continuation lines)
        string? level = null, ts = null, shard = null, group = null, facility = null;
        StringBuilder? msg = null;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.TrimEnd();

            // Continuation line: doesn't start a new log entry
            if (!StartsEntry.IsMatch(line))
            {
                if (msg is not null && line.Length > 0)
                    msg.Append('\n').Append(line);
                continue;
            }

            // Flush previous entry before starting a new one
            if (level is not null)
            {
                yield return new LogEntry(node, ts!, level, shard!, group!, facility!, msg!.ToString());
                level = null;
            }

            var mw = WithShard.Match(line);
            if (mw.Success)
            {
                level    = mw.Groups[1].Value;
                ts       = mw.Groups[2].Value;
                shard    = mw.Groups[3].Value;
                group    = mw.Groups[4].Value.Trim();   // strip padding spaces
                facility = mw.Groups[5].Value;
                msg      = new StringBuilder(mw.Groups[6].Value);
                continue;
            }

            var mn = WithoutShard.Match(line);
            if (mn.Success)
            {
                level    = mn.Groups[1].Value;
                ts       = mn.Groups[2].Value;
                shard    = "";
                group    = "";
                facility = mn.Groups[3].Value;
                msg      = new StringBuilder(mn.Groups[4].Value);
            }
        }

        // Flush last entry
        if (level is not null)
            yield return new LogEntry(node, ts!, level, shard!, group!, facility!, msg!.ToString());
    }
}
