using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using SharpMssqlMcp.Models;

namespace SharpMssqlMcp.Services;

public class SqlServerStrategy : IDbStrategy
{
    public string ParameterPrefix => "@";

    public DbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }

    public void DeriveParameters(DbCommand command)
    {
        if (command is SqlCommand sqlCommand)
        {
            SqlCommandBuilder.DeriveParameters(sqlCommand);
        }
    }

    public List<ColumnMetadata> GetColumnMetadata(DbDataReader reader)
    {
        var columns = new List<ColumnMetadata>();
        if (reader is SqlDataReader sqlReader)
        {
            var schemaTable = sqlReader.GetColumnSchema();
            foreach (var column in schemaTable)
            {
                columns.Add(new ColumnMetadata
                {
                    Name = column.ColumnName,
                    Type = column.DataTypeName,
                    Nullable = column.AllowDBNull ?? true
                });
            }
        }
        return columns;
    }

    public (string Server, string Database) GetConnectionStringInfo(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return (builder.DataSource, builder.InitialCatalog);
    }
}
