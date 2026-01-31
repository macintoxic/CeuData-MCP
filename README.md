# Sharp MSSQL MCP Server

Um servidor MCP (Model Context Protocol) de alta performance para Microsoft SQL Server, desenvolvido em .NET 10. Este servidor permite que LLMs (como Claude e ChatGPT via Cursor) interajam com múltiplos bancos de dados SQL Server de forma segura e eficiente.

## 🚀 Funcionalidades

- **Múltiplos DataSources**: Gerencie conexões com diferentes servidores e bancos em um único servidor MCP.
- **Consultas Inteligentes**: Suporte a `execute_query` para DML (SELECT, INSERT, UPDATE, DELETE) com metadados automáticos.
- **Stored Procedures**: Suporte avançado via `execute_procedure` com múltiplos result sets e parâmetros de saída (OUTPUT).
- **Segurança**: Proteção nativa contra SQL Injection via consultas parametrizadas e validação de limites.
- **Resiliência**: Política de retry com backoff exponencial para erros transientes (deadlocks, timeouts).
- **Configuração Flexível**: Suporte a variáveis de ambiente em connection strings (ex: `${SQL_PASSWORD}`).
- **Health Monitoring**: Ferramenta `get_datasources` para monitorar o status das conexões.

## 🛠️ Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (ou Docker para ambiente de teste)

## 📦 Configuração

1. **appsettings.json**: Configure seus bancos de dados no arquivo `src/SharpMssqlMcp/appsettings.json`.
2. **Variáveis de Ambiente**: Crie um arquivo `.env` (veja `.env.example`) com suas credenciais.

## 🖥️ Instalação no Claude Desktop

Adicione a seguinte configuração ao seu `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "sharp-mssql": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "Z:\\Projetos\\sharp-mssql-mcp\\src\\SharpMssqlMcp",
        "--configuration",
        "Release"
      ]
    }
  }
}
```

## 🧪 Desenvolvimento e Testes

O projeto utiliza Docker para testes de integração.

```bash
# Iniciar SQL Server de teste
docker-compose up -d

# Inicializar banco de teste
Get-Content src/SharpMssqlMcp.Tests/setup-test-db.sql | docker exec -i mssql-mcp-test /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "SharpMssqlMcp_123!" -C

# Rodar testes
dotnet test
```

## 🛠️ Ferramentas Disponíveis

### 1. `execute_query`
Executa uma consulta SQL no banco especificado.
- `dataSource`: Nome do banco (ex: "production")
- `query`: SQL Query parametrizada.
- `parameters`: Objeto com os parâmetros (ex: `{"@id": 1}`).

### 2. `execute_procedure`
Executa uma stored procedure.
- `dataSource`: Nome do banco.
- `procedure`: Nome da procedure.
- `parameters`: Parâmetros da procedure.

### 3. `get_datasources`
Lista todos os bancos configurados e seus status de conectividade.

## 📄 Licença

Este projeto está sob a licença MIT.
