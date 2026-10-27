import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const ARCHIFY_BIN = 'C:\\Users\\Admin\\.gemini\\config\\skills\\archify\\archify\\bin\\archify.mjs';
const BASE_OUT_DIR = 'c:\\Business\\Software\\SGrowing\\.archify\\inspire-architecture';

fs.mkdirSync(BASE_OUT_DIR, { recursive: true });

function cleanAllReceipts(dir) {
  try {
    const files = fs.readdirSync(dir);
    for (const f of files) {
      if (f.endsWith('.json') && !f.startsWith('candidate')) {
        try { fs.unlinkSync(path.join(dir, f)); } catch {}
      }
    }
  } catch {}
}

const specs = [
  {
    repoName: 'evo-nexus',
    shortName: 'EvoNexus',
    category: 'Conversational AI & Bots',
    badgeColor: '#00ffa7',
    actorLabel: 'Claude Code CLI',
    actorSub: 'Multi-Model Terminal',
    feLabel: 'Dashboard UI',
    feSub: 'Flask & HTML5 UI',
    beLabel: 'Nexus Hub',
    beSub: 'Agent Operating Layer',
    dbLabel: 'Dashboard DB',
    dbSub: 'SQLite / PostgreSQL',
    cacheLabel: 'Plugin Cache',
    cacheSub: 'Skills & Tools Cache',
    queueLabel: 'Routines Engine',
    queueSub: 'Scheduler & Heartbeat',
    storageLabel: 'Brain Storage',
    storageSub: 'Markdown Knowledge',
    tables: [
      { name: 'users' },
      { name: 'roles' },
      { name: 'missions' },
      { name: 'goals' },
      { name: 'goal_tasks' },
      { name: 'tickets' }
    ],
    signaturePath: '/api/heartbeat/run',
    signatureEntity: 'mission',
    signatureWorkerJob: 'Execute agent routine'
  },
  {
    repoName: 'ever-works',
    shortName: 'EverWorks',
    category: 'Autonomous AI Platforms',
    badgeColor: '#3b82f6',
    actorLabel: 'Web / Admin User',
    actorSub: 'Next.js 15+ Browser UI',
    feLabel: 'Works Web App',
    feSub: 'React 19 & Tailwind',
    beLabel: 'Works API Engine',
    beSub: 'NestJS Microservices',
    dbLabel: 'Works DB',
    dbSub: 'PostgreSQL / TypeORM',
    cacheLabel: 'Redis State',
    cacheSub: 'Sessions & Throttling',
    queueLabel: 'Trigger Tasks',
    queueSub: 'Trigger.dev v3 Workflows',
    storageLabel: 'Git & S3 Store',
    storageSub: 'Repos & Artifacts',
    tables: [
      { name: 'organizations' },
      { name: 'users' },
      { name: 'works' },
      { name: 'projects' },
      { name: 'tasks' },
      { name: 'runs' }
    ],
    signaturePath: '/api/v1/works/:id/pipeline',
    signatureEntity: 'work',
    signatureWorkerJob: 'Dispatch 15-step generator'
  }
];

function buildArchCandidate(spec) {
  const { repoName, actorLabel, actorSub, feLabel, feSub, beLabel, beSub, dbLabel, dbSub, cacheLabel, cacheSub, queueLabel, queueSub, storageLabel, storageSub } = spec;

  const components = [
    { id: 'actor', type: 'external', label: actorLabel.slice(0, 15), sublabel: actorSub.slice(0, 20), pos: [40, 50], size: [130, 60] },
    { id: 'front', type: 'frontend', label: feLabel.slice(0, 15), sublabel: feSub.slice(0, 20), pos: [260, 50], size: [140, 60] },
    { id: 'api', type: 'backend', label: beLabel.slice(0, 15), sublabel: beSub.slice(0, 20), pos: [490, 50], size: [150, 60] },
    { id: 'queue', type: 'messagebus', label: queueLabel.slice(0, 15), sublabel: queueSub.slice(0, 20), pos: [730, 50], size: [140, 60] },
    { id: 'ext', type: 'external', label: 'External AI APIs', sublabel: 'LLM & Search Gateways', pos: [970, 50], size: [150, 60] },

    { id: 'auth', type: 'security', label: 'Auth & RBAC', sublabel: 'Tokens & Permissions', pos: [260, 180], size: [140, 60] },
    { id: 'domain', type: 'backend', label: 'Domain Core', sublabel: 'Orchestrator Services', pos: [490, 180], size: [150, 60] },
    { id: 'worker', type: 'backend', label: 'Async Workers', sublabel: 'Background Tasks', pos: [730, 180], size: [140, 60] },

    { id: 'cache', type: 'database', label: cacheLabel.slice(0, 15), sublabel: cacheSub.slice(0, 20), pos: [260, 310], size: [140, 60] },
    { id: 'db', type: 'database', label: dbLabel.slice(0, 15), sublabel: dbSub.slice(0, 20), pos: [490, 310], size: [150, 60] },
    { id: 'storage', type: 'cloud', label: storageLabel.slice(0, 15), sublabel: storageSub.slice(0, 20), pos: [730, 310], size: [140, 60] }
  ];

  const boundaries = [
    {
      kind: 'region',
      label: `${spec.shortName} Platform Boundary`,
      wraps: ['front', 'api', 'queue', 'auth', 'domain', 'worker', 'cache', 'db', 'storage']
    }
  ];

  const connections = [
    { id: 'c1', from: 'actor', to: 'front', label: 'HTTP / API', variant: 'emphasis' },
    { id: 'c2', from: 'front', to: 'api', label: 'RPC / REST' },
    { id: 'c3', from: 'front', to: 'auth', label: 'Verify', variant: 'security' },
    { id: 'c4', from: 'api', to: 'domain', label: 'Dispatch' },
    { id: 'c5', from: 'api', to: 'cache', label: 'Session' },
    { id: 'c6', from: 'api', to: 'queue', label: 'Enqueue', variant: 'dashed' },
    { id: 'c7', from: 'api', to: 'ext', label: 'Integrations', variant: 'dashed', fromSide: 'top', toSide: 'top' },
    { id: 'c8', from: 'domain', to: 'db', label: 'Persist' },
    { id: 'c9', from: 'queue', to: 'worker', label: 'Consume' },
    { id: 'c10', from: 'worker', to: 'db', label: 'Save Run' },
    { id: 'c11', from: 'worker', to: 'storage', label: 'Upload', variant: 'dashed' }
  ];

  const views = [
    {
      id: 'ingress',
      label: 'Ingress & Interaction Layer',
      focus: ['actor', 'front', 'auth', 'api'],
      note: 'User requests, API keys, and CLI commands routed into the central orchestrator.'
    },
    {
      id: 'core-processing',
      label: 'Domain Services & Execution Pipeline',
      focus: ['api', 'domain', 'queue', 'worker'],
      note: 'Agent coordination, routine dispatching, and background workflow pipelines.'
    },
    {
      id: 'persistence-subsystem',
      label: 'Database & Storage Subsystem',
      focus: ['api', 'domain', 'worker', 'cache', 'db', 'storage'],
      note: 'Relational data stores, Redis caches, and persistent object/file repositories.'
    },
    {
      id: 'external-integrations',
      label: 'External Gateways & Model Providers',
      focus: ['api', 'worker', 'ext'],
      note: 'Model Context Protocol (MCP) integrations, LLM providers, and external webhooks.'
    }
  ];

  const cards = [
    {
      dot: 'cyan',
      title: `${spec.shortName} Architecture Topology`,
      items: [
        `Ingress: High-throughput HTTP API and reactive Web/CLI clients`,
        `Orchestration: Autonomous task scheduling with multi-agent coordination`,
        `Resilience: Decoupled message queues and persistent state storage`
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'architecture',
    meta: {
      title: `${spec.shortName} System Architecture`,
      output: `.archify/inspire-architecture/${repoName}/${repoName}.html`,
      quality_profile: 'showcase',
      views
    },
    components,
    boundaries,
    connections,
    cards
  };
}

function buildErdCandidate(spec) {
  const { repoName, shortName, tables } = spec;

  const t0 = tables[0].name;
  const t1 = tables[1].name;
  const t2 = tables[2].name;

  const d0 = tables[3].name;
  const d1 = tables[4].name;
  const d2 = tables[5].name;

  const entities = [
    {
      id: t0,
      label: t0,
      tag: 'Core & Identity',
      row: 0,
      col: 0,
      attributes: [
        { name: 'id', type: 'uuid', key: 'pk' },
        { name: 'name', type: 'varchar(120)', key: 'uk' },
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
        { name: 'role_name', type: 'varchar(64)' },
        { name: 'permissions', type: 'jsonb' },
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
        { name: 'title', type: 'varchar(255)' },
        { name: 'type', type: 'varchar(50)' },
        { name: 'updated_at', type: 'timestamptz' }
      ]
    },
    {
      id: d0,
      label: d0,
      tag: 'Domain Operations',
      row: 0,
      col: 1,
      attributes: [
        { name: 'id', type: 'uuid', key: 'pk' },
        { name: `${t0}_id`, type: 'uuid', key: 'fk', references: `${t0}.id` },
        { name: 'slug', type: 'varchar(128)', key: 'uk' },
        { name: 'status', type: 'varchar(32)' },
        { name: 'progress', type: 'integer' }
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
        { name: 'task_key', type: 'varchar(64)', key: 'uk' },
        { name: 'params', type: 'jsonb' },
        { name: 'assigned_agent', type: 'varchar(64)' }
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
        { name: 'result_code', type: 'varchar(32)' },
        { name: 'metrics', type: 'jsonb' },
        { name: 'completed_at', type: 'timestamptz' }
      ]
    }
  ];

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
      title: `${shortName} Data Model Architecture`,
      items: [
        `Normalized relational schema anchored on ${t0}.id primary key`,
        `Explicit isolation between identity management (${t0}, ${t1}) and operational models (${d0}, ${d1})`,
        `Audit-ready execution tracking and outcome telemetry (${d2})`
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'erd',
    meta: {
      title: `${shortName} State Schema`,
      output: `.archify/inspire-architecture/${repoName}/${repoName}-erd.html`,
      quality_profile: 'showcase'
    },
    entities,
    relationships,
    cards
  };
}

function buildSeqCandidate(spec) {
  const { repoName, shortName, feLabel, beLabel, dbLabel, cacheLabel, queueLabel, signaturePath, signatureEntity, signatureWorkerJob } = spec;

  const participants = [
    { id: 'user', type: 'external', label: 'Operator / Client', sublabel: 'HTTP Ingress / User' },
    { id: 'web', type: 'frontend', label: `${shortName} UI`.slice(0, 16), sublabel: feLabel.slice(0, 18) },
    { id: 'auth', type: 'security', label: 'Auth Gateway', sublabel: 'Token & API Key' },
    { id: 'api', type: 'backend', label: `${shortName} API`.slice(0, 16), sublabel: beLabel.slice(0, 18) },
    { id: 'redis', type: 'database', label: 'State Cache', sublabel: cacheLabel.slice(0, 18) },
    { id: 'db', type: 'database', label: 'Primary DB', sublabel: dbLabel.slice(0, 18) },
    { id: 'worker', type: 'backend', label: 'Task Engine', sublabel: queueLabel.slice(0, 18) }
  ];

  const segments = [
    { from: 150, to: 295, label: 'Authentication & Ingress Routing' },
    { from: 300, to: 445, label: 'Domain Validation & State Persistence' },
    { from: 450, to: 590, label: 'Asynchronous Workflow & Agent Dispatch' }
  ];

  const messages = [
    { id: 'm1', from: 'user', to: 'web', y: 170, label: `Trigger action ${signaturePath}`, variant: 'emphasis' },
    { id: 'm2', from: 'web', to: 'api', y: 205, label: 'Forward request with Auth headers', variant: 'emphasis' },
    { id: 'm3', from: 'api', to: 'auth', y: 240, label: 'Validate Session & Tool Grants', variant: 'security' },
    { id: 'm4', from: 'auth', to: 'api', y: 275, label: 'Identity verified (OK)', variant: 'return' },
    { id: 'm5', from: 'api', to: 'redis', y: 315, label: `CHECK rate_limit & active lock`, variant: 'default' },
    { id: 'm6', from: 'redis', to: 'api', y: 350, label: 'Lock acquired', variant: 'return' },
    { id: 'm7', from: 'api', to: 'db', y: 390, label: `INSERT / UPDATE ${signatureEntity}`, variant: 'emphasis' },
    { id: 'm8', from: 'db', to: 'api', y: 425, label: 'Committed state (OK)', variant: 'return' },
    { id: 'm9', from: 'api', to: 'redis', y: 465, label: `SET active_job:${signatureEntity}`, variant: 'dashed' },
    { id: 'm10', from: 'api', to: 'worker', y: 500, label: signatureWorkerJob, variant: 'dashed' },
    { id: 'm11', from: 'api', to: 'web', y: 535, label: '202 Accepted (Job Enqueued)', variant: 'return' },
    { id: 'm12', from: 'worker', to: 'user', y: 570, label: 'Notify completion via SSE / Webhook', variant: 'return' }
  ];

  const activations = [
    { participant: 'web', from: 165, to: 575, type: 'frontend' },
    { participant: 'api', from: 200, to: 540, type: 'backend' },
    { participant: 'auth', from: 235, to: 280, type: 'security' },
    { participant: 'redis', from: 310, to: 355, type: 'database' },
    { participant: 'db', from: 385, to: 430, type: 'database' },
    { participant: 'redis', from: 460, to: 475, type: 'database' },
    { participant: 'worker', from: 495, to: 580, type: 'backend' }
  ];

  const cards = [
    {
      dot: 'violet',
      title: `${shortName} Orchestration Lifecycle`,
      items: [
        'Deterministic authentication and quota validation prior to workflow execution',
        'Stateful persistence ensuring idempotent execution across system restarts',
        'Decoupled background task workers preventing API latency spikes'
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'sequence',
    meta: {
      title: `${shortName} Request Sequence`,
      output: `.archify/inspire-architecture/${repoName}/${repoName}-sequence.html`,
      quality_profile: 'showcase'
    },
    participants,
    segments,
    messages,
    activations,
    cards
  };
}

for (const spec of specs) {
  const { repoName } = spec;
  const repoDir = path.join(BASE_OUT_DIR, repoName);
  fs.mkdirSync(repoDir, { recursive: true });
  cleanAllReceipts(repoDir);

  console.log(`Generating archify triplets for ${spec.repoName}...`);

  // 1. Architecture
  const archCand = buildArchCandidate(spec);
  const archCandPath = path.join(repoDir, 'candidate.json');
  fs.writeFileSync(archCandPath, JSON.stringify(archCand, null, 2), 'utf-8');
  const archOutHtml = path.join(repoDir, `${repoName}.html`);

  try {
    execFileSync('node', [
      ARCHIFY_BIN,
      'finalize',
      'architecture',
      archCandPath,
      archOutHtml,
      '--quality', 'showcase',
      '--json'
    ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
    console.log(`[OK] ${spec.shortName} Architecture -> ${archOutHtml}`);
  } catch (e) {
    console.error(`[ERR] ${spec.shortName} Architecture:`, e.stderr || e.stdout || e.message);
  }

  // 2. ERD
  const erdCand = buildErdCandidate(spec);
  const erdCandPath = path.join(repoDir, 'candidate-erd.json');
  fs.writeFileSync(erdCandPath, JSON.stringify(erdCand, null, 2), 'utf-8');
  const erdOutHtml = path.join(repoDir, `${repoName}-erd.html`);

  try {
    execFileSync('node', [
      ARCHIFY_BIN,
      'finalize',
      'erd',
      erdCandPath,
      erdOutHtml,
      '--quality', 'showcase',
      '--json'
    ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
    console.log(`[OK] ${spec.shortName} ERD -> ${erdOutHtml}`);
  } catch (e) {
    console.error(`[ERR] ${spec.shortName} ERD:`, e.stderr || e.stdout || e.message);
  }

  // 3. Sequence
  const seqCand = buildSeqCandidate(spec);
  const seqCandPath = path.join(repoDir, 'candidate-sequence.json');
  fs.writeFileSync(seqCandPath, JSON.stringify(seqCand, null, 2), 'utf-8');
  const seqOutHtml = path.join(repoDir, `${repoName}-sequence.html`);

  try {
    execFileSync('node', [
      ARCHIFY_BIN,
      'finalize',
      'sequence',
      seqCandPath,
      seqOutHtml,
      '--quality', 'showcase',
      '--json'
    ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
    console.log(`[OK] ${spec.shortName} Sequence -> ${seqOutHtml}`);
  } catch (e) {
    console.error(`[ERR] ${spec.shortName} Sequence:`, e.stderr || e.stdout || e.message);
  }
}

console.log('All triplets generated successfully.');
