using Microsoft.Data.SqlClient;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Services;

public static class ErrorResponseBuilder
{
    public static JsonRpcResponse BuildError(object? id, Exception ex, string? dataSource = null, string? query = null)
    {
        var errorCode = ErrorCodes.InternalError;
        var message = "An internal error occurred.";

        if (ex is KeyNotFoundException)
        {
            errorCode = ErrorCodes.DataSourceNotFound;
            message = ex.Message;
        }
        else if (ex is SqlException sqlEx)
        {
            (errorCode, message) = MapSqlException(sqlEx);
        }
        else if (ex is ArgumentException)
        {
            errorCode = ErrorCodes.ParameterMismatch;
            message = ex.Message;
        }
        else if (ex is OperationCanceledException)
        {
            errorCode = ErrorCodes.QueryTimeout;
            message = "The operation was cancelled or timed out.";
        }

        return JsonRpcResponse.Failure(id, ErrorCodes.InternalJsonRpcError, "Database query error", new
        {
            errorCode,
            sqlError = ex.Message,
            query,
            dataSource
        });
    }

    private static (string Code, string Message) MapSqlException(SqlException ex)
    {
        return ex.Number switch
        {
            -2 or 11 or 121 => (ErrorCodes.QueryTimeout, "Execution Timeout Expired."),
            102 or 156 or 170 => (ErrorCodes.QuerySyntaxError, "Incorrect syntax near the query."),
            229 or 262 or 916 => (ErrorCodes.PermissionDenied, "Permission denied for the database operation."),
            4060 or 18456 => (ErrorCodes.ConnectionFailed, "Failed to connect to the SQL Server or Database."),
            _ => (ErrorCodes.InternalError, $"SQL Error ({ex.Number}): {ex.Message}")
        };
    }
}
