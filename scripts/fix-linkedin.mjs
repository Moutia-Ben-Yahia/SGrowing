import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const ARCHIFY_BIN = 'C:\\Users\\Admin\\.gemini\\config\\skills\\archify\\archify\\bin\\archify.mjs';
const BASE_OUT_DIR = 'c:\\Business\\Software\\MarketingStudio\\.archify\\inspire-architecture';

const repo = 'linkedin-outreach-mcp';
const dir = path.join(BASE_OUT_DIR, repo);
fs.mkdirSync(dir, { recursive: true });
fs.readdirSync(dir).forEach(f => {
  try { fs.unlinkSync(path.join(dir, f)); } catch {}
});

const cand = {
  schema_version: 1,
  diagram_type: 'architecture',
  meta: {
    title: 'LinkedIn Outreach MCP Detailed Architecture',
    output: '.archify/inspire-architecture/linkedin-outreach-mcp/linkedin-outreach-mcp.html',
    quality_profile: 'showcase',
    views: [
      {
        id: 'ingress-and-clients',
        label: 'Client Ingress & Frontends',
        focus: ['actor', 'front', 'auth', 'api'],
        note: 'Claude and agent clients authenticate and issue tool requests.'
      },
      {
        id: 'core-processing',
        label: 'Core Services & Workers',
        focus: ['api', 'domain', 'queue', 'worker'],
        note: 'MCP tool calls route to campaign engines and rate-limited queue workers.'
      },
      {
        id: 'persistence-subsystem',
        label: 'Data, Cache & Storage',
        focus: ['cache', 'db', 'storage'],
        note: 'SQLite persists campaign leads and SQLite/JSON files store configuration.'
      },
      {
        id: 'external-integrations',
        label: 'External Integrations',
        focus: ['api', 'ext'],
        note: 'Outreach dispatch to LinkedIn Web and REST endpoints.'
      }
    ]
  },
  components: [
    { id: 'actor', type: 'external', label: 'AI Agents & Users', sublabel: 'Claude & MCP Host', pos: [40, 50], size: [130, 60] },
    { id: 'front', type: 'frontend', label: 'Outreach Web UI', sublabel: 'Campaign Dashboard', pos: [260, 50], size: [140, 60] },
    { id: 'api', type: 'backend', label: 'Outreach MCP Core', sublabel: 'Python FastMCP Engine', pos: [490, 50], size: [150, 60] },
    { id: 'queue', type: 'messagebus', label: 'Rate-Limit Queue', sublabel: 'Task & Throttle Broker', pos: [730, 50], size: [140, 60] },
    { id: 'ext', type: 'external', label: 'LinkedIn Platform', sublabel: 'Messages & Profiles', pos: [970, 50], size: [150, 60] },

    { id: 'auth', type: 'security', label: 'Session & Cookies', sublabel: 'Encrypted Credential Store', pos: [260, 180], size: [140, 60] },
    { id: 'domain', type: 'backend', label: 'Prospect Engine', sublabel: 'Personalization & Sequences', pos: [490, 180], size: [150, 60] },
    { id: 'worker', type: 'backend', label: 'Outreach Worker', sublabel: 'Throttled Dispatcher', pos: [730, 180], size: [140, 60] },

    { id: 'cache', type: 'database', label: 'In-Memory Cache', sublabel: 'Profile & Cooldown State', pos: [260, 310], size: [140, 60] },
    { id: 'db', type: 'database', label: 'SQLite Store', sublabel: 'Local Campaign DB', pos: [490, 310], size: [150, 60] },
    { id: 'storage', type: 'cloud', label: 'Export Storage', sublabel: 'CSV & Lead Logs', pos: [730, 310], size: [140, 60] }
  ],
  boundaries: [
    {
      kind: 'region',
      label: 'LinkedIn Outreach MCP Runtime Cluster',
      wraps: ['front', 'api', 'queue', 'auth', 'domain', 'worker', 'cache', 'db', 'storage']
    }
  ],
  connections: [
    { id: 'actor-to-front', from: 'actor', to: 'front', label: 'HTTPS', variant: 'emphasis' },
    { id: 'front-to-api', from: 'front', to: 'api', label: 'stdio / SSE' },
    { id: 'front-to-auth', from: 'front', to: 'auth', label: 'Auth Token', variant: 'security' },
    { id: 'api-to-domain', from: 'api', to: 'domain', label: 'Dispatch' },
    { id: 'api-to-cache', from: 'api', to: 'cache', label: 'State' },
    { id: 'api-to-queue', from: 'api', to: 'queue', label: 'Enqueue', variant: 'dashed' },
    { id: 'api-to-ext', from: 'api', to: 'ext', label: 'Webhooks', variant: 'dashed', fromSide: 'top', toSide: 'top' },
    { id: 'domain-to-db', from: 'domain', to: 'db', label: 'SQL Data' },
    { id: 'queue-to-worker', from: 'queue', to: 'worker', label: 'Consume' },
    { id: 'worker-to-db', from: 'worker', to: 'db', label: 'Commit' },
    { id: 'worker-to-storage', from: 'worker', to: 'storage', label: 'Assets', variant: 'dashed' }
  ],
  cards: [
    {
      dot: 'emerald',
      title: 'MCP Server Architecture',
      items: [
        'Exposes structured tool calls over stdio and SSE transport',
        'FastMCP Python engine orchestrates lead finding and messaging',
        'Automated LinkedIn connection and follow-up sequences'
      ]
    },
    {
      dot: 'cyan',
      title: 'Throttling & Local State',
      items: [
        'SQLite provides zero-config local persistence for prospect tracking and state',
        'In-memory cooldown cache protects against LinkedIn rate limits and bans',
        'Local CSV and JSON exports for reporting'
      ]
    },
    {
      dot: 'violet',
      title: 'Security & Protocol Adherence',
      items: [
        'Encrypted credential and cookie storage',
        'Complies with Model Context Protocol specifications for agent tool usage'
      ]
    }
  ]
};

const candFile = path.join(dir, 'candidate.json');
fs.writeFileSync(candFile, JSON.stringify(cand, null, 2), 'utf8');
const outHtml = path.join(dir, 'linkedin-outreach-mcp.html');

const res = execFileSync('node', [
  ARCHIFY_BIN,
  'finalize',
  'architecture',
  candFile,
  outHtml,
  '--quality', 'showcase',
  '--json'
], { encoding: 'utf8' });

console.log('Result for linkedin-outreach-mcp:', JSON.parse(res).ok);
