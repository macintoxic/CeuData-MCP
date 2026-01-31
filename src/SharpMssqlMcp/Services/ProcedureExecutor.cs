using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
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

        using var connection = _connectionManager.GetConnection(dataSourceName);
        await RetryPolicy.ExecuteAsync(async () => await connection.OpenAsync(ct), ct);

        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = timeout;

        // Introspect parameters
        SqlCommandBuilder.DeriveParameters((SqlCommand)command);

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                var paramName = param.Key.StartsWith("@") ? param.Key : "@" + param.Key;
                if (command.Parameters.Contains(paramName))
                {
                    command.Parameters[paramName].Value = param.Value ?? DBNull.Value;
                }
            }
        }

        // To support output parameters without knowing the schema, we'd need to introspect.
        // For now, let's assume the user just wants to see what the procedure returns.
        // If we want to support output params as requested in STEP 17:
        // "Add output parameter support to ProcedureExecutor"
        // We might need a way for the user to specify which ones are Output.
        // Let's add a basic heuristic: if it's explicitly requested in a future version.
        // Or for now, we'll just execute it and if there are output params defined in the command (somehow), we read them.

        using var reader = await command.ExecuteReaderAsync(ct);
        
        int resultSetIndex = 1;
        do
        {
            var resultSet = new ResultSet
            {
                Name = $"ResultSet{resultSetIndex++}",
                Columns = GetColumnMetadata(reader)
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
            foreach (SqlParameter p in command.Parameters)
            {
                if (p.Direction == ParameterDirection.Output || p.Direction == ParameterDirection.InputOutput || p.Direction == ParameterDirection.ReturnValue)
                {
                    result.OutputParameters[p.ParameterName] = p.Value == DBNull.Value ? null : p.Value;
                }
            }
        }

        result.Success = true;
        sw.Stop();
        result.Metadata.ExecutionTimeMs = sw.ElapsedMilliseconds;

        return result;
    }

    private List<ColumnMetadata> GetColumnMetadata(SqlDataReader reader)
    {
        var columns = new List<ColumnMetadata>();
        if (!reader.HasRows && reader.FieldCount == 0) return columns;

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
