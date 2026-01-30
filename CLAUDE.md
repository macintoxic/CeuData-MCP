# sharp-mssql-mcp Development Guide

This is a C# MCP (Model Context Protocol) server for Microsoft SQL Server, enabling LLMs to query databases, inspect schemas, and execute SQL commands through a standardized protocol.

## Quick Start

### Build the Project
```bash
dotnet build
```

### Run the MCP Server
```bash
dotnet run
```

The server communicates with MCP clients via JSON-RPC 2.0 over stdio.

### Run Unit Tests
```bash
dotnet test
```

### Publish for Distribution
```bash
dotnet publish -c Release -o ./publish
```

## Project Structure

### Core Components

- **Tools**: SQL query execution capabilities exposed to clients
- **Resources**: Database schema information and metadata
- **Prompts**: Query templates and assistance suggestions

### Communication Flow

1. MCP client connects via stdio
2. Server receives JSON-RPC 2.0 requests
3. Requests processed (tool calls, resource reads, prompt suggestions)
4. Results returned as JSON-RPC responses
5. Errors handled gracefully with proper error codes

## SQL Server Integration

### Configuration

Connection strings should be configured via:
- **appsettings.json** (for development)
- **Environment variables** (for production)

Example appsettings.json:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=master;Integrated Security=true;Encrypt=false;"
  }
}
```

### Query Execution Best Practices

1. **Always use parameterized queries** - Prevents SQL injection attacks
   ```csharp
   // ✅ Good
   var cmd = new SqlCommand("SELECT * FROM Users WHERE Id = @id", connection);
   cmd.Parameters.AddWithValue("@id", userId);

   // ❌ Bad - SQL injection risk
   var cmd = new SqlCommand($"SELECT * FROM Users WHERE Id = {userId}", connection);
   ```

2. **Connection Pooling** - Connections are automatically pooled by SqlConnection
   - Keep connection strings identical for maximum pool reuse
   - Default pool size: 100 connections

3. **Schema Inspection** - Common queries for database information:
   ```sql
   -- Get all tables
   SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'

   -- Get columns for a table
   SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='TableName'

   -- Get primary keys
   SELECT CONSTRAINT_NAME, TABLE_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
   WHERE CONSTRAINT_TYPE='PRIMARY KEY'
   ```

4. **Transaction Handling** - For multi-statement operations:
   ```csharp
   using (SqlTransaction transaction = connection.BeginTransaction())
   {
       // Execute multiple commands
       transaction.Commit(); // or Rollback() on error
   }
   ```

5. **Timeout Configuration** - Set appropriate command timeouts:
   ```csharp
   command.CommandTimeout = 30; // seconds
   ```

### Security Considerations

- **Input Validation**: Always validate and sanitize user input
- **Query Limits**: Restrict queries to prevent resource exhaustion
  - Set statement timeouts (e.g., 30 seconds max)
  - Limit result set sizes
  - Restrict operations on system tables
- **Connection Security**:
  - Use Integrated Security when possible
  - Use SSL/TLS encryption for remote connections (`Encrypt=true;`)
  - Store sensitive credentials in environment variables, not in code
- **Error Messages**: Avoid exposing sensitive database details in error responses
- **Least Privilege**: Database user should have minimal required permissions

## Development Workflow

### Testing with MCP Clients

#### Claude Desktop Configuration
Add to `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "sharp-mssql": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/sharp-mssql-mcp"]
    }
  }
}
```

#### Local Testing
1. Build the project: `dotnet build`
2. Run the server: `dotnet run`
3. Connect an MCP client to the stdio communication stream
4. Send test requests to verify tool execution

### Common Development Tasks

- **Add a New Tool**: Implement handler in the tools module, register with MCP server
- **Add Schema Support**: Extend resource handlers to expose new schema metadata
- **Test Error Handling**: Simulate database connection failures, timeouts, invalid queries
- **Performance Testing**: Use SQL Server profiler to monitor query execution

### Debugging

Enable detailed logging by configuring log levels in appsettings.json:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  }
}
```

## MCP Protocol Overview

### Request Types

1. **Tool Calls** - Execute SQL commands
   ```json
   {
     "jsonrpc": "2.0",
     "method": "tools/call",
     "params": {
       "name": "query",
       "arguments": { "sql": "SELECT * FROM Users" }
     }
   }
   ```

2. **Resource Reads** - Get database schema/metadata
   ```json
   {
     "jsonrpc": "2.0",
     "method": "resources/read",
     "params": { "uri": "mssql://database/schema" }
   }
   ```

3. **Prompt Suggestions** - Provide query templates
   ```json
   {
     "jsonrpc": "2.0",
     "method": "prompts/get",
     "params": { "name": "query_template" }
   }
   ```

### Error Handling

Errors should follow JSON-RPC 2.0 format:
```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32603,
    "message": "Internal error",
    "data": { "details": "Connection timeout" }
  }
}
```

Common error codes:
- `-32600` - Invalid Request
- `-32601` - Method not found
- `-32602` - Invalid params
- `-32603` - Internal error
- `-32000` to `-32099` - Server error (reserved for implementation)

## Dependencies

Key NuGet packages:
- `System.Data.SqlClient` or `Microsoft.Data.SqlClient` - SQL Server connectivity
- `JSON.NET` or `System.Text.Json` - JSON serialization
- `xUnit` or `NUnit` - Unit testing
- `Moq` - Mocking for tests

## Building for Production

1. Update version in .csproj file
2. Run tests: `dotnet test`
3. Build release: `dotnet publish -c Release -o ./publish`
4. Distribute the published executable

## References

- [MCP Specification](https://modelcontextprotocol.io/)
- [System.Data.SqlClient Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient)
- [SQL Server Security Best Practices](https://learn.microsoft.com/en-us/sql/relational-databases/security/security-center-for-sql-server-database-engine-and-azure-sql-database)
