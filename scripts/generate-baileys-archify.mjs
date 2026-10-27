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

const spec = {
  repoName: 'Baileys',
  shortName: 'Baileys',
  category: 'WhatsApp & Messaging Gateways',
  badgeColor: '#25d366',
  feLabel: 'Client App',
  feSub: 'Consumer Node/TS App',
  beLabel: 'WASocket Core',
  beSub: 'Noise & Protocol Engine',
  dbLabel: 'State Store',
  dbSub: 'Multi-File / SQL Auth',
  cacheLabel: 'Signal Key Cache',
  cacheSub: 'PreKeys & Sessions',
  queueLabel: 'Baileys Events',
  queueSub: 'Typed Event Emitter',
  storageLabel: 'Media Stream',
  storageSub: 'Encrypted CDN I/O',
  tables: [
    { name: 'sessions' },
    { name: 'creds' },
    { name: 'pre_keys' },
    { name: 'chats' },
    { name: 'messages' },
    { name: 'contacts' }
  ],
  signaturePath: 'sendMessage(jid, { text })',
  signatureEntity: 'message',
  signatureWorkerJob: 'Emit messages.upsert'
};

function buildArchCandidate(spec) {
  const { repoName, feLabel, feSub, beLabel, beSub, dbLabel, dbSub, cacheLabel, cacheSub, queueLabel, queueSub, storageLabel, storageSub } = spec;

  const components = [
    { id: 'actor', type: 'external', label: 'WhatsApp Server', sublabel: 'WhatsApp Cellular WS', pos: [40, 50], size: [130, 60] },
    { id: 'front', type: 'frontend', label: feLabel.slice(0, 15), sublabel: feSub.slice(0, 20), pos: [260, 50], size: [140, 60] },
    { id: 'api', type: 'backend', label: beLabel.slice(0, 15), sublabel: beSub.slice(0, 20), pos: [490, 50], size: [150, 60] },
    { id: 'queue', type: 'messagebus', label: queueLabel.slice(0, 15), sublabel: queueSub.slice(0, 20), pos: [730, 50], size: [140, 60] },
    { id: 'ext', type: 'external', label: 'External App', sublabel: 'Bot or CRM Consumer', pos: [970, 50], size: [150, 60] },

    { id: 'auth', type: 'security', label: 'Signal Protocol', sublabel: 'Curve25519 & Noise', pos: [260, 180], size: [140, 60] },
    { id: 'domain', type: 'backend', label: 'Socket Handlers', sublabel: 'Binary Frame Codec', pos: [490, 180], size: [150, 60] },
    { id: 'worker', type: 'backend', label: 'Event Dispatch', sublabel: 'Handler Callbacks', pos: [730, 180], size: [140, 60] },

    { id: 'cache', type: 'database', label: cacheLabel.slice(0, 15), sublabel: cacheSub.slice(0, 20), pos: [260, 310], size: [140, 60] },
    { id: 'db', type: 'database', label: dbLabel.slice(0, 15), sublabel: dbSub.slice(0, 20), pos: [490, 310], size: [150, 60] },
    { id: 'storage', type: 'cloud', label: storageLabel.slice(0, 15), sublabel: storageSub.slice(0, 20), pos: [730, 310], size: [140, 60] }
  ];

  const boundaries = [
    {
      kind: 'region',
      label: `${repoName} Runtime Cluster`,
      wraps: ['front', 'api', 'queue', 'auth', 'domain', 'worker', 'cache', 'db', 'storage']
    }
  ];

  const connections = [
    { id: 'c1', from: 'actor', to: 'front', label: 'WebSocket', variant: 'emphasis' },
    { id: 'c2', from: 'front', to: 'api', label: 'Socket Core' },
    { id: 'c3', from: 'front', to: 'auth', label: 'Signal Key', variant: 'security' },
    { id: 'c4', from: 'api', to: 'domain', label: 'Dispatch' },
    { id: 'c5', from: 'api', to: 'cache', label: 'State' },
    { id: 'c6', from: 'api', to: 'queue', label: 'Enqueue', variant: 'dashed' },
    { id: 'c7', from: 'api', to: 'ext', label: 'Callbacks', variant: 'dashed', fromSide: 'top', toSide: 'top' },
    { id: 'c8', from: 'domain', to: 'db', label: 'Store State' },
    { id: 'c9', from: 'queue', to: 'worker', label: 'Dispatch' },
    { id: 'c10', from: 'worker', to: 'db', label: 'Commit' },
    { id: 'c11', from: 'worker', to: 'storage', label: 'Media', variant: 'dashed' }
  ];

  const views = [
    {
      id: 'ingress',
      label: 'Client Ingress & Frontends',
      focus: ['actor', 'front', 'auth', 'api'],
      note: `WhatsApp binary WebSockets connect through ${beLabel} using Noise handshake.`
    },
    {
      id: 'core-processing',
      label: 'Core Services & Workers',
      focus: ['api', 'domain', 'queue', 'worker'],
      note: 'Binary frames are decrypted via Signal protocol and dispatched to typed event streams.'
    },
    {
      id: 'persistence-subsystem',
      label: 'Storage & Cache Subsystem',
      focus: ['api', 'domain', 'worker', 'cache', 'db', 'storage'],
      note: 'Cryptographic identity keys, sessions, and encrypted media buffers.'
    },
    {
      id: 'external-integrations',
      label: 'External Integrations',
      focus: ['api', 'worker', 'ext'],
      note: 'Consumer applications subscribe to real-time events via typed callbacks.'
    }
  ];

  const cards = [
    {
      dot: 'cyan',
      title: `${repoName} Architectural Topology`,
      items: [
        `Protocol: Direct WebSocket connection to WhatsApp Web servers without browser binaries`,
        `Cryptography: Full Signal Protocol (Curve25519, Noise_XX, AES-GCM)`,
        `Events: Event-driven typed emitter powering high-throughput bots and gateways`
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'architecture',
    meta: {
      title: `${repoName} System Architecture`,
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
  const { repoName, tables } = spec;

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
        { name: 'session_id', type: 'varchar(64)', key: 'uk' },
        { name: 'jid', type: 'varchar(128)' },
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
        { name: 'noise_key', type: 'bytea' },
        { name: 'identity_key', type: 'bytea' },
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
        { name: 'key_id', type: 'integer' },
        { name: 'public_key', type: 'bytea' },
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
        { name: 'chat_jid', type: 'varchar(128)', key: 'uk' },
        { name: 'unread_count', type: 'integer' },
        { name: 'last_message_at', type: 'timestamptz' }
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
        { name: 'message_id', type: 'varchar(64)', key: 'uk' },
        { name: 'payload', type: 'jsonb' },
        { name: 'timestamp', type: 'timestamptz' }
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
        { name: 'contact_jid', type: 'varchar(128)' },
        { name: 'push_name', type: 'varchar(128)' },
        { name: 'synced_at', type: 'timestamptz' }
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
      title: `${repoName} Normalized State Schema`,
      items: [
        `Strict session & cryptographic credential persistence anchored on ${t0}.id primary key`,
        `Clean separation between cryptographic ${t1} and operational ${d0} chat caches`,
        `High-performance pre-key index supporting rapid Signal Protocol Double Ratchet handshakes`
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'erd',
    meta: {
      title: `${repoName} State Schema`,
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
    { id: 'user', type: 'external', label: 'WhatsApp Server', sublabel: 'WebSocket Network' },
    { id: 'web', type: 'frontend', label: `${shortName} Socket`.slice(0, 16), sublabel: feLabel.slice(0, 18) },
    { id: 'auth', type: 'security', label: 'Signal Engine', sublabel: 'Noise & PreKeys' },
    { id: 'api', type: 'backend', label: `${shortName} Core`.slice(0, 16), sublabel: beLabel.slice(0, 18) },
    { id: 'redis', type: 'database', label: 'Session Cache', sublabel: cacheLabel.slice(0, 18) },
    { id: 'db', type: 'database', label: 'Auth Store', sublabel: dbLabel.slice(0, 18) },
    { id: 'worker', type: 'backend', label: 'Event Emitter', sublabel: queueLabel.slice(0, 18) }
  ];

  const segments = [
    { from: 150, to: 295, label: 'WebSocket Handshake & Signal Decryption' },
    { from: 300, to: 445, label: 'Session State & Binary Node Decoding' },
    { from: 450, to: 590, label: 'Typed Event Dispatch to Consumer' }
  ];

  const messages = [
    { id: 'm1', from: 'user', to: 'web', y: 170, label: 'WSS Binary Frame (Noise Frame)', variant: 'emphasis' },
    { id: 'm2', from: 'web', to: 'api', y: 205, label: 'Decode Binary Protocol Node', variant: 'emphasis' },
    { id: 'm3', from: 'api', to: 'auth', y: 240, label: 'Decrypt Signal Double Ratchet', variant: 'security' },
    { id: 'm4', from: 'auth', to: 'api', y: 275, label: 'Decrypted Proto Message (OK)', variant: 'return' },
    { id: 'm5', from: 'api', to: 'redis', y: 315, label: `GET prekey:${signatureEntity}`, variant: 'default' },
    { id: 'm6', from: 'redis', to: 'api', y: 350, label: 'Key verified', variant: 'return' },
    { id: 'm7', from: 'api', to: 'db', y: 390, label: `UPDATE session_state`, variant: 'emphasis' },
    { id: 'm8', from: 'db', to: 'api', y: 425, label: 'Committed state (OK)', variant: 'return' },
    { id: 'm9', from: 'api', to: 'redis', y: 465, label: `CACHE message_receipt`, variant: 'dashed' },
    { id: 'm10', from: 'api', to: 'worker', y: 500, label: signatureWorkerJob, variant: 'dashed' },
    { id: 'm11', from: 'api', to: 'web', y: 535, label: 'Send ACK Receipt to WA Server', variant: 'return' },
    { id: 'm12', from: 'worker', to: 'user', y: 570, label: 'Client Consumer receives message', variant: 'return' }
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
      title: `${repoName} End-to-End Inbound Message Lifecycle`,
      items: [
        'Direct binary WebSocket connection without headless browser overhead',
        'In-memory Signal Protocol decryption with hardware-accelerated Curve25519',
        'Typed event stream emission enabling ultra-fast bot responses'
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'sequence',
    meta: {
      title: `${repoName} Request Sequence`,
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

async function run() {
  console.log(`Generating archify triplets for ${spec.repoName}...`);

  const { repoName } = spec;
  const repoDir = path.join(BASE_OUT_DIR, repoName);
  fs.mkdirSync(repoDir, { recursive: true });

  cleanAllReceipts(repoDir);

  // 1. Architecture
  const archCand = buildArchCandidate(spec);
  const archCandFile = path.join(repoDir, 'candidate.json');
  fs.writeFileSync(archCandFile, JSON.stringify(archCand, null, 2), 'utf8');
  const archOutHtml = path.join(repoDir, `${repoName}.html`);

  try {
    execFileSync('node', [
      ARCHIFY_BIN,
      'finalize',
      'architecture',
      archCandFile,
      archOutHtml,
      '--quality', 'showcase',
      '--json'
    ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
    console.log(`[OK] ${repoName} Architecture -> ${archOutHtml}`);
  } catch (e) {
    console.error(`[ERR] ${repoName} Architecture:`, e.stderr || e.stdout || e.message);
  }

  // 2. ERD
  const erdCand = buildErdCandidate(spec);
  const erdCandFile = path.join(repoDir, 'candidate-erd.json');
  fs.writeFileSync(erdCandFile, JSON.stringify(erdCand, null, 2), 'utf8');
  const erdOutHtml = path.join(repoDir, `${repoName}-erd.html`);

  try {
    execFileSync('node', [
      ARCHIFY_BIN,
      'finalize',
      'erd',
      erdCandFile,
      erdOutHtml,
      '--quality', 'showcase',
      '--json'
    ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
    console.log(`[OK] ${repoName} ERD -> ${erdOutHtml}`);
  } catch (e) {
    console.error(`[ERR] ${repoName} ERD:`, e.stderr || e.stdout || e.message);
  }

  // 3. Sequence
  const seqCand = buildSeqCandidate(spec);
  const seqCandFile = path.join(repoDir, 'candidate-seq.json');
  fs.writeFileSync(seqCandFile, JSON.stringify(seqCand, null, 2), 'utf8');
  const seqOutHtml = path.join(repoDir, `${repoName}-sequence.html`);

  try {
    execFileSync('node', [
      ARCHIFY_BIN,
      'finalize',
      'sequence',
      seqCandFile,
      seqOutHtml,
      '--quality', 'showcase',
      '--json'
    ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
    console.log(`[OK] ${repoName} Sequence -> ${seqOutHtml}`);
  } catch (e) {
    console.error(`[ERR] ${repoName} Sequence:`, e.stderr || e.stdout || e.message);
  }
}

run().catch(console.error);
