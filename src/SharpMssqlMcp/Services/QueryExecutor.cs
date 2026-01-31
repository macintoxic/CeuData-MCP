using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Services;

public class QueryExecutor
{
    private readonly ConnectionManager _connectionManager;

    public QueryExecutor(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public async Task<QueryResult> ExecuteQueryAsync(
        string dataSourceName, 
        string query, 
        IDictionary<string, object?>? parameters = null, 
        int maxRows = 10000, 
        int timeout = 300,
        CancellationToken ct = default)
    {
        var result = new QueryResult();
        var sw = Stopwatch.StartNew();

        using var connection = _connectionManager.GetConnection(dataSourceName);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = query;
        command.CommandTimeout = timeout;

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                var sqlParam = command.CreateParameter();
                sqlParam.ParameterName = param.Key.StartsWith("@") ? param.Key : "@" + param.Key;
                sqlParam.Value = param.Value ?? DBNull.Value;
                command.Parameters.Add(sqlParam);
            }
        }

        using var reader = await command.ExecuteReaderAsync(ct);
        
        // Extract metadata
        result.Metadata.Columns = GetColumnMetadata(reader);

        // Read data
        int count = 0;
        while (count < maxRows && await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                row[reader.GetName(i)] = value == DBNull.Value ? null : value;
            }
            result.Data.Add(row);
            count++;
        }

        result.RowsAffected = reader.RecordsAffected;
        result.Success = true;
        
        sw.Stop();
        result.Metadata.ExecutionTimeMs = sw.ElapsedMilliseconds;

        return result;
    }

    private List<ColumnMetadata> GetColumnMetadata(SqlDataReader reader)
    {
        var columns = new List<ColumnMetadata>();
        var schemaTable = reader.GetColumnSchema();

        foreach (var column in schemaTable)
        {
            columns.Add(new ColumnMetadata
            {
                Name = column.ColumnName,
                Type = column.DataTypeName,
                Nullable = column.AllowDBNull ?? true
            });
        }

        return columns;
    }
}
