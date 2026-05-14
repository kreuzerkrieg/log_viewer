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
        cmd.CommandText = """
            CREATE TABLE logs (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                node      TEXT    NOT NULL,
                timestamp TEXT    NOT NULL,   -- YYYY-MM-DD HH:MM:SS.mmm, ISO-sortable
                level     INTEGER NOT NULL,   -- 0=error 1=warn 2=info 3=debug 4=trace
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
        var pTs    = cmd.Parameters.Add("$ts",    SqliteType.Text);
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
    public DataTable Query(string sql = """
        SELECT
            node,
            timestamp,
            CASE level
                WHEN 0 THEN 'ERROR' WHEN 1 THEN 'WARN' WHEN 2 THEN 'INFO'
                WHEN 3 THEN 'DEBUG' WHEN 4 THEN 'TRACE'
                ELSE CAST(level AS TEXT)
            END AS level,
            COALESCE(CAST(shard AS TEXT), '') AS shard,
            grp       AS grp,
            facility,
            message
        FROM logs
        ORDER BY timestamp, id
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

