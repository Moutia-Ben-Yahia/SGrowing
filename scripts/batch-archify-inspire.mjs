import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const ARCHIFY_BIN = 'C:\\Users\\Admin\\.gemini\\config\\skills\\archify\\archify\\bin\\archify.mjs';
const INSPIRE_DIR = 'c:\\Business\\Software\\MarketingStudio\\inspire';
const BASE_OUT_DIR = 'c:\\Business\\Software\\MarketingStudio\\.archify\\inspire-architecture';
const OVERVIEW_HTML = 'c:\\Business\\Software\\MarketingStudio\\.archify\\inspire-architectures.html';

fs.mkdirSync(BASE_OUT_DIR, { recursive: true });

function parseRepo(repoName) {
  const repoPath = path.join(INSPIRE_DIR, repoName);
  let files = [];
  try { files = fs.readdirSync(repoPath); } catch { return null; }

  let pkg = null;
  if (files.includes('package.json')) {
    try { pkg = JSON.parse(fs.readFileSync(path.join(repoPath, 'package.json'), 'utf8')); } catch {}
  }

  // Scan compose files
  const composeFile = files.find(f => f.toLowerCase().includes('docker-compose') && (f.endsWith('.yml') || f.endsWith('.yaml'))) ||
                      files.find(f => f.toLowerCase().startsWith('compose.') && (f.endsWith('.yml') || f.endsWith('.yaml')));
  let composeText = '';
  if (composeFile) {
    try { composeText = fs.readFileSync(path.join(repoPath, composeFile), 'utf8'); } catch {}
  }

  // Scan dependencies and keywords
  let allDeps = '';
  if (pkg) {
    allDeps += Object.keys(pkg.dependencies || {}).join(' ') + ' ' + Object.keys(pkg.devDependencies || {}).join(' ');
  }
  if (files.includes('requirements.txt')) {
    try { allDeps += ' ' + fs.readFileSync(path.join(repoPath, 'requirements.txt'), 'utf8'); } catch {}
  }
  if (files.includes('pyproject.toml')) {
    try { allDeps += ' ' + fs.readFileSync(path.join(repoPath, 'pyproject.toml'), 'utf8'); } catch {}
  }
  if (files.includes('go.mod')) {
    try { allDeps += ' ' + fs.readFileSync(path.join(repoPath, 'go.mod'), 'utf8'); } catch {}
  }
  if (files.includes('Gemfile')) {
    try { allDeps += ' ' + fs.readFileSync(path.join(repoPath, 'Gemfile'), 'utf8'); } catch {}
  }
  if (files.includes('composer.json')) {
    try { allDeps += ' ' + fs.readFileSync(path.join(repoPath, 'composer.json'), 'utf8'); } catch {}
  }

  let readmeSnippet = '';
  const readmeFile = files.find(f => f.toLowerCase().startsWith('readme'));
  if (readmeFile) {
    try {
      const rm = fs.readFileSync(path.join(repoPath, readmeFile), 'utf8');
      const lines = rm.split('\n').map(l => l.trim()).filter(l => l && !l.startsWith('#') && !l.startsWith('!'));
      readmeSnippet = lines.slice(0, 3).join(' ').replace(/[\[\]\(\)*`]/g, '').slice(0, 150);
    } catch {}
  }

  const combined = (composeText + ' ' + allDeps + ' ' + files.join(' ')).toLowerCase();

  // Stack detection
  let stack = 'Node.js';
  if (files.includes('go.mod')) stack = 'Go';
  else if (files.includes('Gemfile')) stack = 'Ruby on Rails';
  else if (files.includes('requirements.txt') || files.includes('pyproject.toml')) stack = 'Python';
  else if (files.includes('composer.json')) stack = 'PHP';
  else if (files.includes('Cargo.toml')) stack = 'Rust';
  else if (repoName.toLowerCase() === 'asterisk') stack = 'C / Telephony';

  // Database detection
  let dbType = 'PostgreSQL';
  let dbSublabel = 'Relational DB :5432';
  if (combined.includes('mysql') || combined.includes('mariadb')) {
    dbType = 'MySQL';
    dbSublabel = 'Relational DB :3306';
  } else if (combined.includes('mongo')) {
    dbType = 'MongoDB';
    dbSublabel = 'Document Store :27017';
  } else if (combined.includes('sqlite') || combined.includes('litestream')) {
    dbType = 'SQLite';
    dbSublabel = 'Embedded Database';
  }

  // Cache & queue detection
  const hasRedis = combined.includes('redis') || combined.includes('valkey') || combined.includes('memcached');
  const hasQueue = combined.includes('bull') || combined.includes('celery') || combined.includes('sidekiq') || 
                   combined.includes('rabbitmq') || combined.includes('temporal') || combined.includes('kafka') ||
                   combined.includes('asynq');

  // Frontend detection
  const hasFrontend = files.includes('apps') || files.includes('packages') || files.includes('frontend') || 
                      files.includes('client') || files.includes('web') || files.includes('static') ||
                      combined.includes('react') || combined.includes('vue') || combined.includes('next') || 
                      combined.includes('nuxt') || combined.includes('svelte') || files.includes('vite.config.ts') ||
                      files.includes('tailwind.config.js');

  // Actors
  let actorLabel = 'Users & Clients';
  let actorSublabel = 'Web & Mobile Browser';
  let actorIcon = 'person';
  if (combined.includes('telegram') || repoName === 'aiogram' || repoName === 'grammY') {
    actorLabel = 'Telegram Users';
    actorSublabel = 'Bot Client & Groups';
    actorIcon = 'messagebus';
  } else if (combined.includes('discord') || repoName === 'discord.js' || repoName === 'Red-DiscordBot') {
    actorLabel = 'Discord Gateway';
    actorSublabel = 'Guild WebSocket Events';
    actorIcon = 'messagebus';
  } else if (repoName === 'asterisk') {
    actorLabel = 'SIP Endpoints';
    actorSublabel = 'VoIP Phone Clients';
    actorIcon = 'external';
  }

  // Frontend sublabel
  let feLabel = `${repoName} UI`;
  let feSublabel = 'Web Dashboard';
  if (combined.includes('next')) feSublabel = 'Next.js Frontend';
  else if (combined.includes('react')) feSublabel = 'React Dashboard';
  else if (combined.includes('vue')) feSublabel = 'Vue.js Dashboard';
  else if (combined.includes('svelte')) feSublabel = 'SvelteKit App';

  // Backend sublabel
  let beLabel = `${repoName} Server`;
  let beSublabel = `${stack} API Core`;
  if (combined.includes('nest')) beSublabel = 'NestJS API Server';
  else if (combined.includes('fastify')) beSublabel = 'Fastify HTTP Server';
  else if (combined.includes('express')) beSublabel = 'Express API Server';
  else if (combined.includes('fastapi')) beSublabel = 'FastAPI Async Engine';
  else if (combined.includes('django')) beSublabel = 'Django Core App';
  else if (combined.includes('rails')) beSublabel = 'Rails Application Server';
  else if (combined.includes('gin')) beSublabel = 'Gin Go HTTP Server';
  else if (repoName === 'asterisk') {
    beLabel = 'Asterisk Core PBX';
    beSublabel = 'Channels & Dialplan';
  }

  return {
    repoName,
    stack,
    hasFrontend,
    actorLabel,
    actorSublabel,
    actorIcon,
    feLabel,
    feSublabel,
    beLabel,
    beSublabel,
    dbType,
    dbSublabel,
    hasRedis,
    hasQueue,
    description: pkg?.description || readmeSnippet || `Open source ${stack} solution: ${repoName}`
  };
}

function buildCandidate(info) {
  const { repoName, stack, hasFrontend, actorLabel, actorSublabel, actorIcon,
          feLabel, feSublabel, beLabel, beSublabel, dbType, dbSublabel,
          hasRedis, hasQueue, description } = info;

  const components = [];
  const connections = [];
  const wraps = [];

  // Actor sits on the left outside the boundary
  components.push({
    id: 'actor',
    type: 'external',
    label: actorLabel,
    sublabel: actorSublabel,
    icon: actorIcon,
    pos: [40, 50],
    size: [130, 60]
  });

  let beColX = 230;
  let dbColX = 450;

  if (hasFrontend) {
    components.push({
      id: 'frontend',
      type: 'frontend',
      label: feLabel,
      sublabel: feSublabel,
      pos: [230, 50],
      size: [140, 60]
    });
    wraps.push('frontend');

    connections.push({
      id: 'actor-to-fe',
      from: 'actor',
      to: 'frontend',
      label: 'HTTPS',
      variant: 'emphasis'
    });

    beColX = 440;
    dbColX = 660;

    components.push({
      id: 'backend',
      type: 'backend',
      label: beLabel,
      sublabel: beSublabel,
      pos: [beColX, 50],
      size: [150, 60]
    });
    wraps.push('backend');

    connections.push({
      id: 'fe-to-be',
      from: 'frontend',
      to: 'backend',
      label: 'REST / WS'
    });
  } else {
    beColX = 230;
    dbColX = 450;

    components.push({
      id: 'backend',
      type: 'backend',
      label: beLabel,
      sublabel: beSublabel,
      pos: [beColX, 50],
      size: [150, 60]
    });
    wraps.push('backend');

    connections.push({
      id: 'actor-to-be',
      from: 'actor',
      to: 'backend',
      label: 'API Calls',
      variant: 'emphasis'
    });
  }

  components.push({
    id: 'db',
    type: 'database',
    label: dbType,
    sublabel: dbSublabel,
    pos: [dbColX, 50],
    size: [140, 60]
  });
  wraps.push('db');

  connections.push({
    id: 'be-to-db',
    from: 'backend',
    to: 'db',
    label: 'SQL / Store'
  });

  if (hasRedis) {
    components.push({
      id: 'redis',
      type: 'database',
      label: 'Redis',
      sublabel: 'Cache & PubSub',
      pos: [beColX, 160],
      size: [150, 60]
    });
    wraps.push('redis');

    connections.push({
      id: 'be-to-redis',
      from: 'backend',
      to: 'redis',
      label: 'Cache / State'
    });
  }

  if (hasQueue) {
    components.push({
      id: 'worker',
      type: 'backend',
      label: 'Async Worker',
      sublabel: 'Job Processor',
      pos: [dbColX, 160],
      size: [140, 60]
    });
    wraps.push('worker');

    connections.push({
      id: 'be-to-worker',
      from: 'backend',
      to: 'worker',
      label: 'Enqueue',
      variant: 'dashed'
    });

    connections.push({
      id: 'worker-to-db',
      from: 'worker',
      to: 'db',
      label: 'Persist'
    });
  }

  // External sits strictly outside on the right
  const extColX = dbColX + 210;
  components.push({
    id: 'ext',
    type: 'external',
    label: 'External Integrations',
    sublabel: 'SMTP / Storage / APIs',
    pos: [extColX, 50],
    size: [150, 60]
  });

  connections.push({
    id: 'be-to-ext',
    from: 'backend',
    to: 'ext',
    label: 'External I/O',
    variant: 'dashed',
    fromSide: 'top',
    toSide: 'top'
  });

  const boundaries = [
    {
      kind: 'region',
      label: `${repoName} Runtime Cluster`,
      wraps
    }
  ];

  const cards = [
    {
      dot: 'emerald',
      title: 'Architecture & Framework',
      items: [
        `Built on ${stack} with decoupled runtime responsibilities`,
        `Core logic orchestrated by ${beLabel}`,
        description.length > 120 ? description.slice(0, 120) + '...' : description
      ]
    },
    {
      dot: 'cyan',
      title: 'Data & Persistence',
      items: [
        `Primary data store powered by ${dbType}`,
        hasRedis ? 'Redis accelerates queries with in-memory caching and transient state' : 'Lightweight state management directly via primary persistence',
        hasQueue ? 'Background queues decouple compute-heavy workloads from user requests' : 'Direct request-response processing cycle'
      ]
    },
    {
      dot: 'violet',
      title: 'Connectivity & Boundaries',
      items: [
        'Secure communication ingress across standard protocols',
        'Modular adapters for external messaging, object storage, and notifications'
      ]
    }
  ];

  return {
    schema_version: 1,
    diagram_type: 'architecture',
    meta: {
      title: `${repoName} Architecture`,
      output: `.archify/inspire-architecture/${repoName}/${repoName}.html`,
      quality_profile: 'showcase'
    },
    components,
    boundaries,
    connections,
    cards
  };
}

async function run() {
  const repos = fs.readdirSync(INSPIRE_DIR).filter(f => fs.statSync(path.join(INSPIRE_DIR, f)).isDirectory());
  console.log(`Discovered ${repos.length} repositories in inspire.`);

  const results = [];
  let passed = 0;
  let failed = 0;

  for (let i = 0; i < repos.length; i++) {
    const repo = repos[i];
    const info = parseRepo(repo);
    if (!info) {
      console.log(`[${i+1}/${repos.length}] Skipping ${repo}`);
      continue;
    }

    const candidate = buildCandidate(info);
    const repoOutDir = path.join(BASE_OUT_DIR, repo);
    fs.mkdirSync(repoOutDir, { recursive: true });

    const candidateFile = path.join(repoOutDir, 'candidate.json');
    fs.writeFileSync(candidateFile, JSON.stringify(candidate, null, 2), 'utf8');

    const outHtml = path.join(repoOutDir, `${repo}.html`);
    const relHtml = `.archify/inspire-architecture/${repo}/${repo}.html`;

    try {
      const res = execFileSync('node', [
        ARCHIFY_BIN,
        'finalize',
        'architecture',
        candidateFile,
        outHtml,
        '--quality', 'showcase',
        '--json'
      ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });

      const parsed = JSON.parse(res);
      if (parsed.ok) {
        passed++;
        results.push({
          repo,
          stack: info.stack,
          db: info.dbType,
          hasRedis: info.hasRedis,
          hasQueue: info.hasQueue,
          description: info.description,
          status: 'success',
          htmlFile: outHtml,
          relHtml
        });
        process.stdout.write(`[${i+1}/${repos.length}] [OK] ${repo}\n`);
      } else {
        failed++;
        results.push({
          repo,
          stack: info.stack,
          status: 'fail',
          error: parsed.diagnostics?.[0]?.message || 'Unknown error'
        });
        process.stdout.write(`[${i+1}/${repos.length}] [FAIL] ${repo}: ${parsed.diagnostics?.[0]?.message}\n`);
      }
    } catch (err) {
      failed++;
      const stderr = err.stdout || err.stderr || err.message;
      let msg = 'Failed';
      try {
        const p = JSON.parse(stderr);
        msg = p.diagnostics?.[0]?.message || stderr.slice(0, 100);
      } catch {
        msg = stderr.slice(0, 100);
      }
      results.push({
        repo,
        stack: info.stack,
        status: 'fail',
        error: msg
      });
      process.stdout.write(`[${i+1}/${repos.length}] [ERR] ${repo}: ${msg}\n`);
    }
  }

  console.log(`\n========================================`);
  console.log(`Batch Summary: ${passed} Passed, ${failed} Failed out of ${repos.length}`);
  console.log(`========================================`);

  // Generate Overview Master Catalog HTML
  generateMasterCatalog(results);
}

function generateMasterCatalog(results) {
  const successful = results.filter(r => r.status === 'success');
  const total = results.length;
  
  const stacks = [...new Set(successful.map(r => r.stack))].sort();

  const cardsHtml = successful.map(r => `
    <div class="repo-card" data-stack="${r.stack.toLowerCase()}" data-name="${r.repo.toLowerCase()}">
      <div class="card-header">
        <h3 class="repo-title">${r.repo}</h3>
        <span class="badge badge-stack">${r.stack}</span>
      </div>
      <p class="repo-desc">${r.description || 'Open-source software platform'}</p>
      <div class="tags">
        <span class="tag">📦 ${r.db}</span>
        ${r.hasRedis ? '<span class="tag">⚡ Redis Cache</span>' : ''}
        ${r.hasQueue ? '<span class="tag">🔄 Async Worker</span>' : ''}
      </div>
      <div class="card-actions">
        <a href="${r.repo}/${r.repo}.html" class="btn btn-primary" target="_blank">Explore Diagram &rarr;</a>
      </div>
    </div>
  `).join('\n');

  const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Inspire Repositories - Architecture Catalog</title>
  <style>
    :root {
      --bg: #0b0f19;
      --card-bg: #111827;
      --border: #1f2937;
      --text: #f9fafb;
      --muted: #9ca3af;
      --primary: #3b82f6;
      --primary-hover: #2563eb;
      --badge-bg: #1e3a8a;
      --badge-text: #93c5fd;
      --tag-bg: #1f2937;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      background: var(--bg);
      color: var(--text);
      line-height: 1.5;
      padding: 2.5rem 1.5rem;
    }
    .container { max-width: 1300px; margin: 0 auto; }
    header { margin-bottom: 2.5rem; text-align: center; }
    h1 { font-size: 2.25rem; font-weight: 700; margin-bottom: 0.5rem; letter-spacing: -0.025em; }
    p.lead { color: var(--muted); font-size: 1.1rem; max-width: 700px; margin: 0 auto; }
    .stats-bar {
      display: flex;
      justify-content: center;
      gap: 2rem;
      margin: 1.5rem 0;
    }
    .stat-pill {
      background: var(--card-bg);
      border: 1px solid var(--border);
      padding: 0.5rem 1.25rem;
      border-radius: 9999px;
      font-size: 0.95rem;
    }
    .stat-num { font-weight: 700; color: #60a5fa; }
    .controls {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem;
      background: var(--card-bg);
      padding: 1rem 1.25rem;
      border-radius: 0.75rem;
      border: 1px solid var(--border);
    }
    .search-input {
      flex: 1;
      min-width: 260px;
      padding: 0.6rem 1rem;
      background: #0b0f19;
      border: 1px solid var(--border);
      border-radius: 0.5rem;
      color: var(--text);
      font-size: 0.95rem;
    }
    .search-input:focus { outline: none; border-color: var(--primary); }
    .filter-group { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .filter-btn {
      background: #1f2937;
      color: var(--muted);
      border: none;
      padding: 0.4rem 0.85rem;
      border-radius: 0.375rem;
      font-size: 0.85rem;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .filter-btn.active, .filter-btn:hover {
      background: var(--primary);
      color: #fff;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
      gap: 1.5rem;
    }
    .repo-card {
      background: var(--card-bg);
      border: 1px solid var(--border);
      border-radius: 0.75rem;
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      transition: transform 0.15s ease, border-color 0.15s ease;
    }
    .repo-card:hover {
      transform: translateY(-2px);
      border-color: #374151;
    }
    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 0.75rem;
      gap: 0.5rem;
    }
    .repo-title { font-size: 1.15rem; font-weight: 600; color: #fff; }
    .badge {
      font-size: 0.75rem;
      padding: 0.2rem 0.6rem;
      border-radius: 9999px;
      font-weight: 600;
      white-space: nowrap;
    }
    .badge-stack { background: var(--badge-bg); color: var(--badge-text); }
    .repo-desc {
      color: var(--muted);
      font-size: 0.875rem;
      margin-bottom: 1rem;
      flex-grow: 1;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .tags {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
      margin-bottom: 1.25rem;
    }
    .tag {
      background: var(--tag-bg);
      font-size: 0.75rem;
      padding: 0.2rem 0.5rem;
      border-radius: 0.25rem;
      color: #d1d5db;
    }
    .btn {
      display: inline-block;
      width: 100%;
      text-align: center;
      padding: 0.6rem 1rem;
      border-radius: 0.5rem;
      font-weight: 500;
      font-size: 0.9rem;
      text-decoration: none;
      transition: background 0.15s ease;
    }
    .btn-primary { background: var(--primary); color: #fff; }
    .btn-primary:hover { background: var(--primary-hover); }
  </style>
</head>
<body>
  <div class="container">
    <header>
      <h1>Inspire Open Source Architecture Catalog</h1>
      <p class="lead">Interactive architecture diagrams compiled with Archify for open-source marketing, CRM, communications, and automation systems.</p>
      <div class="stats-bar">
        <div class="stat-pill"><span class="stat-num">${successful.length}</span> / ${total} Architectures Generated</div>
        <div class="stat-pill"><span class="stat-num">${stacks.length}</span> Distinct Tech Stacks</div>
      </div>
    </header>

    <div class="controls">
      <input type="text" id="searchInput" class="search-input" placeholder="Search repositories by name, stack, or database...">
      <div class="filter-group">
        <button class="filter-btn active" data-filter="all">All (${successful.length})</button>
        ${stacks.map(s => `<button class="filter-btn" data-filter="${s.toLowerCase()}">${s}</button>`).join('')}
      </div>
    </div>

    <div class="grid" id="repoGrid">
      ${cardsHtml}
    </div>
  </div>

  <script>
    const searchInput = document.getElementById('searchInput');
    const filterBtns = document.querySelectorAll('.filter-btn');
    const cards = document.querySelectorAll('.repo-card');

    let activeFilter = 'all';

    function filterCards() {
      const q = searchInput.value.toLowerCase().trim();
      cards.forEach(card => {
        const name = card.dataset.name;
        const stack = card.dataset.stack;
        const text = card.textContent.toLowerCase();

        const matchesFilter = activeFilter === 'all' || stack.includes(activeFilter);
        const matchesSearch = !q || text.includes(q);

        if (matchesFilter && matchesSearch) {
          card.style.display = 'flex';
        } else {
          card.style.display = 'none';
        }
      });
    }

    searchInput.addEventListener('input', filterCards);

    filterBtns.forEach(btn => {
      btn.addEventListener('click', () => {
        filterBtns.forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        activeFilter = btn.dataset.filter;
        filterCards();
      });
    });
  </script>
</body>
</html>`;

  fs.writeFileSync(OVERVIEW_HTML, html, 'utf8');
  console.log(`Generated Master Catalog: ${OVERVIEW_HTML}`);
}

run().catch(console.error);
