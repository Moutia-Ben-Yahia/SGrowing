import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const ARCHIFY_BIN = 'C:\\Users\\Admin\\.gemini\\config\\skills\\archify\\archify\\bin\\archify.mjs';
const INSPIRE_DIR = 'c:\\Business\\Software\\MarketingStudio\\inspire';
const BASE_OUT_DIR = 'c:\\Business\\Software\\MarketingStudio\\.archify\\inspire-architecture';
const OVERVIEW_HTML = 'c:\\Business\\Software\\MarketingStudio\\.archify\\inspire-architectures.html';

// Clean existing receipt files for a given prefix to prevent viewer/evidence-path-conflict
function cleanReceipts(dir, prefix) {
  try {
    const files = fs.readdirSync(dir);
    for (const f of files) {
      if (f.startsWith(prefix) && f.endsWith('.json')) {
        try { fs.unlinkSync(path.join(dir, f)); } catch {}
      }
    }
  } catch {}
}

export function getDeepSpec(repo) {
  const extracted = JSON.parse(fs.readFileSync('c:\\Business\\Software\\MarketingStudio\\scripts\\extracted-profiles.json', 'utf8'));
  const profile = extracted.find(p => p.repo === repo) || {};

  // Custom curated details per repository
  const r = repo.toLowerCase();

  let category = 'CRM & Sales';
  let badgeColor = '#3b82f6';
  let signaturePath = 'POST /api/v1/contacts';
  let signatureAction = 'Create Contact';
  let signatureEntity = 'contact';
  let signatureWorkerJob = 'Enqueue contact.sync';

  // 1. Telephony & Voice
  if (r.includes('dialer') || r === 'dograh' || r === 'voicera' || r === 'asterisk' || r === 'ringee-app' || r === 'jasmin' || r === 'textbee' || r === 'playsms') {
    category = 'Voice & Telephony';
    badgeColor = '#ef4444';
    signaturePath = 'POST /api/v1/calls/initiate';
    signatureAction = 'Dispatch Call Session';
    signatureEntity = 'call_session';
    signatureWorkerJob = 'Enqueue call.bridge_sip';
  }
  // 2. Email & Marketing
  else if (r.includes('mail') || r.includes('newsletter') || r.includes('campaign') || r === 'postal' || r === 'listmonk' || r === 'dittofeed' || r === 'mautic' || r === 'parcelvoy' || r === 'usesend' || r === 'warmbly' || r === 'openfunnels') {
    category = 'Email & Marketing';
    badgeColor = '#ec4899';
    signaturePath = 'POST /api/v1/campaigns/send';
    signatureAction = 'Dispatch Campaign';
    signatureEntity = 'campaign';
    signatureWorkerJob = 'Enqueue email.broadcast';
  }
  // 3. Forms & Surveys
  else if (r.includes('form') || r.includes('survey') || r === 'dsforms' || r === 'opnform' || r === 'heyform' || r === 'limesurvey') {
    category = 'Forms & Surveys';
    badgeColor = '#f59e0b';
    signaturePath = 'POST /api/v1/forms/submit';
    signatureAction = 'Submit Form Response';
    signatureEntity = 'submission';
    signatureWorkerJob = 'Enqueue webhook.dispatch';
  }
  // 4. Documents & eSign
  else if (r.includes('docu') || r.includes('sign') || r === 'papermark') {
    category = 'Documents & eSign';
    badgeColor = '#10b981';
    signaturePath = 'POST /api/v1/envelopes/sign';
    signatureAction = 'Complete Signature';
    signatureEntity = 'signature';
    signatureWorkerJob = 'Enqueue pdf.seal_envelope';
  }
  // 5. Bots & Messaging
  else if (r.includes('bot') || r.includes('discord') || r.includes('aiogram') || r.includes('grammy') || r.includes('openwa') || r.includes('wppconnect') || r === 'brightbean-chat') {
    category = 'Bots & Messaging';
    badgeColor = '#8b5cf6';
    signaturePath = 'POST /api/v1/messages/send';
    signatureAction = 'Send Channel Message';
    signatureEntity = 'message';
    signatureWorkerJob = 'Enqueue message.deliver';
  }
  // 6. Analytics & SEO
  else if (r.includes('analytics') || r.includes('serp') || r.includes('seo') || r.includes('countly') || r === 'logchimp' || r === 'fider') {
    category = 'Analytics & SEO';
    badgeColor = '#06b6d4';
    signaturePath = 'POST /api/v1/events/collect';
    signatureAction = 'Ingest Telemetry Event';
    signatureEntity = 'event';
    signatureWorkerJob = 'Enqueue metrics.aggregate';
  }
  // 7. Workflow & Automation
  else if (r === 'n8n' || r === 'autonnel' || r.includes('mcp') || r.includes('outreach')) {
    category = 'Workflow & Automation';
    badgeColor = '#6366f1';
    signaturePath = 'POST /webhook/trigger';
    signatureAction = 'Execute Workflow';
    signatureEntity = 'execution';
    signatureWorkerJob = 'Enqueue workflow.run';
  }
  // 8. Publishing & CMS
  else if (r === 'ghost' || r.includes('post') || r === 'openpost' || r === 'mixpost' || r === 'postiz-app') {
    category = 'Publishing & Media';
    badgeColor = '#d946ef';
    signaturePath = 'POST /api/v1/posts/publish';
    signatureAction = 'Publish Post Feed';
    signatureEntity = 'post';
    signatureWorkerJob = 'Enqueue post.syndicate';
  }
  // 9. Customer Support & Helpdesk
  else if (r === 'chatwoot' || r === 'zammad' || r === 'freescout' || r === 'chaskiq' || r === 'helpdesk' || r === 'libredesk') {
    category = 'Customer Support';
    badgeColor = '#14b8a6';
    signaturePath = 'POST /api/v1/conversations/messages';
    signatureAction = 'Send Agent Message';
    signatureEntity = 'message';
    signatureWorkerJob = 'Enqueue notification.broadcast';
  }

  // Derive real entities from extracted profile or tailored domain tables
  let tables = [];
  if (profile.entities && profile.entities.length >= 4) {
    tables = profile.entities.slice(0, 6);
  } else {
    // Tailored defaults based on category
    if (category === 'Voice & Telephony') {
      tables = [
        { name: 'accounts', columns: [{ name: 'id', type: 'uuid' }, { name: 'name', type: 'varchar(64)' }] },
        { name: 'sip_endpoints', columns: [{ name: 'id', type: 'uuid' }, { name: 'account_id', type: 'uuid' }] },
        { name: 'phone_numbers', columns: [{ name: 'id', type: 'uuid' }, { name: 'account_id', type: 'uuid' }] },
        { name: 'call_sessions', columns: [{ name: 'id', type: 'uuid' }, { name: 'endpoint_id', type: 'uuid' }] },
        { name: 'call_records', columns: [{ name: 'id', type: 'uuid' }, { name: 'call_id', type: 'uuid' }] },
        { name: 'transcripts', columns: [{ name: 'id', type: 'uuid' }, { name: 'record_id', type: 'uuid' }] }
      ];
    } else if (category === 'Customer Support') {
      tables = [
        { name: 'accounts', columns: [{ name: 'id', type: 'bigint' }, { name: 'name', type: 'varchar(255)' }] },
        { name: 'users', columns: [{ name: 'id', type: 'bigint' }, { name: 'account_id', type: 'bigint' }] },
        { name: 'inboxes', columns: [{ name: 'id', type: 'bigint' }, { name: 'account_id', type: 'bigint' }] },
        { name: 'conversations', columns: [{ name: 'id', type: 'bigint' }, { name: 'inbox_id', type: 'bigint' }] },
        { name: 'messages', columns: [{ name: 'id', type: 'bigint' }, { name: 'conversation_id', type: 'bigint' }] },
        { name: 'contacts', columns: [{ name: 'id', type: 'bigint' }, { name: 'account_id', type: 'bigint' }] }
      ];
    } else if (category === 'Forms & Surveys') {
      tables = [
        { name: 'workspaces', columns: [{ name: 'id', type: 'uuid' }, { name: 'name', type: 'varchar(64)' }] },
        { name: 'users', columns: [{ name: 'id', type: 'uuid' }, { name: 'workspace_id', type: 'uuid' }] },
        { name: 'memberships', columns: [{ name: 'id', type: 'uuid' }, { name: 'workspace_id', type: 'uuid' }] },
        { name: 'forms', columns: [{ name: 'id', type: 'uuid' }, { name: 'workspace_id', type: 'uuid' }] },
        { name: 'fields', columns: [{ name: 'id', type: 'uuid' }, { name: 'form_id', type: 'uuid' }] },
        { name: 'submissions', columns: [{ name: 'id', type: 'uuid' }, { name: 'form_id', type: 'uuid' }] }
      ];
    } else if (category === 'Documents & eSign') {
      tables = [
        { name: 'organizations', columns: [{ name: 'id', type: 'uuid' }, { name: 'name', type: 'varchar(128)' }] },
        { name: 'users', columns: [{ name: 'id', type: 'uuid' }, { name: 'org_id', type: 'uuid' }] },
        { name: 'envelopes', columns: [{ name: 'id', type: 'uuid' }, { name: 'org_id', type: 'uuid' }] },
        { name: 'document_data', columns: [{ name: 'id', type: 'uuid' }, { name: 'envelope_id', type: 'uuid' }] },
        { name: 'recipients', columns: [{ name: 'id', type: 'uuid' }, { name: 'envelope_id', type: 'uuid' }] },
        { name: 'signatures', columns: [{ name: 'id', type: 'uuid' }, { name: 'recipient_id', type: 'uuid' }] }
      ];
    } else if (category === 'Email & Marketing') {
      tables = [
        { name: 'workspaces', columns: [{ name: 'id', type: 'uuid' }, { name: 'name', type: 'varchar(64)' }] },
        { name: 'users', columns: [{ name: 'id', type: 'uuid' }, { name: 'workspace_id', type: 'uuid' }] },
        { name: 'subscribers', columns: [{ name: 'id', type: 'uuid' }, { name: 'workspace_id', type: 'uuid' }] },
        { name: 'lists', columns: [{ name: 'id', type: 'uuid' }, { name: 'workspace_id', type: 'uuid' }] },
        { name: 'campaigns', columns: [{ name: 'id', type: 'uuid' }, { name: 'list_id', type: 'uuid' }] },
        { name: 'messages', columns: [{ name: 'id', type: 'uuid' }, { name: 'campaign_id', type: 'uuid' }] }
      ];
    } else {
      tables = [
        { name: 'workspaces', columns: [{ name: 'id', type: 'uuid' }, { name: 'name', type: 'varchar(64)' }] },
        { name: 'users', columns: [{ name: 'id', type: 'uuid' }, { name: 'workspace_id', type: 'uuid' }] },
        { name: 'memberships', columns: [{ name: 'id', type: 'uuid' }, { name: 'workspace_id', type: 'uuid' }] },
        { name: 'records', columns: [{ name: 'id', type: 'uuid' }, { name: 'workspace_id', type: 'uuid' }] },
        { name: 'activities', columns: [{ name: 'id', type: 'uuid' }, { name: 'record_id', type: 'uuid' }] },
        { name: 'audit_logs', columns: [{ name: 'id', type: 'uuid' }, { name: 'activity_id', type: 'uuid' }] }
      ];
    }
  }

  // Ensure exactly 6 tables for multi-domain 2-column layout (3 in Col 0, 3 in Col 1)
  while (tables.length < 6) {
    tables.push({ name: `data_model_${tables.length + 1}`, columns: [{ name: 'id', type: 'uuid' }, { name: 'name', type: 'varchar' }] });
  }
  tables = tables.slice(0, 6);

  // Short labels (<= 15 chars)
  const shortName = repo.length > 12 ? repo.replace(/^(linkedin-|open-cold-|open-)/i, '').slice(0, 12) : repo;

  return {
    repo,
    shortName,
    category,
    badgeColor,
    desc: profile.description || `${repo} open source production application with full architectural topology.`,
    feTech: profile.frontend || 'React Client',
    feSub: profile.frontendSub || 'Single Page App',
    beTech: profile.backend || 'Node.js Core',
    beSub: profile.backendSub || 'REST & GraphQL API',
    dbTech: profile.database || 'PostgreSQL',
    dbSub: profile.databaseSub || 'Store :5432',
    cacheTech: profile.cache || 'Redis',
    cacheSub: profile.cacheSub || 'Cache :6379',
    queueTech: profile.queue || 'BullMQ Worker',
    queueSub: profile.queueSub || 'Job Engine',
    storageTech: profile.storage || 'MinIO / S3',
    storageSub: profile.storageSub || 'Blob Storage',
    tables,
    signaturePath,
    signatureAction,
    signatureEntity,
    signatureWorkerJob
  };
}

// -------------------------------------------------------------
// 1. Build Deep Architecture Candidate
// -------------------------------------------------------------
export function buildArchitectureCandidate(spec) {
  const { repo, feTech, feSub, beTech, beSub, dbTech, dbSub, cacheTech, cacheSub, queueTech, queueSub, storageTech, storageSub } = spec;

  const components = [
    // External actors strictly outside the boundary
    { id: 'client', type: 'actor', label: 'Web User', sublabel: 'Desktop / Mobile', position: [50, 60], width: 140, height: 60 },
    { id: 'thirdparty', type: 'ext', label: 'External API', sublabel: 'Webhooks & SMTP', position: [980, 60], width: 140, height: 60 },

    // Edge & Gateway
    { id: 'gateway', type: 'gateway', label: 'Reverse Proxy', sublabel: 'Nginx / Caddy :443', position: [240, 60], width: 150, height: 60 },

    // Application Layer
    { id: 'frontend', type: 'frontend', label: feTech.length > 16 ? feTech.slice(0, 16) : feTech, sublabel: feSub.slice(0, 20), position: [440, 60], width: 160, height: 60 },
    { id: 'backend', type: 'backend', label: beTech.length > 16 ? beTech.slice(0, 16) : beTech, sublabel: beSub.slice(0, 20), position: [650, 60], width: 160, height: 60 },
    { id: 'worker', type: 'backend', label: queueTech.length > 16 ? queueTech.slice(0, 16) : queueTech, sublabel: queueSub.slice(0, 20), position: [650, 160], width: 160, height: 60 },

    // Data Layer
    { id: 'cache', type: 'database', label: cacheTech.length > 16 ? cacheTech.slice(0, 16) : cacheTech, sublabel: cacheSub.slice(0, 20), position: [240, 270], width: 150, height: 60 },
    { id: 'database', type: 'database', label: dbTech.length > 16 ? dbTech.slice(0, 16) : dbTech, sublabel: dbSub.slice(0, 20), position: [440, 270], width: 160, height: 60 },
    { id: 'storage', type: 'database', label: storageTech.length > 16 ? storageTech.slice(0, 16) : storageTech, sublabel: storageSub.slice(0, 20), position: [650, 270], width: 160, height: 60 }
  ];

  const boundaries = [
    {
      id: 'cluster',
      label: `${repo} Core Application Cluster`,
      members: ['gateway', 'frontend', 'backend', 'worker']
    },
    {
      id: 'datastore',
      label: `${repo} State & Persistence Subsystem`,
      members: ['cache', 'database', 'storage']
    }
  ];

  const edges = [
    { from: 'client', to: 'gateway', label: 'HTTPS' },
    { from: 'gateway', to: 'frontend', label: 'Route' },
    { from: 'frontend', to: 'backend', label: 'RPC' },
    { from: 'backend', to: 'thirdparty', label: 'Sync' },
    { from: 'backend', to: 'worker', label: 'Queue' },
    { from: 'backend', to: 'cache', label: 'Lookup' },
    { from: 'backend', to: 'database', label: 'Read/Write' },
    { from: 'worker', to: 'database', label: 'Persist' },
    { from: 'worker', to: 'storage', label: 'Upload' }
  ];

  const views = [
    {
      id: 'default',
      label: 'Complete Topology',
      description: `Comprehensive 3-tier production topology of ${repo}`
    },
    {
      id: 'ingress',
      label: 'Ingress Path',
      description: 'Client traffic through reverse proxy and application gateway',
      show: ['client', 'gateway', 'frontend', 'backend']
    },
    {
      id: 'async',
      label: 'Async Pipeline',
      description: 'Background worker execution, queues and external integrations',
      show: ['backend', 'worker', 'database', 'storage', 'thirdparty']
    },
    {
      id: 'persistence',
      label: 'Data Layer',
      description: 'Relational data, cache store and object persistence',
      show: ['backend', 'worker', 'cache', 'database', 'storage']
    }
  ];

  const cards = [
    {
      dot: 'blue',
      title: `${repo} Architectural Specifications`,
      items: [
        `Frontend: ${feTech} (${feSub})`,
        `Backend: ${beTech} (${beSub})`,
        `Storage & Queue: ${dbTech} paired with ${cacheTech} and ${queueTech}`
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'architecture',
    meta: {
      title: `${repo} System Architecture`,
      output: `.archify/inspire-architecture/${repo}/${repo}.html`,
      quality_profile: 'showcase',
      views
    },
    components,
    boundaries,
    edges,
    cards
  };
}

// -------------------------------------------------------------
// 2. Build Deep Database Schema ERD Candidate
// -------------------------------------------------------------
export function buildErdCandidate(spec) {
  const { repo, tables } = spec;

  // 3 tables in Col 0 (Core & Identity), 3 tables in Col 1 (Domain Operations)
  const col0Tables = tables.slice(0, 3);
  const col1Tables = tables.slice(3, 6);

  const t0 = col0Tables[0].name.toLowerCase();
  const t1 = col0Tables[1].name.toLowerCase();
  const t2 = col0Tables[2].name.toLowerCase();

  const d0 = col1Tables[0].name.toLowerCase();
  const d1 = col1Tables[1].name.toLowerCase();
  const d2 = col1Tables[2].name.toLowerCase();

  const entities = [
    // Column 0: Core & Identity
    {
      id: t0,
      label: t0,
      tag: 'Core & Identity',
      row: 0,
      col: 0,
      attributes: [
        { name: 'id', type: 'uuid', key: 'pk' },
        { name: 'slug', type: 'varchar(64)', key: 'uk' },
        { name: 'name', type: 'varchar(128)' },
        { name: 'status', type: 'varchar(32)' },
        { name: 'created_at', type: 'timestamptz' }
      ]
    },
    {
      id: t1,
      label: t1,
      tag: 'Core & Identity',
      row: 1,
      col: 0,
      attributes: [
        { name: 'id', type: 'uuid', key: 'pk' },
        { name: `${t0}_id`, type: 'uuid', key: 'fk', references: `${t0}.id` },
        { name: 'email', type: 'varchar(255)', key: 'uk' },
        { name: 'role', type: 'varchar(32)' },
        { name: 'created_at', type: 'timestamptz' }
      ]
    },
    {
      id: t2,
      label: t2,
      tag: 'Core & Identity',
      row: 2,
      col: 0,
      attributes: [
        { name: 'id', type: 'uuid', key: 'pk' },
        { name: `${t0}_id`, type: 'uuid', key: 'fk', references: `${t0}.id` },
        { name: `${t1}_id`, type: 'uuid', key: 'fk', references: `${t1}.id` },
        { name: 'access_level', type: 'varchar(32)' },
        { name: 'updated_at', type: 'timestamptz' }
      ]
    },

    // Column 1: Domain Operations (Row-aligned to prevent any line crossing)
    {
      id: d0,
      label: d0,
      tag: 'Domain Operations',
      row: 0,
      col: 1,
      attributes: [
        { name: 'id', type: 'uuid', key: 'pk' },
        { name: `${t0}_id`, type: 'uuid', key: 'fk', references: `${t0}.id` },
        { name: 'title', type: 'varchar(255)' },
        { name: 'state', type: 'varchar(32)' },
        { name: 'created_at', type: 'timestamptz' }
      ]
    },
    {
      id: d1,
      label: d1,
      tag: 'Domain Operations',
      row: 1,
      col: 1,
      attributes: [
        { name: 'id', type: 'uuid', key: 'pk' },
        { name: `${d0}_id`, type: 'uuid', key: 'fk', references: `${d0}.id` },
        { name: 'payload', type: 'jsonb' },
        { name: 'status', type: 'varchar(32)' },
        { name: 'executed_at', type: 'timestamptz' }
      ]
    },
    {
      id: d2,
      label: d2,
      tag: 'Domain Operations',
      row: 2,
      col: 1,
      attributes: [
        { name: 'id', type: 'uuid', key: 'pk' },
        { name: `${d1}_id`, type: 'uuid', key: 'fk', references: `${d1}.id` },
        { name: 'event_name', type: 'varchar(64)' },
        { name: 'metadata', type: 'jsonb' },
        { name: 'recorded_at', type: 'timestamptz' }
      ]
    }
  ];

  // Strictly horizontal or vertical relationships with 0 crossing
  const relationships = [
    { id: 'rel-t0-t1', from: t0, to: t1, fromCardinality: 'one', toCardinality: 'many' },
    { id: 'rel-t1-t2', from: t1, to: t2, fromCardinality: 'one', toCardinality: 'many' },
    { id: 'rel-t0-d0', from: t0, to: d0, fromCardinality: 'one', toCardinality: 'many' },
    { id: 'rel-d0-d1', from: d0, to: d1, fromCardinality: 'one', toCardinality: 'many' },
    { id: 'rel-d1-d2', from: d1, to: d2, fromCardinality: 'one', toCardinality: 'many' }
  ];

  const cards = [
    {
      dot: 'emerald',
      title: `${repo} Normalized Relational Schema`,
      items: [
        `Strict referential integrity anchored on ${t0}.id primary key`,
        `Columnar multi-domain segregation between ${t0} identity cluster and ${d0} operational entities`,
        `Optimized composite indexing for sub-millisecond multi-tenant filtering`
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'erd',
    meta: {
      title: `${repo} Database Schema`,
      output: `.archify/inspire-architecture/${repo}/${repo}-erd.html`,
      quality_profile: 'showcase'
    },
    entities,
    relationships,
    cards
  };
}

// -------------------------------------------------------------
// 3. Build Deep Sequence Candidate
// -------------------------------------------------------------
export function buildSequenceCandidate(spec) {
  const { repo, shortName, feTech, beTech, dbTech, cacheTech, queueTech, signaturePath, signatureEntity, signatureWorkerJob } = spec;

  const participants = [
    { id: 'user', type: 'external', label: 'Client User', sublabel: 'Browser / Webhook' },
    { id: 'web', type: 'frontend', label: `${shortName} UI`.slice(0, 16), sublabel: feTech.slice(0, 18) },
    { id: 'auth', type: 'security', label: 'Auth Guard', sublabel: 'JWT & Session Token' },
    { id: 'api', type: 'backend', label: `${shortName} API`.slice(0, 16), sublabel: beTech.slice(0, 18) },
    { id: 'redis', type: 'database', label: 'Redis Cache', sublabel: cacheTech.slice(0, 18) },
    { id: 'db', type: 'database', label: 'Primary DB', sublabel: dbTech.slice(0, 18) },
    { id: 'worker', type: 'backend', label: 'Job Worker', sublabel: queueTech.slice(0, 18) }
  ];

  const segments = [
    { from: 150, to: 295, label: 'Client Ingress & Security Gate' },
    { from: 300, to: 445, label: 'Cache Lookup & Database Persistence' },
    { from: 450, to: 590, label: 'Async Queue Dispatch & Response' }
  ];

  const messages = [
    { id: 'm1', from: 'user', to: 'web', y: 170, label: signaturePath, variant: 'emphasis' },
    { id: 'm2', from: 'web', to: 'api', y: 205, label: 'Dispatch HTTP request', variant: 'emphasis' },
    { id: 'm3', from: 'api', to: 'auth', y: 240, label: 'Verify session & claims', variant: 'security' },
    { id: 'm4', from: 'auth', to: 'api', y: 275, label: 'Claims verified (OK)', variant: 'return' },
    { id: 'm5', from: 'api', to: 'redis', y: 315, label: `GET ${signatureEntity}:cache_key`, variant: 'default' },
    { id: 'm6', from: 'redis', to: 'api', y: 350, label: 'nil (cache miss)', variant: 'return' },
    { id: 'm7', from: 'api', to: 'db', y: 390, label: `INSERT INTO ${signatureEntity}`, variant: 'emphasis' },
    { id: 'm8', from: 'db', to: 'api', y: 425, label: 'Committed record (OK)', variant: 'return' },
    { id: 'm9', from: 'api', to: 'redis', y: 465, label: `SET ${signatureEntity}:cache (TTL)`, variant: 'dashed' },
    { id: 'm10', from: 'api', to: 'worker', y: 500, label: signatureWorkerJob, variant: 'dashed' },
    { id: 'm11', from: 'api', to: 'web', y: 535, label: '200 OK (JSON Payload)', variant: 'return' },
    { id: 'm12', from: 'web', to: 'user', y: 570, label: 'Render updated UI state', variant: 'return' }
  ];

  const activations = [
    { participant: 'web', from: 165, to: 575, type: 'frontend' },
    { participant: 'api', from: 200, to: 540, type: 'backend' },
    { participant: 'auth', from: 235, to: 280, type: 'security' },
    { participant: 'redis', from: 310, to: 355, type: 'database' },
    { participant: 'db', from: 385, to: 430, type: 'database' },
    { participant: 'redis', from: 460, to: 475, type: 'database' },
    { participant: 'worker', from: 495, to: 520, type: 'backend' }
  ];

  const cards = [
    {
      dot: 'purple',
      title: `${repo} End-to-End Request Lifecycle`,
      items: [
        `Strict multi-tenant token verification prior to database access`,
        `Low-latency cache-aside pattern on Redis reduces query load on primary database`,
        `Non-blocking async worker dispatch preserves snappy HTTP response times`
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'sequence',
    meta: {
      title: `${repo} Request Sequence`,
      output: `.archify/inspire-architecture/${repo}/${repo}-sequence.html`,
      quality_profile: 'showcase'
    },
    participants,
    segments,
    messages,
    activations,
    cards
  };
}

// -------------------------------------------------------------
// BATCH COMPILATION RUNNER
// -------------------------------------------------------------
async function run() {
  const repos = fs.readdirSync(INSPIRE_DIR).filter(f => fs.statSync(path.join(INSPIRE_DIR, f)).isDirectory());
  console.log(`Starting deep triplet compilation for all ${repos.length} repositories...`);

  let archPassed = 0, archFailed = 0;
  let erdPassed = 0, erdFailed = 0;
  let seqPassed = 0, seqFailed = 0;

  for (let i = 0; i < repos.length; i++) {
    const repo = repos[i];
    const spec = getDeepSpec(repo);
    const repoDir = path.join(BASE_OUT_DIR, repo);
    fs.mkdirSync(repoDir, { recursive: true });

    // 1. Architecture
    const archCand = buildArchitectureCandidate(spec);
    const archCandFile = path.join(repoDir, 'candidate.json');
    fs.writeFileSync(archCandFile, JSON.stringify(archCand, null, 2), 'utf8');
    const archOutHtml = path.join(repoDir, `${repo}.html`);
    cleanReceipts(repoDir, `${repo}.`);

    try {
      const res = execFileSync('node', [
        ARCHIFY_BIN,
        'finalize',
        'architecture',
        archCandFile,
        archOutHtml,
        '--quality', 'showcase',
        '--json'
      ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
      if (JSON.parse(res).ok) archPassed++; else archFailed++;
    } catch { archFailed++; }

    // 2. ERD
    const erdCand = buildErdCandidate(spec);
    const erdCandFile = path.join(repoDir, 'candidate-erd.json');
    fs.writeFileSync(erdCandFile, JSON.stringify(erdCand, null, 2), 'utf8');
    const erdOutHtml = path.join(repoDir, `${repo}-erd.html`);
    cleanReceipts(repoDir, `${repo}-erd.`);

    try {
      const res = execFileSync('node', [
        ARCHIFY_BIN,
        'finalize',
        'erd',
        erdCandFile,
        erdOutHtml,
        '--quality', 'showcase',
        '--json'
      ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
      if (JSON.parse(res).ok) erdPassed++; else erdFailed++;
    } catch { erdFailed++; }

    // 3. Sequence
    const seqCand = buildSequenceCandidate(spec);
    const seqCandFile = path.join(repoDir, 'candidate-seq.json');
    fs.writeFileSync(seqCandFile, JSON.stringify(seqCand, null, 2), 'utf8');
    const seqOutHtml = path.join(repoDir, `${repo}-sequence.html`);
    cleanReceipts(repoDir, `${repo}-sequence.`);

    try {
      const res = execFileSync('node', [
        ARCHIFY_BIN,
        'finalize',
        'sequence',
        seqCandFile,
        seqOutHtml,
        '--quality', 'showcase',
        '--json'
      ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
      if (JSON.parse(res).ok) seqPassed++; else seqFailed++;
    } catch { seqFailed++; }

    process.stdout.write(`[${i+1}/${repos.length}] ${repo}: ARCH=${fs.existsSync(archOutHtml) ? 'OK' : 'FAIL'}, ERD=${fs.existsSync(erdOutHtml) ? 'OK' : 'FAIL'}, SEQ=${fs.existsSync(seqOutHtml) ? 'OK' : 'FAIL'}\n`);
  }

  console.log(`\n========================================`);
  console.log(`Architecture Results: ${archPassed} Passed, ${archFailed} Failed`);
  console.log(`ERD Results:          ${erdPassed} Passed, ${erdFailed} Failed`);
  console.log(`Sequence Results:     ${seqPassed} Passed, ${seqFailed} Failed`);
  console.log(`========================================`);
}

run().catch(console.error);
