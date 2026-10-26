---
title: "Vondera.app Architectural and Functional Wiki"
sidebar:
  hidden: true
aiGenerated: true
---

# Vondera.app System and Functional Architecture

> Comprehensive technical and functional reverse-engineered reference for **Vondera** (`vondera.app`): the leading Egyptian mobile-first commerce operating system, conversational social commerce platform ("Vinbox"), and integrated courier fulfillment hub.

---

## 1. Executive Overview and Market Positioning

**Vondera** was created in Cairo, Egypt, to power the booming ecosystem of social-first online merchants who sell predominantly via **Instagram, Facebook, TikTok, and WhatsApp**, rather than through traditional desktop websites.

In Egypt and the broader MENA region, over 80% of independent merchants manage their commerce operations on mobile devices. They face severe friction:
1. Fragmented communication across multiple messaging channels (DMs, comments, WhatsApp chats).
2. Manual order drafting while chatting with buyers.
3. Complex courier logistics in Egypt (dominated by local carriers like **Bosta**, **Mylerz**, and **Aramex Egypt**) requiring manual re-entry on shipping portals.
4. Cash-on-Delivery (COD) cash collection risk and tracking disputes.

Vondera solves this by providing a unified **Commerce Operating System (OS)** with native mobile apps (iOS & Android) and web access. It combines a drag-and-drop website builder, a centralized social inbox (**Vinbox**), multi-courier API integrations with automated Airway Bill (AWB) generation, real-time stock sync with international suppliers (CJ Dropshipping, Printify, Shopify), and role-based staff management tailored for Egyptian retailers.

```
┌───────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                       Vondera Platform Core                                       │
│          Mobile-First Social Commerce, Conversational CRM ("Vinbox") & Courier Hub                │
└──────────────────────────────────────────────────┬────────────────────────────────────────────────┘
                                                   │
         ┌─────────────────────────────────────────┼────────────────────────────────────────┐
         ▼                                         ▼                                        ▼
┌─────────────────────────────┐       ┌─────────────────────────────┐      ┌─────────────────────────────┐
│   Mobile-First Storefront   │       │   Vinbox: Omnichannel       │      │   Integrated Egyptian       │
│   & AI Page Builder         │       │   Social Commerce CRM       │      │   Courier Hub (Bosta, etc.) │
│  - Instant mobile webstore  │       │  - WhatsApp Cloud API       │      │  - Direct "Assign to Courier"│
│  - Egyptian phone formatting│       │  - Instagram DM & FB inbox  │      │  - Instant AWB label print  │
│  - Sub-zone delivery fees   │       │  - In-chat order creation   │      │  - Live parcel tracking     │
└─────────────────────────────┘       └─────────────────────────────┘      └─────────────────────────────┘
         │                                         │                                        │
         └─────────────────────────────────────────┼────────────────────────────────────────┘
                                                   ▼
                                      ┌─────────────────────────────┐
                                      │  Multi-Channel Inventory &  │
                                      │  Dropshipping Sync Engine   │
                                      │ - Real-time Shopify 2-way   │
                                      │ - CJ Dropshipping / Printify│
                                      │ - Staff roles & commissions │
                                      └─────────────────────────────┘
```

### Core Value Proposition & Market Differentiators
- **True Mobile-First Management:** Store owners and staff can configure products, assign orders to couriers, chat with clients, and print receipts directly from smartphone apps.
- **"Vinbox" Conversational Selling:** Merges customer chats from WhatsApp, Instagram, Facebook Messenger, and web chat into one workspace where agents can generate orders and send checkout links without leaving the conversation.
- **Native Bosta & Egyptian Courier APIs:** 1-click order transmission to Bosta, Mylerz, and Aramex Egypt, generating barcoded shipping labels and scheduling driver pickups automatically.
- **Egyptian Market Localization:** Precise zone-based delivery pricing (Cairo/Giza, Delta, Alexandria, Upper Egypt / Sa'id, Red Sea, Canal cities) and 11-digit Egyptian phone validation (`010`, `011`, `012`, `015`).
- **Affordable EGP-Denominated Pricing:** Subscription tiers priced locally in Egyptian Pounds (~400 to 1,500+ EGP/month) without international payment barrier constraints.

---

## 2. Core Entities and Reverse-Engineered Data Model

### 2.1 Domain Entity Relationship Model

```
┌─────────────────────────────────────────────────────────────────┐
│                        Merchant Business                        │
│      (Business Name, Owner ID, Currency: EGP, Tax / CR Number)  │
└───────────────────────────────┬─────────────────────────────────┘
                                │ 1:N
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                          Store Instance                         │
│         (Subdomain *.vondera.app, Custom Domain, Settings)      │
└───────────────┬───────────────────────────────┬─────────────────┘
                │ 1:N                           │ 1:N
                ▼                               ▼
┌───────────────────────────────┐   ┌─────────────────────────────┐
│      Unified Product Model    │   │     Omnichannel Customer    │
│  (Title, Base Cost, Price,    │   │ (Name, Phone, WhatsApp ID, │
│   Shopify Sync ID, Stock)     │   │  IG Handle, Governorates)   │
└───────────────┬───────────────┘   └─────────────┬───────────────┘
                │                                 │
                │ 1:N                             │ 1:N
                ▼                                 ▼
┌───────────────────────────────┐   ┌─────────────────────────────┐
│         Product Bundle        │   │    Vinbox Conversation      │
│  (Composite SKU, Items Map,   │   │ (Channel: WA/IG/FB, Messages│
│   Discounted Package Price)   │   │  Assigned Agent, State)     │
└───────────────┬───────────────┘   └─────────────┬───────────────┘
                │                                 │
                └───────────────┬─────────────────┘
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                           Order Record                          │
│  (Order #, Source: Vinbox/Web, Governorate, Zone, Delivery Cost,│
│   COD Total, Courier ID, Courier AWB, Courier Tracking Status)  │
└───────────────────────────────┬─────────────────────────────────┘
                                │ 1:N
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                        Staff Activity Log                       │
│  (Agent ID, Role: Cashier/Picker, Action Taken, Commission Due) │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Class Diagram Architecture

```mermaid
classDiagram
    class MerchantStore {
        +BigInt id
        +String businessName
        +String slug
        +String primaryCurrency
        +List~CourierAccount~ courierAccounts
        +assignOrderToBosta(orderId)
        +syncShopifyInventory()
    }

    class VinboxConversation {
        +BigInt id
        +ChannelType channel
        +String externalContactId
        +String customerName
        +ConversationStatus status
        +BigInt assignedAgentId
        +sendMessage(body)
        +createDraftOrder(cartItems)
    }

    class VonderaOrder {
        +BigInt id
        +String orderNumber
        +OrderSource origin
        +String customerPhone
        +EgyptianGovernorate governorate
        +String district
        +Decimal subtotal
        +Decimal shippingCost
        +Decimal codAmount
        +CourierProvider selectedCourier
        +String airwayBillNumber
        +ShipmentState deliveryStatus
        +generateAWB()
        +cancelShipment()
    }

    class BostaCourierBridge {
        +String apiKey
        +String businessId
        +createDelivery(VonderaOrder order)
        +printAwbLabel(awb)
        +schedulePickup(pickupDetails)
        +trackShipment(awb)
    }

    class StaffMember {
        +BigInt id
        +String fullName
        +StaffRole role
        +Decimal salesCommissionRate
        +Boolean canViewProfits
    }

    MerchantStore "1" *-- "many" VinboxConversation : manages
    VinboxConversation "1" --> "0..1" VonderaOrder : converts_into
    MerchantStore "1" *-- "many" VonderaOrder : fulfills
    VonderaOrder "many" --> "1" BostaCourierBridge : routed_through
    MerchantStore "1" *-- "many" StaffMember : employs
```

### 2.3 Comprehensive Database Schema Specification

#### Table: `vondera_stores`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Store identifier |
| `owner_id` | `BIGINT UNSIGNED` | `NOT NULL, INDEX` | - | Merchant user reference |
| `store_handle` | `VARCHAR(64)` | `UNIQUE NOT NULL` | - | Subdomain handle |
| `custom_domain` | `VARCHAR(191)` | `UNIQUE NULL` | `NULL` | Custom branded domain |
| `currency` | `VARCHAR(3)` | `NOT NULL` | `'EGP'` | Store operating currency |
| `bosta_api_key` | `TEXT` | `NULL` | `NULL` | Encrypted Bosta API token |
| `mylerz_api_key` | `TEXT` | `NULL` | `NULL` | Encrypted Mylerz API token |
| `shopify_access_token`| `TEXT` | `NULL` | `NULL` | OAuth token for Shopify sync |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `CURRENT_TIMESTAMP` | Account creation date |

#### Table: `vondera_orders`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Order identifier |
| `store_id` | `BIGINT UNSIGNED` | `NOT NULL, INDEX` | - | Store reference |
| `order_number` | `VARCHAR(32)` | `UNIQUE NOT NULL` | - | e.g. `VON-EG-50491` |
| `source` | `ENUM('web_store','vinbox_whatsapp','vinbox_instagram','manual')` | `NOT NULL` | `'web_store'` | Order intake channel |
| `customer_name` | `VARCHAR(120)` | `NOT NULL` | - | Customer full name |
| `phone_number` | `VARCHAR(20)` | `NOT NULL, INDEX` | - | Egyptian mobile (11 digits) |
| `governorate` | `VARCHAR(50)` | `NOT NULL, INDEX` | - | Egyptian Governorate (e.g. Cairo, Giza, Alexandria) |
| `zone_district` | `VARCHAR(100)` | `NOT NULL` | - | Sub-district (e.g. Nasr City, Maadi, Dokki) |
| `street_address`| `TEXT` | `NOT NULL` | - | Full street address and apartment |
| `cod_amount` | `DECIMAL(10,2)` | `NOT NULL` | `0.00` | Net Cash on Delivery amount in EGP |
| `courier_code` | `VARCHAR(32)` | `NULL, INDEX` | `NULL` | `bosta`, `mylerz`, `aramex_eg`, `custom` |
| `awb_number` | `VARCHAR(64)` | `NULL, INDEX` | `NULL` | Courier Airway Bill number |
| `tracking_state`| `VARCHAR(32)` | `NOT NULL` | `'draft'` | `draft`, `courier_assigned`, `picked_up`, `out_for_delivery`, `delivered`, `returned` |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `CURRENT_TIMESTAMP` | Order timestamp |

#### Table: `vinbox_messages`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Message ID |
| `conversation_id`| `BIGINT UNSIGNED`| `NOT NULL, INDEX` | - | Associated conversation |
| `sender_type` | `ENUM('customer','agent','bot')` | `NOT NULL` | - | Sender identity |
| `message_type` | `ENUM('text','image','order_card','voice')` | `NOT NULL` | `'text'` | Content type |
| `body` | `TEXT` | `NULL` | `NULL` | Text message body |
| `media_url` | `VARCHAR(512)` | `NULL` | `NULL` | Uploaded audio or image file |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `CURRENT_TIMESTAMP` | Dispatch timestamp |

---

## 3. Reverse-Engineered System Architecture & Engineering Blueprint

```mermaid
flowchart TD
    subgraph ClientApplications ["Merchant Mobile & Consumer Touchpoints"]
        MobileAppOwner["Merchant iOS & Android Native App (Flutter / React Native)"]
        MerchantWebAdmin["Web Admin Console (React / Next.js)"]
        ShopperMobile["Shopper Storefront (Mobile Browser PWA)"]
        WhatsAppUser["Customer WhatsApp / Instagram Messenger"]
    end

    subgraph EdgeGateway ["API Gateway & Reverse Proxy"]
        Cloudflare["Cloudflare CDN & DDoS Protection"]
        KongGateway["Kong / Traefik API Gateway & Rate Limiter"]
    end

    subgraph MicroservicesStack ["Vondera Backend Services"]
        StoreService["Storefront & Catalog Service (Node.js / Express)"]
        VinboxService["Vinbox Omnichannel Hub (NestJS / WebSocket Gateway)"]
        OrderService["Order Processing & Billing Engine"]
        CourierService["Egyptian Courier Gateway Adapter (Bosta / Mylerz Bridge)"]
        DropshipSyncWorker["Shopify & CJ Dropshipping Sync Worker"]
    end

    subgraph MessagingAndQueues ["Message Broker & Realtime Mesh"]
        RabbitMQ["RabbitMQ / Kafka (Order Events & Courier Webhooks)"]
        SocketIO["Socket.IO Cluster (Vinbox Realtime Chat & Push Notifications)"]
        RedisStore["Redis 7 (Session Store, Rate Limits & Pub/Sub)"]
    end

    subgraph ThirdPartyLogistics ["Egyptian Courier & Channel APIs"]
        BostaAPI["Bosta Delivery API (REST v2)"]
        MylerzAPI["Mylerz Logistics API"]
        MetaCloudAPI["Meta Graph API (WhatsApp Cloud, IG DMs, Messenger)"]
        ShopifyAPI["Shopify Admin GraphQL API"]
    end

    subgraph StorageLayer ["Database Cluster"]
        PostgresPrimary[(PostgreSQL 16 Primary - Relations & Invoices)]
        MongoConversations[(MongoDB - Vinbox Unstructured Chat Logs)]
        S3Bucket[(AWS S3 / Wasabi - Shipping Labels & Receipts)]
    end

    MobileAppOwner --> Cloudflare
    MerchantWebAdmin --> Cloudflare
    ShopperMobile --> Cloudflare
    WhatsAppUser --> MetaCloudAPI

    Cloudflare --> KongGateway
    KongGateway --> StoreService
    KongGateway --> VinboxService
    KongGateway --> OrderService
    KongGateway --> CourierService

    MetaCloudAPI -->|Inbound Webhooks| VinboxService
    VinboxService --> SocketIO
    SocketIO -->|Live Notification| MobileAppOwner
    VinboxService --> MongoConversations

    OrderService --> RabbitMQ
    RabbitMQ --> CourierService
    CourierService --> BostaAPI
    CourierService --> MylerzAPI
    CourierService --> S3Bucket

    RabbitMQ --> DropshipSyncWorker
    DropshipSyncWorker --> ShopifyAPI

    StoreService --> PostgresPrimary
    OrderService --> PostgresPrimary
    StoreService --> RedisStore
```

### 3.1 Technology Stack & Operational Efficiency
- **Mobile Stack:** **Flutter / Dart** cross-platform client codebase compiling to high-performance native binaries on both iOS and Android, allowing camera barcode scanning for warehouse inventory counting and direct Bluetooth receipt printer integration.
- **Backend Services:** **Node.js (NestJS / TypeScript)** microservices for the conversational inbox and order dispatch, running on Kubernetes (EKS / DigitalOcean).
- **Messaging Engine:** **RabbitMQ** managing high-velocity order event queues, ensuring courier API rate limits from Bosta and Mylerz are respected via exponential backoff retry mechanisms.
- **Data Architecture:** Hybrid persistence model: **PostgreSQL 16** for transactional relational consistency (orders, products, financial balances) and **MongoDB** for flexible, high-volume unstructured social chat threads across WhatsApp and Instagram.

---

## 4. Key Operational Mechanics & Deep Technical Differentiators

### 4.1 "Vinbox": In-Chat Order Creation Workflow
Rather than forcing a customer chatting on Instagram or WhatsApp to open an external browser link, the merchant creates an order directly within the chat interface:
1. The customer texts: *"I want the oversized black hoodie, size L"*.
2. The agent taps **"Create Order"** in Vinbox.
3. A modal appears with the merchant's catalog; the agent selects the product and size.
4. The system auto-fills the customer's name and past delivery address from previous orders linked to that phone number.
5. The agent clicks **"Send Confirmation Link"** or **"Confirm Directly"**.
6. An interactive WhatsApp message card is dispatched containing the order details, COD amount, and a 1-tap confirmation button.

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Shopper (WhatsApp)
    actor Agent as Store Agent (Vinbox App)
    participant Vinbox as Vinbox Hub
    participant OrderEng as Order Engine
    participant Courier as Bosta API

    Customer->>Vinbox: "Send me 2 bottles of Hair Serum to Maadi"
    Vinbox->>Agent: Push Notification on Mobile Phone
    Agent->>Vinbox: Select Product [Hair Serum x2]
    Vinbox->>OrderEng: POST /orders/draft (Auto-link customer phone)
    OrderEng-->>Vinbox: Draft Order #VON-EG-9912 created
    Agent->>Vinbox: Click "Assign to Bosta"
    Vinbox->>Courier: POST /api/v2/deliveries (Pickup: Store, Drop: Maadi)
    Courier-->>Vinbox: Return AWB: "BST-849102" + PDF Label URL
    Vinbox->>Customer: Automated WhatsApp message with tracking link & AWB
```

### 4.2 Native Bosta Courier API Bridge & Shipping Workflow
Vondera integrates directly with **Bosta's Delivery API**:
- **Automatic Zone Mapping:** Vondera translates colloquial Egyptian district names into Bosta's standardized zone codes (`EG-01` Cairo, `EG-02` Giza, etc.).
- **Live AWB Generation:** Generates a PDF Airway Bill containing Bosta's 2D barcode, store contact, customer address, and the exact COD cash collection amount.
- **Automated Pickup Scheduling:** When 10+ orders are assigned, Vondera triggers a Bosta pickup request: `POST https://api.bosta.co/api/v2/pickups`.
- **Status Reconciliation:** Subscribes to Bosta webhooks (`DELIVERED`, `TERMINATED`, `RETURNED_TO_STORE`) to update store inventory and COD remittance accounts automatically.

### 4.3 Multi-Channel Inventory Sync (Shopify & CJ Dropshipping)
Many Egyptian sellers maintain an international Shopify store alongside their local domestic operations.
- Vondera acts as an inventory bridge: when an item is sold via COD in Egypt on Vondera, a background worker triggers an inventory adjustment call to Shopify:
  ```graphql
  mutation inventoryAdjustQuantities($input: InventoryAdjustQuantitiesInput!) {
    inventoryAdjustQuantities(input: $input) {
      userErrors { field message }
      inventoryLevel { available }
    }
  }
  ```
- This prevents overselling across disparate channels without manual spreadsheet reconciliations.

---

## 5. Comprehensive REST & Webhook API Surface Specification

### 5.1 Push Order to Courier (Bosta Integration API)
```http
POST /api/v1/orders/10492/assign-courier
Content-Type: application/json
Authorization: Bearer von_sec_8192a8b9f01c23

{
  "courier": "bosta",
  "pickup_address_id": "wh_cairo_central",
  "notes": "Please call before arrival - gate 4",
  "allow_open_package": true
}
```

#### Response (200 OK):
```json
{
  "success": true,
  "shipment": {
    "courier": "bosta",
    "awb": "BST-EG-4910291",
    "tracking_url": "https://bosta.co/tracking?tracking_number=BST-EG-4910291",
    "label_pdf_url": "https://cdn.vondera.app/labels/BST-EG-4910291.pdf",
    "status": "courier_assigned",
    "cod_to_collect": 650.00,
    "currency": "EGP"
  }
}
```

### 5.2 Vinbox Inbound WhatsApp Webhook
```http
POST /api/v1/webhooks/meta-whatsapp
Content-Type: application/json
X-Hub-Signature-256: sha256=49c81920ae10ff82...

{
  "object": "whatsapp_business_account",
  "entry": [
    {
      "id": "1092837465",
      "changes": [
        {
          "value": {
            "messaging_product": "whatsapp",
            "messages": [
              {
                "from": "201012345678",
                "id": "wamid.HBgLMjAx...",
                "timestamp": "1729941820",
                "text": {
                  "body": "Hi, is the white linen shirt available in XL?"
                },
                "type": "text"
              }
            ]
          }
        }
      ]
    }
  ]
}
```

---

## 6. Strategic Takeaways & Architecture Lessons for MarketingStudio / SGrowing

1. **Conversational Commerce as First-Class Ingestion:** MarketingStudio should allow sales agents to draft orders, calculate delivery fees, and trigger payment/COD links directly from within its omnichannel inbox.
2. **Mobile App Operational Autonomy:** Giving merchants full operational control via native iOS/Android apps dramatically improves retention in high-velocity emerging markets.
3. **Local Courier Gateway Abstraction Layer:** Implement a standardized Courier Adapter interface (`createShipment`, `generateLabel`, `schedulePickup`, `parseWebhook`) so adding regional couriers (Bosta, Yalidine, Aramex) requires only a driver plugin.
4. **Phone-Number-First Customer Identity:** In social commerce, email addresses are rarely provided; the primary phone number must serve as the primary foreign key across conversations, orders, and CRM records.
