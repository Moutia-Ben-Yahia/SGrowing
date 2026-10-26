# SGrowing — AI Omnicommunication & Autonomous Outreach Platform

## Project Overview

SGrowing is an enterprise-grade multi-tenant AI Omnicommunication & Autonomous Outreach platform built with **.NET 10** (C# 14, ASP.NET Core Minimal APIs, EF Core, PostgreSQL), **Angular 19** (Signals, Standalone Components, Tailwind CSS), and native **SignalR** real-time streaming.

## Solution Architecture

- **Solution file:** `SGrowing.slnx`
- **Backend Architecture:** Modular Minimal APIs + Domain-Driven Outbox / Event Pattern
  - `src/backend/SGrowing.Api`: ASP.NET Core 10 Minimal APIs, Modular grouping (`SGrowing.Api.Modules.*`), Scalar API reference (`/scalar/v1`), SignalR Hubs (`/hubs/*`), JWT Authentication, Multi-tenant middleware.
  - `src/backend/SGrowing.Core`: Multi-tenancy (`ITenantContext`), Domain Entities, Outbox messaging (`OutboxMessage`), Events, Diagnostics.
  - `src/backend/SGrowing.Worker`: Background hosted worker service for outbox dispatch, cadence execution, and warmup.
  - `tests/SGrowing.Tests`: Unit and integration tests (xUnit).
- **Frontend Architecture:**
  - `src/frontend`: Angular 19, Standalone Components, Signals, Tailwind CSS.

## Build and Test Commands

### Backend (.NET 10 / C# 14)
```bash
# Build solution
dotnet build SGrowing.slnx

# Run API
dotnet run --project src/backend/SGrowing.Api

# Run Worker
dotnet run --project src/backend/SGrowing.Worker

# Run Tests
dotnet test tests/SGrowing.Tests

# EF Core Database Migrations (PostgreSQL)
dotnet ef migrations add <MigrationName> --project src/backend/SGrowing.Api
dotnet ef database update --project src/backend/SGrowing.Api
```

### Frontend (Angular 19)
```bash
cd src/frontend
npm install
npm start          # Runs on http://localhost:4200
npm run build      # Production build
npm test           # Unit tests
```

### Infrastructure (Docker)
```bash
docker compose up -d
```

## Modern C# 14 / .NET 10 Conventions

1. **Use Modern C# 14 features**:
   - Primary constructors for classes and services (`public class LeadService(SGrowingDbContext db, TimeProvider clock)`).
   - Collection expressions (`List<string> tags = ["lead", "active"];`).
   - File-scoped namespaces (`namespace SGrowing.Api.Modules.CRM;`).
   - Immutability with records, `readonly record struct`, and `required` properties.
   - Raw string literals (`""" ... """`) for multiline SQL, templates, and JSON.
2. **Entity Framework Core**:
   - Do NOT wrap EF Core in unnecessary repository layers. Inject `SGrowingDbContext` directly.
   - Always pass `CancellationToken` to all async operations.
   - Use `AsNoTracking()` for read queries.
   - Use `TimeProvider` instead of `DateTime.UtcNow` or `DateTime.Now`.
3. **Minimal APIs**:
   - Group endpoints by feature module inside `src/backend/SGrowing.Api/Modules/<ModuleName>/`.
   - Use `TypedResults` with explicit return types for OpenAPI/Scalar generation.
   - Enforce tenant isolation via `ITenantContext`.
4. **Codebase Navigation via Roslyn MCP**:
   - Prefer Roslyn MCP tools (`cwm-roslyn-navigator`) over costly whole-file reading:
     - `get_symbol_source` to read a single method instead of an entire class.
     - `get_file_outline` to inspect types and members before opening a file.
     - `find_symbol`, `find_references`, `get_type_hierarchy`, and `get_diagnostics`.

## Agent Guidance & Skills

Project-level skills and specialist agents from `dotnet-claude-kit` are installed in `.agents/` and `.claude/`.
See [AGENTS.md](file:///c:/Business/Software/SGrowing/AGENTS.md) for the agent routing table and intent resolution.
Consult `docs/knowledge/` for architecture decision records (ADRs) and antipattern guides.
