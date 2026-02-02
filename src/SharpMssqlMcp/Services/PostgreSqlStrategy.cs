using System.Data.Common;
using Npgsql;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Services;

public class PostgreSqlStrategy : IDbStrategy
{
    public string ParameterPrefix => ":"; // Or "$" but ":" is common in many ORMs, Npgsql supports both. "$" is positional, ":" is named.

    public DbConnection CreateConnection(string connectionString)
    {
        return new NpgsqlConnection(connectionString);
    }

    public void DeriveParameters(DbCommand command)
    {
        // Npgsql doesn't have a CommandBuilder.DeriveParameters like SqlClient.
        // For stored procedures in Postgres, we usually just pass parameters by name or position.
        // However, if we want to support it, we'd need to query pg_proc.
        // For now, let's keep it simple as a placeholder or implement a basic version.
    }

    public List<ColumnMetadata> GetColumnMetadata(DbDataReader reader)
    {
        var columns = new List<ColumnMetadata>();
        var schema = reader.GetColumnSchema();
        foreach (var column in schema)
        {
            columns.Add(new ColumnMetadata
            {
                Name = column.ColumnName,
                Type = column.DataTypeName ?? "unknown",
                Nullable = column.AllowDBNull ?? true
            });
        }
        return columns;
    }

    public (string Server, string Database) GetConnectionStringInfo(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return (builder.Host ?? "unknown", builder.Database ?? "unknown");
    }
}
