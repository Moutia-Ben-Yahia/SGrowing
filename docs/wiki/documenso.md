# documenso - Architecture & Technical Wiki

> **Open-Source Enterprise Document Signing Infrastructure, Cryptographic PDF Sealing & Multi-Tenant Envelope Orchestration Engine**  
> GitHub: [documenso/documenso](https://github.com/documenso/documenso.git) | Stack: TypeScript, React 18, React Router v7 (Remix), Hono, PostgreSQL 15, Prisma ORM, Kysely, Inngest, Google Cloud KMS / Local P12, @libpdf/core, Tailwind CSS | License: AGPL-3.0

---

## 1. Executive Summary & Value Proposition

**documenso** is the leading open-source electronic signature platform built to serve as an extensible, transparent, and legally compliant alternative to proprietary e-sign vendors such as DocuSign, Adobe Sign, and PandaDoc. Architected as a modern TypeScript monorepo using Turborepo and npm workspaces, Documenso provides developers and organizations with total programmatic control over their document workflows, field placements, audit trails, and cryptographic key infrastructure.

Modern sales, legal, and revenue teams face predatory pricing, per-envelope API metering, vendor lock-in, and sovereignty risks when handling legally binding contracts, NDAs, and customer agreements through closed-source clouds. Documenso eliminates these barriers by providing self-hostable signing infrastructure supporting legally binding Electronic Signatures (SES), Advanced Electronic Signatures (AES), and Qualified Electronic Signatures (QES via Cloud Signature Consortium CSC standards) with zero data leakage.

### Core Problems Solved
- **Predatory Per-Envelope & Per-Seat API Pricing**: Replaces arbitrary billing barriers with unlimited self-hosted document signing, field templating, and webhook triggers.
- **Data Sovereignty & Legal Compliance**: Retains all raw PDF binaries, signed artifacts, and PII within internal Postgres databases or private S3 buckets in compliance with GDPR, HIPAA, and eIDAS.
- **Cryptographic Tamper-Evidence**: Implements digital PDF signatures using PKCS#7/CMS containers, SHA-256 byte-range hashes, local PKCS#12 certificates or hardware security modules (Google Cloud KMS HSM), and RFC 3161 Time-Stamp Authorities (TSA).
- **Extensible Multi-Tenant Organizations**: Supports hierarchical enterprise tenancy with organizations, teams, custom email branding, and unified API tokens.
- **Flexible Workflow Integration**: Provides both contract-based REST (API v1 via ts-rest) and typed action-based RPC (API v2 via tRPC and OpenAPI) for headless embedding into CRMs and client portals.

---

## 2. High-Level System Architecture

Documenso pairs a React Router v7 (Remix) web interface and headless Hono API server with swappable asynchronous job workers, storage engines, and cryptographic signing providers.

```mermaid
flowchart TD
    subgraph Clients ["Client Applications & Ingress"]
        WebUser["Web Browser\n(React 18 / Remix Frontend)"]
        EmbeddedIframe["Embedded Signing Widget\n(External Portal / Iframe)"]
        RestApiClient["External Systems / CRM\n(REST API v1 / OpenAPI v2)"]
    end

    subgraph AppServer ["Application Server (apps/remix - Hono Engine)"]
        HonoRouter["Hono Server Gateway\n(Port 3000)"]
        TrpcRouter["tRPC Router & OpenAPI\n(packages/trpc)"]
        TsRestRouter["ts-rest V1 Router\n(packages/api)"]
        AuthModule["Auth & Passkeys\n(Arctic / WebAuthn)"]
    end

    subgraph CoreDomain ["Core Domain Services (packages/lib)"]
        DocManager["Document & Envelope Manager"]
        FieldEngine["Field Layout & Coordinate Engine"]
        AuditRecorder["DocumentAuditLog Service"]
        JobDispatcher["Job Trigger Engine\n(Inngest or Local DB Queue)"]
    end

    subgraph SigningEngine ["Cryptographic Signing Subsystem (packages/signing)"]
        PdfParser["PDF Coordinate & Parser\n(@libpdf/core, pdfjs-dist)"]
        LocalSigner["Local PKCS#12 Signer\n(OpenSSL .p12 Keys)"]
        HsmSigner["Google Cloud KMS HSM\n(Hardware Key Protection)"]
        TsaClient["RFC 3161 TSA Service\n(Trusted Timestamping)"]
        CertAppender["Audit Trail PDF Generator"]
    end

    subgraph DataTier ["Data Persistence & Storage"]
        PostgresDB[("PostgreSQL 15 Database\n(Prisma ORM + Kysely)")]
        StorageProvider["Storage Provider\n(S3 Bucket / CloudFront / Base64 DB)"]
    end

    WebUser --> HonoRouter
    EmbeddedIframe --> HonoRouter
    RestApiClient --> HonoRouter

    HonoRouter --> TrpcRouter
    HonoRouter --> TsRestRouter
    HonoRouter --> AuthModule

    TrpcRouter --> DocManager
    TsRestRouter --> DocManager
    DocManager --> FieldEngine
    DocManager --> AuditRecorder
    DocManager --> JobDispatcher

    JobDispatcher --> SigningEngine
    SigningEngine --> PdfParser
    PdfParser --> LocalSigner
    PdfParser --> HsmSigner
    SigningEngine --> TsaClient
    SigningEngine --> CertAppender

    DocManager <--> PostgresDB
    AuditRecorder --> PostgresDB
    DocManager <--> StorageProvider
    CertAppender --> StorageProvider
```

### Architectural Lifecycle of an Envelope
1. **Creation & Templating**: A user or API client uploads a source PDF. `StorageProvider` saves the master file, while `DocumentManager` creates an `Envelope` record and extracts page geometry.
2. **Field Coordinate Mapping**: Signature, text, date, and checkbox fields are attached to specific `Recipient` records with normalized $(X, Y, \text{Width}, \text{Height})$ viewport coordinates.
3. **Invitation & Signing Phase**: Recipients receive tokenized magic links. When viewing, they render the PDF via `pdfjs-dist`. Upon signing, recipient inputs, IP addresses, and user-agent fingerprints are immutably logged to `DocumentAuditLog`.
4. **Sealing & Cryptographic Signing**: Once all signers complete their steps, `internal.seal-document` triggers. The PDF engine renders visual signature widgets into a final PDF layer, appends a generated Certificate of Completion, injects a PKCS#7 digital signature container signed via local `.p12` or Cloud HSM, queries an RFC 3161 TSA server, and seals the document.

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class DocumentService {
        +createDocument(file, title) Document
        +addRecipient(docId, email) Recipient
        +sendForSignature(docId) Void
    }

    class SignatureEngine {
        +applySignature(docId, fieldId, signatureData) Void
        +cryptographicallySignPdf(docId) Buffer
    }

    class Document {
        +String id
        +String title
        +String status
        +List~Recipient~ recipients
    }

    DocumentService --> Document : manages
    DocumentService --> SignatureEngine : signs
```

---

## 3. Technology Stack & Key Dependencies

| Component | Framework / Tool | Architectural Role |
|---|---|---|
| **Runtime & Server** | Node.js (v18+ / v20), Hono | High-efficiency micro-framework running the Remix application runtime |
| **Frontend Framework** | React 18, React Router v7 (Remix) | Server-rendered pages with optimistic client navigation and hydration |
| **Styling & Components** | Tailwind CSS, Radix UI, Lucide, Shadcn UI | Accessible, headless UI design system with consistent typography |
| **Database & ORM** | PostgreSQL 15, Prisma ORM, Kysely | Relational persistence with type-safe query generation and complex SQL joins |
| **API Abstractions** | tRPC, ts-rest, OpenAPI / Swagger | Unified type-sharing between frontend client and headless API consumers |
| **Authentication** | Arctic (OAuth), SimpleWebAuthn (Passkeys) | Secure session management, enterprise SSO, and biometric device authentication |
| **PDF Manipulation** | `@libpdf/core`, `pdfjs-dist` | Coordinate calculation, PDF page flattening, and visual signature placement |
| **Cryptographic Signer** | `@documenso/signing`, Node crypto, Google KMS | Digital certificate hashing, PKCS#12 signing, hardware HSM support |
| **Async Queues** | Inngest / Local Database Queue | Durable background execution for PDF sealing, email alerts, and webhooks |
| **Email Delivery** | React Email, Nodemailer | Templated transactional notifications for signing requests and completions |

---

## 4. Repository Layout & Key Modules

```text
inspire/documenso/
├── apps/
│   ├── remix/                       # Core Remix/Hono application (UI + API routes)
│   ├── openpage-api/                # Public telemetry and analytics microservice
│   └── docs/                        # Mintlify documentation workspace
├── packages/
│   ├── api/                         # Contract-based REST API v1 (ts-rest)
│   ├── trpc/                        # Action-based API v2 (tRPC + OpenAPI endpoints)
│   │   └── server/                  # Routers: document, template, envelope, recipient, field
│   ├── signing/                     # Cryptographic PDF signing engine
│   │   ├── transports/              # local.ts (.p12), google-cloud.ts (Cloud KMS)
│   │   └── helpers/                 # tsa.ts (RFC 3161 Time-Stamp Authority client)
│   ├── lib/                         # Core domain logic
│   │   ├── universal/               # Shared validators, upload helpers, formatting
│   │   ├── server-only/             # Server business operations (seal, audit, auth)
│   │   └── jobs/                    # Inngest/Local job definitions & workers
│   ├── prisma/                      # Database schema, Kysely client, migrations
│   │   └── schema.prisma            # Canonical data models for envelopes and signatures
│   ├── auth/                        # Arctic OAuth adapters, WebAuthn passkey registration
│   ├── ui/                          # Shared React design system (Shadcn UI components)
│   └── email/                       # React Email transactional templates and mailers
```

---

## 5. Core Features & Capabilities

### 5.1 Multi-Recipient Envelope Lifecycle & Routing
Documenso organizes signing flows through `Envelope` entities containing one or more documents. Envelopes support structured signing sequences (e.g., Signer 1 must complete before Signer 2 receives an invitation) or simultaneous unconstrained signing. Signer roles include `SIGNER`, `APPROVER`, `VIEWER`, and `CC`.

### 5.2 Cryptographic PDF Digital Signatures & Cloud HSM
Unlike simple image stamping, Documenso applies real digital signatures in accordance with PDF ISO 32000-1 specifications:
- **Byte-Range Hashing**: The document bytes outside the signature container are hashed via SHA-256.
- **PKCS#7 Container**: The hash is encrypted with a private key and embedded into the PDF dictionary.
- **Hardware Protection**: In addition to local `.p12` certificates, Documenso integrates with **Google Cloud KMS Cloud HSM**, ensuring private signing keys never reside in volatile server memory.

### 5.3 Tamper-Evident Audit Trails & Certificates of Completion
Every document completion automatically appends a legally enforceable Certificate of Completion summarizing:
- Unique Document GUID and SHA-256 file checksums (pre-signed and post-signed).
- Detailed signer timeline: Sent, Viewed, Consented, and Signed timestamps (UTC).
- Signer verification metadata: Verified email, IP address, geolocation, and user-agent device string.

### 5.4 Advanced Form Field Types & Dynamic Pre-Filling
Documenso supports dynamic form field placement across document pages:
- **Field Types**: Signature, Initials, Free Text, Date, Checkbox, Radio, and Dropdown.
- **Pre-filling**: Fields can be programmatically populated via API during envelope generation before reaching the signer.

### 5.5 Multi-Tenant Organizations, Teams & White-Labeling
Enterprises can partition users into distinct `Organisation` and `Team` workspaces with role-based access control (`ADMIN`, `MANAGER`, `MEMBER`). Custom domains, white-labeled email sender configurations, and branded signing portals are natively supported.

---

## 6. Data Models & Schema Architecture

Documenso's relational architecture is defined in Prisma ORM, modeling users, multi-tenant organizations, envelopes, documents, recipients, and cryptographic audit trails.

```mermaid
erDiagram
    Organisation ||--o{ Team : "contains"
    Organisation ||--o{ User : "members"
    Team ||--o{ Envelope : "owns"
    User ||--o{ Envelope : "creates"

    Envelope ||--|{ EnvelopeItem : "groups"
    Envelope ||--|{ Recipient : "assigned to"
    Envelope ||--o{ DocumentAuditLog : "tracks"
    
    EnvelopeItem ||--|| DocumentData : "stores binary"
    EnvelopeItem ||--|| DocumentMeta : "has metadata"
    
    Recipient ||--o{ Field : "fills"
    Field ||--o| Signature : "contains"
    Recipient ||--o{ Signature : "signs"
```

### Core Schema Entity Definitions

#### `Envelope` (`Envelope`)
- `id` (`varchar(36)`, PK): Unique UUID identifying the envelope.
- `title` (`varchar(255)`): Envelope name or subject line.
- `status` (`enum`): `DRAFT`, `PENDING`, `COMPLETED`, `REJECTED`, `CANCELLED`.
- `teamId` (`varchar(36)`, FK): Reference to owning `Team`.
- `userId` (`varchar(36)`, FK): Creator `User` ID.
- `createdAt` (`timestamp`), `completedAt` (`timestamp`, nullable).

#### `EnvelopeItem` (`EnvelopeItem`) & `DocumentData`
- `id` (`varchar(36)`, PK): Identifier for individual document inside envelope.
- `envelopeId` (`varchar(36)`, FK): Parent `Envelope`.
- `documentDataId` (`varchar(36)`, FK): Foreign key to raw storage reference.
- `type` (`enum`): Document file format (e.g., `PDF`).
- `DocumentData.data`: Storage URL (S3 URI) or direct Base64-encoded PDF binary.

#### `Recipient` (`Recipient`)
- `id` (`varchar(36)`, PK): Unique recipient identifier.
- `envelopeId` (`varchar(36)`, FK): Reference to parent envelope.
- `email` (`varchar(255)`): Signer's email address.
- `name` (`varchar(255)`): Signer's full name.
- `role` (`enum`): `SIGNER`, `APPROVER`, `VIEWER`, `CC`.
- `signingOrder` (`integer`): Execution step order in sequential workflows.
- `token` (`varchar(64)`, unique): Secure magic token used to authenticate recipient signing sessions.

#### `Field` (`Field`) & `Signature`
- `id` (`varchar(36)`, PK): Field identifier.
- `recipientId` (`varchar(36)`, FK): Recipient assigned to complete this field.
- `type` (`enum`): `SIGNATURE`, `INITIALS`, `NAME`, `EMAIL`, `DATE`, `TEXT`, `CHECKBOX`.
- `page` (`integer`): 1-based PDF page index.
- `positionX` (`float`), `positionY` (`float`): Normalized percentage coordinate on page.
- `width` (`float`), `height` (`float`): Normalized dimensions.
- `customText` (`text`, nullable): Pre-filled or user-entered value.

#### `DocumentAuditLog` (`DocumentAuditLog`)
- `id` (`varchar(36)`, PK): Audit log entry ID.
- `envelopeId` (`varchar(36)`, FK): Associated envelope.
- `type` (`enum`): `DOCUMENT_CREATED`, `DOCUMENT_SENT`, `DOCUMENT_VIEWED`, `DOCUMENT_SIGNED`, `DOCUMENT_SEALED`.
- `ipAddress` (`varchar(45)`): IPv4 or IPv6 of the actor.
- `userAgent` (`text`): Browser user agent string.
- `createdAt` (`timestamp`): Immutable log timestamp.

---

### 6.1 Comprehensive Database Schema Specification

Documenso runs on PostgreSQL with Prisma ORM for e-signature tracking.

#### Table: `Document`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Document ID |
| `title` | `TEXT` | `NOT NULL` | - | Document title |
| `status` | `TEXT` | `NOT NULL` | `'DRAFT'` | State (`DRAFT`, `PENDING`, `COMPLETED`) |
| `documentDataId`| `TEXT` | `NOT NULL` | - | S3 file reference |

---

## 7. API Architecture, Webhooks & Key Endpoints

Documenso exposes a RESTful API V2 mounted at `/api/v2/*` generated via tRPC and OpenAPI specifications.

### Key API Endpoints

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `POST` | `/api/v2/document/create` | Upload and initialize a new document draft | API Token |
| `POST` | `/api/v2/envelope/create` | Create an envelope container for multiple documents | API Token |
| `POST` | `/api/v2/recipient/create` | Add signers, approvers, or CC recipients | API Token |
| `POST` | `/api/v2/field/create` | Add signature or text fields to a document | API Token |
| `POST` | `/api/v2/envelope/send` | Distribute envelope invitations to signers | API Token |
| `GET` | `/api/v2/envelope/:id` | Fetch envelope status, recipients, and signed file URLs | API Token |
| `POST` | `/api/v2/envelope/resend` | Resend reminder notifications to pending signers | API Token |

### API Payload Example: Create and Send Envelope
```http
POST /api/v2/envelope/create HTTP/1.1
Host: documenso.internal.net
Authorization: Bearer doc_live_79a2fc44b82d31e59c
Content-Type: application/json

{
  "title": "Enterprise Master Service Agreement - Cyberdyne",
  "documents": [
    {
      "title": "MSA_2026.pdf",
      "data": "https://storage.internal.net/raw-docs/msa_template.pdf"
    }
  ],
  "recipients": [
    {
      "name": "Sarah Connor",
      "email": "sarah.connor@cyberdyne.io",
      "role": "SIGNER",
      "signingOrder": 1,
      "fields": [
        {
          "type": "SIGNATURE",
          "page": 4,
          "positionX": 15.5,
          "positionY": 72.0,
          "width": 30.0,
          "height": 8.0
        },
        {
          "type": "DATE",
          "page": 4,
          "positionX": 55.0,
          "positionY": 72.0,
          "width": 20.0,
          "height": 5.0
        }
      ]
    }
  ]
}
```

### Outbound Webhook Event: `envelope.completed`
```json
{
  "event": "envelope.completed",
  "data": {
    "envelopeId": "env_892a-4bc1-9921",
    "title": "Enterprise Master Service Agreement - Cyberdyne",
    "completedAt": "2026-10-24T12:04:11.230Z",
    "downloadUrl": "https://documenso.internal.net/api/v2/envelope/env_892a/download",
    "certificateUrl": "https://documenso.internal.net/api/v2/envelope/env_892a/certificate",
    "sha256Digest": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
  }
}
```

---

### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Sender as Document Creator
    participant App as Documenso App
    participant Engine as SignatureEngine
    actor Signer as Signer

    Sender->>App: Uploads PDF & places signature box
    App->>Signer: Emails invitation link
    Signer->>App: Opens doc & draws signature
    App->>Engine: cryptographicallySignPdf(docId)
    Engine-->>App: Signed PDF with audit hash
    App-->>Sender: Both parties notified with completed PDF
```

---

## 8. Strategic Value & Takeaways for MarketingStudio

### Reusable Architectural Patterns
1. **Normalized Viewport Percentage Coordinates**: Documenso positions fields using relative percentages $(0-100\%)$ rather than absolute points or pixels. MarketingStudio should adopt this coordinate system across its PDF and document editors so templates render consistently regardless of viewport zoom, mobile DPI, or screen size.
2. **Pluggable HSM / KMS Signing Abstraction**: Decoupling the cryptographic signature generation (`@documenso/signing`) from the web framework enables MarketingStudio to offer self-hosted software signing for staging while switching to FIPS 140-2 Level 3 HSM keys (AWS KMS / Cloud KMS) for enterprise production contracts.
3. **Immutable Document Audit Trail**: Persisting every document event (`VIEWED`, `CONSENTED`, `SIGNED`) into a dedicated audit entity with client IP and browser hash provides undisputed legal non-repudiation.

### Convergence Blueprint for MarketingStudio
- **Native Contract & Proposal Closing**: Embed Documenso's envelope generation and signing canvas directly into MarketingStudio's CRM pipeline, converting qualified deals into executed agreements without leaving the CRM.
- **Automated Sales Proposal Generation**: Wire n8n workflow triggers to dynamically assemble proposals, inject client metadata into Documenso template fields, and dispatch signing links to decision-makers.
- **Unified Deal Telemetry**: Combine Documenso's signing status webhooks with Papermark's document viewing analytics to provide sales reps with real-time alerts when prospects review contract clauses.
