---
title: "YouCan.shop Architectural and Functional Wiki"
sidebar:
  hidden: true
aiGenerated: true
---

# YouCan.shop System and Functional Architecture

> Comprehensive technical and functional reverse-engineered reference for **YouCan** (`youcan.shop`): the multi-tenant global e-commerce engine, developer ecosystem (Theme SDK, Celeste UI, YouCan CLI), and native payment infrastructure ("YouCan Pay") powering Cash-On-Delivery across MENA and Africa.

---

## 1. Executive Overview and Market Positioning

**YouCan** was founded by digital entrepreneur Said Aghassaiy (Simo Life) and an international engineering team to solve a fundamental problem in emerging e-commerce markets: **the prohibitive cost and architectural mismatch of Western platforms like Shopify**.

For digital merchants in Africa and the Middle East, traditional SaaS platforms presented severe barriers:
- Fixed monthly subscriptions ($39 - $399/mo) in US Dollars, subject to strict foreign currency exchange limits and credit card blocks by local banks.
- Checkout flows designed around credit cards, forcing mobile Cash-on-Delivery (COD) buyers through multi-step shopping carts that induced 70%+ abandonment.
- High fees for basic apps (currency converters, upsells, order bumps, COD address forms).

YouCan disrupted this landscape by introducing a **"Pay As You Sell" model** (0% monthly subscription, charging only a nominal ~0.25% - 0.5% commission on successful sales), pairing it with an **Express Checkout engine**, a developer platform (Liquid templating, **Celeste UI**, YouCan CLI), and an integrated payment gateway (**YouCan Pay**). Today, YouCan powers hundreds of thousands of merchants across Morocco, Algeria, Tunisia, Egypt, the Gulf Cooperation Council (GCC), and Francophone West Africa, processing billions of dirhams, dinars, and riyals in GMV.

```
┌───────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                       YouCan Platform Core                                        │
│          Scalable Multi-Tenant E-Commerce Cloud, Developer Ecosystem & YouCan Pay Gateway        │
└──────────────────────────────────────────────────┬────────────────────────────────────────────────┘
                                                   │
         ┌─────────────────────────────────────────┼────────────────────────────────────────┐
         ▼                                         ▼                                        ▼
┌─────────────────────────────┐       ┌─────────────────────────────┐      ┌─────────────────────────────┐
│   High-Velocity Storefront  │       │   Developer & App Ecosystem │      │   Fintech Payment & COD     │
│   & Express Checkout Engine │       │   (CLI, Celeste UI, Liquid) │      │   Infrastructure (YouCan Pay)│
│  - No-Cart 1-page checkout  │       │  - Official YouCan CLI      │      │  - Native 'yp.js' gateway   │
│  - Native Upsell & Bump     │       │  - Liquid Theme Engine      │      │  - Multi-currency auto-rates│
│  - Native Arabic RTL & LTR  │       │  - OAuth 2.0 App Marketplace│      │  - COD city/zone shipping   │
└─────────────────────────────┘       └─────────────────────────────┘      └─────────────────────────────┘
         │                                         │                                        │
         └─────────────────────────────────────────┼────────────────────────────────────────┘
                                                   ▼
                                      ┌─────────────────────────────┐
                                      │  Real-Time Streaming Fabric │
                                      │  & Integration Engine       │
                                      │ - Instant Google Sheets sync│
                                      │ - Meta / TikTok Server CAPI │
                                      │ - Courier Webhook Pipeline  │
                                      └─────────────────────────────┘
```

### Core Value Proposition & Market Differentiators
- **Pay As You Sell (Zero Upfront Barrier):** Merchants can launch stores without monthly credit card billing; YouCan collects only a negligible fraction of completed transactions.
- **"Express Checkout" (No-Cart Architecture):** Bypasses the shopping cart completely. Shoppers land on a product page, select variants, and complete their COD or card order inside a single frictionless checkout form.
- **Enterprise Developer Tooling:** First regional platform to offer an open developer ecosystem: **YouCan CLI** for scaffolding themes and apps, **Celeste UI** (Vue component library) for admin UI consistency, and a full **Liquid Theme Engine**.
- **Integrated Payment Gateway ("YouCan Pay"):** Direct native card processing (`yp.js`) with instant activation, low transaction fees, and automated fraud security, running side-by-side with COD.
- **Native Google Sheets Real-Time Streaming:** Instant bi-directional sync transmitting every order into Google Sheets within milliseconds for media buyers and call center operations.

---

## 2. Core Entities and Reverse-Engineered Data Model

### 2.1 Domain Entity Relationship Model

```
┌─────────────────────────────────────────────────────────────────┐
│                        Merchant Account                         │
│     (ID, Email, Owner Name, Identity KYC, Primary Currency)     │
└───────────────────────────────┬─────────────────────────────────┘
                                │ 1:N
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                         Store Instance                          │
│   (ID, Subdomain *.youcan.shop, Custom Domain, Plan, Theme ID)  │
└───────────────┬───────────────────────────────┬─────────────────┘
                │ 1:N                           │ 1:N
                ▼                               ▼
┌───────────────────────────────┐   ┌─────────────────────────────┐
│      Product & Variants       │   │      Installed Apps         │
│  (Title, Slug, Price, Stock,  │   │  (App ID, OAuth Tokens,     │
│   Weight, Images, Metadata)   │   │   Granted Scopes, Webhooks) │
└───────────────┬───────────────┘   └─────────────────────────────┘
                │ 1:N
                ▼
┌───────────────────────────────┐
│          Order Record         │
│  (Order #, Customer, Phone,   │
│   Payment Method: COD/Card,   │
│   Total, Currency, Status)    │
└───────────────┬───────────────┘
                │
                ├───────────────────────────────┬─────────────────┐
                │ 1:N                           │ 1:1             │ 1:N
                ▼                               ▼                 ▼
┌───────────────────────────────┐   ┌─────────────────────┐  ┌───────────────────┐
│          Order Item           │   │  YouCan Pay Record  │  │ Google Sheet Sync │
│  (Product ID, Variant ID, Qty,│   │  (Payment Intent ID,│  │ (Sheet ID, Row #, │
│   Unit Price, Is Bump/Upsell) │   │   Token, Fee, Status│  │  Sync State, Error│
└───────────────────────────────┘   └─────────────────────┘  └───────────────────┘
```

### 2.2 Class Diagram Architecture

```mermaid
classDiagram
    class YouCanStore {
        +BigInt id
        +String storeSlug
        +String customDomain
        +String activeThemeId
        +String defaultCurrency
        +List~Product~ products
        +List~Order~ orders
        +installApp(appId, oauthScopes)
        +syncOrderToSheets(orderId)
    }

    class ThemeEngine {
        +String themeId
        +Map~String, LiquidTemplate~ templates
        +compileLiquid(template, context)
        +renderStorefront(requestContext)
    }

    class YouCanOrder {
        +BigInt id
        +String orderNumber
        +PaymentType paymentMethod
        +String customerPhone
        +String destinationCity
        +Decimal subtotal
        +Decimal shippingCost
        +Decimal totalAmount
        +OrderStatus status
        +processCardPayment(paymentToken)
        +dispatchOrderWebhook()
    }

    class YouCanPayGateway {
        +String merchantKey
        +createPaymentIntent(amount, currency)
        +verifyToken(token)
        +capturePayment(intentId)
        +refund(intentId, amount)
    }

    class AppMarketplaceBridge {
        +String clientId
        +String clientSecret
        +List~String~ permissions
        +verifyOAuthSignature(signature)
        +emitWebhookEvent(eventType, payload)
    }

    YouCanStore "1" *-- "many" YouCanOrder : receives
    YouCanStore "1" --> "1" ThemeEngine : renders_via
    YouCanOrder "many" --> "0..1" YouCanPayGateway : processed_by
    YouCanStore "1" *-- "many" AppMarketplaceBridge : extends_with
```

### 2.3 Comprehensive Database Schema Specification

#### Table: `stores`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Unique Store Identifier |
| `seller_id` | `BIGINT UNSIGNED` | `NOT NULL, INDEX` | - | Merchant user ID |
| `slug` | `VARCHAR(64)` | `UNIQUE NOT NULL` | - | e.g. `maroc-fashion.youcan.shop` |
| `custom_domain` | `VARCHAR(191)` | `UNIQUE NULL` | `NULL` | Custom domain linked via CNAME |
| `currency` | `VARCHAR(3)` | `NOT NULL` | `'MAD'` | Base currency (`MAD`, `TND`, `DZD`, `USD`) |
| `commission_rate` | `DECIMAL(4,4)` | `NOT NULL` | `0.0050` | 0.50% standard transaction commission |
| `youcan_pay_enabled`| `BOOLEAN` | `NOT NULL` | `TRUE` | Enable YouCan Pay processing |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `CURRENT_TIMESTAMP` | Account creation timestamp |

#### Table: `orders`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Internal order record ID |
| `store_id` | `BIGINT UNSIGNED` | `NOT NULL, INDEX` | - | Store reference |
| `order_number` | `VARCHAR(32)` | `UNIQUE NOT NULL` | - | Formatted ID: `YC-2026-88129` |
| `payment_type` | `ENUM('cod','youcan_pay','stripe','paypal')` | `NOT NULL` | `'cod'` | Payment method |
| `customer_first_name`| `VARCHAR(100)` | `NOT NULL` | - | Customer first name |
| `customer_phone` | `VARCHAR(25)` | `NOT NULL, INDEX` | - | Contact phone number |
| `city` | `VARCHAR(100)` | `NOT NULL, INDEX` | - | Delivery destination city |
| `address` | `TEXT` | `NOT NULL` | - | Full street address |
| `subtotal` | `DECIMAL(12,2)` | `NOT NULL` | `0.00` | Items subtotal |
| `shipping_amount`| `DECIMAL(10,2)` | `NOT NULL` | `0.00` | Shipping fee |
| `total` | `DECIMAL(12,2)` | `NOT NULL` | `0.00` | Grand total |
| `status` | `VARCHAR(32)` | `NOT NULL, INDEX` | `'pending'` | `pending`, `confirmed`, `shipped`, `delivered`, `cancelled` |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `CURRENT_TIMESTAMP` | Order timestamp |

#### Table: `oauth_apps`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Application ID |
| `app_uuid` | `CHAR(36)` | `UNIQUE NOT NULL` | - | App public UUID |
| `name` | `VARCHAR(100)` | `NOT NULL` | - | App name (e.g., Google Sheets Sync) |
| `client_id` | `VARCHAR(64)` | `UNIQUE NOT NULL` | - | OAuth Client ID |
| `client_secret` | `VARCHAR(128)` | `NOT NULL` | - | OAuth Client Secret |
| `redirect_uri` | `VARCHAR(512)` | `NOT NULL` | - | Callback URL |
| `scopes` | `JSON` | `NOT NULL` | - | e.g. `["read-orders", "edit-products"]` |

---

## 3. Reverse-Engineered System Architecture & Engineering Blueprint

```mermaid
flowchart TD
    subgraph GlobalShoppers ["Global & Regional Shoppers"]
        ConsumerMobile["Mobile Browser (Safari / Chrome)"]
        ConsumerApp["Shopper Touchpoint (PWA)"]
    end

    subgraph EdgeCDNCluster ["Global Edge & DNS Routing"]
        CloudflareAnycast["Cloudflare Anycast CDN (DDoS, WAF, SSL Termination)"]
    end

    subgraph PublicStorefrontFleet ["Storefront Rendering Fleet"]
        LiquidSSRNodes["Node.js / Go Liquid SSR Fleet (Storefront Rendering)"]
        StoreAPIGateway["Storefront Public API Gateway"]
    end

    subgraph CoreBackendFleet ["Core Multi-Tenant Backend (Kubernetes)"]
        AdminAPIService["Admin REST API (api.youcan.shop)"]
        CheckoutEngine["Express Checkout & Order Processing Engine"]
        ThemeCompiler["Theme Compiler & Asset Packager"]
        AppOAuthGateway["OAuth 2.0 Auth Server & Webhook Mesh"]
    end

    subgraph FintechPaymentMesh ["YouCan Pay Infrastructure"]
        YouCanPayCore["YouCan Pay Tokenizer & Transaction Core"]
        BankSwitchBridge["Regional Card Processing Network / 3DS"]
    end

    subgraph MessageBrokerAndRealtime ["Event Streaming & Job Queues"]
        KafkaEventBus["Apache Kafka (Order & Telemetry Event Streams)"]
        RedisCacheCluster["Redis Cluster (Store Cache, Rates & Sessions)"]
    end

    subgraph ThirdPartyIntegrations ["External Developer & Ad Mesh"]
        ThirdPartyApps["Partner Apps (via App Marketplace API)"]
        MetaCAPI["Meta Conversions API"]
        TikTokEvents["TikTok Events API"]
        GoogleSheetsAPI["Google Sheets API v4"]
    end

    subgraph PersistenceLayer ["Persistent Data Tier"]
        PostgresSharded[(PostgreSQL Sharded Database - High-Scale OLTP)]
        S3AssetStore[(AWS S3 / Cloudflare R2 - Themes, Media & Assets)]
    end

    ConsumerMobile --> CloudflareAnycast
    ConsumerApp --> CloudflareAnycast

    CloudflareAnycast --> LiquidSSRNodes
    CloudflareAnycast --> StoreAPIGateway

    LiquidSSRNodes --> RedisCacheCluster
    StoreAPIGateway --> CheckoutEngine

    CheckoutEngine --> YouCanPayCore
    YouCanPayCore --> BankSwitchBridge

    CheckoutEngine --> KafkaEventBus
    AdminAPIService --> KafkaEventBus

    KafkaEventBus --> AppOAuthGateway
    AppOAuthGateway --> ThirdPartyApps
    KafkaEventBus --> MetaCAPI
    KafkaEventBus --> TikTokEvents
    KafkaEventBus --> GoogleSheetsAPI

    AdminAPIService --> PostgresSharded
    ThemeCompiler --> S3AssetStore
```

### 3.1 Technology Stack & Operational Efficiency
- **Storefront & Templating:** High-performance **Liquid templating engine** running on distributed Node.js/Go rendering clusters, rendering storefront pages in under 80ms by leveraging Redis edge-caching for product models and catalog hierarchies.
- **Admin & Developer UI:** **Celeste UI** component framework built on **Vue 3**, enforcing visual consistency across first-party dashboards and third-party partner applications.
- **Microservices Orchestration:** Horizontally autoscaled container clusters managed via Kubernetes (EKS), decoupling storefront browsing traffic from backend admin and payment processing.
- **Fintech Tokenization (`yp.js`):** Direct client-side credit card field isolation complying with PCI-DSS Level 1 specifications, exchanging sensitive card details for transient tokens before submitting to `api.youcanpay.com`.

---

## 4. Key Operational Mechanics & Deep Technical Differentiators

### 4.1 "Express Checkout" (No-Cart Architecture)
Instead of forcing shoppers through a 4-step funnel (Cart ➔ Account ➔ Shipping ➔ Card), YouCan embeds a direct checkout module:
```html
<!-- YouCan Express Checkout Embed -->
<div id="youcan-express-checkout" class="yc-checkout-wrapper">
  <div class="product-variant-selector">
    <button class="variant-btn active" data-variant="red">Rouge</button>
    <button class="variant-btn" data-variant="blue">Bleu</button>
  </div>
  
  <div class="yc-direct-fields">
    <input type="text" id="yc-name" placeholder="Nom complet" />
    <input type="tel" id="yc-phone" placeholder="Numéro de téléphone" />
    <input type="text" id="yc-city" placeholder="Ville" />
    <textarea id="yc-address" placeholder="Adresse complète"></textarea>
  </div>

  <!-- Integrated Order Bump -->
  <div class="yc-order-bump">
    <input type="checkbox" id="bump-check" />
    <label for="bump-check">Ajouter la garantie express 1 an (+49 MAD)</label>
  </div>

  <button id="yc-buy-now" class="yc-btn-primary">
    Acheter Maintenant - Paiement à la livraison
  </button>
</div>
```
When submitted, the order is created atomically via `POST /api/checkout/express`, skipping cart table writes completely.

### 4.2 Developer Ecosystem & YouCan CLI
YouCan empowers third-party agencies and SaaS builders with an official CLI:
```bash
# Install YouCan CLI
npm install -g @youcan/cli

# Scaffold a new custom theme
youcan theme init my-custom-theme

# Start local hot-reloading theme dev server
youcan theme dev

# Deploy theme directly to merchant store
youcan theme push

# Scaffold an App Marketplace integration
youcan app init my-inventory-sync-app
```
Theme templates use familiar Liquid syntax:
```liquid
{% for product in collections.featured.products %}
  <div class="product-card">
    <img src="{{ product.featured_image.url }}" alt="{{ product.title }}" />
    <h3>{{ product.title }}</h3>
    <span class="price">{{ product.price | money }}</span>
    {% render 'express-buy-button', product: product %}
  </div>
{% endfor %}
```

### 4.3 Native Google Sheets Streaming Engine
1. Every time an order status updates (`CREATED`, `CONFIRMED`, `SHIPPED`, `CANCELLED`), an event is published to the Kafka bus.
2. The Google Sheets Worker picks up the event, batches updates, and writes directly to the merchant's target spreadsheet via the Google Sheets API v4.
3. If an agent changes the order status inside Google Sheets (e.g. typing `Confirmed` in Column G), a webhook relays the change back to YouCan, synchronizing both systems in real time.

---

## 5. Comprehensive REST & Webhook API Surface Specification

### 5.1 Admin API: Fetch Store Orders
```http
GET /api/orders?status=pending&limit=25
Host: api.youcan.shop
Authorization: Bearer yc_live_tok_991823abce84
Accept: application/json
```

#### Response (200 OK):
```json
{
  "data": [
    {
      "id": "ord_91820349",
      "order_number": "YC-2026-88129",
      "customer": {
        "name": "Hamza El Mansouri",
        "phone": "+212612345678",
        "email": "hamza@example.com"
      },
      "shipping_address": {
        "city": "Casablanca",
        "country": "MA",
        "address1": "Boulevard d'Anfa, Résidence Al Amal"
      },
      "payment_method": "cod",
      "currency": "MAD",
      "subtotal": 350.00,
      "shipping_cost": 30.00,
      "total": 380.00,
      "status": "pending",
      "created_at": "2026-10-26T11:05:00Z"
    }
  ],
  "meta": {
    "current_page": 1,
    "total": 142
  }
}
```

### 5.2 YouCan Pay Token Charge Endpoint
```http
POST /api/v1/payments/charge
Host: api.youcanpay.com
Authorization: Bearer sec_yp_883a910bf29871
Content-Type: application/json

{
  "token_id": "tok_card_99182ab912",
  "amount": 38000,
  "currency": "MAD",
  "order_id": "ord_91820349",
  "customer": {
    "name": "Hamza El Mansouri",
    "email": "hamza@example.com"
  }
}
```

---

## 6. Strategic Takeaways & Architecture Lessons for MarketingStudio / SGrowing

1. **Theme SDK & Open Theming Engine:** Providing Liquid or JSX-based open theming allows agencies to build bespoke landing pages and storefronts, creating an organic ecosystem moat.
2. **First-Party Component Library (Celeste UI Pattern):** Publishing a standardized UI library for external apps and plugins guarantees that the entire software ecosystem looks cohesive and modern.
3. **Pay As You Sell Business Model:** For emerging markets, lowering the upfront subscription barrier and taking a micro-percentage of GMV drives rapid mass adoption among solopreneurs and media buyers.
4. **Native Payment Gateway Integration:** Combining COD with a proprietary card payment engine (YouCan Pay model) captures revenue across both cash and digital payment rails while controlling checkout UX.
