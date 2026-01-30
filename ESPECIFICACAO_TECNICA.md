# Especificação Técnica - Sharp MSSQL MCP Server

## 1. Visão Geral da Arquitetura

### Modelo
- **Tipo**: MCP Stdio-based (Inter-Process Communication)
- **Instâncias**: Uma única instância do servidor MCP
- **Suporte**: Múltiplos SQL Servers pré-configurados
- **Comunicação**: JSON-RPC 2.0 via stdin/stdout

### Fluxo
```
Claude Desktop / Cliente MCP
    ↓
    Inicia: dotnet run (sharp-mssql-mcp)
    ↓
    Envia JSON-RPC 2.0 via stdin
    ↓
    MCP Server (processo único)
    ├─ Carrega appsettings.json
    ├─ Inicializa pool de conexões para cada DataSource
    ├─ Processa tool calls
    ├─ Retorna resultados via stdout
    ↓
    Cliente recebe resposta JSON-RPC 2.0
```

---

## 2. Configuração de DataSources

### appsettings.json

```json
{
  "DataSources": {
    "production": {
      "description": "SQL Server Production - Ecommerce",
      "server": "sql-prod.company.com",
      "port": 1433,
      "database": "ecommerce",
      "authentication": {
        "type": "SqlServer",
        "username": "${SQL_PROD_USER}",
        "password": "${SQL_PROD_PASSWORD}"
      },
      "options": {
        "encrypt": true,
        "trustServerCertificate": false,
        "connectionTimeout": 30,
        "commandTimeout": 300
      }
    },
    "development": {
      "description": "SQL Server Local Development",
      "server": "localhost",
      "port": 1433,
      "database": "ecommerce_dev",
      "authentication": {
        "type": "IntegratedSecurity"
      },
      "options": {
        "encrypt": false,
        "connectionTimeout": 15,
        "commandTimeout": 60
      }
    },
    "analytics": {
      "description": "SQL Server Analytics - Data Warehouse",
      "server": "sql-analytics.company.com",
      "port": 1433,
      "database": "dwh",
      "authentication": {
        "type": "AzureAD",
        "tenantId": "${AZURE_TENANT_ID}",
        "clientId": "${AZURE_CLIENT_ID}",
        "clientSecret": "${AZURE_CLIENT_SECRET}"
      },
      "options": {
        "encrypt": true,
        "trustServerCertificate": false,
        "connectionTimeout": 30,
        "commandTimeout": 300
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

### Tipos de Autenticação Suportados

1. **IntegratedSecurity** (Windows Auth)
   ```json
   {
     "type": "IntegratedSecurity"
   }
   ```

2. **SqlServer** (user/password)
   ```json
   {
     "type": "SqlServer",
     "username": "${SQL_USER}",
     "password": "${SQL_PASSWORD}"
   }
   ```

3. **AzureAD** (Managed Identity / Service Principal)
   ```json
   {
     "type": "AzureAD",
     "tenantId": "${AZURE_TENANT_ID}",
     "clientId": "${AZURE_CLIENT_ID}",
     "clientSecret": "${AZURE_CLIENT_SECRET}"
   }
   ```

### Variáveis de Ambiente
- Todas as referências `${VAR}` são substituídas por variáveis de ambiente
- Credenciais **nunca** devem ser hardcoded
- Em produção, usar secrets management (Azure Key Vault, AWS Secrets Manager, etc.)

---

## 3. Tools Disponíveis

### 3.1 Tool: `execute_query`

**Propósito**: Executar queries SQL (SELECT, INSERT, UPDATE, DELETE)

#### Request
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "execute_query",
    "arguments": {
      "dataSource": "production",
      "query": "SELECT * FROM Users WHERE Status = @status",
      "parameters": {
        "@status": "Active"
      },
      "options": {
        "maxRows": 1000,
        "timeout": 60
      }
    }
  },
  "id": 1
}
```

#### Parâmetros

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `dataSource` | string | ✅ | Nome do datasource configurado |
| `query` | string | ✅ | SQL query (suporta parâmetros) |
| `parameters` | object | ❌ | Parâmetros nomeados ex: `{"@id": 123}` |
| `options.maxRows` | number | ❌ | Limite de linhas (default: 10000) |
| `options.timeout` | number | ❌ | Timeout em segundos (default: 300) |

#### Response - Sucesso
```json
{
  "jsonrpc": "2.0",
  "result": {
    "success": true,
    "rowsAffected": 5,
    "data": [
      {
        "Id": 1,
        "Name": "John Doe",
        "Status": "Active",
        "CreatedAt": "2024-01-15T10:30:00Z"
      },
      {
        "Id": 2,
        "Name": "Jane Smith",
        "Status": "Active",
        "CreatedAt": "2024-01-16T14:22:00Z"
      }
    ],
    "metadata": {
      "columns": [
        {
          "name": "Id",
          "type": "int",
          "nullable": false
        },
        {
          "name": "Name",
          "type": "nvarchar",
          "nullable": false
        },
        {
          "name": "Status",
          "type": "nvarchar",
          "nullable": true
        },
        {
          "name": "CreatedAt",
          "type": "datetime2",
          "nullable": false
        }
      ],
      "executionTimeMs": 245
    }
  },
  "id": 1
}
```

#### Response - Erro
```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32603,
    "message": "Database query error",
    "data": {
      "errorCode": "QUERY_TIMEOUT",
      "sqlError": "Execution Timeout Expired",
      "query": "SELECT * FROM Users WHERE Status = @status",
      "dataSource": "production"
    }
  },
  "id": 1
}
```

---

### 3.2 Tool: `execute_procedure`

**Propósito**: Executar stored procedures

#### Request
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "execute_procedure",
    "arguments": {
      "dataSource": "production",
      "procedure": "sp_GetUserOrders",
      "parameters": {
        "@userId": 123,
        "@status": "Pending"
      },
      "options": {
        "timeout": 120,
        "includeOutputParameters": true
      }
    }
  },
  "id": 2
}
```

#### Parâmetros

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `dataSource` | string | ✅ | Nome do datasource configurado |
| `procedure` | string | ✅ | Nome da stored procedure |
| `parameters` | object | ❌ | Parâmetros (input e output) |
| `options.timeout` | number | ❌ | Timeout em segundos (default: 300) |
| `options.includeOutputParameters` | boolean | ❌ | Incluir parâmetros OUTPUT no resultado (default: false) |

#### Response - Sucesso
```json
{
  "jsonrpc": "2.0",
  "result": {
    "success": true,
    "resultSets": [
      {
        "name": "Orders",
        "rowCount": 3,
        "data": [
          {
            "OrderId": 1001,
            "OrderDate": "2024-01-15T00:00:00Z",
            "Total": 150.50,
            "Status": "Pending"
          },
          {
            "OrderId": 1002,
            "OrderDate": "2024-01-16T00:00:00Z",
            "Total": 200.00,
            "Status": "Pending"
          },
          {
            "OrderId": 1003,
            "OrderDate": "2024-01-17T00:00:00Z",
            "Total": 75.25,
            "Status": "Pending"
          }
        ]
      }
    ],
    "outputParameters": {
      "@totalAmount": 425.75,
      "@orderCount": 3
    },
    "metadata": {
      "procedureName": "sp_GetUserOrders",
      "executionTimeMs": 523
    }
  },
  "id": 2
}
```

#### Response - Múltiplos Result Sets
```json
{
  "jsonrpc": "2.0",
  "result": {
    "success": true,
    "resultSets": [
      {
        "name": "Users",
        "rowCount": 1,
        "data": [
          {
            "UserId": 123,
            "Name": "John Doe",
            "Email": "john@example.com"
          }
        ]
      },
      {
        "name": "Orders",
        "rowCount": 5,
        "data": [...]
      },
      {
        "name": "Payments",
        "rowCount": 5,
        "data": [...]
      }
    ],
    "metadata": {
      "executionTimeMs": 1205
    }
  },
  "id": 2
}
```

---

### 3.3 Tool: `get_datasources`

**Propósito**: Listar todos os datasources configurados

#### Request
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_datasources",
    "arguments": {}
  },
  "id": 3
}
```

#### Response
```json
{
  "jsonrpc": "2.0",
  "result": {
    "success": true,
    "dataSources": [
      {
        "name": "production",
        "description": "SQL Server Production - Ecommerce",
        "server": "sql-prod.company.com",
        "database": "ecommerce",
        "status": "connected",
        "lastCheckTime": "2024-01-30T15:45:30Z"
      },
      {
        "name": "development",
        "description": "SQL Server Local Development",
        "server": "localhost",
        "database": "ecommerce_dev",
        "status": "connected",
        "lastCheckTime": "2024-01-30T15:45:25Z"
      },
      {
        "name": "analytics",
        "description": "SQL Server Analytics - Data Warehouse",
        "server": "sql-analytics.company.com",
        "database": "dwh",
        "status": "disconnected",
        "lastError": "Connection timeout",
        "lastCheckTime": "2024-01-30T15:40:15Z"
      }
    ]
  },
  "id": 3
}
```

---

## 4. Gerenciamento de Conexões

### Connection Pooling

- **Tipo**: Automático via SqlClient
- **Pool Size**: 100 conexões por padrão
- **Min Pool Size**: 5
- **Max Pool Size**: Configurável por datasource
- **Connection Lifetime**: 0 (indefinida, mas renovada a cada uso)

### Lifecycle

```
Startup:
  ├─ Ler appsettings.json
  ├─ Validar cada datasource
  ├─ Tentar conexão de teste
  ├─ Iniciar pool
  └─ Registrar status

Durante execução:
  ├─ Retirar conexão do pool
  ├─ Executar comando
  ├─ Devolver conexão ao pool
  └─ Se erro: log e retry automático

Shutdown:
  ├─ Fechar todas as pools
  └─ Limpar recursos
```

### Retry Logic

```csharp
// Exemplo de política de retry automático
- Conexão recusada: retry 3x com backoff exponencial (100ms, 200ms, 400ms)
- Timeout: NOT retried (retornar erro imediatamente)
- Deadlock: retry 1x
- Erro de autenticação: NOT retried
```

---

## 5. Tratamento de Erros

### Códigos de Erro

| Código | Significado | Retry? | Ação Recomendada |
|--------|-------------|--------|------------------|
| `DATASOURCE_NOT_FOUND` | Datasource não existe | ❌ | Verificar nome do datasource |
| `CONNECTION_FAILED` | Falha ao conectar | ✅ | Validar credenciais, rede, SQL Server |
| `QUERY_TIMEOUT` | Query excedeu timeout | ❌ | Aumentar timeout ou otimizar query |
| `QUERY_SYNTAX_ERROR` | Erro de sintaxe SQL | ❌ | Corrigir query |
| `PARAMETER_MISMATCH` | Parâmetro não existe | ❌ | Verificar nomes de parâmetros |
| `PERMISSION_DENIED` | Usuário sem permissão | ❌ | Verificar credenciais e grants |
| `DATABASE_NOT_FOUND` | Database não existe | ❌ | Verificar database na config |
| `INTERNAL_ERROR` | Erro interno do MCP | ❌ | Verificar logs |

### Exemplo de Erro

```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32603,
    "message": "Database query error",
    "data": {
      "errorCode": "QUERY_SYNTAX_ERROR",
      "sqlError": "Incorrect syntax near the keyword 'SELEC'",
      "query": "SELEC * FROM Users",
      "dataSource": "production",
      "line": 1,
      "column": 1
    }
  },
  "id": 1
}
```

---

## 6. Segurança

### Proteção contra SQL Injection

✅ **SEMPRE usar parâmetros nomeados:**
```json
{
  "query": "SELECT * FROM Users WHERE Id = @id AND Status = @status",
  "parameters": {
    "@id": 123,
    "@status": "Active"
  }
}
```

❌ **NUNCA concatenar strings:**
```json
{
  "query": "SELECT * FROM Users WHERE Id = 123 AND Status = 'Active'"
}
```

### Validação de Input

1. **Query Length**: Máximo 1MB
2. **Parâmetros**: Máximo 1000 por query
3. **Result Set**: Máximo 100MB (streaming para datasets maiores)
4. **Timeout obrigatório**: Mínimo 1s, máximo 3600s

### Auditoria

- Log de todas as queries executadas (sem parâmetros sensíveis)
- Log de conexões/desconexões
- Log de erros e timeouts
- Formato: JSON estruturado para fácil análise

```json
{
  "timestamp": "2024-01-30T15:45:30.123Z",
  "eventType": "query_executed",
  "dataSource": "production",
  "procedureName": "sp_GetUserOrders",
  "parametersCount": 2,
  "rowsAffected": 3,
  "executionTimeMs": 245,
  "success": true
}
```

---

## 7. Configuração do Cliente MCP

### Claude Desktop (claude_desktop_config.json)

```json
{
  "mcpServers": {
    "sharp-mssql": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Projetos\\sharp-mssql-mcp",
        "--configuration",
        "Release"
      ],
      "env": {
        "SQL_PROD_USER": "sa",
        "SQL_PROD_PASSWORD": "${SQL_PROD_PASSWORD}",
        "SQL_ANALYTICS_USER": "analytics_reader",
        "AZURE_TENANT_ID": "${AZURE_TENANT_ID}",
        "AZURE_CLIENT_ID": "${AZURE_CLIENT_ID}",
        "AZURE_CLIENT_SECRET": "${AZURE_CLIENT_SECRET}"
      }
    }
  }
}
```

---

## 8. Exemplo de Uso Completo

### Cenário: Consultar usuários ativos e suas encomendas

**Passo 1**: Listar datasources
```json
{
  "method": "tools/call",
  "params": {
    "name": "get_datasources",
    "arguments": {}
  }
}
```

**Passo 2**: Executar query
```json
{
  "method": "tools/call",
  "params": {
    "name": "execute_query",
    "arguments": {
      "dataSource": "production",
      "query": "SELECT Id, Name, Email FROM Users WHERE Status = @status ORDER BY CreatedAt DESC",
      "parameters": {
        "@status": "Active"
      },
      "options": {
        "maxRows": 100,
        "timeout": 30
      }
    }
  }
}
```

**Passo 3**: Para cada usuário, executar procedure
```json
{
  "method": "tools/call",
  "params": {
    "name": "execute_procedure",
    "arguments": {
      "dataSource": "production",
      "procedure": "sp_GetUserOrders",
      "parameters": {
        "@userId": 123,
        "@status": "Pending"
      },
      "options": {
        "timeout": 60,
        "includeOutputParameters": true
      }
    }
  }
}
```

---

## 9. Não Suportado (MVP)

- ❌ Transações (BEGIN/COMMIT/ROLLBACK) - cada query é auto-commit
- ❌ Cursores persistentes entre chamadas
- ❌ Execução de DDL (CREATE/ALTER/DROP) - apenas DML e procedures
- ❌ Backup/Restore
- ❌ Replicação de dados
- ❌ Testes de conectividade/health check (vem em v2)

---

## 10. Roadmap Futuro

### v1.1
- [ ] Transaction support (BEGIN/COMMIT/ROLLBACK)
- [ ] Health check tool
- [ ] Query builder helpers

### v1.2
- [ ] Support to DDL operations
- [ ] Batch execute
- [ ] Query templates/saved queries

### v2.0
- [ ] Async/await for long-running queries
- [ ] Streaming large result sets
- [ ] Query optimization suggestions
- [ ] Schema introspection tools

---

## 11. Build & Deploy

### Build Local
```bash
dotnet build
dotnet run
```

### Build Release
```bash
dotnet publish -c Release -o ./publish
```

### Docker (futuro)
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "sharp-mssql-mcp.dll"]
```

---

## Referências

- [MCP Specification](https://modelcontextprotocol.io/)
- [Microsoft.Data.SqlClient](https://github.com/dotnet/SqlClient)
- [JSON-RPC 2.0](https://www.jsonrpc.org/specification)
- [SQL Server Security Best Practices](https://learn.microsoft.com/en-us/sql/relational-databases/security/)
