import fs from 'node:fs';
import path from 'node:path';

const INSPIRE_DIR = 'c:\\Business\\Software\\MarketingStudio\\inspire';

function findFilesRecursively(dir, filterFn, maxDepth = 4, depth = 0) {
  if (depth > maxDepth) return [];
  const results = [];
  try {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const e of entries) {
      if (['node_modules', '.git', 'dist', 'build', '.next', 'coverage', 'tmp', 'vendor'].includes(e.name)) continue;
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

export function analyzeRepo(repoName) {
  const dir = path.join(INSPIRE_DIR, repoName);
  const info = {
    repo: repoName,
    title: repoName,
    description: '',
    language: 'TypeScript / JavaScript',
    frontend: 'React / Next.js',
    backend: 'Node.js',
    database: 'PostgreSQL',
    cache: 'Redis',
    queue: 'BullMQ',
    storage: 'S3 / MinIO',
    externalServices: ['Webhooks', 'SMTP / SES', 'OAuth 2.0'],
    entities: [],
    primaryWorkflow: {
      action: 'Core Transaction',
      endpoint: '/api/v1/resource',
      payload: 'Resource data',
      event: 'resource.created'
    }
  };

  if (!fs.existsSync(dir)) return info;

  // 1. Root files
  let rootFiles = [];
  try { rootFiles = fs.readdirSync(dir); } catch { return info; }

  // 2. Package.json
  if (rootFiles.includes('package.json')) {
    try {
      const pkg = JSON.parse(fs.readFileSync(path.join(dir, 'package.json'), 'utf8'));
      if (pkg.description) info.description = pkg.description;
      const allDeps = { ...pkg.dependencies, ...pkg.devDependencies };
      
      if (allDeps['next']) { info.frontend = 'Next.js App Router'; info.backend = 'Next.js API & Server Actions'; }
      else if (allDeps['react']) { info.frontend = 'React (SPA)'; }
      else if (allDeps['vue']) { info.frontend = 'Vue.js / Vite'; }
      else if (allDeps['@angular/core']) { info.frontend = 'Angular UI'; }
      else if (allDeps['svelte']) { info.frontend = 'SvelteKit'; }

      if (allDeps['@nestjs/core']) { info.backend = 'NestJS Backend'; }
      else if (allDeps['express']) { info.backend = 'Express.js Engine'; }
      else if (allDeps['fastify']) { info.backend = 'Fastify High-Throughput'; }
      else if (allDeps['hono']) { info.backend = 'Hono Lightweight Edge API'; }

      if (allDeps['bullmq'] || allDeps['bull']) { info.queue = 'BullMQ Worker Engine'; }
      if (allDeps['prisma'] || allDeps['@prisma/client']) { info.database = 'PostgreSQL (Prisma ORM)'; }
      else if (allDeps['typeorm']) { info.database = 'PostgreSQL (TypeORM)'; }
      else if (allDeps['drizzle-orm']) { info.database = 'PostgreSQL (Drizzle ORM)'; }
      else if (allDeps['mongoose'] || allDeps['mongodb']) { info.database = 'MongoDB'; }
    } catch {}
  }

  // 3. Rails
  if (rootFiles.includes('Gemfile')) {
    info.language = 'Ruby';
    info.backend = 'Ruby on Rails (Puma)';
    info.frontend = 'Hotwire & Stimulus / ViewComponents';
    info.queue = 'Sidekiq Background Jobs';
    info.database = 'PostgreSQL (ActiveRecord)';
  }

  // 4. Go
  if (rootFiles.includes('go.mod')) {
    info.language = 'Go';
    info.backend = 'Go Engine (Goroutines)';
    info.queue = 'Asynq / Redis Queue';
    info.database = 'PostgreSQL (pgx)';
    try {
      const mod = fs.readFileSync(path.join(dir, 'go.mod'), 'utf8');
      if (mod.includes('gin-gonic/gin')) info.backend = 'Gin Web Framework (Go)';
      else if (mod.includes('go-chi/chi')) info.backend = 'Chi Lightweight Router (Go)';
      else if (mod.includes('gofiber/fiber')) info.backend = 'Fiber HTTP (Go)';
    } catch {}
  }

  // 5. Python
  if (rootFiles.includes('requirements.txt') || rootFiles.includes('pyproject.toml')) {
    info.language = 'Python';
    info.backend = 'FastAPI ASGI';
    info.queue = 'Celery Worker Pool';
    info.database = 'PostgreSQL (SQLAlchemy)';
    let pyText = '';
    try {
      if (rootFiles.includes('requirements.txt')) pyText += fs.readFileSync(path.join(dir, 'requirements.txt'), 'utf8');
      if (rootFiles.includes('pyproject.toml')) pyText += fs.readFileSync(path.join(dir, 'pyproject.toml'), 'utf8');
      if (pyText.includes('django')) {
        info.backend = 'Django WSGI/ASGI';
        info.database = 'PostgreSQL (Django ORM)';
        info.frontend = 'Django Templates & HTMX';
      } else if (pyText.includes('fastapi')) {
        info.backend = 'FastAPI ASGI (Uvicorn)';
      }
    } catch {}
  }

  // 6. PHP / Laravel
  if (rootFiles.includes('composer.json')) {
    info.language = 'PHP';
    info.backend = 'Laravel (PHP-FPM)';
    info.frontend = 'Blade & Livewire / Vue';
    info.queue = 'Laravel Horizon / Redis';
    info.database = 'MySQL / PostgreSQL (Eloquent)';
  }

  // 7. Schema Extraction
  // A. Rails schema.rb
  const schemaRbPath = path.join(dir, 'db', 'schema.rb');
  if (fs.existsSync(schemaRbPath)) {
    try {
      const schemaText = fs.readFileSync(schemaRbPath, 'utf8');
      const tableRegex = /create_table\s+"([^"]+)"[^\n]*\s+do\s+\|t\|([\s\S]*?)end/g;
      let m;
      while ((m = tableRegex.exec(schemaText)) !== null) {
        const tableName = m[1];
        const body = m[2];
        const cols = [];
        const colLines = body.split('\n');
        for (const line of colLines) {
          const colMatch = line.match(/t\.([a-z_]+)\s+"([^"]+)"/);
          if (colMatch) {
            cols.push({ name: colMatch[2], type: colMatch[1] });
          }
        }
        info.entities.push({ name: tableName, columns: cols.slice(0, 6) });
      }
    } catch {}
  }

  // B. Prisma schemas
  if (info.entities.length === 0) {
    const prismaFiles = findFilesRecursively(dir, name => name.endsWith('.prisma'), 5);
    for (const pf of prismaFiles) {
      try {
        const text = fs.readFileSync(pf, 'utf8');
        const modelRegex = /model\s+(\w+)\s+{([\s\S]*?)}/g;
        let m;
        while ((m = modelRegex.exec(text)) !== null) {
          const modelName = m[1];
          const body = m[2];
          const cols = [];
          const lines = body.split('\n');
          for (const l of lines) {
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
          info.entities.push({ name: modelName, columns: cols.slice(0, 6) });
        }
      } catch {}
      if (info.entities.length > 5) break;
    }
  }

  // C. SQL migration / queries extraction
  if (info.entities.length === 0) {
    const sqlFiles = findFilesRecursively(dir, name => name.endsWith('.sql'), 4);
    for (const sf of sqlFiles.slice(0, 5)) {
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
          info.entities.push({ name: rawName, columns: cols.slice(0, 6) });
        }
      } catch {}
    }
  }

  // D. NestJS / TypeORM / Drizzle models or directories
  if (info.entities.length === 0) {
    const modulesDir = path.join(dir, 'packages', 'twenty-server', 'src', 'modules');
    if (fs.existsSync(modulesDir)) {
      try {
        const subdirs = fs.readdirSync(modulesDir).filter(f => fs.statSync(path.join(modulesDir, f)).isDirectory());
        for (const sub of subdirs) {
          info.entities.push({
            name: sub,
            columns: [
              { name: 'id', type: 'uuid' },
              { name: 'workspace_id', type: 'uuid' },
              { name: 'name', type: 'varchar' },
              { name: 'created_at', type: 'timestamp' }
            ]
          });
        }
      } catch {}
    }
  }

  return info;
}

// Quick test run
const testSample = ['twenty', 'chatwoot', 'listmonk', 'formbricks', 'documenso', 'docuseal', 'fider', 'countly-server', 'Ghost', 'n8n', 'AgentSDR', 'DeskcommCRM'];
for (const repo of testSample) {
  const res = analyzeRepo(repo);
  console.log(`Repo: ${repo}`);
  console.log(`  Tech: Lang=${res.language}, FE=${res.frontend}, BE=${res.backend}, DB=${res.database}, Queue=${res.queue}`);
  console.log(`  Entities found: ${res.entities.length} ->`, res.entities.slice(0, 5).map(e => e.name).join(', '));
}
