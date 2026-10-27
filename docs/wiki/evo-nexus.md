# evo-nexus (EvoNexus) - Architecture & Technical Wiki

> **Multi-Agent Operating Layer & Orchestration Hub for Claude Code CLI & Autonomous Teams**  
> GitHub: [evolution-foundation/evo-nexus](https://github.com/evolution-foundation/evo-nexus) | Stack: Python 3.12+, Flask / SQLAlchemy, Claude Code CLI, OpenClaude, Docker Swarm / Compose, Model Context Protocol (MCP) | License: Apache-2.0

---

## 1. Executive Summary & Value Proposition

**EvoNexus** is the foundational multi-agent operating layer developed by the Evolution Foundation. Engineered as an autonomous orchestration platform built around the Claude Code CLI protocol, EvoNexus transforms a standard terminal installation into an integrated workforce of **38 specialized AI agents** (17 business agents and 21 engineering agents) and **190+ markdown-driven skills**.

Crucially, EvoNexus is not a simple chatbot; it is an active enterprise operating layer that executes scheduled background routines, generates interactive HTML executive reports, triages customer support inboxes, synchronizes calendar events, audits financial health, and orchestrates the development lifecycle of the Evolution Foundation ecosystem (including [Evo CRM Community](evo-crm-community.md), [Evolution API](evolution-api.md), and [Evolution Go](evolution-go.md)).

### Core Problems Solved
- **Vendor Lock-in Elimination**: While defaulting to Anthropic's Claude Code CLI, EvoNexus integrates [OpenClaude](https://www.npmjs.com/package/@gitlawb/openclaude) to route agent prompts transparently across 200+ models on OpenRouter, OpenAI, Google Gemini, AWS Bedrock, Google Vertex AI, or local Ollama instances without modifying agent prompts.
- **Dual-Layer Organizational Division**: Separates operational concerns into two distinct agent groups:
  - **17 Business Agents**: Driving operations, finance, community moderation, marketing copy, human resources, legal compliance, and customer support.
  - **21 Engineering Agents**: Covering architecture, iterative planning, code review, security auditing, testing, and retrospective analysis following a canonical 6-phase engineering lifecycle (`dev-phases.md`).
- **Markdown-First Agent Architecture**: Agents and skills are declared as `.md` system instructions and workflows rather than rigid compiled code. Adding or modifying an agent requires simply creating or editing a markdown file in `.claude/agents/` or via modular plugin manifests.
- **Enterprise Heartbeats & Scheduled Routines**: Employs an asynchronous scheduler (`scheduler.py`) and heartbeat runner (`heartbeat_runner.py`) that wake agents on cron schedules to execute automated auditing, backlog grooming, and repository maintenance.
- **Unified Management Dashboard**: Backed by a Flask/SQLAlchemy engine, EvoNexus provides an executive web dashboard with RBAC, ticket inboxes, goal tracking, terminal sessions, and audit logging.

---

## 2. High-Level System Architecture

The EvoNexus architecture bridges human operators, terminal-based CLI tools, scheduled daemons, and external LLM/MCP providers into a centralized operating plane.

```mermaid
flowchart TD
    subgraph HumanAndTriggers ["Human & Ingress Interfaces"]
        Operator["Operator / Developer (Terminal)"]
        DashboardUser["Executive / Manager (Web Dashboard)"]
        CronSchedule["Automated Cron / Scheduler Daemon"]
    end

    subgraph DashboardEngine ["EvoNexus Control Plane (dashboard/backend)"]
        FlaskServer["Flask Application Engine (:5000)"]
        AuthSecurity["Auth & RBAC Security Layer"]
        TicketInbox["Ticket & Support Inbox Controller"]
        HeartbeatDispatcher["Heartbeat & Mission Dispatcher"]
        PluginManager["Plugin & Skill Registry Scanner"]
    end

    subgraph AgentOperatingCore ["Multi-Agent Execution Layer"]
        CLIProtocol["Claude Code CLI Protocol / OpenClaude Bridge"]
        
        subgraph BusinessAgentsPool ["17 Business Agents (.claude/agents/)"]
            Biz1["Apex (Executive & Strategy)"]
            Biz2["Pulse (Community & Support)"]
            Biz3["Flux (Marketing & Growth)"]
            Biz4["Clawdia (Operations & Sync)"]
        end

        subgraph EngineeringAgentsPool ["21 Engineering Agents (.claude/agents/)"]
            Eng1["Architect (System Design)"]
            Eng2["Planner (Task Decomposition)"]
            Eng3["Code Reviewer (PR & Static Analysis)"]
            Eng4["Security Auditor (OWASP & Threat Model)"]
        end
    end

    subgraph SkillsAndMCP ["Capabilities & Tools Engine"]
        SkillsEngine["190+ Reusable Markdown Skills"]
        MCPApapter["Model Context Protocol (MCP) Bridge"]
        ExternalTools["Google Suite, GitHub, Linear, Canva, Telegram"]
    end

    subgraph PersistenceLayer ["Storage & Brain Subsystem"]
        RelationalDB["Dashboard DB (SQLite / PostgreSQL)"]
        BrainRepo["Brain Repository (Markdown Memory & Handoffs)"]
        PluginCache["Plugin & Tool Scan Cache"]
    end

    Operator --> CLIProtocol
    DashboardUser --> FlaskServer
    CronSchedule --> HeartbeatDispatcher

    FlaskServer --> AuthSecurity
    FlaskServer --> TicketInbox
    FlaskServer --> HeartbeatDispatcher
    FlaskServer --> PluginManager

    HeartbeatDispatcher --> CLIProtocol
    CLIProtocol --> BusinessAgentsPool
    CLIProtocol --> EngineeringAgentsPool

    BusinessAgentsPool <--> SkillsEngine
    EngineeringAgentsPool <--> SkillsEngine
    SkillsEngine <--> MCPApapter
    MCPApapter <--> ExternalTools

    FlaskServer <--> RelationalDB
    HeartbeatDispatcher <--> BrainRepo
    PluginManager <--> PluginCache
```

---

## 2.1 Core Domain & Service Class Architecture

EvoNexus organizes backend operations into relational models for mission tracking, scheduled heartbeats, goal decomposition, and plugin auditing.

```mermaid
classDiagram
    class User {
        +int id
        +string username
        +string email
        +string role
        +boolean is_active
        +datetime created_at
        +datetime last_login
        +setPassword(password)
        +checkPassword(password)
    }

    class Mission {
        +int id
        +string title
        +string description
        +string status
        +int priority
        +datetime deadline
        +datetime created_at
    }

    class GoalProject {
        +int id
        +string name
        +string description
        +string status
        +datetime created_at
    }

    class Goal {
        +int id
        +int project_id
        +string title
        +string description
        +string status
        +int target_value
        +int current_value
    }

    class GoalTask {
        +int id
        +int goal_id
        +string title
        +string assigned_agent
        +string status
        +json params
    }

    class Heartbeat {
        +int id
        +string agent_name
        +string cron_expression
        +boolean enabled
        +datetime last_run
        +datetime next_run
    }

    class HeartbeatRun {
        +int id
        +int heartbeat_id
        +string status
        +text logs
        +datetime started_at
        +datetime finished_at
    }

    class Ticket {
        +int id
        +string title
        +string customer_jid
        +string status
        +string priority
        +string assigned_agent
        +datetime created_at
    }

    User --> Mission : creates
    GoalProject --> Goal : contains
    Goal --> GoalTask : decomposes
    Heartbeat --> HeartbeatRun : logs
    Mission ..> GoalProject : aligns with
    Ticket --> GoalTask : generates
```

---

## 3. Technology Stack & Key Dependencies

| Layer | Component / Tool | Role in EvoNexus |
| :--- | :--- | :--- |
| **Agent Protocol** | Claude Code CLI, OpenClaude | Foundation CLI protocol supporting Anthropic, OpenRouter, OpenAI, and Gemini |
| **Backend API** | Python 3.12+, Flask, Werkzeug | REST API and WebSocket controller for the control plane |
| **ORM & Database** | SQLAlchemy, SQLite / PostgreSQL | Structured persistence for users, RBAC, tickets, goals, and heartbeat runs |
| **Task Scheduler** | Python `apscheduler` / `schedule`, asyncio | Cron-based autonomous background agent dispatching |
| **Tool Integration** | Model Context Protocol (MCP) | Standardized client-server bridge for GitHub, Gmail, Linear, Canva, Telegram |
| **Container & Swarm**| Docker, Docker Compose, Docker Swarm | Production containerization (`Dockerfile.swarm`, `evonexus.stack.yml`) |
| **Security & Auth** | Flask-Login, bcrypt, RBAC | Multi-user session management and login rate throttling (`LoginThrottle`) |

---

## 4. Repository Layout & Key Modules

```
inspire/evo-nexus/
├── .claude/
│   ├── agents/                      # 38 System prompt markdown definitions for agents
│   └── rules/                       # Core workflow rules (dev-phases.md, dev-cycle.md)
├── cli/                             # Node.js/TypeScript command line helper wrapper
│   ├── bin/                         # CLI executables
│   └── src/                         # CLI entry points and templates
├── dashboard/                       # Executive web interface
│   ├── backend/                     # Flask REST API backend
│   │   ├── app.py                   # Main Flask application and router endpoints
│   │   ├── models.py                # Complete SQLAlchemy data models
│   │   ├── heartbeat_runner.py      # Background heartbeat execution engine
│   │   ├── ticket_inbox.py          # Unified support ticket inbox
│   │   ├── plugin_loader.py         # Plugin and skill scanner
│   │   └── auth_security.py         # Authentication and role guards
│   ├── frontend/                    # Web dashboard frontend components and assets
│   └── terminal-server/             # Web-based xterm terminal server
├── memory/                          # Persistent agent memory logs and state files
├── plugins/                         # Extensible plugin bundles (skills, hooks, agents)
├── scheduler.py                     # Root scheduler daemon executing automated routines
├── backup.py                        # Automated database and brain repository backup tool
├── ROUTINES.md                      # Declarative definitions of daily/weekly agent routines
├── docker-compose.yml               # Local multi-service orchestration
└── evonexus.stack.yml               # Production Docker Swarm stack definition
```

---

## 5. Core Features & Capabilities

### 5.1 Dual-Layer Agent Architecture
EvoNexus partitions autonomous responsibilities across two dedicated operational tiers:
1. **Business Operations Layer**: Agents such as `Apex` (overall strategic direction), `Clawdia` (daily calendar, email triage, and ops synchronization), `Pulse` (community sentiment and customer engagement), and `Flux` (campaign copywriting and growth experiments).
2. **Software Engineering Layer**: Adheres to a strict 6-phase engineering discipline:
   - *Phase 1: Architecture* (`Architect`)
   - *Phase 2: Planning & Decomposition* (`Planner`)
   - *Phase 3: Implementation & TDD* (`Developer`)
   - *Phase 4: Code Review* (`Code Reviewer`)
   - *Phase 5: Security & Verification* (`Security Auditor`)
   - *Phase 6: Retrospective & Release* (`Retrospective Agent`)

### 5.2 Declarative Routines & Heartbeats
Rather than waiting for user interaction, EvoNexus agents wake autonomously via `scheduler.py`:
- **Morning Standup Routine**: Pulls commits from GitHub, checks unread Linear tickets, checks calendar conflicts, and drafts an executive briefing.
- **Community Health Audit**: Scans community channels, summarizes unanswered member queries, and queues draft replies.
- **Continuous Backlog Grooming**: Evaluates completed tasks, updates `Goal` progress percentages, and archives finished tickets.

### 5.3 Model Context Protocol (MCP) Integration
EvoNexus natively mounts MCP servers into the Claude Code runtime environment, allowing agents to execute external operations deterministically:
- `github-mcp`: Review PRs, create branches, and merge code.
- `google-calendar-mcp` & `gmail-mcp`: Schedule executive meetings and draft outreach responses.
- `linear-mcp`: Transition issues and manage agile sprints.

---

## 6. Data Models & Schema Architecture

```mermaid
erDiagram
    USERS ||--o{ ROLES : assigned
    USERS ||--o{ AUDIT_LOG : generates
    USERS ||--o{ MISSIONS : initiates
    
    MISSIONS ||--o{ GOAL_PROJECTS : decomposes_into
    GOAL_PROJECTS ||--o{ GOALS : contains
    GOALS ||--o{ GOAL_TASKS : executes

    HEARTBEAT ||--o{ HEARTBEAT_RUN : records
    TICKETS ||--o{ TICKET_COMMENTS : receives
    TICKETS ||--o{ TICKET_ACTIVITY : logs

    USERS {
        int id PK
        string username UK
        string email UK
        string password_hash
        string role "admin, operator, viewer"
        boolean is_active
        datetime created_at
    }

    MISSIONS {
        int id PK
        string title
        text description
        string status "draft, in_progress, completed"
        int priority
        datetime deadline
    }

    GOALS {
        int id PK
        int project_id FK
        string title
        string status "not_started, active, achieved"
        int target_value
        int current_value
    }

    GOAL_TASKS {
        int id PK
        int goal_id FK
        string title
        string assigned_agent
        string status "pending, running, done, failed"
        json params
    }

    HEARTBEAT {
        int id PK
        string agent_name
        string cron_expression
        boolean enabled
        datetime last_run
    }

    HEARTBEAT_RUN {
        int id PK
        int heartbeat_id FK
        string status "success, error"
        text logs
        datetime started_at
        datetime finished_at
    }

    TICKETS {
        int id PK
        string title
        string customer_jid
        string status "open, pending, closed"
        string priority
        string assigned_agent
    }
```

---

## 7. Operational Workflows & Sequence Diagrams

### 7.1 Autonomous Scheduled Heartbeat Execution Flow

```mermaid
sequenceDiagram
    autonumber
    actor Scheduler as Scheduler Daemon (scheduler.py)
    participant Core as Heartbeat Runner
    participant Agent as Claude Code CLI (Apex / Pulse)
    participant MCP as MCP Tool Server (GitHub / Gmail)
    participant DB as SQLite / Postgres DB
    participant Dash as Web Dashboard

    Scheduler->>Core: Trigger Heartbeat (Agent: "Pulse", Routine: "CommunityAudit")
    Core->>DB: Record HeartbeatRun (Status: "running")
    Core->>Agent: Spawn agent process with injected system prompt & memory
    
    Agent->>MCP: Query unread community tickets & mentions
    MCP-->>Agent: Return unread messages & user issues
    Agent->>Agent: Analyze sentiment, generate summary & draft resolutions
    
    Agent->>MCP: Post automated status updates / replies
    MCP-->>Agent: Acknowledge posts
    
    Agent-->>Core: Process exits with exit code 0 and summary payload
    Core->>DB: Update HeartbeatRun (Status: "success", Logs: payload)
    Core->>DB: Create Ticket / GoalTask entries for escalations
    Core->>Dash: Emit WebSocket event (Heartbeat Complete)
```

---

## 8. Strategic Value & Takeaways for MarketingStudio / SGrowing

### 1. Unified Operating Layer Pattern
EvoNexus demonstrates how to organize autonomous marketing and engineering initiatives into distinct agent specializations (17 business, 21 engineering) while sharing a single persistent memory store. MarketingStudio can adopt this taxonomy to separate creative content generation from technical campaign analytics.

### 2. Multi-Model Provider Freedom via OpenClaude
By abstracting the underlying LLM via OpenClaude, teams are not held hostage to single-provider rate limits, outages, or pricing tiers. Critical background tasks can route to high-speed low-cost models (e.g. Gemini Flash / Haiku), while high-level strategic reasoning runs on frontier models (Claude Opus / Sonnet / GPT-4o).

### 3. Asynchronous Routine Heartbeats
The pattern of combining cron-scheduled Python daemons with prompt-driven CLI agents allows an organization to function continuously 24/7 without requiring active human prompt loops.
