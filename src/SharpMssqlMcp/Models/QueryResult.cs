namespace SharpMssqlMcp.Models;

public class QueryResult
{
    public bool Success { get; set; }
    public int RowsAffected { get; set; }
    public List<Dictionary<string, object?>> Data { get; set; } = new();
    public QueryMetadata Metadata { get; set; } = new();
}

public class QueryMetadata
{
    public List<ColumnMetadata> Columns { get; set; } = new();
    public long ExecutionTimeMs { get; set; }
}

public class ColumnMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Nullable { get; set; }
}
