# SGrowing Growth OS — External MCP Client Integration Guide

This guide describes how to connect external AI clients (**Claude Desktop**, **Cursor IDE**, and custom autonomous agent runtimes) to the 17 enterprise tools provided by the `SGrowing.McpServer`.

---

## 1. Overview & Architecture

`SGrowing.McpServer` implements the standard [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) specification (protocol version `2024-11-05`). It supports dual transports:
1. **Standard I/O (`--stdio`)**: Used for local AI desktop applications (Claude Desktop, Cursor) where the host process spawns the MCP server as a child process and communicates via JSON-RPC 2.0 over standard input/output.
2. **HTTP / JSON-RPC Endpoint (`/mcp` & `/api/v1/mcp/tools`)**: Used for web applications, Docker orchestrations, and remote agent clouds.

### Transport Guarantees
- **Pure JSON-RPC on `stdout`**: When running with `--stdio`, all ASP.NET Core framework and lifetime log messages are strictly redirected to `stderr`, guaranteeing zero JSON parse errors for external clients.
- **Port Collision Prevention**: In stdio mode, Kestrel binds dynamically to a loopback port (`127.0.0.1:0`), ensuring it never conflicts with local development backends running on port 5000.
- **Graceful Lifecycle**: When the client closes the standard input stream (EOF), the server automatically triggers clean shutdown with exit code `0`.

---

## 2. Connecting Claude Desktop

### Configuration File Location
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Linux**: `~/.config/Claude/claude_desktop_config.json`

### Windows Configuration (Compiled Binary)
```json
{
  "mcpServers": {
    "sgrowing-growth-os": {
      "command": "C:\\Business\\Software\\SGrowing\\src\\backend\\SGrowing.McpServer\\bin\\Debug\\net10.0\\SGrowing.McpServer.exe",
      "args": ["--stdio"],
      "env": {
        "ConnectionStrings__DefaultConnection": "Host=localhost;Port=5432;Database=sgrowing;Username=postgres;Password=postgres_enterprise_secure_pwd_2026!",
        "ClickHouse__Endpoint": "http://localhost:8123",
        "Minio__Endpoint": "http://localhost:9000"
      }
    }
  }
}
```

### Cross-Platform Configuration (via `dotnet run`)
```json
{
  "mcpServers": {
    "sgrowing-growth-os": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Business\\Software\\SGrowing\\src\\backend\\SGrowing.McpServer\\SGrowing.McpServer.csproj",
        "--",
        "--stdio"
      ],
      "env": {
        "ConnectionStrings__DefaultConnection": "Host=localhost;Port=5432;Database=sgrowing;Username=postgres;Password=postgres_enterprise_secure_pwd_2026!"
      }
    }
  }
}
```

---

## 3. Connecting Cursor IDE

Place the following configuration in your project root at `.cursor/mcp.json` or copy from [`config/mcp/cursor_mcp.json`](file:///c:/Business/Software/SGrowing/config/mcp/cursor_mcp.json):

```json
{
  "mcpServers": {
    "sgrowing-growth-os": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/backend/SGrowing.McpServer/SGrowing.McpServer.csproj",
        "--",
        "--stdio"
      ],
      "env": {
        "ConnectionStrings__DefaultConnection": "Host=localhost;Port=5432;Database=sgrowing;Username=postgres;Password=postgres_enterprise_secure_pwd_2026!",
        "ClickHouse__Endpoint": "http://localhost:8123",
        "Minio__Endpoint": "http://localhost:9000"
      }
    }
  }
}
```

---

## 4. Enumerated Enterprise Tool Roster (17 Tools)

| Tool Name | Domain | Description |
|-----------|--------|-------------|
| `analytics_get_realtime_roas` | Analytics & OLAP | Sub-second aggregated ROAS and CPA metrics across ad channels |
| `analytics_get_funnel_conversion` | Analytics & OLAP | Step-to-step drop-off and conversion rates across sales funnels |
| `courier_reconcile_remittance` | Logistics & Finance | Reconciles Algerian COD courier bordereau statements (Yalidine, ZR Express, Procolis) |
| `campaign_audit_and_rebalance` | AI Marketing | Multi-agent budget rebalancing with cryptographic HITL approval gating |
| `document_vault_verify_seal` | Cryptography & Security | ISO 32000-1 PAdES-B-LTV and RFC 3161 cryptographic seal verification |
| `document_mirror_to_s3` | Storage & Cloud | Replicates sealed documents to immutable S3/MinIO cold storage |
| `crm_search_leads` | CRM | Searches prospects by name, company, email, status, and lead score |
| `outbound_send_spintax_campaign` | Outbound | Dispatches permutation-variant outreach sequence to prospects |
| `deliverability_run_placement_test` | Deliverability | Evaluates inbox placement across Google Workspace, M365, and Yahoo |
| `contracts_dispatch_for_signature` | Legal | Multi-party cryptographic contract envelope with OTP verification |
| `courier_generate_waybill` | Logistics | Generates COD tracking numbers and barcode 128 payloads |
| `waterfall_enrich_lead` | Data Enrichment | Waterfall cascade (Hunter → Apollo → Prospeo) lead enrichment |
| `callcenter_acquire_order_lock` | E-Commerce | Anti-collision sliding-window TTL lock on orders |
| `cod_calculate_net_margin` | E-Commerce | Computes true COD net margin factoring in logistics, COGS, and return penalties |
| `seo_execute_multipage_audit` | SEO | High-concurrency website graph crawler for canonical and link health |
| `agency_deploy_snapshot_blueprint` | Agency Multi-Tenancy | Deploys agency pipelines and workflow DAGs into client sub-accounts |
| `gateways_dispatch_android_sms` | Telephony & Gateways | Dispatches SMS messages through enrolled physical Android devices |

---

## 5. Automated Verification Harness

To verify proper operation of the MCP stdio transport, run the automated test suite:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/test-mcp-client.ps1
```

The script executes:
1. `initialize` handshake
2. `notifications/initialized`
3. `ping`
4. `tools/list` (verifies exactly 17 tools returned)
5. `tools/call` for `analytics_get_realtime_roas`
6. `tools/call` for `cod_calculate_net_margin`
7. `tools/call` for `courier_reconcile_remittance`
8. Clean EOF shutdown validation
