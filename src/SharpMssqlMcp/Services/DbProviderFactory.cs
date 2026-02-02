using System.Data.Common;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Services;

public static class DbProviderFactory
{
    public static IDbStrategy GetStrategy(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "sqlserver" => new SqlServerStrategy(),
            "postgresql" or "postgres" => new PostgreSqlStrategy(),
            _ => throw new NotSupportedException($"Database provider '{provider}' is not supported.")
        };
    }
}
