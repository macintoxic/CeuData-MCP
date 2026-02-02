using System.Data.Common;
using System.Diagnostics;
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

        var (connection, provider) = _connectionManager.GetConnection(dataSourceName);
        using (connection)
        {
            await RetryPolicy.ExecuteAsync(async () => await connection.OpenAsync(ct), ct);

            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = timeout;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    var dbParam = command.CreateParameter();
                    var prefix = provider.Strategy.ParameterPrefix;
                    dbParam.ParameterName = param.Key.StartsWith(prefix) ? param.Key : prefix + param.Key;
                    dbParam.Value = param.Value ?? DBNull.Value;
                    command.Parameters.Add(dbParam);
                }
            }

            using var reader = await command.ExecuteReaderAsync(ct);
            
            // Extract metadata using strategy
            result.Metadata.Columns = provider.Strategy.GetColumnMetadata(reader);

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
        }
        
        sw.Stop();
        result.Metadata.ExecutionTimeMs = sw.ElapsedMilliseconds;

        return result;
    }
}
