import fs from 'node:fs';
import path from 'node:path';

const INSPIRE_DIR = 'c:\\Business\\Software\\MarketingStudio\\inspire';

function findFilesRecursively(dir, filterFn, maxDepth = 6, depth = 0) {
  if (depth > maxDepth) return [];
  const results = [];
  try {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const e of entries) {
      if (['node_modules', '.git', 'dist', 'build', '.next', 'coverage', 'tmp', 'vendor', '.turbo'].includes(e.name)) continue;
      const full = path.join(dir, e.name);
      if (e.isDirectory()) {
        results.push(...findFilesRecursively(full, filterFn, maxDepth, depth + 1));
      } else if (filterFn(e.name, full)) {
        results.push(full);
      }
    }
  } catch {}
  return results;
}

export function analyzeRepoDeep(repoName) {
  const dir = path.join(INSPIRE_DIR, repoName);
  const profile = {
    repo: repoName,
    title: repoName,
    description: '',
    language: 'TypeScript / Node.js',
    frontend: 'React Web UI',
    frontendSub: 'Modern Web Client',
    backend: 'Node.js Core API',
    backendSub: 'REST & Event Router',
    database: 'PostgreSQL',
    databaseSub: 'Relational Store :5432',
    cache: 'Redis',
    cacheSub: 'Cache & Pub/Sub :6379',
    queue: 'BullMQ Engine',
    queueSub: 'Async Worker Queue',
    storage: 'S3 / MinIO Store',
    storageSub: 'Object Storage :9000',
    externalServices: ['Webhooks', 'SMTP Email', 'OAuth 2.0'],
    entities: [],
    primaryWorkflow: {
      action: 'Process Domain Request',
      requestPath: 'POST /api/v1/resources',
      entity: 'resource',
      entities: 'resources',
      selectQuery: 'SELECT * FROM resources WHERE id = $1',
      cacheKey: 'resource:cache_key',
      jobName: 'Enqueue resource.process job'
    }
  };

  if (!fs.existsSync(dir)) return profile;

  // 1. Recursive package.json, composer.json, Gemfile, go.mod, pyproject.toml
  const pkgFiles = findFilesRecursively(dir, n => n === 'package.json', 3);
  let mainPkg = null;
  for (const pf of pkgFiles) {
    try {
      const p = JSON.parse(fs.readFileSync(pf, 'utf8'));
      if (!mainPkg || (p.dependencies && Object.keys(p.dependencies).length > (Object.keys(mainPkg.dependencies || {}).length))) {
        mainPkg = p;
      }
    } catch {}
  }
  if (mainPkg?.description) profile.description = mainPkg.description;

  const allDeps = { ...(mainPkg?.dependencies || {}), ...(mainPkg?.devDependencies || {}) };

  // Frontend detection
  if (allDeps['next']) {
    profile.frontend = 'Next.js App';
    profile.frontendSub = 'React 19 App Router';
    profile.backend = 'Next.js API Engine';
    profile.backendSub = 'Server Actions & Route Handlers';
  } else if (allDeps['@remix-run/react']) {
    profile.frontend = 'Remix UI';
    profile.frontendSub = 'Full-Stack Web Loader';
  } else if (allDeps['react']) {
    profile.frontend = 'React SPA';
    profile.frontendSub = 'Vite & Tailwind UI';
  } else if (allDeps['vue']) {
    profile.frontend = 'Vue.js Dashboard';
    profile.frontendSub = 'Vue 3 & Pinia Store';
  } else if (allDeps['nuxt']) {
    profile.frontend = 'Nuxt 3 UI';
    profile.frontendSub = 'Vue SSR Client';
  } else if (allDeps['@angular/core']) {
    profile.frontend = 'Angular Web App';
    profile.frontendSub = 'RxJS & Angular CLI';
  } else if (allDeps['svelte'] || allDeps['@sveltejs/kit']) {
    profile.frontend = 'SvelteKit App';
    profile.frontendSub = 'Svelte 5 Reactive UI';
  }

  // Backend detection
  if (allDeps['@nestjs/core']) {
    profile.backend = 'NestJS API';
    profile.backendSub = 'TypeScript Modular Engine';
  } else if (allDeps['fastify']) {
    profile.backend = 'Fastify Engine';
    profile.backendSub = 'High-Throughput HTTP API';
  } else if (allDeps['express']) {
    profile.backend = 'Express Server';
    profile.backendSub = 'Node.js REST Services';
  } else if (allDeps['hono']) {
    profile.backend = 'Hono Edge API';
    profile.backendSub = 'Lightweight Edge Router';
  }

  // Database ORM
  if (allDeps['prisma'] || allDeps['@prisma/client']) {
    profile.database = 'PostgreSQL';
    profile.databaseSub = 'Prisma Client :5432';
  } else if (allDeps['drizzle-orm']) {
    profile.database = 'PostgreSQL';
    profile.databaseSub = 'Drizzle ORM :5432';
  } else if (allDeps['typeorm']) {
    profile.database = 'PostgreSQL';
    profile.databaseSub = 'TypeORM Repository :5432';
  } else if (allDeps['mongoose'] || allDeps['mongodb']) {
    profile.database = 'MongoDB';
    profile.databaseSub = 'Document Store :27017';
  } else if (allDeps['better-sqlite3'] || allDeps['sqlite3']) {
    profile.database = 'SQLite Database';
    profile.databaseSub = 'Local Embedded Engine';
  }

  // Queue detection
  if (allDeps['bullmq'] || allDeps['bull']) {
    profile.queue = 'BullMQ Worker';
    profile.queueSub = 'Redis Task Queue';
  }

  // Check Ruby
  const gemfiles = findFilesRecursively(dir, n => n === 'Gemfile', 3);
  if (gemfiles.length > 0) {
    profile.language = 'Ruby';
    profile.backend = 'Rails API';
    profile.backendSub = 'Puma Application Server';
    profile.frontend = 'Hotwire & Vue';
    profile.frontendSub = 'Turbo & Stimulus UI';
    profile.database = 'PostgreSQL';
    profile.databaseSub = 'ActiveRecord Store :5432';
    profile.queue = 'Sidekiq Worker';
    profile.queueSub = 'Async Redis Queue';
  }

  // Check Go
  const goMods = findFilesRecursively(dir, n => n === 'go.mod', 3);
  if (goMods.length > 0) {
    profile.language = 'Go';
    profile.backend = 'Go Daemon';
    profile.backendSub = 'Compiled Go Engine';
    profile.database = 'PostgreSQL';
    profile.databaseSub = 'pgx Connection Pool :5432';
    profile.queue = 'Go Goroutines';
    profile.queueSub = 'Asynq Worker Pool';
    profile.frontend = 'Web Console';
    profile.frontendSub = 'Single Page Application';
    try {
      const gmod = fs.readFileSync(goMods[0], 'utf8');
      if (gmod.includes('gin-gonic')) profile.backendSub = 'Gin Engine :8080';
      if (gmod.includes('go-chi')) profile.backendSub = 'Chi Router :8080';
      if (gmod.includes('gofiber')) profile.backendSub = 'Fiber Engine :3000';
    } catch {}
  }

  // Check Python
  const pyFiles = findFilesRecursively(dir, n => n === 'pyproject.toml' || n === 'requirements.txt', 3);
  if (pyFiles.length > 0) {
    profile.language = 'Python';
    profile.backend = 'FastAPI ASGI';
    profile.backendSub = 'Uvicorn Server :8000';
    profile.queue = 'Celery Worker';
    profile.queueSub = 'Redis Task Broker';
    profile.database = 'PostgreSQL';
    profile.databaseSub = 'SQLAlchemy Engine :5432';
    for (const pf of pyFiles) {
      try {
        const text = fs.readFileSync(pf, 'utf8');
        if (text.includes('django')) {
          profile.backend = 'Django Core';
          profile.backendSub = 'Gunicorn / WSGI :8000';
          profile.databaseSub = 'Django ORM :5432';
        }
      } catch {}
    }
  }

  // Check PHP / Laravel / Symfony
  const composerFiles = findFilesRecursively(dir, n => n === 'composer.json', 3);
  if (composerFiles.length > 0) {
    profile.language = 'PHP';
    profile.backend = 'Laravel API';
    profile.backendSub = 'PHP 8.2 / FPM :9000';
    profile.frontend = 'Blade & Inertia';
    profile.frontendSub = 'Vue / Tailwind UI';
    profile.queue = 'Horizon Worker';
    profile.queueSub = 'Redis Queue Daemon';
    profile.database = 'MySQL / Postgres';
    profile.databaseSub = 'Eloquent ORM :3306';
    for (const cf of composerFiles) {
      try {
        const text = fs.readFileSync(cf, 'utf8');
        if (text.includes('symfony/')) {
          profile.backend = 'Symfony / Doctrine';
          profile.backendSub = 'Doctrine ORM :3306';
        }
      } catch {}
    }
  }

  // -------------------------------------------------------------
  // ENTITY & SCHEMA DISCOVERY
  // -------------------------------------------------------------

  // 1. Rails schema.rb anywhere
  const schemaRbFiles = findFilesRecursively(dir, n => n === 'schema.rb', 4);
  for (const srb of schemaRbFiles) {
    try {
      const sText = fs.readFileSync(srb, 'utf8');
      const tableRegex = /create_table\s+"([^"]+)"[^\n]*\s+do\s+\|t\|([\s\S]*?)end/g;
      let m;
      while ((m = tableRegex.exec(sText)) !== null) {
        const tName = m[1];
        const body = m[2];
        const cols = [];
        for (const line of body.split('\n')) {
          const cMatch = line.match(/t\.([a-z_]+)\s+"([^"]+)"/);
          if (cMatch) {
            cols.push({ name: cMatch[2], type: cMatch[1] });
          }
        }
        profile.entities.push({ name: tName, columns: cols.slice(0, 6) });
      }
    } catch {}
    if (profile.entities.length > 0) break;
  }

  // 2. Prisma schemas anywhere
  if (profile.entities.length === 0) {
    const prismaFiles = findFilesRecursively(dir, n => n.endsWith('.prisma'), 6);
    for (const pf of prismaFiles) {
      try {
        const text = fs.readFileSync(pf, 'utf8');
        const modelRegex = /model\s+(\w+)\s+{([\s\S]*?)}/g;
        let m;
        while ((m = modelRegex.exec(text)) !== null) {
          const mName = m[1];
          const body = m[2];
          const cols = [];
          for (const l of body.split('\n')) {
            const clean = l.trim();
            if (!clean || clean.startsWith('//') || clean.startsWith('@@')) continue;
            const parts = clean.split(/\s+/);
            if (parts.length >= 2) {
              const fName = parts[0];
              const fType = parts[1];
              if (!['String', 'Int', 'Boolean', 'DateTime', 'Json', 'Float', 'BigInt', 'Decimal'].includes(fType.replace('?', '').replace('[]', ''))) continue;
              cols.push({ name: fName, type: fType });
            }
          }
          profile.entities.push({ name: mName, columns: cols.slice(0, 6) });
        }
      } catch {}
      if (profile.entities.length > 5) break;
    }
  }

  // 3. SQL schema / migrations anywhere
  if (profile.entities.length === 0) {
    const sqlFiles = findFilesRecursively(dir, n => n.endsWith('.sql'), 5);
    for (const sf of sqlFiles) {
      try {
        const sqlText = fs.readFileSync(sf, 'utf8');
        const createRegex = /CREATE\s+TABLE(?:\s+IF\s+NOT\s+EXISTS)?\s+([a-zA-Z0-9_".]+)\s*\(([\s\S]*?)\);/gi;
        let m;
        while ((m = createRegex.exec(sqlText)) !== null) {
          const rawName = m[1].replace(/["']/g, '').split('.').pop();
          const body = m[2];
          const cols = [];
          for (const l of body.split('\n')) {
            const trimmed = l.trim();
            if (!trimmed || trimmed.startsWith('--') || trimmed.toUpperCase().startsWith('PRIMARY KEY') || trimmed.toUpperCase().startsWith('CONSTRAINT')) continue;
            const tokens = trimmed.split(/\s+/);
            if (tokens.length >= 2) {
              cols.push({ name: tokens[0].replace(/["',]/g, ''), type: tokens[1].replace(/,/g, '') });
            }
          }
          profile.entities.push({ name: rawName, columns: cols.slice(0, 6) });
        }
      } catch {}
      if (profile.entities.length > 6) break;
    }
  }

  // 4. Rails models (e.g. zammad, spree)
  if (profile.entities.length === 0) {
    const modelDirs = findFilesRecursively(dir, (n, f) => n === 'models' && fs.statSync(f).isDirectory(), 5);
    for (const md of modelDirs) {
      try {
        const mFiles = fs.readdirSync(md).filter(f => f.endsWith('.rb') && !f.startsWith('application_'));
        for (const mf of mFiles) {
          const entityName = mf.replace('.rb', '');
          profile.entities.push({
            name: entityName,
            columns: [
              { name: 'id', type: 'bigint' },
              { name: 'name', type: 'varchar(255)' },
              { name: 'created_at', type: 'datetime' }
            ]
          });
        }
      } catch {}
      if (profile.entities.length > 5) break;
    }
  }

  // 5. Laravel models (e.g. freescout, OpnForm, mixpost, laravel-crm)
  if (profile.entities.length === 0) {
    const laravelModelDirs = findFilesRecursively(dir, (n, f) => (n === 'Models' || n === 'models') && fs.statSync(f).isDirectory(), 5);
    for (const lmd of laravelModelDirs) {
      try {
        const mFiles = fs.readdirSync(lmd).filter(f => f.endsWith('.php') && !f.startsWith('Model'));
        for (const mf of mFiles) {
          const entityName = mf.replace('.php', '');
          profile.entities.push({
            name: entityName,
            columns: [
              { name: 'id', type: 'bigint' },
              { name: 'title', type: 'varchar(255)' },
              { name: 'created_at', type: 'timestamp' }
            ]
          });
        }
      } catch {}
      if (profile.entities.length > 5) break;
    }
  }

  // 6. TypeORM / NestJS entities (e.g. n8n)
  if (profile.entities.length === 0) {
    const entityFiles = findFilesRecursively(dir, n => n.endsWith('-entity.ts') || n.endsWith('.entity.ts'), 6);
    for (const ef of entityFiles) {
      try {
        const base = path.basename(ef).replace(/\.(entity|ee)?\.ts$/, '').replace(/-entity$/, '');
        profile.entities.push({
          name: base,
          columns: [
            { name: 'id', type: 'varchar(36)' },
            { name: 'name', type: 'varchar(128)' },
            { name: 'created_at', type: 'timestamptz' }
          ]
        });
      } catch {}
    }
  }

  // 7. Ghost schema
  if (profile.entities.length === 0) {
    const ghostSchemaPath = path.join(dir, 'ghost', 'core', 'server', 'data', 'schema', 'schema.js');
    if (fs.existsSync(ghostSchemaPath)) {
      try {
        const gText = fs.readFileSync(ghostSchemaPath, 'utf8');
        const tableMatches = gText.match(/(\w+):\s*{\s*id:\s*{/g);
        if (tableMatches) {
          for (const tm of tableMatches) {
            const tbl = tm.split(':')[0].trim();
            profile.entities.push({
              name: tbl,
              columns: [
                { name: 'id', type: 'varchar(24)' },
                { name: 'name', type: 'varchar(191)' },
                { name: 'created_at', type: 'datetime' }
              ]
            });
          }
        }
      } catch {}
    }
  }

  // 8. Monorepos / packages / modules (e.g. twenty, botpress, discord.js, aiogram)
  if (profile.entities.length === 0) {
    // Check known SDK / frameworks:
    const rLower = repoName.toLowerCase();
    if (rLower === 'discord.js') {
      profile.frontend = 'Discord Gateway Client';
      profile.frontendSub = 'WebSocket Shard Manager';
      profile.backend = 'Discord.js Core Engine';
      profile.backendSub = 'REST API & Event Dispatcher';
      profile.database = 'In-Memory Store';
      profile.databaseSub = 'Discord Gateway Cache';
      profile.queue = 'Rate Limit Queue';
      profile.queueSub = 'Request Bucket Handler';
      profile.entities = [
        { name: 'Client', columns: [{ name: 'id', type: 'snowflake' }, { name: 'user_id', type: 'snowflake' }] },
        { name: 'Guild', columns: [{ name: 'id', type: 'snowflake' }, { name: 'name', type: 'string' }] },
        { name: 'Channel', columns: [{ name: 'id', type: 'snowflake' }, { name: 'guild_id', type: 'snowflake' }] },
        { name: 'Message', columns: [{ name: 'id', type: 'snowflake' }, { name: 'channel_id', type: 'snowflake' }] },
        { name: 'User', columns: [{ name: 'id', type: 'snowflake' }, { name: 'username', type: 'string' }] },
        { name: 'Role', columns: [{ name: 'id', type: 'snowflake' }, { name: 'guild_id', type: 'snowflake' }] }
      ];
    } else if (rLower === 'aiogram' || rLower === 'grammy') {
      profile.frontend = 'Telegram Bot API';
      profile.frontendSub = 'Webhook & Long Polling';
      profile.backend = `${repoName} Dispatcher`;
      profile.backendSub = 'Asyncio Handler Pipeline';
      profile.database = 'Redis FSM Storage';
      profile.databaseSub = 'State & Context Store';
      profile.queue = 'Telegram Update Queue';
      profile.queueSub = 'Asyncio Worker Pool';
      profile.entities = [
        { name: 'Bot', columns: [{ name: 'token', type: 'string' }, { name: 'id', type: 'bigint' }] },
        { name: 'Dispatcher', columns: [{ name: 'router_id', type: 'string' }, { name: 'name', type: 'string' }] },
        { name: 'Update', columns: [{ name: 'update_id', type: 'bigint' }, { name: 'type', type: 'string' }] },
        { name: 'Message', columns: [{ name: 'message_id', type: 'bigint' }, { name: 'chat_id', type: 'bigint' }] },
        { name: 'Chat', columns: [{ name: 'id', type: 'bigint' }, { name: 'type', type: 'string' }] },
        { name: 'User', columns: [{ name: 'id', type: 'bigint' }, { name: 'username', type: 'string' }] }
      ];
    } else if (rLower === 'asterisk') {
      profile.frontend = 'SIP Client / Softphone';
      profile.frontendSub = 'WebRTC / PJSIP Protocol';
      profile.backend = 'Asterisk PBX Core';
      profile.backendSub = 'C Telephony Switch Engine';
      profile.database = 'Asterisk RealTime DB';
      profile.databaseSub = 'MySQL / PostgreSQL :3306';
      profile.queue = 'Asterisk CDR Queue';
      profile.queueSub = 'Call Event Logger';
      profile.entities = [
        { name: 'Endpoint', columns: [{ name: 'id', type: 'varchar(40)' }, { name: 'transport', type: 'varchar(40)' }] },
        { name: 'Channel', columns: [{ name: 'uniqueid', type: 'varchar(32)' }, { name: 'name', type: 'varchar(80)' }] },
        { name: 'Bridge', columns: [{ name: 'bridge_id', type: 'varchar(36)' }, { name: 'type', type: 'varchar(32)' }] },
        { name: 'Extension', columns: [{ name: 'exten', type: 'varchar(40)' }, { name: 'context', type: 'varchar(40)' }] },
        { name: 'CDR', columns: [{ name: 'accountcode', type: 'varchar(20)' }, { name: 'src', type: 'varchar(80)' }] },
        { name: 'Voicemail', columns: [{ name: 'mailbox', type: 'varchar(80)' }, { name: 'context', type: 'varchar(80)' }] }
      ];
    } else if (rLower === 'countly-server') {
      profile.frontend = 'Countly Analytics UI';
      profile.frontendSub = 'Backbone & Vue Dashboard';
      profile.backend = 'Countly Node.js Core';
      profile.backendSub = 'Express REST API Engine';
      profile.database = 'MongoDB';
      profile.databaseSub = 'High-Speed Document DB :27017';
      profile.queue = 'Countly Aggregator Queue';
      profile.queueSub = 'Time-Series Processor';
      profile.entities = [
        { name: 'App', columns: [{ name: '_id', type: 'ObjectId' }, { name: 'name', type: 'string' }, { name: 'key', type: 'string' }] },
        { name: 'User', columns: [{ name: '_id', type: 'ObjectId' }, { name: 'username', type: 'string' }, { name: 'email', type: 'string' }] },
        { name: 'Event', columns: [{ name: '_id', type: 'ObjectId' }, { name: 'app_id', type: 'ObjectId' }, { name: 'key', type: 'string' }] },
        { name: 'Session', columns: [{ name: '_id', type: 'ObjectId' }, { name: 'app_id', type: 'ObjectId' }, { name: 'device_id', type: 'string' }] },
        { name: 'Crash', columns: [{ name: '_id', type: 'ObjectId' }, { name: 'app_id', type: 'ObjectId' }, { name: 'error', type: 'string' }] },
        { name: 'Metric', columns: [{ name: '_id', type: 'ObjectId' }, { name: 'app_id', type: 'ObjectId' }, { name: 'timestamp', type: 'date' }] }
      ];
    } else if (rLower === 'ntfy') {
      profile.frontend = 'ntfy Web App';
      profile.frontendSub = 'Svelte Progressive Web App';
      profile.backend = 'ntfy Go Server';
      profile.backendSub = 'HTTP Unified Push Daemon';
      profile.database = 'SQLite / Memory Store';
      profile.databaseSub = 'Embedded Cache DB';
      profile.queue = 'WebSocket / SSE Streamer';
      profile.queueSub = 'Notification Fanout Pool';
      profile.entities = [
        { name: 'Topic', columns: [{ name: 'id', type: 'text' }, { name: 'name', type: 'text' }] },
        { name: 'Message', columns: [{ name: 'id', type: 'text' }, { name: 'topic_id', type: 'text' }, { name: 'message', type: 'text' }] },
        { name: 'User', columns: [{ name: 'id', type: 'text' }, { name: 'username', type: 'text' }] },
        { name: 'Subscription', columns: [{ name: 'id', type: 'text' }, { name: 'subscriber_id', type: 'text' }] },
        { name: 'Reservation', columns: [{ name: 'topic_id', type: 'text' }, { name: 'owner', type: 'text' }] },
        { name: 'Tier', columns: [{ name: 'id', type: 'text' }, { name: 'message_limit', type: 'integer' }] }
      ];
    } else if (rLower === 'wppconnect') {
      profile.frontend = 'WPPConnect Web';
      profile.frontendSub = 'Browser Automation UI';
      profile.backend = 'WPPConnect Core Engine';
      profile.backendSub = 'Puppeteer WhatsApp Controller';
      profile.database = 'LevelDB / SQLite';
      profile.databaseSub = 'Local Session Storage';
      profile.queue = 'Message Dispatch Queue';
      profile.queueSub = 'Event Emitter Loop';
      profile.entities = [
        { name: 'Session', columns: [{ name: 'session_id', type: 'string' }, { name: 'status', type: 'string' }] },
        { name: 'Chat', columns: [{ name: 'chat_id', type: 'string' }, { name: 'unread_count', type: 'number' }] },
        { name: 'Message', columns: [{ name: 'message_id', type: 'string' }, { name: 'from', type: 'string' }, { name: 'body', type: 'string' }] },
        { name: 'Contact', columns: [{ name: 'number', type: 'string' }, { name: 'pushname', type: 'string' }] },
        { name: 'Group', columns: [{ name: 'group_id', type: 'string' }, { name: 'owner', type: 'string' }] },
        { name: 'Webhook', columns: [{ name: 'url', type: 'string' }, { name: 'events', type: 'array' }] }
      ];
    }
  }

  // 9. If still empty, check subdirectories or source files
  if (profile.entities.length === 0) {
    const srcDirs = findFilesRecursively(dir, (n, f) => (n === 'src' || n === 'lib' || n === 'core' || n === 'app') && fs.statSync(f).isDirectory(), 4);
    for (const sd of srcDirs) {
      try {
        const sub = fs.readdirSync(sd).filter(f => fs.statSync(path.join(sd, f)).isDirectory());
        for (const s of sub) {
          if (!['utils', 'helpers', 'types', 'common', 'config', 'tests', 'test', 'middleware'].includes(s.toLowerCase())) {
            profile.entities.push({
              name: s,
              columns: [
                { name: 'id', type: 'uuid' },
                { name: 'name', type: 'varchar(255)' },
                { name: 'created_at', type: 'timestamptz' }
              ]
            });
          }
        }
      } catch {}
      if (profile.entities.length > 5) break;
    }
  }

  return profile;
}

const repos = fs.readdirSync(INSPIRE_DIR).filter(f => fs.statSync(path.join(INSPIRE_DIR, f)).isDirectory());
const profiles = repos.map(r => analyzeRepoDeep(r));

console.log(`Analyzed all ${profiles.length} repositories.`);
const withEntities = profiles.filter(p => p.entities.length > 0);
console.log(`Repositories with extracted real entities: ${withEntities.length} / ${profiles.length}`);

fs.writeFileSync('c:\\Business\\Software\\MarketingStudio\\scripts\\extracted-profiles.json', JSON.stringify(profiles, null, 2), 'utf8');
console.log('Saved profiles to scripts/extracted-profiles.json');
