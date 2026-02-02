using System.Data;
using System.Data.Common;
using System.Diagnostics;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Services;

public class ProcedureExecutor
{
    private readonly ConnectionManager _connectionManager;

    public ProcedureExecutor(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public async Task<ProcedureResult> ExecuteProcedureAsync(
        string dataSourceName, 
        string procedureName, 
        IDictionary<string, object?>? parameters = null, 
        int timeout = 300,
        bool includeOutputParameters = true,
        CancellationToken ct = default)
    {
        var result = new ProcedureResult();
        var sw = Stopwatch.StartNew();
        result.Metadata.ProcedureName = procedureName;

        var (connection, provider) = _connectionManager.GetConnection(dataSourceName);
        using (connection)
        {
            await RetryPolicy.ExecuteAsync(async () => await connection.OpenAsync(ct), ct);

            using var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = timeout;

            // Introspect parameters using strategy
            provider.Strategy.DeriveParameters(command);

            if (parameters != null)
            {
                var prefix = provider.Strategy.ParameterPrefix;
                foreach (var param in parameters)
                {
                    var paramName = param.Key.StartsWith(prefix) ? param.Key : prefix + param.Key;
                    if (command.Parameters.Contains(paramName))
                    {
                        command.Parameters[paramName].Value = param.Value ?? DBNull.Value;
                    }
                }
            }

            using var reader = await command.ExecuteReaderAsync(ct);
            
            int resultSetIndex = 1;
            do
            {
                var resultSet = new ResultSet
                {
                    Name = $"ResultSet{resultSetIndex++}",
                    Columns = provider.Strategy.GetColumnMetadata(reader)
                };

                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        row[reader.GetName(i)] = value == DBNull.Value ? null : value;
                    }
                    resultSet.Data.Add(row);
                    resultSet.RowCount++;
                }
                
                result.ResultSets.Add(resultSet);
            } while (await reader.NextResultAsync(ct));

            reader.Close(); // Must close reader to access output parameters

            if (includeOutputParameters)
            {
                foreach (DbParameter p in command.Parameters)
                {
                    if (p.Direction == ParameterDirection.Output || p.Direction == ParameterDirection.InputOutput || p.Direction == ParameterDirection.ReturnValue)
                    {
                        result.OutputParameters[p.ParameterName] = p.Value == DBNull.Value ? null : p.Value;
                    }
                }
            }
        }

        result.Success = true;
        sw.Stop();
        result.Metadata.ExecutionTimeMs = sw.ElapsedMilliseconds;

        return result;
    }
}
