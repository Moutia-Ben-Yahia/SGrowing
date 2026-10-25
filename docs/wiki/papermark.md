# papermark - Architecture & Technical Wiki

> **Open-Source Document Sharing Infrastructure, Virtual Data Rooms (VDR), Per-Page Viewing Telemetry & Secure Lead Gating Platform**  
> GitHub: [mfts/papermark](https://github.com/mfts/papermark.git) | Stack: TypeScript, Next.js (App Router), React, PostgreSQL, Prisma ORM, Trigger.dev, Tailwind CSS, S3 / Vercel Blob, BoxyHQ Jackson | License: AGPL-3.0

---

## 1. Executive Summary & Value Proposition

**papermark** is the premier open-source document sharing, pitch deck tracking, and Virtual Data Room (VDR) platform built as a transparent, self-hostable alternative to DocSend, Dropbox DocSend, and Carta. Powered by a Next.js fullstack architecture, Prisma ORM, and PostgreSQL, Papermark provides founders, investment teams, and sales professionals with granular, per-page document telemetry, dynamic watermarking, and ironclad access controls.

When distributing high-stakes pitch decks, confidential financial models, proposals, and sales collaterals, organizations suffer from "blind distribution" — losing visibility over who opened the file, which pages captured interest, how long they read, and whether the document was forwarded to unauthorized competitors. Papermark eliminates this opacity by turning static PDFs into instrumented, secure web experiences with real-time engagement telemetry, lead capture gates, and tamper-resistant dynamic watermarks.

### Core Problems Solved
- **DocSend Monopoly & Expensive Seat Metering**: Replaces high-cost proprietary document tracking subscriptions with an extensible, unlimited self-hosted solution.
- **Pitch Deck & Proposal Black Hole**: Tracks exact viewer dwell time per individual page, generating granular engagement curves that reveal which slides captured attention and where prospects dropped off.
- **Uncontrolled Document Leaking & Forwarding**: Enforces strict access controls including password gating, email verification, domain whitelisting, and dynamic watermarks embedding the viewer's email and IP directly across pages.
- **M&A and Investment Due Diligence Overhead**: Delivers enterprise Virtual Data Rooms (VDR) equipped with multi-tier permission groups, viewer role policies, document indexing, and integrated Q&A conversation threads.
- **Lead Capture Friction**: Converts passive document viewing into active inbound lead generation through customizable lead capture forms, email gates, and automated CRM webhook alerts.

---

## 2. High-Level System Architecture

Papermark integrates a Next.js web application with high-resolution page rasterization, WebSocket/REST telemetry ingestion, asynchronous background processing via Trigger.dev, and multi-file Prisma persistence.

```mermaid
flowchart TD
    subgraph ClientExperiences ["Client Experiences"]
        AdminDashboard["Next.js Admin Console\n(Deck Management & Analytics)"]
        DocumentViewer["Interactive PDF/Slide Viewer\n(Next.js App Router /view/[id])"]
        DataroomPortal["Virtual Data Room Portal\n(Multi-Document Due Diligence)"]
    end

    subgraph AppRouterLayer ["Next.js Fullstack Server (Edge & Node.js Runtime)"]
        ViewerRoute["Document Viewer Server Components\n(app/view/[id]/page.tsx)"]
        TelemetryAPI["Telemetry Ingress Endpoints\n(api/view/track, api/view/page)"]
        GatekeeperService["Access & Gating Service\n(Password, Email, Domain, NDA)"]
        WatermarkEngine["Dynamic Watermark Engine\n(Canvas SVG / PDF Stamping)"]
    end

    subgraph AsyncOrchestration ["Background Task Engine (Trigger.dev)"]
        TriggerWorker["Trigger.dev Worker Cluster"]
        PageRasterizer["PDF Page Splitter & Rasterizer\n(Generates WebP / SVG Pages)"]
        AiSummarizer["AI Deck Analyzer & Summarizer\n(Extracts Topics & Highlights)"]
        NotificationDispatcher["Instant Email & Webhook Dispatcher"]
    end

    subgraph DataStorage ["Data & Blob Storage Tier"]
        PostgresDB[("PostgreSQL Database\n(Prisma Multi-File Schema)")]
        BlobStore[("Object Storage\n(AWS S3 / Cloudflare R2 / Vercel Blob)")]
    end

    AdminDashboard --> AppRouterLayer
    DocumentViewer --> ViewerRoute
    DataroomPortal --> ViewerRoute

    ViewerRoute --> GatekeeperService
    GatekeeperService --> WatermarkEngine
    DocumentViewer --> TelemetryAPI

    TelemetryAPI --> PostgresDB
    GatekeeperService --> PostgresDB

    AppRouterLayer --> TriggerWorker
    TriggerWorker --> PageRasterizer
    TriggerWorker --> AiSummarizer
    TriggerWorker --> NotificationDispatcher

    PageRasterizer --> BlobStore
    WatermarkEngine --> BlobStore
    AppRouterLayer <--> PostgresDB
```

### Telemetry & Ingestion Lifecycle
1. **Document Upload & Page Splitting**: When an author uploads a PDF or pitch deck, `Trigger.dev` kicks off an async worker that splits the document into individual high-definition images (`DocumentPage`) stored in S3/R2.
2. **Access Evaluation**: A visitor accesses a sharing link (`Link`). `GatekeeperService` validates expiration dates, password challenges, domain whitelists, and email verification.
3. **Dynamic Watermarking**: If watermarking is enabled, the viewer's identity (e.g., `viewer@investor.com`, IP, and UTC timestamp) is dynamically rendered over the page layers.
4. **Heartbeat & View Telemetry**: As the reader browses slides, client-side event listeners track viewport visibility, scroll position, and dwell time. Periodic beacon requests stream to `/api/view/track`.
5. **Analytics Aggregation**: dWell time per slide is persisted in `View` and `DocumentPage` records, aggregating deck drop-off curves, average reading duration, and triggering real-time email alerts to the document owner.

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class DocumentService {
        +uploadDocument(file) Document
        +createSecureLink(docId, options) Link
    }

    class PageViewTracker {
        +recordView(linkId, pageNumber, duration) View
    }

    DocumentService --> PageViewTracker : tracks
```

---

## 3. Technology Stack & Key Dependencies

| Component | Framework / Library | Role & Details |
|---|---|---|
| **Fullstack Framework** | Next.js 14/15, React 18 | App Router architecture, Server Components, and API routes |
| **Language & Tooling** | TypeScript 5.x, pnpm, Tailwind CSS | Type-safe development with modern utility-first responsive styling |
| **Database & ORM** | PostgreSQL, Prisma ORM | Relational schema partitioned across modular `.prisma` schema files |
| **Async Background Tasks**| Trigger.dev (`@trigger.dev/sdk`) | Cloud-native, durable background job execution for rasterization and alerts |
| **Object Storage** | AWS S3 / Cloudflare R2 / Vercel Blob | High-capacity storage for original PDFs, split page assets, and avatars |
| **Authentication & SSO** | NextAuth.js, BoxyHQ Jackson | Multi-tenant auth, magic links, Google/GitHub OAuth, and Enterprise SAML/SSO |
| **PDF Processing** | `pdfjs-dist`, `canvas`, `@pdf-lib` | Server-side PDF extraction, page count determination, and SVG rendering |
| **UI Components** | Radix UI, Framer Motion, Lucide | Accessible primitive components with fluid page-turn transitions |
| **Analytics & Charts** | Recharts, Tremor | Interactive slide-by-slide dwell time graphs and drop-off visualization |

---

## 4. Repository Layout & Key Modules

```text
inspire/papermark/
├── app/
│   ├── (auth)/                      # Authentication routes (login, register, sso)
│   ├── (dashboard)/                 # Owner dashboard: documents, datarooms, analytics
│   ├── (viewer)/view/[id]/          # Public/gated interactive document viewing surface
│   └── api/                         # REST API endpoints (links, documents, telemetry, webhooks)
├── prisma/
│   └── schema/                      # Modular multi-file Prisma schema architecture
│       ├── document.prisma          # Document, DocumentVersion, DocumentPage models
│       ├── link.prisma              # Link, LinkPreset, VisitorGroup models
│       ├── dataroom.prisma          # Dataroom, DataroomDocument, PermissionGroup
│       ├── conversation.prisma      # Dataroom Q&A, Message, ConversationView
│       ├── annotation.prisma        # DocumentAnnotation and markup layers
│       ├── redaction.prisma         # DocumentRedactionJob and redactions
│       └── schema.prisma            # Core User, Account, View, and Viewer models
├── lib/
│   ├── auth/                        # NextAuth session helpers and API token verification
│   ├── jobs/                        # Trigger.dev background task runners and schedulers
│   ├── pdf/                         # PDF page rasterization, dimension parsing, and watermarking
│   └── telemetry/                   # Dwell time aggregation and analytics calculations
├── components/
│   ├── viewer/                      # Document viewer canvas, zoom controls, thumbnail ribbon
│   └── analytics/                   # Page dwell time histograms and audience geo charts
└── trigger.config.ts                # Trigger.dev orchestrator configuration
```

---

## 5. Core Features & Capabilities

### 5.1 Slide-by-Slide Dwell Time & Drop-Off Telemetry
Papermark instruments document reading sessions with precision timing. Every page transition dispatches timing telemetry, recording exact dwell milliseconds spent on each slide. The dashboard aggregates these metrics into slide drop-off curves, allowing founders and sales reps to determine which pricing slides or product features captivate prospects and where attention wanes.

### 5.2 Dynamic Client & Server-Side Watermarking
To prevent unauthorized distribution of proprietary pitch decks, Papermark injects dynamic watermarks directly into the viewer stream. The watermark renders the viewer's authenticated email, IP address, and viewing timestamp diagonally across every slide. If an unauthorized viewer captures a screenshot or print-screen, their identity is irreversibly watermarked into the leak.

### 5.3 Granular Access Gating & Lead Capture
Links (`Link`) can be customized with strict gatekeeping policies:
- **Email Capture & Verification**: Requires visitors to submit their email and verify with a one-time passcode before unlocking the document.
- **Passcode Protection**: Enforces cryptographic passcode verification.
- **Domain Whitelisting**: Restricts access exclusively to corporate domains (e.g., only `@sequoiacap.com` or `@a16z.com`).
- **Click-Wrap NDA Agreements**: Mandates acceptance of a binding Non-Disclosure Agreement prior to viewing.

### 5.4 Virtual Data Rooms (VDR) & Due Diligence Hubs
Beyond single documents, Papermark provides Virtual Data Rooms (`Dataroom`) for complex enterprise transactions, M&A due diligence, and capital raises. VDRs feature folder hierarchies, granular permission groups (`PermissionGroup`), viewer groups, integrated Q&A message boards (`DataroomQuestion`), and task progress tracking.

### 5.5 Document Annotations, Redactions & AI Summaries
Teams can collaborate directly within documents using point-and-click annotations (`DocumentAnnotation`). Confidential figures or trade secrets can be permanently excised via `DocumentRedactionJob`, while integrated LLMs generate instant executive summaries and answer reader questions through an AI pitch assistant.

---

## 6. Data Models & Schema Architecture

Papermark organizes its relational persistence across modular Prisma schema domains, cleanly separating documents, sharing links, telemetry views, and data rooms.

```mermaid
erDiagram
    User ||--o{ Document : "owns"
    User ||--o{ Dataroom : "owns"
    
    Document ||--o{ DocumentVersion : "versions"
    DocumentVersion ||--|{ DocumentPage : "splits into"
    Document ||--o{ Link : "shared via"

    Link ||--o{ View : "records"
    Viewer ||--o{ View : "initiates"
    View ||--o{ DocumentPage : "measures dwell on"

    Dataroom ||--o{ DataroomDocument : "organizes"
    Dataroom ||--o{ PermissionGroup : "controls"
    PermissionGroup ||--o{ ViewerGroup : "authorizes"
```

### Core Schema Entity Definitions

#### `Document` & `DocumentPage` (`document.prisma`)
- `id` (`String`, PK): Unique document identifier.
- `name` (`String`): Title of the document or deck.
- `url` (`String`): S3 link to original master PDF.
- `totalPages` (`Int`): Number of pages in the document.
- `teamId` (`String`, FK), `ownerId` (`String`, FK).
- `DocumentPage`:
  - `id` (`String`, PK), `documentVersionId` (`String`, FK).
  - `pageNumber` (`Int`): 1-based page index.
  - `imageUrl` (`String`): URL of the rasterized WebP page image.
  - `aspectRatio` (`Float`): Width-to-height ratio for responsive scaling.

#### `Link` (`link.prisma`)
- `id` (`String`, PK): Link identifier.
- `documentId` (`String`, FK): Target document.
- `slug` (`String`, unique): Public access URL path (e.g., `/view/pitch-2026`).
- `password` (`String`, nullable): Hashed access password.
- `requireEmail` (`Boolean`): Requires email submission to unlock.
- `emailValidation` (`Boolean`): Requires OTP email verification.
- `allowDownload` (`Boolean`): Enables or disables raw PDF download.
- `watermark` (`Boolean`): Overlays viewer identity watermarks.
- `domainRestriction` (`String`, nullable): Allowed email domain pattern.
- `expiresAt` (`DateTime`, nullable): Automatic link expiration date.

#### `View` & `Viewer` (`schema.prisma`)
- `id` (`String`, PK): Unique view session ID.
- `linkId` (`String`, FK): Source link accessed.
- `viewerId` (`String`, FK, nullable): Identified viewer entity.
- `totalDuration` (`Int`): Total seconds spent reading.
- `completed` (`Boolean`): Whether reader reached the final slide.
- `country` (`String`), `city` (`String`): GeoIP location data.
- `browser` (`String`), `os` (`String`), `device` (`String`): User-agent details.
- `pageTimes` (`Json`): Key-value map of `{ [pageNumber]: dwellSeconds }`.

#### `Dataroom` (`dataroom.prisma`)
- `id` (`String`, PK): Data room ID.
- `name` (`String`): Name of due diligence room.
- `status` (`Enum`): `ACTIVE`, `ARCHIVED`, `DRAFT`.
- `watermarkText` (`String`, nullable): Custom static or dynamic watermark template.

---

### 6.1 Comprehensive Database Schema Specification

Papermark stores pitch decks, links, and viewer analytics with Prisma on PostgreSQL.

#### Table: `Link`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Unique link slug |
| `documentId` | `TEXT` | `NOT NULL` | - | Target pitch deck |
| `isPasswordProtected`| `BOOLEAN` | `NOT NULL` | `false` | Security flag |
| `viewsCount` | `INTEGER` | `NOT NULL` | `0` | Total view counter |

---

## 7. API Architecture, Webhooks & Key Endpoints

Papermark exposes RESTful JSON endpoints for programmatic document management, link generation, and live telemetry ingestion.

### Key API Endpoints

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `POST` | `/api/documents` | Upload new document and initiate background rasterization | Bearer Token / Session |
| `POST` | `/api/links` | Create a customized, instrumented tracking link | Bearer Token / Session |
| `GET` | `/api/documents/:id/analytics` | Fetch aggregate deck metrics and slide dwell times | Bearer Token / Session |
| `POST` | `/api/view/verify` | Verify email OTP or password to unlock a gated link | Public |
| `POST` | `/api/view/track` | Stream live page dwell duration and viewing heartbeats | Public (Session Token) |
| `GET` | `/api/datarooms/:id` | Retrieve data room hierarchy, documents, and permissions | Bearer Token / Session |

### API Payload Example: Create Tracked Document Link
```http
POST /api/links HTTP/1.1
Host: papermark.internal.net
Authorization: Bearer pm_live_90af2b831c9e47d
Content-Type: application/json

{
  "documentId": "doc_89a1c4b2-e14",
  "name": "Sequoia Capital Seed Pitch",
  "requireEmail": true,
  "emailValidation": true,
  "allowDownload": false,
  "watermark": true,
  "domainRestriction": "sequoiacap.com",
  "notifyOnView": true
}
```

### Outbound Webhook Event: `document.viewed`
```json
{
  "event": "document.viewed",
  "timestamp": "2026-10-24T12:12:30Z",
  "data": {
    "documentId": "doc_89a1c4b2-e14",
    "documentName": "Series_A_Pitch_Deck.pdf",
    "linkSlug": "sequoia-seed-2026",
    "viewer": {
      "email": "partner@sequoiacap.com",
      "ip": "136.24.112.98",
      "geo": {
        "city": "Menlo Park",
        "country": "US"
      }
    },
    "metrics": {
      "totalDurationSeconds": 412,
      "pagesViewed": 14,
      "totalPages": 15,
      "topDwellPages": [
        { "page": 8, "title": "Unit Economics", "seconds": 128 },
        { "page": 12, "title": "Competitive Moat", "seconds": 94 }
      ]
    }
  }
}
```

---

### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Founder as Founder
    participant App as Papermark
    actor Investor as Investor

    Founder->>App: Uploads deck & generates link
    Investor->>App: Opens deck link
    App->>App: Track page viewing time (Page 1: 45s, Page 2: 120s)
    App-->>Founder: Real-time notification: Investor viewing slide 2
```

---

## 8. Strategic Value & Takeaways for MarketingStudio

### Reusable Architectural Patterns
1. **Slide-by-Slide Dwell Time Telemetry Beacon**: Papermark's beacon protocol (buffering page viewing durations and streaming heartbeat beacons via `navigator.sendBeacon`) provides MarketingStudio with the exact technical blueprint for tracking sales collateral and marketing proposals.
2. **Dynamic Leak-Proof Watermarking**: Overlapping dynamically rendered canvas watermarks with viewer emails, IP addresses, and timestamps provides high-trust sales teams with an ironclad defense against leaked marketing strategies and confidential pricing.
3. **Multi-Domain Prisma Schema Organization**: Partitioning massive schemas across domain-specific files (`dataroom.prisma`, `link.prisma`, `document.prisma`) avoids monolithic schema bloat and enables rapid modular scaling.

### Convergence Blueprint for MarketingStudio
- **Sales Proposal & Deck Instrumentation**: Embed Papermark's viewer engine directly into MarketingStudio's outbound SDR and AE sequences, notifying reps the exact second a prospect opens a proposal or dwells on pricing.
- **Automated Investor & Client Data Rooms**: Equip MarketingStudio agency clients with self-branded Virtual Data Rooms to share campaign performance decks, brand guidelines, and confidential creative assets securely.
- **Intent-Driven Deal Scoring**: Feed slide dwell metrics directly into MarketingStudio's CRM lead scoring algorithm — automatically promoting leads to "High Intent" when they spend over 90 seconds reviewing pricing or service tier slides.
