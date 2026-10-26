---
title: "TikTak PRO Architectural and Functional Wiki"
sidebar:
  hidden: true
aiGenerated: true
---

# TikTak PRO System and Functional Architecture

> Comprehensive technical and functional reverse-engineered reference for **TikTak PRO** (`tiktakpro.com`): the complete Tunisian e-commerce platform, integrated call center CRM ("TikTak Caller"), SMS automation suite, and multi-warehouse inventory engine.

---

## 1. Executive Overview and Market Positioning

**TikTak PRO** is an all-in-one e-commerce management ecosystem created to give online retailers in **Tunisia and Francophone Africa** an enterprise-grade backoffice without requiring dozens of fragmented plugins.

While traditional platforms focus solely on the digital storefront, the operational bottleneck in African Cash-on-Delivery (COD) e-commerce lies in **order validation and inventory reconciliation**:
- Over 40% of orders placed online are either impulsive, duplicate, or have invalid addresses. Without a swift, structured phone call or SMS validation workflow, merchants lose enormous sums on failed shipments.
- Call centers face operational chaos: agents accidentally call the same customer at once, stock is sold out before confirmation occurs, and returns from couriers sit in warehouses without being returned to active inventory.

TikTak PRO solves these challenges by combining:
1. **A Visual Drag-and-Drop Storefront Builder** with over 50 mobile-first conversion themes.
2. **"TikTak Caller"**: A purpose-built Call Center CRM featuring intelligent call queues, real-time server-side agent locking, and automated screen pops.
3. **Automated SMS & Messaging Gateways** (integrated with **WinSMS**) for automated 1-click customer confirmations and automated follow-up sequences for unreachable leads.
4. **Resilient Multi-Stock & Warehouse Management**: Real-time stock reservation at checkout, status-driven inventory quarantines, and automatic restock upon courier parcel returns (*retour*).
5. **Turnkey Logistics Integrations**: Direct API connectors with Tunisian couriers (Aramex, Livri, Express Courrier, First Delivery) for bulk Airway Bill (AWB) generation and automated COD cash remittance tracking.

```
┌───────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                      TikTak PRO Platform Core                                     │
│          Unified E-Commerce ERP, "TikTak Caller" CRM, SMS Automation & Logistics OS               │
└──────────────────────────────────────────────────┬────────────────────────────────────────────────┘
                                                   │
         ┌─────────────────────────────────────────┼────────────────────────────────────────┐
         ▼                                         ▼                                        ▼
┌─────────────────────────────┐       ┌─────────────────────────────┐      ┌─────────────────────────────┐
│    Storefront & Theming     │       │   TikTak Caller CRM &       │      │   Multi-Warehouse Stock     │
│   (50+ Localized Templates) │       │   SMS Automation (WinSMS)   │      │   & Reservation Engine      │
│  - Drag & drop page builder │       │  - Anti-collision agent lock│      │  - Instant stock reservation│
│  - Mobile conversion layout │       │  - Smart call queue pop-up  │      │  - Quarantine during confirm│
│  - Fast localized checkout  │       │  - 1-click SMS confirm link │      │  - Auto-restock on return   │
└─────────────────────────────┘       └─────────────────────────────┘      └─────────────────────────────┘
         │                                         │                                        │
         └─────────────────────────────────────────┼────────────────────────────────────────┘
                                                   ▼
                                      ┌─────────────────────────────┐
                                      │  Tunisian Courier Hub &     │
                                      │  Financial Cash Ledger      │
                                      │ - Bulk AWB & Bordereaux     │
                                      │ - Live courier status sync  │
                                      │ - Driver remittance audit   │
                                      └─────────────────────────────┘
```

### Core Value Proposition & Market Differentiators
- **TikTak Caller Built Directly into the ERP:** Eliminates external CRM subscriptions. Confirmation agents have an integrated dialing interface with call logs, customer purchase history, and automated status transitions.
- **Server-Side Collision Prevention:** Guarantees that when an agent dials a lead, that lead is immediately locked across all other screens in the call center.
- **WinSMS Automated Confirmation Workflows:** Sends automated SMS confirmation messages with interactive validation links. If the buyer taps *"Confirm My Order"*, the status automatically shifts to `Confirmed`, eliminating manual phone calls.
- **Defensive Multi-Warehouse Inventory:** Prevents overselling by automatically holding stock in a reserved state upon checkout, releasing it if canceled, and re-injecting it into inventory the moment a courier marks a parcel as `Returned to Warehouse`.
- **Comprehensive Mobile Apps (iOS/Android):** Allows business owners to review live sales figures, listen to call recordings, approve shipments, and manage warehouses on the go.

---

## 2. Core Entities and Reverse-Engineered Data Model

### 2.1 Domain Entity Relationship Model

```
┌─────────────────────────────────────────────────────────────────┐
│                        Merchant Company                         │
│       (ID, Company Name, Tax ID, Phone, Currency: TND)          │
└───────────────────────────────┬─────────────────────────────────┘
                                │ 1:N
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                            Warehouse                            │
│           (ID, Warehouse Name, Location, Is Primary)            │
└───────────────┬───────────────────────────────┬─────────────────┘
                │ 1:N                           │ 1:N
                ▼                               ▼
┌───────────────────────────────┐   ┌─────────────────────────────┐
│       Stock Allocation        │   │        Store Instance       │
│  (Warehouse ID, Variant ID,   │   │  (Subdomain, Custom Domain, │
│   Physical Qty, Reserved Qty) │   │   Theme ID, Tracking Pixels)│
└───────────────┬───────────────┘   └─────────────┬───────────────┘
                │ Matches                         │ 1:N
                ▼                                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                           Order Record                          │
│  (Order #, Customer Name, Phone, Governorate, Subtotal, COD,    │
│   Stock State: Reserved/Committed/Released, Confirmation State) │
└───────────────┬───────────────────────────────┬─────────────────┘
                │ 1:N                           │ 1:N
                ▼                               ▼
┌───────────────────────────────┐   ┌─────────────────────────────┐
│          Call Session         │   │         SMS Dispatch        │
│  (Agent ID, Call Timestamp,   │   │  (Gateway: WinSMS, Template,│
│   Outcome, Recording File,    │   │   Confirmation Link Token,  │
│   Next Call Back Schedule)    │   │   Delivery Status, Clicked) │
└───────────────────────────────┘   └─────────────────────────────┘
```

### 2.2 Class Diagram Architecture

```mermaid
classDiagram
    class MerchantStore {
        +BigInt id
        +String companyName
        +List~Warehouse~ warehouses
        +List~CallCenterAgent~ agents
        +createOrder(orderData)
        +generateCarrierManifest(orderIds)
    }

    class Warehouse {
        +BigInt id
        +String name
        +String city
        +List~StockItem~ inventory
        +reserveStock(variantId, qty)
        +commitStock(variantId, qty)
        +releaseStock(variantId, qty)
        +restockReturnedParcel(awb)
    }

    class TikTakOrder {
        +BigInt id
        +String orderNumber
        +String customerPhone
        +TunisianGovernorate governorate
        +OrderStage stage
        +Decimal codPayable
        +BigInt lockedByAgentId
        +DateTime lockExpiresAt
        +acquireAgentLock(agentId)
        +recordCallOutcome(outcome, notes)
        +sendConfirmationSms()
    }

    class TikTakCallerService {
        +List~TikTakOrder~ pendingQueue
        +getNextLead(agentId)
        +extendLock(orderId, agentId)
        +releaseLock(orderId)
    }

    class WinSmsGateway {
        +String apiKey
        +String senderId
        +sendVerificationSms(phone, token)
        +sendDeliveryNotification(phone, awb, carrier)
    }

    MerchantStore "1" *-- "many" Warehouse : operates
    Warehouse "1" *-- "many" StockItem : contains
    MerchantStore "1" *-- "many" TikTakOrder : processes
    TikTakOrder --> TikTakCallerService : managed_by
    TikTakOrder --> WinSmsGateway : notified_by
```

### 2.3 Comprehensive Database Schema Specification

#### Table: `warehouses`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Warehouse ID |
| `store_id` | `BIGINT UNSIGNED` | `NOT NULL, INDEX` | - | Store reference |
| `name` | `VARCHAR(100)` | `NOT NULL` | - | Warehouse label (e.g., Dépôt Tunis Charguia) |
| `governorate` | `VARCHAR(50)` | `NOT NULL` | - | Geographical region |
| `is_default` | `BOOLEAN` | `NOT NULL` | `FALSE` | Primary fulfillment center |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `CURRENT_TIMESTAMP` | Creation date |

#### Table: `inventory_items`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Stock record ID |
| `warehouse_id` | `BIGINT UNSIGNED` | `NOT NULL, INDEX` | - | Warehouse reference |
| `product_variant_id`| `BIGINT UNSIGNED`| `NOT NULL, INDEX` | - | Variant reference |
| `physical_quantity` | `INT NOT NULL` | `CHECK (physical_quantity >= 0)` | `0` | Actual items in warehouse |
| `reserved_quantity` | `INT NOT NULL` | `CHECK (reserved_quantity >= 0)` | `0` | Items held in pending orders |
| `available_quantity`| `INT GENERATED ALWAYS AS (physical_quantity - reserved_quantity)` | `STORED` | - | Quantity available for sale |
| `reorder_threshold` | `INT NOT NULL` | `DEFAULT 5` | `5` | Low stock alert trigger level |

#### Table: `call_logs`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Call log ID |
| `order_id` | `BIGINT UNSIGNED` | `NOT NULL, INDEX` | - | Associated order |
| `agent_id` | `BIGINT UNSIGNED` | `NOT NULL, INDEX` | - | Call center agent user ID |
| `outcome` | `ENUM('confirmed','no_answer','call_back','cancelled','wrong_number')` | `NOT NULL` | - | Call status |
| `notes` | `TEXT` | `NULL` | `NULL` | Agent commentary and instructions |
| `call_back_at` | `TIMESTAMP` | `NULL` | `NULL` | Scheduled follow-up time |
| `call_duration_sec`| `INT UNSIGNED` | `NOT NULL` | `0` | Duration of phone call |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `CURRENT_TIMESTAMP` | Call timestamp |

---

## 3. Reverse-Engineered System Architecture & Engineering Blueprint

```mermaid
flowchart TD
    subgraph UserInterfaces ["Storefronts & Agent Workspaces"]
        CustomerStorefront["Shopper Web / Mobile PWA"]
        AgentDesk["TikTak Caller Desk (WebRTC / WebSockets)"]
        MerchantMobileApp["Merchant iOS & Android App (Flutter)"]
    end

    subgraph EdgeLayer ["Edge Infrastructure"]
        Cloudflare["Cloudflare Edge (WAF, SSL & Static Asset CDN)"]
    end

    subgraph CoreBackendStack ["TikTak PRO Core Monolith & Services"]
        APIGateway["API Controller Cluster (PHP 8.2 / Laravel 10)"]
        CheckoutModule["High-Performance COD Checkout Handler"]
        StockEngine["Real-Time Multi-Warehouse Stock Engine"]
        CallerEngine["TikTak Caller Queue & Concurrency Dispatcher"]
        LogisticsEngine["Tunisian Courier Gateway Router"]
    end

    subgraph RealTimeAndQueueMesh ["Broker & Concurrency Control"]
        RedisCluster["Redis Cluster (Queue, Caller Locks & Cache)"]
        ReverbWebSockets["Laravel Reverb (Live Queue Updates & Audio Ka-Ching)"]
    end

    subgraph ExternalServices ["External Telephony, SMS & Logistics"]
        WinSmsAPI["WinSMS Gateway API (SMS Outbound)"]
        CourierAramex["Aramex Tunisia Logistics API"]
        CourierLivri["Livri Express API"]
        MetaEvents["Meta Conversions API (CAPI)"]
    end

    subgraph StorageTier ["Persistence Layer"]
        MySQLPrimary[(MySQL 8 Primary Database)]
        MySQLReplica[(MySQL 8 Read Replica)]
        StorageS3[(S3 Compatible Storage - Audio Logs & AWBs)]
    end

    CustomerStorefront --> Cloudflare
    AgentDesk --> Cloudflare
    MerchantMobileApp --> Cloudflare

    Cloudflare --> APIGateway
    Cloudflare --> CheckoutModule

    CheckoutModule --> StockEngine
    CheckoutModule --> RedisCluster
    CheckoutModule --> ReverbWebSockets

    ReverbWebSockets -->|New Order Audio Pop| AgentDesk
    ReverbWebSockets -->|Instant Notification| MerchantMobileApp

    CallerEngine --> RedisCluster
    AgentDesk --> CallerEngine
    CallerEngine --> WinSmsAPI

    LogisticsEngine --> CourierAramex
    LogisticsEngine --> CourierLivri

    APIGateway --> MySQLPrimary
    APIGateway --> MySQLReplica
    AgentDesk --> StorageS3
```

### 3.1 Technology Stack & Operational Efficiency
- **Backend Architecture:** Laravel 10 on PHP 8.2 with Octane / Swoole high-concurrency worker threads, guaranteeing response times under 80ms for order placement and call center queue polling.
- **Real-Time Layer:** **Laravel Reverb WebSockets** handling sub-millisecond status pushes to confirmation agents, broadcasting state changes when orders are locked, confirmed, or rescheduled.
- **Stock Integrity:** Database transactions with pessimistic row locking (`SELECT ... FOR UPDATE`) during checkout to eliminate race conditions and negative inventory allocations during high-volume advertising spikes.

---

## 4. Key Operational Mechanics & Deep Technical Differentiators

### 4.1 "TikTak Caller" Smart Queuing & Concurrency Lock
1. Confirmation agents open the **TikTak Caller** workspace.
2. The agent clicks **"Next Lead"** (`POST /api/v1/caller/next`).
3. The queue algorithm selects the highest-priority pending lead:
   - *Priority 1:* Scheduled callbacks (`call_back_at <= NOW()`).
   - *Priority 2:* Unanswered leads (`outcome = 'no_answer'` and `call_attempts < 3`).
   - *Priority 3:* Fresh inbound leads sorted by submission timestamp.
4. The system locks the lead in Redis: `SET lock:lead:4012 agent_id_12 EX 240 NX`.
5. The agent's browser immediately displays:
   - Customer name and clickable `tel:` link.
   - Products ordered, sizes, and total COD amount.
   - Previous purchase history (e.g., *"Customer successfully received 3 orders in past 6 months"* or *"Warning: 1 previous delivery refused"*).
6. When the call ends, the agent clicks a single button:
   - **Confirmé (Green):** Stock status moves from *Reserved* to *Committed*; order moves to fulfillment.
   - **Pas de Réponse (Yellow):** Triggers an automated SMS via WinSMS (*"Nous avons essayé de vous joindre pour votre commande..."*) and reschedules call attempt for +2 hours.
   - **Annulé (Red):** Releases reserved stock immediately.

```mermaid
sequenceDiagram
    autonumber
    actor Agent as Confirmation Agent
    participant Caller as TikTak Caller Engine
    participant Redis as Redis Lock Store
    participant WinSMS as WinSMS Gateway
    participant Stock as Warehouse Inventory

    Agent->>Caller: Click "Next Call"
    Caller->>Redis: Check & Acquire lock on Order #8841
    Redis-->>Caller: Lock Granted (TTL: 4 mins)
    Caller-->>Agent: Render Customer Screen Pop (Name, Phone, History)
    Agent->>Agent: Dials Customer (No answer after 5 rings)
    Agent->>Caller: Select Outcome: "Pas de Réponse"
    Caller->>Caller: Increment Call Attempts (Attempt 1 of 3)
    Caller->>WinSMS: Send Automated Recovery SMS with Confirmation Link
    Caller->>Redis: Release Lock & Reschedule Callback for +120 mins
    Caller-->>Agent: Queue Next Available Lead
```

### 4.2 Automated Stock Life-Cycle & Return ("Retour") Reintegration
In Cash-on-Delivery, stock cannot simply be decremented at checkout:
1. **Order Placed:** `physical_quantity = 100`, `reserved_quantity = 1`. `available_quantity = 99`.
2. **Order Confirmed & Shipped:** Item leaves warehouse. `physical_quantity = 99`, `reserved_quantity = 0`.
3. **Delivery Refused / Customer Unreachable:** Courier marks parcel as `Returned to Depot`.
4. **Warehouse Scan In:** When the returned box arrives back at the merchant's warehouse, the staff member scans the AWB barcode with the TikTak PRO mobile app.
5. The system instantly matches the AWB, marks the order as `Returned`, and increments inventory: `physical_quantity = 100`. No stock is ever lost or unaccounted for.

---

## 5. Comprehensive REST & Webhook API Surface Specification

### 5.1 Caller Queue Fetch Endpoint
```http
POST /api/v1/caller/acquire-next
Content-Type: application/json
Authorization: Bearer agent_tok_99812a4b89

{
  "agent_id": 14,
  "supported_governorates": ["Tunis", "Ariana", "Ben Arous", "Nabeul"],
  "mode": "smart_dialer"
}
```

#### Response (200 OK):
```json
{
  "has_lead": true,
  "lead": {
    "order_id": 8841,
    "order_number": "TT-TN-10928",
    "customer_name": "Ines Cherif",
    "phone": "97443322",
    "governorate": "Ariana",
    "delegation": "Menzah 6",
    "items": [
      { "name": "Robe Bohème Fleurie", "variant": "Taille M", "qty": 1, "price": 89.000 }
    ],
    "total_cod": 96.000,
    "past_orders_count": 2,
    "historical_delivery_rate": 100,
    "lock_ttl_seconds": 240
  }
}
```

### 5.2 WinSMS Confirmation Callback Webhook
When a customer clicks the SMS verification link:
```http
POST /api/v1/webhooks/sms-confirm
Content-Type: application/json
X-Signature: sha256=fa8819029bc019...

{
  "token": "sms_cf_89102834b928",
  "order_number": "TT-TN-10928",
  "action": "confirmed_by_customer",
  "ip_address": "197.1.44.12",
  "confirmed_at": "2026-10-26T11:40:02Z"
}
```

---

## 6. Strategic Takeaways & Architecture Lessons for MarketingStudio / SGrowing

1. **Integrated Agent Dialing & Screen Pop:** In high-volume outbound calling, forcing agents to look at disparate screens kills productivity. MarketingStudio should implement a built-in Call Center Agent View with automated lead delivery and screen pop.
2. **Dynamic Stock Quarantine vs Blind Decrement:** In COD commerce, tracking inventory as `Available`, `Reserved`, `In-Transit`, and `Returned` is essential to prevent inventory drift and stockouts.
3. **Automated Multichannel Escalation:** If a buyer does not answer a confirmation phone call, the platform should automatically escalate to SMS and WhatsApp confirmation triggers before canceling the order.
4. **Barcode Return Reintegration:** Scanning courier return manifests directly back into active stock via mobile devices eliminates manual warehouse inventory counting errors.
