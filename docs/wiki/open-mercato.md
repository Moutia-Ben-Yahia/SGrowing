# open-mercato (Open Mercato) - Architecture & Technical Wiki

> **The AI-Engineering Foundation Framework for Modular Business Backends, ERP, CRM & Commerce**  
> GitHub: [open-mercato/open-mercato](https://github.com/open-mercato/open-mercato) | Stack: TypeScript, Next.js 15+ (App Router, React 19), Tailwind CSS, PostgreSQL, Kysely / Drizzle, Redis, Docker, Yarn Berry v4 | License: MIT

---

## 1. Executive Summary & Value Proposition

**open-mercato** is an open-source TypeScript framework built on Next.js and PostgreSQL, purpose-engineered as an **AI-Engineering Foundation Framework**. While conventional application frameworks require starting from a blank slate or stitching together disparate libraries, Open Mercato provides a 80%-complete enterprise back-office foundation out of the box—complete with ready-to-run business modules for CRM, ERP, commerce, warehousing (WMS), and automated workflows.

Crucially, Open Mercato is designed from inception for **AI coding agents** (Claude Code, Cursor, Codex, and Cezar): every project embeds strict architectural rules, module boundaries, database access patterns, and automated verification skills directly within `AGENTS.md` and `.ai/specs/`.

### Core Problems Solved
- **AI Coding Agent Chaos & Drift**: Generative AI tools often place code haphazardly without architectural discipline; Open Mercato provides rigid boundaries, module conventions, and dependency injection guards that guide AI agents to write production-grade code.
- **Repetitive Back-Office Scaffolding**: Provides pre-built, production-tested modules for customer management, product catalogs, shopping carts, checkout, shipping carriers, and invoicing.
- **Autonomous Agent Workflows (Cezar Integration)**: Integrates with Cezar, an agentic development environment that executes engineering tasks in isolated git worktrees, verifying builds and tests before merging.
- **Deep Web Research & Intelligence Ingestion**: Packages native AI research toolkits (`web-research`, `ai-assistant`) supporting multi-engine web crawling (Firecrawl, Exa, Tavily, SearXNG) for autonomous market intelligence and customer data enrichment.
- **Modular Monolith Composition**: Encapsulates discrete business capabilities into standalone modules with isolated database schemas, API routes, and event hooks.

---

## 2. High-Level System Architecture

Open Mercato is structured as a modern Yarn Berry monorepo pairing the Next.js full-stack back-office web application (`apps/mercato`) with modular domain packages (`packages/*`).

```mermaid
flowchart TD
    subgraph ClientAndAgents ["Users & AI Autonomous Agents"]
        AdminUser["Business Operator / Admin (Next.js Dashboard)"]
        CodingAgents["AI Coding Agents (Claude Code / Codex / Cursor / Cezar)"]
        ExternalClients["Customer Storefronts & Mobile Clients"]
    end

    subgraph AppServer ["Next.js Full-Stack Application (apps/mercato)"]
        AppRouter["App Router (React 19 Server Components)"]
        RouteHandlers["API Route Handlers (/api/v1/...)"]
        ServerActions["Secure Server Actions & Form Mutations"]
        AgentSpecs[".ai/specs/ & AGENTS.md Architectural Harness"]
        
        subgraph DIAndBootstrap ["Core Runtime Core (bootstrap.ts & di.ts)"]
            DependencyInjection["Dependency Injection Container"]
            ModuleRegistry["Dynamic Module Registry (modules.ts)"]
            EventBus["Synchronous & Async Event Bus (packages/events)"]
        end
    end

    subgraph BusinessModules ["Core Domain Modules (packages/core/src/modules/*)"]
        CustomersModule["Customers & Accounts Module"]
        SalesModule["Sales, Invoices & Orders Module"]
        CatalogModule["Product Catalog & Inventory Module"]
        WmsModule["Warehouse Management (WMS) Module"]
        WorkflowsModule["Automated Business Rules & Workflows"]
        AiAssistantModule["AI Assistant & Tool Calling Engine"]
        WebResearchModule["Multi-Engine Web Research (Tavily/Exa/Firecrawl)"]
    end

    subgraph InfrastructureTier ["Persistence, Queue & External Services"]
        PostgresDB[(PostgreSQL 15+ Relational Database)]
        RedisCache[(Redis - Cache & Queue Broker :6379)]
        MercatoQueue["Queue Engine (packages/queue)"]
        StorageEngine["S3-Compatible Object Store (packages/storage-s3)"]
        PaymentGateways["Stripe / TPay Gateways"]
    end

    AdminUser --> AppRouter
    CodingAgents -->|Operates via| AgentSpecs
    ExternalClients --> RouteHandlers

    AppRouter --> ServerActions
    ServerActions --> DependencyInjection
    RouteHandlers --> DependencyInjection

    DependencyInjection --> ModuleRegistry
    ModuleRegistry --> BusinessModules
    BusinessModules --> EventBus

    EventBus --> MercatoQueue
    MercatoQueue --> RedisCache
    BusinessModules --> PostgresDB
    BusinessModules --> StorageEngine
    BusinessModules --> PaymentGateways
```

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Customer {
        +UUID id
        +String email
        +String companyName
        +String status
        +List~SalesOrder~ orders
        +createOrder(items) SalesOrder
    }

    class SalesOrder {
        +UUID id
        +UUID customerId
        +String orderNumber
        +Float totalAmount
        +String status
        +List~OrderItem~ items
        +recalculateTotal() Float
        +transitionStatus(newStatus) Void
    }

    class OrderItem {
        +UUID id
        +UUID orderId
        +UUID catalogItemId
        +Int quantity
        +Float unitPrice
    }

    class CatalogItem {
        +UUID id
        +String sku
        +String name
        +Float price
        +Int stockLevel
        +reserveStock(qty) Boolean
    }

    class WorkflowEngine {
        +triggerEvent(eventName, payload) Void
        +evaluateRules(context) Boolean
        +executeAction(actionName, params) Void
    }

    class AiResearchAgent {
        +searchQuery(query, engine) List~SearchResult~
        +extractCompanyProfile(domain) CompanyData
        +enrichCustomer(customerId) Void
    }

    Customer "1" *-- "many" SalesOrder : places
    SalesOrder "1" *-- "many" OrderItem : contains
    CatalogItem "1" *-- "many" OrderItem : fulfills
    SalesOrder ..> WorkflowEngine : triggers
    AiResearchAgent ..> Customer : enriches
```

---

## 3. Technology Stack & Key Dependencies

- **Framework & Runtime**: Next.js 15+ (App Router), React 19, TypeScript 5+, Node.js 22+
- **Styling**: Tailwind CSS, PostCSS, Radix UI component primitives (`packages/ui`)
- **Database & Query Builders**: PostgreSQL 15+ with Kysely / Drizzle type-safe SQL query engines
- **Queue & Async Execution**: Redis-backed queue system (`packages/queue`) with cron scheduler (`packages/scheduler`)
- **Package Management**: Yarn 4.x Berry with zero-install workspaces
- **AI Integrations**:
  - `packages/ai-assistant`: LLM prompt orchestration, function calling, tool registration
  - `packages/web-research`: Multi-provider intelligence crawlers (Exa, Firecrawl, Tavily, SearXNG)
  - Native support for coding agent IDEs (Cezar, Claude Code)
- **Payment & Shipping Gateways**: `gateway-stripe`, `gateway-tpay`, `carrier-inpost`
- **Testing**: Jest with custom DOM and database testing utilities

---

## 4. Repository Layout & Key Modules

```
open-mercato/
├── apps/
│   ├── mercato/                       # Main Next.js enterprise web application
│   │   ├── src/
│   │   │   ├── app/                   # Next.js App Router (Admin dashboard, dynamic views)
│   │   │   ├── modules/               # Application-level custom business extensions
│   │   │   ├── bootstrap.ts           # Service container initialization & DI lifecycle
│   │   │   ├── di.ts                  # Typed dependency injection resolver
│   │   │   └── modules.ts             # Dynamic module loader and hook registry
│   │   └── next.config.ts             # Next.js compiler settings and package transpilation
│   └── docs/                          # Developer and architecture documentation
├── packages/
│   ├── core/                          # Foundational business modules
│   │   └── src/modules/
│   │       ├── customers/             # Customer directory, accounts, groups, and credit limits
│   │       ├── sales/                 # Quotes, sales orders, order items, status machines
│   │       ├── catalog/               # Products, variants, SKUs, inventory stock levels
│   │       ├── cart/                  # Cart calculations, promo discounts, tax lines
│   │       ├── wms/                   # Warehouse locations, bin picking, fulfillment
│   │       ├── workflows/             # Event-triggered rule evaluator and automations
│   │       ├── business_rules/        # Dynamic predicate rules (e.g., minimum margin checks)
│   │       ├── auth/                  # RBAC permissions, session tokens, password hashing
│   │       └── api_keys/              # API key issuing, rotation, and rate-limiting
│   ├── ai-assistant/                  # AI agent orchestration and tool registries
│   ├── web-research/                  # Web crawling and search integration core
│   │   ├── web-research-firecrawl/    # Firecrawl web scraping adapter
│   │   ├── web-research-tavily/       # Tavily AI search adapter
│   │   └── web-research-exa/          # Exa semantic web retrieval adapter
│   ├── queue/                         # Redis / in-memory task queues and retry logic
│   ├── events/                        # Event bus emitter and subscriber registration
│   ├── gateway-stripe/                # Stripe payment gateway integration
│   ├── checkout/                      # One-page checkout flow orchestration
│   └── ui/                            # Reusable React component library
├── .ai/specs/                         # Spec-first AI coding instructions
├── AGENTS.md                          # Global rules and skills for AI agents
├── docker-compose.yml                 # Local PostgreSQL, Redis, and Mailpit environment
└── package.json                       # Root workspaces configuration
```

---

## 5. Core Features & Capabilities

### 5.1 Spec-First AI Coding Harness (`AGENTS.md`)
Open Mercato pioneers an architecture-first approach to AI-assisted coding:
- Specs live directly beside the codebase in `.ai/specs/` (`plan`, `approve`, `implement`).
- The repository’s `AGENTS.md` defines precise conventions: where new entities belong, how database queries must be typed via Kysely/Drizzle, and how UI forms must utilize the design system.
- Cezar runs tasks inside isolated git worktrees, executing test suites before opening pull requests.

### 5.2 Pluggable Modular Architecture
The framework avoids the pitfalls of monolithic spaghettification by treating every capability as an independent module (`packages/core/src/modules/`):
- Modules register database migrations, REST endpoints, and event listeners via standard manifest interfaces.
- Modules can be toggled on/off via configuration flags (`feature_toggles`).
- External developers can scaffold standalone packages using `create-mercato-module`.

### 5.3 Automated Business Rule & Workflow Engine
The `workflows` and `business_rules` modules allow non-developers to configure conditional operational logic:
- *Condition*: `order.total_amount > 5000 AND customer.credit_score < 700`
- *Action*: `require_manager_approval(orderId)` or `enqueue_slack_notification(channel)`
- Evaluated synchronously during request execution or asynchronously via `packages/queue`.

### 5.4 Autonomous Web Research & Intelligence
Through `packages/web-research`, Open Mercato can autonomously research entities:
- When a new B2B customer signs up, a background agent invokes Tavily/Firecrawl to crawl the customer's domain.
- The AI extracts company size, industry classification, key decision-makers, and revenue brackets, saving the enriched profile directly into `customers`.

---

## 6. Data Models & Schema Architecture

```mermaid
erDiagram
    ORGANIZATIONS ||--o{ USERS : "employs"
    ORGANIZATIONS ||--o{ CUSTOMERS : "manages"
    CUSTOMERS ||--o{ SALES_ORDERS : "places"
    
    CATALOG_ITEMS ||--o{ ORDER_ITEMS : "stocked as"
    SALES_ORDERS ||--o{ ORDER_ITEMS : "contains"
    
    ORGANIZATIONS ||--o{ WORKFLOWS : "configures"

    ORGANIZATIONS {
        uuid id PK
        string name
        string slug UK
        string default_currency
        timestamptz created_at
    }

    USERS {
        uuid id PK
        uuid organization_id FK
        string email UK
        string role "admin, operator, viewer"
        boolean is_active
    }

    CUSTOMERS {
        uuid id PK
        uuid organization_id FK
        string company_name
        string email
        string tax_number
        decimal credit_limit
        jsonb enrichment_metadata
        timestamptz created_at
    }

    SALES_ORDERS {
        uuid id PK
        uuid customer_id FK
        string order_number UK
        decimal total_amount
        string status "Draft, Confirmed, Processing, Fulfilled, Cancelled"
        string payment_status "Unpaid, Authorized, Paid"
        timestamptz created_at
    }

    CATALOG_ITEMS {
        uuid id PK
        string sku UK
        string title
        decimal price
        int stock_quantity
        boolean is_available
    }

    WORKFLOWS {
        uuid id PK
        uuid organization_id FK
        string event_trigger "order.created, customer.registered"
        jsonb condition_rules
        jsonb execution_actions
        boolean is_active
    }
```

---

## 7. Operational Workflows & Sequence Diagrams

### 7.1 Autonomous Order Ingestion, Risk Evaluation & AI Enrichment

```mermaid
sequenceDiagram
    autonumber
    actor Buyer as B2B Customer (Portal)
    participant Mercato as Next.js API (Mercato Engine)
    participant Rules as Workflow & Rules Engine
    participant Agent as Web Research AI Agent
    participant DB as PostgreSQL Database
    participant Queue as Mercato Queue (Redis)

    Buyer->>Mercato: POST /api/v1/sales/orders (items, paymentTerms)
    Mercato->>DB: INSERT into sales_orders (status: Draft)
    Mercato->>Rules: Evaluate risk rules (creditLimit, margin)
    
    alt Risk Threshold Exceeded
        Rules-->>Mercato: Flag for manual review
        Mercato->>DB: UPDATE sales_orders status = PENDING_APPROVAL
    else Normal Processing
        Rules-->>Mercato: Approved automatically
        Mercato->>DB: UPDATE sales_orders status = CONFIRMED
        Mercato->>Queue: Enqueue order.confirmed event
    end

    Mercato-->>Buyer: 201 Created (Order Confirmed)

    Queue->>Agent: Process background customer enrichment
    Agent->>Agent: Query Tavily / Firecrawl for company domain intel
    Agent->>DB: UPDATE customers SET enrichment_metadata = {...}
```

---

## 8. Strategic Value & Takeaways for MarketingStudio / SGrowing

### Reusable Architectural Patterns
1. **AI-Coding Agent Harness (`AGENTS.md`)**: The spec-first guidelines, test-first verifications, and module structure conventions in Open Mercato can be directly integrated into MarketingStudio to supercharge Claude Code pairing.
2. **Pluggable Domain Module Architecture**: The clean dependency injection and dynamic module registry (`modules.ts`) provide an architectural pattern for extending MarketingStudio with third-party tools and plugins.
3. **Embedded Web Research Subsystem**: The unified multi-crawler abstraction (`packages/web-research` supporting Exa, Tavily, and Firecrawl) can be directly leveraged in MarketingStudio's AI SDR and outbound research pipelines.
