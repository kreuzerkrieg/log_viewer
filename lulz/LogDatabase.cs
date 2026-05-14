namespace lulz;

using System.Data;
using Microsoft.Data.Sqlite;

/// <summary>
/// In-memory SQLite store for parsed log entries.
/// Holds one database per loaded file; call Dispose() and create a new
/// instance to load a different file.
/// </summary>
public sealed class LogDatabase : IDisposable
{
    private readonly SqliteConnection _conn;

    public LogDatabase()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        CreateTable();
    }

    private void CreateTable()
    {
        using var cmd = _conn.CreateCommand();

        // ── system lookup: log level id → name ──────────────────────────────
        cmd.CommandText = """
            CREATE TABLE log_levels (
                id   INTEGER PRIMARY KEY,
                name TEXT    NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText = """
            INSERT INTO log_levels (id, name) VALUES
                (0, 'ERROR'),
                (1, 'WARN'),
                (2, 'INFO'),
                (3, 'DEBUG'),
                (4, 'TRACE')
            """;
        cmd.ExecuteNonQuery();

        // ── main log table ───────────────────────────────────────────────────
        cmd.CommandText = """
            CREATE TABLE logs (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                node      TEXT    NOT NULL,
                timestamp INTEGER NOT NULL,   -- Unix milliseconds; filter with < > BETWEEN
                level     INTEGER NOT NULL    REFERENCES log_levels(id),
                shard     INTEGER,            -- NULL for entries without [shard N]
                grp       TEXT    NOT NULL,
                facility  TEXT    NOT NULL,
                message   TEXT    NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText = "CREATE INDEX idx_node      ON logs(node)";       cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE INDEX idx_level     ON logs(level)";      cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE INDEX idx_facility  ON logs(facility)";   cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE INDEX idx_timestamp ON logs(timestamp)";  cmd.ExecuteNonQuery();
    }

    /// <summary>Bulk-inserts entries inside a single transaction.</summary>
    /// <returns>Number of rows inserted.</returns>
    public int Insert(IEnumerable<LogEntry> entries)
    {
        using var tx  = _conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO logs (node, timestamp, level, shard, grp, facility, message)
            VALUES ($node, $ts, $lvl, $shard, $grp, $fac, $msg)
            """;

        var pNode  = cmd.Parameters.Add("$node",  SqliteType.Text);
        var pTs    = cmd.Parameters.Add("$ts",    SqliteType.Integer);
        var pLvl   = cmd.Parameters.Add("$lvl",   SqliteType.Integer);
        var pShard = cmd.Parameters.Add("$shard", SqliteType.Integer);
        var pGrp   = cmd.Parameters.Add("$grp",   SqliteType.Text);
        var pFac   = cmd.Parameters.Add("$fac",   SqliteType.Text);
        var pMsg   = cmd.Parameters.Add("$msg",   SqliteType.Text);

        int count = 0;
        foreach (var e in entries)
        {
            pNode.Value  = e.Node;
            pTs.Value    = e.Timestamp;
            pLvl.Value   = e.Level;
            pShard.Value = e.Shard.HasValue ? (object)e.Shard.Value : DBNull.Value;
            pGrp.Value   = e.Group;
            pFac.Value   = e.Facility;
            pMsg.Value   = e.Message;
            cmd.ExecuteNonQuery();
            count++;
        }

        tx.Commit();
        return count;
    }

    /// <summary>Returns true if entries for this node are already in the database.</summary>
    public bool ContainsNode(string node)
    {
        using var cmd   = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM logs WHERE node = $node LIMIT 1";
        cmd.Parameters.AddWithValue("$node", node);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    /// <summary>Returns the distinct node names currently loaded.</summary>
    public IReadOnlyList<string> LoadedNodes()
    {
        using var cmd   = _conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT node FROM logs ORDER BY node";
        using var reader = cmd.ExecuteReader();
        var nodes = new List<string>();
        while (reader.Read()) nodes.Add(reader.GetString(0));
        return nodes;
    }

    /// <summary>Runs an arbitrary SELECT and returns the result as a DataTable.</summary>
    /// <param name="sql">
    /// Query against the raw schema. Use <c>timestamp</c> as INTEGER Unix-ms for filtering,
    /// e.g. <c>WHERE timestamp BETWEEN $from AND $to</c>.
    /// The default query formats it to a human-readable UTC string for display.
    /// </param>
    public DataTable Query(string sql = """
        SELECT
            l.node,
            strftime('%Y-%m-%d %H:%M:%f', l.timestamp / 1000.0, 'unixepoch') AS timestamp,
            COALESCE(ll.name, CAST(l.level AS TEXT)) AS level,
            COALESCE(CAST(l.shard AS TEXT), '') AS shard,
            l.grp,
            l.facility,
            l.message
        FROM logs l
        LEFT JOIN log_levels ll ON ll.id = l.level
        ORDER BY l.timestamp, l.id
        """)
    {
        using var cmd    = _conn.CreateCommand();
        cmd.CommandText  = sql;
        using var reader = cmd.ExecuteReader();
        var dt = new DataTable();
        dt.Load(reader);
        return dt;
    }

    public void Dispose() => _conn.Dispose();
}

