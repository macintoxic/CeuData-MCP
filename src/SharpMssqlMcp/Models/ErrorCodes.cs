namespace SharpMssqlMcp.Models;

public static class ErrorCodes
{
    public const string DataSourceNotFound = "DATASOURCE_NOT_FOUND";
    public const string ConnectionFailed = "CONNECTION_FAILED";
    public const string QueryTimeout = "QUERY_TIMEOUT";
    public const string QuerySyntaxError = "QUERY_SYNTAX_ERROR";
    public const string ParameterMismatch = "PARAMETER_MISMATCH";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string DatabaseNotFound = "DATABASE_NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";

    // JSON-RPC reserved error codes
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalJsonRpcError = -32603;
}
