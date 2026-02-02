using System.Data.Common;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Services;

public interface IDbStrategy
{
    string ParameterPrefix { get; }
    DbConnection CreateConnection(string connectionString);
    void DeriveParameters(DbCommand command);
    List<ColumnMetadata> GetColumnMetadata(DbDataReader reader);
    (string Server, string Database) GetConnectionStringInfo(string connectionString);
}
