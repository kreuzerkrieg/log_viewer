namespace lulz;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Parses Scylla / Seastar text log files.
/// </summary>
public static class LogParser
{
    /// <summary>Maps Scylla log level names to the C++ log_level enum ordinals.</summary>
    private static readonly Dictionary<string, int> LevelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ERROR"] = 0,
        ["WARN"]  = 1,
        ["INFO"]  = 2,
        ["DEBUG"] = 3,
        ["TRACE"] = 4,
    };

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

        string? levelStr = null, tsRaw = null, shardStr = null, group = null, facility = null;
        StringBuilder? msg = null;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.TrimEnd();

            if (!StartsEntry.IsMatch(line))
            {
                if (msg is not null && line.Length > 0)
                    msg.Append('\n').Append(line);
                continue;
            }

            if (levelStr is not null)
            {
                yield return MakeEntry(node, levelStr, tsRaw!, shardStr, group!, facility!, msg!.ToString());
                levelStr = null;
            }

            var mw = WithShard.Match(line);
            if (mw.Success)
            {
                levelStr = mw.Groups[1].Value;
                tsRaw    = mw.Groups[2].Value;
                shardStr = mw.Groups[3].Value;
                group    = mw.Groups[4].Value.Trim();
                facility = mw.Groups[5].Value;
                msg      = new StringBuilder(mw.Groups[6].Value);
                continue;
            }

            var mn = WithoutShard.Match(line);
            if (mn.Success)
            {
                levelStr = mn.Groups[1].Value;
                tsRaw    = mn.Groups[2].Value;
                shardStr = null;
                group    = "";
                facility = mn.Groups[3].Value;
                msg      = new StringBuilder(mn.Groups[4].Value);
            }
        }

        if (levelStr is not null)
            yield return MakeEntry(node, levelStr, tsRaw!, shardStr, group!, facility!, msg!.ToString());
    }

    private static LogEntry MakeEntry(
        string node, string levelStr, string tsRaw,
        string? shardStr, string group, string facility, string message)
    {
        // Normalize timestamp: replace comma millisecond separator with dot
        var ts = tsRaw.Replace(',', '.');

        var level = LevelMap.TryGetValue(levelStr, out var l) ? l : 2; // default INFO

        int? shard = shardStr is not null && int.TryParse(shardStr, out var s) ? s : null;

        return new LogEntry(node, ts, level, shard, group, facility, message);
    }
}
