using Microsoft.Data.SqlClient;

namespace SharpMssqlMcp.Services;

public static class RetryPolicy
{
    public static async Task ExecuteAsync(Func<Task> action, CancellationToken ct = default)
    {
        int retryCount = 0;
        int maxRetries = 3;
        int delayMs = 100;

        while (true)
        {
            try
            {
                await action();
                return;
            }
            catch (SqlException ex) when (IsTransient(ex) && retryCount < maxRetries)
            {
                retryCount++;
                await Console.Error.WriteLineAsync($"Transient error detected (Attempt {retryCount}/{maxRetries}): {ex.Message}. Retrying in {delayMs}ms...");
                await Task.Delay(delayMs, ct);
                delayMs *= 2; // Exponential backoff
            }
        }
    }

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        int retryCount = 0;
        int maxRetries = 3;
        int delayMs = 100;

        while (true)
        {
            try
            {
                return await action();
            }
            catch (SqlException ex) when (IsTransient(ex) && retryCount < maxRetries)
            {
                retryCount++;
                await Console.Error.WriteLineAsync($"Transient error detected (Attempt {retryCount}/{maxRetries}): {ex.Message}. Retrying in {delayMs}ms...");
                await Task.Delay(delayMs, ct);
                delayMs *= 2; // Exponential backoff
            }
        }
    }

    private static bool IsTransient(SqlException ex)
    {
        // Common transient SQL errors
        // 1205: Deadlock
        // -2: Timeout
        // 4060, 40197, 40501, 40613, 49918, 49919, 49920: Azure SQL Transient errors
        return ex.Number switch
        {
            1205 or -2 or 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 => true,
            _ => false
        };
    }
}
