using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Manifest.Data;

/// <summary>
/// The handful of places where SQLite and PostgreSQL disagree, so the repositories
/// can stay one set of classes with one set of rules instead of two copies that
/// drift. Everything else in the SQL here is written to the common subset both
/// accept - ON CONFLICT, RETURNING, COALESCE, CASE.
/// </summary>
public sealed class SqlDialect
{
    public static readonly SqlDialect Sqlite = new(postgres: false);
    public static readonly SqlDialect Postgres = new(postgres: true);

    SqlDialect(bool postgres) => IsPostgres = postgres;

    public bool IsPostgres { get; }

    public string Name => IsPostgres ? "postgres" : "sqlite";

    /// <summary>
    /// The current time as a SQL expression. SQLite keeps time as sortable text in
    /// datetime('now')'s format; PostgreSQL keeps a real timestamptz.
    /// </summary>
    public string Now => IsPostgres ? "now()" : "datetime('now')";

    /// <summary>
    /// A row lock on the SELECT it is appended to. SQLite has no such thing and does
    /// not need one: BEGIN IMMEDIATE already holds the whole database for writing.
    /// PostgreSQL lets other writers run alongside, so a read that a later write in
    /// the same transaction depends on has to pin the row.
    /// </summary>
    public string ForUpdate => IsPostgres ? " FOR UPDATE" : "";

    /// <summary>
    /// A case-insensitive substring match, which is what SQLite's LIKE already is
    /// for ASCII. ESCAPE '' because PostgreSQL otherwise treats a backslash in what
    /// someone typed as an escape, and a trailing one is an error rather than a
    /// search.
    /// </summary>
    public string Like(string column, string parameter) => IsPostgres
        ? $"{column} ILIKE {parameter} ESCAPE ''"
        : $"{column} LIKE {parameter}";

    /// <summary>
    /// A parameter compared against a COLLATE NOCASE / citext column. Npgsql sends a
    /// string as text, and citext = text is text equality - case-sensitive - so the
    /// parameter has to be citext too for "alice" to find "Alice" as SQLite does.
    /// </summary>
    public string Nocase(string parameter) =>
        IsPostgres ? $"CAST({parameter} AS citext)" : parameter;

    /// <summary>
    /// A point in time as a parameter value. SQLite compares the text, so it gets the
    /// same format datetime('now') writes; PostgreSQL gets the instant itself.
    /// </summary>
    public object Time(DateTime utc) => IsPostgres
        ? DateTime.SpecifyKind(utc, DateTimeKind.Utc)
        : utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// Serialises everyone working on the same <paramref name="key"/> until the
    /// current transaction ends - for checks on rows that may not exist yet, where
    /// there is nothing to take a row lock on. A no-op on SQLite for the same reason
    /// as <see cref="ForUpdate"/>.
    /// </summary>
    public void Lock(DbConnection conn, string key)
    {
        if (!IsPostgres) return;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))";
        cmd.Bind("@key", key);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// What a unique constraint failure names - the constraint on PostgreSQL, the
    /// "table.column" message on SQLite - or null if the error is something else.
    /// </summary>
    public static string? UniqueViolation(Exception e) => e switch
    {
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg =>
            pg.ConstraintName ?? pg.MessageText,
        // 19 is SQLITE_CONSTRAINT.
        SqliteException { SqliteErrorCode: 19 } lite => lite.Message,
        _ => null,
    };
}

public static class CommandExtensions
{
    /// <summary>
    /// AddWithValue for any provider. Neither DbCommand nor DbParameterCollection
    /// has one; each provider adds its own.
    /// </summary>
    public static DbParameter Bind(this DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
        return p;
    }

    public static int Exec(this DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteNonQuery();
    }
}
