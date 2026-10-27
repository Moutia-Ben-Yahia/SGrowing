# ever-works (Ever Works Platform) - Architecture & Technical Wiki

> **Autonomous Agentic Runtime & Software Workshop: From Idea to Researched, Coded, and Deployed Business**  
> GitHub: [ever-works/ever-works](https://github.com/ever-works/ever-works) | Stack: TypeScript, NestJS, Next.js 15+, React 19, TypeORM (PostgreSQL), Turborepo / pnpm, Trigger.dev v3, Model Context Protocol (MCP) | License: AGPL-3.0

---

## 1. Executive Summary & Value Proposition

**Ever Works** ("The Workshop for AI") is an open-source, extensible agentic runtime platform engineered by Ever Co. LTD (makers of [Ever Gauzy](ever-gauzy.md), [Ever Demand](ever-demand.md), and [Ever Teams](ever-teams.md)). Designed as a complete 24/7 autonomous production shop, Ever Works takes a high-level prompt or business concept and orchestrates teams of AI agents to autonomously research the domain, write structured content, generate complete software applications and sites, commit code to Git, and deploy to modern hosting targets.

Ever Works enforces a **Git-native lifecycle** where every artifact—code, content models, documentation, and comparison matrices—lives as native Git commits. This eliminates vendor lock-in, ensures full provenance, and allows human developers to collaborate seamlessly with autonomous agent workflows via standard pull requests.

### Core Problems Solved
- **Fragmented AI Workflows**: Replaces disconnected coding assistants, copywriters, and deployment scripts with a unified, 15-step deterministic generation pipeline.
- **Surface-Level Hallucination**: Employs an autonomous research phase where agents perform deep web searches (Tavily, Brave, Exa, Firecrawl), extract citations, and verify claims before generating a single line of code or copy.
- **True Code & Content Ownership**: Unlike closed SaaS website builders, Ever Works commits every generation into user-owned GitHub repositories with standard file structures and clean dependencies.
- **Scheduled Continuous Improvement**: Agents do not terminate after initial deployment; they run on cron schedules to harvest new market data, update comparison charts, optimize SEO copy, and open maintenance PRs.
- **Multi-Surface Architecture**: Provides a single monorepo powering a Next.js 15+ Web application, NestJS REST API, CLI, Electron desktop app, Kubernetes hosting operator, and native Model Context Protocol (MCP) server.

---

## 2. High-Level System Architecture

Ever Works is architected as a high-performance Turborepo monorepo divided into consumer applications (`apps/`) and reusable modular packages (`packages/`).

```mermaid
flowchart TD
    subgraph ClientSurfaces ["Client Surfaces & Interfaces (apps/)"]
        WebApp["Web App (Next.js 15+ / React 19)"]
        AdminDashboard["Admin Dashboard (Nest / React)"]
        CLIClient["Ever Works CLI (@ever-works/cli)"]
        DesktopApp["Desktop App (Electron)"]
        MCPServer["MCP Server (apps/mcp)"]
    end

    subgraph ApiControlPlane ["API & Orchestration Plane (apps/api)"]
        NestApp["NestJS Core API Engine"]
        AuthModule["Auth & RBAC (OAuth / JWT)"]
        WorkAgentModule["Work Agent & Pipeline Controller"]
        PluginRegistry["Plugin Management Subsystem"]
        BillingModule["Subscriptions & Budgets"]
    end

    subgraph AgentRuntimeCore ["Autonomous Agent Subsystem (packages/agent)"]
        PipelineEngine["15-Step Pipeline Engine"]
        ResearchAgent["Research & Crawl Agent"]
        ContentAgent["Content Generation Agent"]
        CodeAgent["Code Generation & Scaffold Agent"]
        DeployAgent["Deployment & CI Agent"]
    end

    subgraph ExtensiblePlugins ["Plugin Ecosystem (packages/plugins)"]
        AIProviders["AI Providers (OpenRouter, OpenAI, Anthropic, Gemini, Groq)"]
        SearchPlugins["Search (Tavily, Brave, Exa, Firecrawl, SerpAPI)"]
        Extractors["Extractors (Local, Notion, Scrapfly, PDF)"]
        DeployPlugins["Deploy Targets (Vercel, Docker, GitHub Pages)"]
    end

    subgraph ExecutionAndStorage ["Background Processing & Persistence"]
        TriggerDev["Trigger.dev v3 Task Engine"]
        PostgresDB["PostgreSQL (TypeORM Relational Entities)"]
        RedisCache["Redis (Cache, BullMQ, Throttling)"]
        GitStorage["GitHub / Git VCS Repositories"]
        S3Storage["S3 / MinIO (Media & Artifacts)"]
    end

    WebApp --> NestApp
    AdminDashboard --> NestApp
    CLIClient --> NestApp
    DesktopApp --> NestApp
    MCPServer --> NestApp

    NestApp --> AuthModule
    NestApp --> WorkAgentModule
    NestApp --> PluginRegistry
    NestApp --> BillingModule

    WorkAgentModule --> PipelineEngine
    PipelineEngine --> ResearchAgent
    PipelineEngine --> ContentAgent
    PipelineEngine --> CodeAgent
    PipelineEngine --> DeployAgent

    ResearchAgent <--> SearchPlugins
    ResearchAgent <--> Extractors
    ContentAgent <--> AIProviders
    CodeAgent <--> AIProviders
    DeployAgent <--> DeployPlugins

    WorkAgentModule --> TriggerDev
    NestApp <--> PostgresDB
    NestApp <--> RedisCache
    CodeAgent <--> GitStorage
    DeployAgent <--> S3Storage
```

---

## 2.1 Core Domain & Service Class Architecture

Ever Works organizes its domain around `Work` entities, autonomous `Runs`, structured `Pipelines`, and modular `Plugins`.

```mermaid
classDiagram
    class Work {
        +string id
        +string organizationId
        +string name
        +string slug
        +string status
        +string description
        +json targetConfig
        +datetime createdAt
        +datetime updatedAt
    }

    class Project {
        +string id
        +string workId
        +string repositoryUrl
        +string branch
        +string framework
        +string deploymentUrl
    }

    class Pipeline {
        +string id
        +string name
        +string version
        +PipelineStep[] steps
        +json config
    }

    class Run {
        +string id
        +string workId
        +string pipelineId
        +string status
        +int currentStepIndex
        +json stepResults
        +datetime startedAt
        +datetime completedAt
    }

    class AgentPlugin {
        +string id
        +string name
        +string category
        +string provider
        +boolean isEnabled
        +json credentials
    }

    class Task {
        +string id
        +string runId
        +string stepName
        +string status
        +json inputPayload
        +json outputArtifacts
        +int tokenCost
    }

    Work "1" --> "*" Project : manages
    Work "1" --> "*" Run : executes
    Run "*" --> "1" Pipeline : follows
    Run "1" --> "*" Task : generates
    Pipeline ..> AgentPlugin : utilizes
```

---

## 3. Technology Stack & Key Dependencies

| Layer | Component | Description & Role |
| :--- | :--- | :--- |
| **Monorepo Architecture** | Turborepo, pnpm workspaces | High-velocity build cache, unified dependency management |
| **Backend API** | NestJS v11+, TypeScript, `@nestjs/swagger` | Modular enterprise backend service with dependency injection |
| **Frontend Applications** | Next.js 15+ (App Router), React 19, Tailwind CSS | Server Components, responsive design, and streaming state |
| **Database & ORM** | PostgreSQL, TypeORM | Relational persistence for multi-tenant organizations, works, and runs |
| **Workflow Engine** | Trigger.dev v3, BullMQ, Redis | Long-running, resilient asynchronous agent task orchestration |
| **AI Orchestration** | OpenRouter, Vercel AI SDK, Composio | Unified multi-model gateway with tool calling and sandboxing |
| **Agent Protocols** | Model Context Protocol (MCP) | Bi-directional IDE and external agent integration |
| **VCS & Deployment** | Octokit (GitHub API), Vercel SDK | Automated branch creation, pull request generation, and deployment |

---

## 4. Repository Layout & Key Modules

```
inspire/ever-works/
├── apps/
│   ├── api/                         # NestJS API backend service
│   │   └── src/                     # Core modules: work-agent, runs, pipelines, auth, billing
│   ├── web/                         # Next.js 15+ web application and user portal
│   ├── admin/                       # Administrative control plane
│   ├── cli/                         # Command-line interface for agent operations
│   ├── mcp/                         # Model Context Protocol server exposing works API
│   ├── desktop/                     # Electron desktop client shell
│   └── hosting-operator/            # Kubernetes / Docker hosting orchestration controller
├── packages/
│   ├── agent/                       # Core autonomous agent runtime & pipeline executor
│   ├── agent-plugins/               # Integrated plugins (OpenAI, Anthropic, Tavily, Vercel)
│   ├── contracts/                   # Shared TypeScript interfaces, DTOs, and schemas
│   ├── plugin/                      # Plugin lifecycle and registration primitives
│   ├── tasks/                       # Trigger.dev background task workflow definitions
│   └── monitoring/                  # Sentry and PostHog telemetry instrumentation
├── docker-compose.yml               # Multi-container local orchestration (Postgres, Redis, API, Web)
├── pnpm-workspace.yaml              # Workspace directory configurations
└── turbo.json                       # Turborepo task pipeline definitions
```

---

## 5. Core Features & Capabilities

### 5.1 The 15-Step Idea-to-Site Pipeline
Ever Works implements a deterministic, multi-stage production pipeline:
1. **Concept Ingestion**: Parses high-level user instructions, target audience, and domain parameters.
2. **Deep Market Research**: Queries search APIs (Tavily/Exa/Brave) to discover competing products, key differentiators, and authoritative industry sources.
3. **Information Extraction**: Crawls and parses target URLs using clean headless extractors or Scrapfly.
4. **Content Architecture Design**: Formulates structured site maps, entity taxonomies, and comparison dimensions.
5. **Copywriting & Editorial Generation**: Produces long-form, citation-backed copy adhering to brand guidelines.
6. **Codebase Scaffolding**: Initializes a modern Next.js/Tailwind or Astro repository structure.
7. **Component Generation**: Generates UI components, landing pages, and interactive comparison tables.
8. **Automated Static Analysis & Testing**: Runs TypeScript type checking and linter validations against generated code.
9. **Git Commit & Repository Sync**: Pushes the generated codebase to a newly created GitHub repository.
10. **Automated Hosting Deployment**: Provisions project hosting on Vercel or Kubernetes via hosting operators.
11. **Screenshot & Visual Validation**: Captures live page previews using ScreenshotOne or Urlbox.
12. **Verification & Audit**: Validates Lighthouse SEO, accessibility, and Core Web Vitals performance.
13. **DNS & Custom Domain Setup**: Configures routing and TLS certificates.
14. **Telemetry & Analytics Ingestion**: Automatically injects analytics tracking (PostHog/Jitsu).
15. **Maintenance Scheduling**: Registers background cron triggers for recurring updates and SEO refreshes.

### 5.2 Git-Native Continuous Maintenance
Unlike traditional website generators where updates require manual re-exports, Ever Works maintains a persistent Git link:
- Scheduled workers monitor external data sources for price changes, new product features, or breaking industry news.
- When updates are required, agents fork a feature branch, apply code/content mutations, and open a GitHub pull request with detailed changelogs for human review.

---

## 6. Data Models & Schema Architecture

```mermaid
erDiagram
    ORGANIZATIONS ||--o{ USERS : employs
    ORGANIZATIONS ||--o{ WORKS : owns
    ORGANIZATIONS ||--o{ AGENT_PLUGINS : configures
    
    WORKS ||--o{ PROJECTS : contains
    WORKS ||--o{ RUNS : executes
    
    RUNS ||--o{ TASKS : consists_of
    RUNS }o--|| PIPELINES : executes_via

    ORGANIZATIONS {
        uuid id PK
        string name UK
        string plan "free, pro, enterprise"
        datetime created_at
    }

    USERS {
        uuid id PK
        uuid organization_id FK
        string email UK
        string password_hash
        string role "owner, admin, member"
        datetime created_at
    }

    WORKS {
        uuid id PK
        uuid organization_id FK
        string name
        string slug UK
        string status "active, building, paused"
        json prompt_config
        datetime created_at
    }

    PROJECTS {
        uuid id PK
        uuid work_id FK
        string github_repo_url
        string default_branch
        string deployment_url
        string framework "nextjs, astro, svelte"
    }

    RUNS {
        uuid id PK
        uuid work_id FK
        uuid pipeline_id FK
        string status "queued, running, succeeded, failed"
        int current_step
        int total_steps
        datetime started_at
        datetime completed_at
    }

    TASKS {
        uuid id PK
        uuid run_id FK
        string step_name
        string status "pending, in_progress, completed, failed"
        json input_data
        json output_data
        int duration_ms
    }

    AGENT_PLUGINS {
        uuid id PK
        uuid organization_id FK
        string plugin_key "tavily, openrouter, vercel"
        boolean is_active
        json encrypted_settings
    }
```

---

## 7. Operational Workflows & Sequence Diagrams

### 7.1 Autonomous "Idea-to-Site" Execution Flow

```mermaid
sequenceDiagram
    autonumber
    actor User as Product Creator / Operator
    participant Web as Next.js Web App
    participant API as NestJS Works API
    participant Trigger as Trigger.dev / Worker
    participant Agent as Agent Pipeline Runner
    participant LLM as OpenRouter / Model Gateway
    participant Git as GitHub API
    participant Hosting as Vercel Deployment

    User->>Web: Submit Work Concept ("Build modern B2B SaaS Comparison Platform")
    Web->>API: POST /api/v1/works (promptConfig)
    API->>API: Persist Work & initialize Run entity
    API->>Trigger: Enqueue Pipeline Job (15-Step Workflow)
    API-->>Web: 202 Accepted (Run ID: run_abc123)

    Trigger->>Agent: Start Pipeline Execution
    
    Note over Agent,LLM: Step 2: Autonomous Market Research
    Agent->>LLM: Perform web search queries & synthesize competitor matrix
    LLM-->>Agent: Return structured research report
    
    Note over Agent,LLM: Step 6 & 7: Code & Content Generation
    Agent->>LLM: Scaffold Next.js application & generate components
    LLM-->>Agent: Stream source code tree & markdown content
    
    Note over Agent,Git: Step 9: Git Commit & PR
    Agent->>Git: Create repository `ever-works/b2b-saas-guide` & commit files
    Git-->>Agent: Repository initialized with main branch
    
    Note over Agent,Hosting: Step 10: Hosting Deployment
    Agent->>Hosting: Trigger build & deploy from GitHub repo
    Hosting-->>Agent: Deployment live at https://b2b-saas-guide.vercel.app
    
    Agent-->>Trigger: Mark all 15 steps completed
    Trigger->>API: Update Run status to "succeeded"
    API-->>Web: Stream completion event via SSE / WebSocket
    Web-->>User: Display live site preview & GitHub repository links
```

---

## 8. Strategic Value & Takeaways for MarketingStudio / SGrowing

### 1. Multi-Step Deterministic Pipelines
Ever Works demonstrates that production-grade AI generation cannot rely on single unconstrained prompts. By breaking the creation process into a 15-step pipeline with explicit checkpoints (Research → Schema → Copy → Code → Verify → Deploy), generation quality and reproducibility increase by orders of magnitude.

### 2. Git as the Primary Source of Truth
Storing both digital marketing copy and application code in standard Git commits allows MarketingStudio to bridge the gap between creative marketing teams and technical developers. Changes made by autonomous agents can be reviewed, reverted, or merged using standard Git PR workflows.

### 3. Integrated MCP Server Architecture
Ever Works' native `apps/mcp` package shows how modern platforms should expose their internal capabilities to AI coding assistants (like Antigravity or Claude Code). MarketingStudio should provide a first-class MCP server allowing agents to query analytics, trigger campaign builds, and manage contact segments directly from external IDEs.
