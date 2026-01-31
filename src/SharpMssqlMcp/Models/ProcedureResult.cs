namespace SharpMssqlMcp.Models;

public class ProcedureResult
{
    public bool Success { get; set; }
    public List<ResultSet> ResultSets { get; set; } = new();
    public Dictionary<string, object?> OutputParameters { get; set; } = new();
    public ProcedureMetadata Metadata { get; set; } = new();
}

public class ResultSet
{
    public string Name { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public List<Dictionary<string, object?>> Data { get; set; } = new();
    public List<ColumnMetadata> Columns { get; set; } = new();
}

public class ProcedureMetadata
{
    public string ProcedureName { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
}
