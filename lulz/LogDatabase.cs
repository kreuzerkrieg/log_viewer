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
                timestamp TEXT NOT NULL,
                level     TEXT NOT NULL,
                shard     TEXT NOT NULL,
                grp       TEXT NOT NULL,
                facility  TEXT NOT NULL,
                message   TEXT NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();

        // Index the most-queried columns
        cmd.CommandText = "CREATE INDEX idx_level    ON logs(level)";    cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE INDEX idx_facility ON logs(facility)"; cmd.ExecuteNonQuery();
    }

    /// <summary>Bulk-inserts entries inside a single transaction.</summary>
    /// <returns>Number of rows inserted.</returns>
    public int Insert(IEnumerable<LogEntry> entries)
    {
        using var tx  = _conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO logs (timestamp, level, shard, grp, facility, message)
            VALUES ($ts, $lvl, $shard, $grp, $fac, $msg)
            """;

        var pTs    = cmd.Parameters.Add("$ts",    SqliteType.Text);
        var pLvl   = cmd.Parameters.Add("$lvl",   SqliteType.Text);
        var pShard = cmd.Parameters.Add("$shard",  SqliteType.Text);
        var pGrp   = cmd.Parameters.Add("$grp",   SqliteType.Text);
        var pFac   = cmd.Parameters.Add("$fac",   SqliteType.Text);
        var pMsg   = cmd.Parameters.Add("$msg",   SqliteType.Text);

        int count = 0;
        foreach (var e in entries)
        {
            pTs.Value    = e.Timestamp;
            pLvl.Value   = e.Level;
            pShard.Value = e.Shard;
            pGrp.Value   = e.Group;   // empty string when no group, never DBNull
            pFac.Value   = e.Facility;
            pMsg.Value   = e.Message;
            cmd.ExecuteNonQuery();
            count++;
        }

        tx.Commit();
        return count;
    }

    /// <summary>Runs an arbitrary SELECT and returns the result as a DataTable.</summary>
    public DataTable Query(string sql =
        "SELECT timestamp, level, shard, grp, facility, message FROM logs ORDER BY id")
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


