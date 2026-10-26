# SGrowing Enterprise Production Deployment Guide

This guide details the deployment of the SGrowing sovereign SaaS platform on Linux / Cloud VPS via Docker Compose and Coolify.

---

## 1. System Topology

```
             ┌────────────────────────────────────────────────────────┐
             │                Reverse Proxy / Cloudflare             │
             └──────────────────────────┬─────────────────────────────┘
                                        │ (Port 80 / 443)
             ┌──────────────────────────▼─────────────────────────────┐
             │            NGINX Frontend Container (web)              │
             │           Angular 21 SPA + Static Bundles              │
             └─────────────┬────────────────────────────┬─────────────┘
                           │                            │
             (Reverse Proxy /api/*)           (WebSockets /hubs/*)
                           │                            │
             ┌─────────────▼────────────────────────────▼─────────────┐
             │              SGrowing.Api (.NET 10)                    │
             │         Modular Monolith Minimal APIs                  │
             └─────────────┬────────────────────────────┬─────────────┘
                           │                            │
            ┌──────────────▼─────────────┐┌─────────────▼─────────────┐
            │   PostgreSQL 17 + pgvector ││        Redis 7 Alpine     │
            │  (Tenancy, Outbox, Tables) ││  (Streams, Caching, Locks)│
            └──────────────▲─────────────┘└─────────────▲─────────────┘
                           │                            │
             ┌─────────────┴────────────────────────────┴─────────────┐
             │             SGrowing.Worker (.NET 10)                  │
             │    Outbox Dispatcher, Retries, Telemetry Processing    │
             └────────────────────────────────────────────────────────┘
```

---

## 2. Environment Variables

Create a `.env` file at the root of the project:

```env
# Database Credentials
POSTGRES_USER=sgrowing_admin
POSTGRES_PASSWORD=generate_a_secure_random_db_password_32_chars
POSTGRES_DB=sgrowing_production

# JWT Authentication
JWT_SECRET=generate_a_secure_jwt_secret_64_characters_minimum_length_here
JWT_ISSUER=SGrowing.Api
JWT_AUDIENCE=SGrowing.Clients

# Redis Connection
REDIS_CONNECTION=redis:6379

# Domain Configuration
PUBLIC_APP_URL=https://app.sgrowing.io
PUBLIC_API_URL=https://app.sgrowing.io/api
```

---

## 3. Deployment with Coolify

1. **New Service**:
   - In Coolify, create a new Service using **Docker Compose**.
2. **Repository Link**:
   - Point to your private SGrowing Git repository (`main` branch).
   - Set the Compose file path to `docker-compose.prod.yml`.
3. **Environment Configuration**:
   - Paste the production variables from the `.env` template into the Coolify Environment Variables tab.
4. **Deploy**:
   - Click **Deploy**. Coolify will build the .NET 10 API, Background Worker, and Angular 21 NGINX container automatically.
   - Attach your custom domain (e.g. `app.sgrowing.io`) with automatic Let's Encrypt SSL.

---

## 4. Health Checks & Observability

The API exposes two Kubernetes-grade health check endpoints:
- **Liveness Probe**: `GET /healthz/live` (Verifies process is responsive).
- **Readiness Probe**: `GET /healthz/ready` (Verifies PostgreSQL database connection and migrations state).

---

## 5. Background Outbox Worker Lifecycle

- The `worker` container automatically polls `OutboxMessages` with exponential backoff and dispatching.
- Redis streams handle real-time message fanout for SignalR chat hubs and webhook web dispatching.
- All failed outbox events are preserved in database dead-letter state for auditability.
