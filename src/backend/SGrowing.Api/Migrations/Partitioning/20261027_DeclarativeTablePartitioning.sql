-- ==============================================================================
-- SGrowing Enterprise Schema Migration: Declarative Table Partitioning by Monthly Range
-- Tables: TelemetryEvents, MessageLogs, WarmupExchangeLogs, AuditLogs
-- Target Engine: PostgreSQL 15+ / 16+ Declarative Partitioning
-- ==============================================================================

-- 1. TELEMETRY EVENTS PARTITIONED TABLE
-- Primary key in partitioned tables must include the partition key (TimestampUtc)
CREATE TABLE IF NOT EXISTS "TelemetryEvents_Partitioned" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SessionId" character varying(100) NOT NULL,
    "VisitorId" character varying(100) NOT NULL,
    "EventName" character varying(150) NOT NULL,
    "Url" text NOT NULL,
    "Referrer" text NOT NULL,
    "DeviceType" character varying(50) NOT NULL,
    "Browser" character varying(50) NOT NULL,
    "CountryCode" character varying(10) NOT NULL,
    "City" character varying(100) NOT NULL,
    "PropertiesJson" text NOT NULL,
    "TimestampUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TelemetryEvents_Partitioned" PRIMARY KEY ("Id", "TimestampUtc")
) PARTITION BY RANGE ("TimestampUtc");

-- Monthly partitions for TelemetryEvents
CREATE TABLE IF NOT EXISTS "TelemetryEvents_2026_10" PARTITION OF "TelemetryEvents_Partitioned"
    FOR VALUES FROM ('2026-10-01 00:00:00+00') TO ('2026-11-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "TelemetryEvents_2026_11" PARTITION OF "TelemetryEvents_Partitioned"
    FOR VALUES FROM ('2026-11-01 00:00:00+00') TO ('2026-12-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "TelemetryEvents_2026_12" PARTITION OF "TelemetryEvents_Partitioned"
    FOR VALUES FROM ('2026-12-01 00:00:00+00') TO ('2027-01-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "TelemetryEvents_Default" PARTITION OF "TelemetryEvents_Partitioned"
    DEFAULT;

CREATE INDEX IF NOT EXISTS "IX_TelemetryEvents_Tenant_Time" 
    ON "TelemetryEvents_Partitioned" ("TenantId", "TimestampUtc" DESC);

-- 2. MESSAGE LOGS PARTITIONED TABLE
CREATE TABLE IF NOT EXISTS "MessageLogs_Partitioned" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "SenderId" uuid NULL,
    "SenderType" integer NOT NULL,
    "Channel" integer NOT NULL,
    "Direction" integer NOT NULL,
    "Content" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MessageLogs_Partitioned" PRIMARY KEY ("Id", "CreatedAtUtc")
) PARTITION BY RANGE ("CreatedAtUtc");

CREATE TABLE IF NOT EXISTS "MessageLogs_2026_10" PARTITION OF "MessageLogs_Partitioned"
    FOR VALUES FROM ('2026-10-01 00:00:00+00') TO ('2026-11-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "MessageLogs_2026_11" PARTITION OF "MessageLogs_Partitioned"
    FOR VALUES FROM ('2026-11-01 00:00:00+00') TO ('2026-12-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "MessageLogs_2026_12" PARTITION OF "MessageLogs_Partitioned"
    FOR VALUES FROM ('2026-12-01 00:00:00+00') TO ('2027-01-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "MessageLogs_Default" PARTITION OF "MessageLogs_Partitioned"
    DEFAULT;

CREATE INDEX IF NOT EXISTS "IX_MessageLogs_Tenant_Created" 
    ON "MessageLogs_Partitioned" ("TenantId", "CreatedAtUtc" DESC);

-- 3. WARMUP EXCHANGE LOGS PARTITIONED TABLE
CREATE TABLE IF NOT EXISTS "WarmupExchangeLogs_Partitioned" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SenderEmail" character varying(200) NOT NULL,
    "RecipientEmail" character varying(200) NOT NULL,
    "Subject" character varying(300) NOT NULL,
    "Status" character varying(50) NOT NULL,
    "WasRescuedFromSpam" boolean NOT NULL,
    "DkimValid" boolean NOT NULL,
    "SpfValid" boolean NOT NULL,
    "ExchangedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WarmupExchangeLogs_Partitioned" PRIMARY KEY ("Id", "ExchangedAtUtc")
) PARTITION BY RANGE ("ExchangedAtUtc");

CREATE TABLE IF NOT EXISTS "WarmupExchangeLogs_2026_10" PARTITION OF "WarmupExchangeLogs_Partitioned"
    FOR VALUES FROM ('2026-10-01 00:00:00+00') TO ('2026-11-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "WarmupExchangeLogs_2026_11" PARTITION OF "WarmupExchangeLogs_Partitioned"
    FOR VALUES FROM ('2026-11-01 00:00:00+00') TO ('2026-12-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "WarmupExchangeLogs_2026_12" PARTITION OF "WarmupExchangeLogs_Partitioned"
    FOR VALUES FROM ('2026-12-01 00:00:00+00') TO ('2027-01-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "WarmupExchangeLogs_Default" PARTITION OF "WarmupExchangeLogs_Partitioned"
    DEFAULT;

CREATE INDEX IF NOT EXISTS "IX_WarmupExchangeLogs_Tenant_Time" 
    ON "WarmupExchangeLogs_Partitioned" ("TenantId", "ExchangedAtUtc" DESC);

-- 4. SECURITY AUDIT LOGS PARTITIONED TABLE
CREATE TABLE IF NOT EXISTS "AuditLogs_Partitioned" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "UserId" character varying(100) NULL,
    "Action" character varying(100) NOT NULL,
    "Resource" character varying(200) NOT NULL,
    "IpAddress" character varying(50) NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AuditLogs_Partitioned" PRIMARY KEY ("Id", "OccurredAtUtc")
) PARTITION BY RANGE ("OccurredAtUtc");

CREATE TABLE IF NOT EXISTS "AuditLogs_2026_10" PARTITION OF "AuditLogs_Partitioned"
    FOR VALUES FROM ('2026-10-01 00:00:00+00') TO ('2026-11-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "AuditLogs_2026_11" PARTITION OF "AuditLogs_Partitioned"
    FOR VALUES FROM ('2026-11-01 00:00:00+00') TO ('2026-12-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "AuditLogs_2026_12" PARTITION OF "AuditLogs_Partitioned"
    FOR VALUES FROM ('2026-12-01 00:00:00+00') TO ('2027-01-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS "AuditLogs_Default" PARTITION OF "AuditLogs_Partitioned"
    DEFAULT;

CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Tenant_Time" 
    ON "AuditLogs_Partitioned" ("TenantId", "OccurredAtUtc" DESC);
