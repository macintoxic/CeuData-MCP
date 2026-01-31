using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Services;

public static class RequestValidator
{
    private const int MaxQueryLength = 1024 * 1024; // 1MB
    private const int MaxParameterCount = 1000;
    private const int MinTimeout = 1;
    private const int MaxTimeout = 3600;

    public static void ValidateQuery(string query, IDictionary<string, object?>? parameters, int timeout)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty.");
        }

        if (query.Length > MaxQueryLength)
        {
            throw new ArgumentException($"Query length exceeds maximum limit of {MaxQueryLength} characters.");
        }

        if (parameters != null && parameters.Count > MaxParameterCount)
        {
            throw new ArgumentException($"Parameter count exceeds maximum limit of {MaxParameterCount}.");
        }

        if (timeout < MinTimeout || timeout > MaxTimeout)
        {
            throw new ArgumentException($"Timeout must be between {MinTimeout} and {MaxTimeout} seconds.");
        }
    }

    public static void ValidateProcedure(string procedure, int timeout)
    {
        if (string.IsNullOrWhiteSpace(procedure))
        {
            throw new ArgumentException("Procedure name cannot be empty.");
        }

        if (timeout < MinTimeout || timeout > MaxTimeout)
        {
            throw new ArgumentException($"Timeout must be between {MinTimeout} and {MaxTimeout} seconds.");
        }
    }
}
