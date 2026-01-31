namespace SharpMssqlMcp.Models;

public class AppConfig
{
    public Dictionary<string, string> ConnectionStrings { get; set; } = new();
    public Dictionary<string, DataSourceConfig> DataSources { get; set; } = new();
}

public class DataSourceConfig
{
    public string Description { get; set; } = string.Empty;
    public string ConnectionStringKey { get; set; } = string.Empty;
    public int CommandTimeout { get; set; } = 300;
}
