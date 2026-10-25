import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const ARCHIFY_BIN = 'C:\\Users\\Admin\\.gemini\\config\\skills\\archify\\archify\\bin\\archify.mjs';
const BASE_OUT_DIR = 'c:\\Business\\Software\\MarketingStudio\\.archify\\inspire-architecture';

const targets = ['chatwoot', 'linkedin-outreach-mcp'];
for (const repo of targets) {
  const dir = path.join(BASE_OUT_DIR, repo);
  fs.mkdirSync(dir, { recursive: true });
  fs.readdirSync(dir).forEach(f => {
    try { fs.unlinkSync(path.join(dir, f)); } catch {}
  });

  let cand = null;
  if (repo === 'chatwoot') {
    cand = {
      schema_version: 1,
      diagram_type: 'architecture',
      meta: {
        title: 'Chatwoot Architecture',
        output: '.archify/inspire-architecture/chatwoot/chatwoot.html',
        quality_profile: 'showcase'
      },
      components: [
        { id: 'actor', type: 'external', label: 'Users & Agents', sublabel: 'Web & Mobile Clients', pos: [40, 50], size: [130, 60] },
        { id: 'frontend', type: 'frontend', label: 'Chatwoot Dashboard', sublabel: 'Vue.js & Vite SPA', pos: [230, 50], size: [140, 60] },
        { id: 'backend', type: 'backend', label: 'Chatwoot Server', sublabel: 'Ruby on Rails API', pos: [440, 50], size: [150, 60] },
        { id: 'db', type: 'database', label: 'PostgreSQL', sublabel: 'Primary Store :5432', pos: [660, 50], size: [140, 60] },
        { id: 'redis', type: 'database', label: 'Redis', sublabel: 'Cache & ActionCable', pos: [440, 160], size: [150, 60] },
        { id: 'worker', type: 'backend', label: 'Sidekiq Workers', sublabel: 'Async Job Processing', pos: [660, 160], size: [140, 60] },
        { id: 'ext', type: 'external', label: 'External Channels', sublabel: 'WhatsApp / Email / Slack', pos: [870, 50], size: [150, 60] }
      ],
      boundaries: [
        { kind: 'region', label: 'Chatwoot Application Cluster', wraps: ['frontend', 'backend', 'db', 'redis', 'worker'] }
      ],
      connections: [
        { id: 'actor-to-fe', from: 'actor', to: 'frontend', label: 'HTTPS', variant: 'emphasis' },
        { id: 'fe-to-be', from: 'frontend', to: 'backend', label: 'REST / WS' },
        { id: 'be-to-db', from: 'backend', to: 'db', label: 'SQL' },
        { id: 'be-to-redis', from: 'backend', to: 'redis', label: 'PubSub' },
        { id: 'be-to-worker', from: 'backend', to: 'worker', label: 'Enqueue', variant: 'dashed' },
        { id: 'worker-to-db', from: 'worker', to: 'db', label: 'Persist' },
        { id: 'be-to-ext', from: 'backend', to: 'ext', label: 'Channel APIs', variant: 'dashed', fromSide: 'top', toSide: 'top' }
      ],
      cards: [
        { dot: 'emerald', title: 'Customer Engagement Engine', items: ['Omnichannel support platform built with Ruby on Rails and Vue.js', 'ActionCable WebSockets provide real-time agent/visitor chat synchronisation'] },
        { dot: 'cyan', title: 'Storage & Caching', items: ['PostgreSQL serves as the primary multi-tenant relational store', 'Redis powers transient session state, PubSub broadcasting, and Sidekiq job queues'] },
        { dot: 'violet', title: 'Channel Integrations', items: ['Two-way bridges for WhatsApp, Facebook, Instagram, Slack, SMS, and Email', 'Webhook triggers dispatch events to external automation services'] }
      ]
    };
  } else {
    cand = {
      schema_version: 1,
      diagram_type: 'architecture',
      meta: {
        title: 'LinkedIn Outreach MCP Architecture',
        output: '.archify/inspire-architecture/linkedin-outreach-mcp/linkedin-outreach-mcp.html',
        quality_profile: 'showcase'
      },
      components: [
        { id: 'actor', type: 'external', label: 'Claude / Agents', sublabel: 'MCP Host Client', pos: [40, 50], size: [130, 60] },
        { id: 'backend', type: 'backend', label: 'Outreach MCP', sublabel: 'Python FastMCP Server', pos: [230, 50], size: [150, 60] },
        { id: 'db', type: 'database', label: 'SQLite Store', sublabel: 'Campaign State :file', pos: [450, 50], size: [140, 60] },
        { id: 'ext', type: 'external', label: 'LinkedIn API', sublabel: 'Messaging & Profiles', pos: [660, 50], size: [150, 60] }
      ],
      boundaries: [
        { kind: 'region', label: 'MCP Service Boundary', wraps: ['backend', 'db'] }
      ],
      connections: [
        { id: 'actor-to-be', from: 'actor', to: 'backend', label: 'stdio / SSE', variant: 'emphasis' },
        { id: 'be-to-db', from: 'backend', to: 'db', label: 'Local State' },
        { id: 'be-to-ext', from: 'backend', to: 'ext', label: 'Outreach Calls', variant: 'dashed', fromSide: 'top', toSide: 'top' }
      ],
      cards: [
        { dot: 'emerald', title: 'Model Context Protocol', items: ['Standardized tool-calling server connecting LLM agents to LinkedIn actions', 'Built with Python and FastMCP runtime'] },
        { dot: 'cyan', title: 'Local Persistence', items: ['Tracks outreach prospects, conversation states, and rate limits locally'] }
      ]
    };
  }

  const candFile = path.join(dir, 'candidate.json');
  fs.writeFileSync(candFile, JSON.stringify(cand, null, 2), 'utf8');
  const outHtml = path.join(dir, `${repo}.html`);

  const res = execFileSync('node', [
    ARCHIFY_BIN,
    'finalize',
    'architecture',
    candFile,
    outHtml,
    '--quality', 'showcase',
    '--json'
  ], { encoding: 'utf8' });

  console.log(`[PASS] ${repo}: ok=${JSON.parse(res).ok}`);
}
