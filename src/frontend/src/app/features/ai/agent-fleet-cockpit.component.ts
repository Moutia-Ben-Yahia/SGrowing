import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface AgentPersona {
  roleKey: string;
  name: string;
  layer: 'Business' | 'Engineering';
  objective: string;
  model: string;
  status: 'Active' | 'Running' | 'Idle';
}

export interface HeartbeatFinding {
  id: string;
  agentName: string;
  status: 'Healthy' | 'Warning' | 'ActionRequired';
  summary: string;
  action: string;
  timestamp: string;
}

@Component({
  selector: 'app-agent-fleet-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Header -->
      <header class="border-b border-slate-800 bg-slate-900/60 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-50">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-slate-200">Dual-Layer 38 Agent Fleet</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-medium">
              Evo-Nexus Multi-Agent OS
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="runHeartbeatSweep()" [disabled]="isSweeping()" class="px-4 py-2 bg-gradient-to-r from-cyan-600 to-teal-600 hover:from-cyan-500 hover:to-teal-500 disabled:opacity-50 text-white rounded-lg text-xs font-bold transition shadow-lg shadow-cyan-950 flex items-center gap-2">
            @if (isSweeping()) {
              <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
              Sweeping Agents...
            } @else {
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z"/></svg>
              Run Autonomous Heartbeat Sweep
            }
          </button>
          <app-language-switcher />
        </div>
      </header>

      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Overview Stats -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Total Agent Workforce</div>
            <div class="text-2xl font-bold text-white mt-2">{{ personas().length }} Personas</div>
            <div class="text-xs text-slate-500 mt-1">17 Business &bull; 21 Engineering</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Active Personas</div>
            <div class="text-2xl font-bold text-emerald-400 mt-2">{{ activeCount() }} Online</div>
            <div class="text-xs text-emerald-500/80 mt-1">Autonomous cron heartbeat active</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Anomalies Detected</div>
            <div class="text-2xl font-bold text-amber-400 mt-2">{{ anomaliesCount() }} Alerts</div>
            <div class="text-xs text-amber-500/80 mt-1">Deal margins & stock shortages</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">LLM Router Engine</div>
            <div class="text-2xl font-bold text-cyan-400 mt-2">OpenClaude</div>
            <div class="text-xs text-slate-500 mt-1">Multi-model fallback & token quotas</div>
          </div>
        </div>

        <!-- Heartbeat Live Findings Banner -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 shadow-xl space-y-3">
          <div class="flex items-center justify-between border-b border-slate-800 pb-3">
            <div class="flex items-center gap-2">
              <span class="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-pulse"></span>
              <h2 class="text-sm font-bold text-white">Autonomous Heartbeat Findings & Actions</h2>
            </div>
            <div class="flex items-center gap-3">
              <button (click)="runRemediation()" [disabled]="isRemediating()" class="px-3 py-1 bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow">
                <span>⚡</span> {{ isRemediating() ? 'Executing Self-Healing...' : 'Execute Automated Remediation' }}
              </button>
              <span class="text-xs text-slate-400 font-mono">Last Cycle: Just Now</span>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
            @for (finding of findings(); track finding.id) {
              <div class="bg-slate-950 p-3.5 rounded-xl border border-slate-800 space-y-2">
                <div class="flex items-center justify-between">
                  <span class="font-bold text-xs text-white">{{ finding.agentName }}</span>
                  <span [class]="getFindingBadge(finding.status)">{{ finding.status }}</span>
                </div>
                <p class="text-xs text-slate-300 leading-snug">{{ finding.summary }}</p>
                <div class="text-[11px] text-cyan-400 font-medium border-t border-slate-900 pt-1.5">
                  &rarr; {{ finding.action }}
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Filter Controls -->
        <div class="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3 border-b border-slate-800 pb-4">
          <div class="flex items-center gap-2">
            <button (click)="filterLayer.set('ALL')" [class.bg-slate-800]="filterLayer() === 'ALL'" class="px-3 py-1.5 rounded-lg text-xs font-semibold text-slate-300 hover:bg-slate-800 transition">
              All Personas ({{ personas().length }})
            </button>
            <button (click)="filterLayer.set('Business')" [class.bg-slate-800]="filterLayer() === 'Business'" class="px-3 py-1.5 rounded-lg text-xs font-semibold text-cyan-400 hover:bg-slate-800 transition">
              Business Layer (17)
            </button>
            <button (click)="filterLayer.set('Engineering')" [class.bg-slate-800]="filterLayer() === 'Engineering'" class="px-3 py-1.5 rounded-lg text-xs font-semibold text-violet-400 hover:bg-slate-800 transition">
              Engineering Layer (21)
            </button>
          </div>

          <input [(ngModel)]="searchQuery" type="text" placeholder="Search personas by role or objective..." class="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-cyan-500 w-full md:w-64">
        </div>

        <!-- Persona Cards Grid -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (p of filteredPersonas(); track p.roleKey) {
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 space-y-3 hover:border-slate-700 transition shadow">
              <div class="flex items-start justify-between gap-2">
                <div>
                  <h3 class="font-bold text-sm text-white">{{ p.name }}</h3>
                  <span class="text-[11px] font-mono text-slate-500">{{ p.roleKey }}</span>
                </div>
                <span [class]="getLayerBadge(p.layer)">{{ p.layer }}</span>
              </div>

              <p class="text-xs text-slate-300 leading-relaxed min-h-[36px]">{{ p.objective }}</p>

              <div class="flex items-center justify-between pt-2 border-t border-slate-800/80 text-[11px]">
                <span class="font-mono text-slate-500">{{ p.model }}</span>
                <button (click)="runAgentTask(p)" class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs font-medium transition">
                  Dispatch Prompt
                </button>
              </div>
            </div>
          }
        </div>
      </main>

      <!-- Task Modal -->
      @if (activeModalAgent()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-sm font-bold text-white">Execute Task with {{ activeModalAgent()?.name }}</h3>
                <span class="text-xs text-slate-400 font-mono">{{ activeModalAgent()?.roleKey }}</span>
              </div>
              <button (click)="activeModalAgent.set(null)" class="text-slate-400 hover:text-white">&times;</button>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Task Prompt / Instruction</label>
                <textarea [(ngModel)]="taskPrompt" rows="3" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-cyan-500" placeholder="Analyze recent COD orders for high return rate patterns."></textarea>
              </div>

              @if (taskOutput()) {
                <div class="p-3 bg-slate-950 border border-slate-800 rounded-lg text-xs font-mono text-emerald-400 max-h-36 overflow-y-auto leading-relaxed">
                  {{ taskOutput() }}
                </div>
              }
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button (click)="activeModalAgent.set(null)" class="px-4 py-2 bg-slate-800 text-slate-300 rounded-lg text-xs font-medium transition">
                Close
              </button>
              <button (click)="executeTask()" [disabled]="isTaskRunning()" class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg text-xs font-bold transition">
                @if (isTaskRunning()) { Executing... } @else { Execute Agent }
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AgentFleetCockpitComponent {
  private translationService = inject(TranslationService);

  filterLayer = signal<'ALL' | 'Business' | 'Engineering'>('ALL');
  searchQuery = '';
  isSweeping = signal<boolean>(false);
  isRemediating = signal<boolean>(false);
  activeModalAgent = signal<AgentPersona | null>(null);
  taskPrompt = 'Audit current deal profit margins and report any anomalies.';
  isTaskRunning = signal<boolean>(false);
  taskOutput = signal<string | null>(null);

  findings = signal<HeartbeatFinding[]>([
    {
      id: 'f-01',
      agentName: 'RevOps Strategist',
      status: 'Warning',
      summary: 'Detected 1 deal with true gross margin below 25% target.',
      action: 'Audit operator labor hours and courier surcharges on account.',
      timestamp: '10:02'
    },
    {
      id: 'f-02',
      agentName: 'Logistics Controller',
      status: 'ActionRequired',
      summary: 'Sfax Hub inventory for SKU-GROW-PRO dropped to 35 units.',
      action: 'Generated automated transfer manifest TRF-20261028-S01.',
      timestamp: '10:02'
    },
    {
      id: 'f-03',
      agentName: 'Delivery Scrum Master',
      status: 'Healthy',
      summary: 'All 5 client onboarding sprint milestones progressing on schedule.',
      action: 'Tokenized client portal active with executive signoffs.',
      timestamp: '10:02'
    },
    {
      id: 'f-04',
      agentName: '.NET Architecture Sentinel',
      status: 'Healthy',
      summary: 'Optimistic concurrency RowVersion and PostGIS bounding-box active.',
      action: 'Connection pooling & circuit breakers verified.',
      timestamp: '10:02'
    }
  ]);

  personas = signal<AgentPersona[]>([
    // Business (17)
    { roleKey: 'revops-strategist', name: 'RevOps Strategist', layer: 'Business', objective: 'Deal velocity and True Gross Margin optimization', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'logistics-controller', name: 'Logistics Controller', layer: 'Business', objective: 'Multi-warehouse inventory routing and SLA carrier oversight', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'delivery-scrum-master', name: 'Delivery Scrum Master', layer: 'Business', objective: 'Client onboarding sprint milestone tracking and signoff', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'outbound-sdr', name: 'Outbound AI SDR', layer: 'Business', objective: 'Deep prospect research, tech stack profiling, and sales hooks', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'customer-support-copilot', name: 'Customer Support Copilot', layer: 'Business', objective: 'Omnichannel inbox resolution and pgvector RAG synthesis', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'billing-auditor', name: 'Billing & Float Auditor', layer: 'Business', objective: 'Courier COD remittance statement reconciliation and cash float', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'churn-risk-sentinel', name: 'Retention & Churn Sentinel', layer: 'Business', objective: 'Predictive LTV tracking and customer dissatisfaction recovery', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'lead-qualifier', name: 'Inbound Lead Qualifier', layer: 'Business', objective: 'Automated conversation intent detection and calendar booking', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'ecommerce-merchandiser', name: 'Catalog Merchandiser', layer: 'Business', objective: 'PIM catalog synchronization and tiered wholesale pricing', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'social-growth-manager', name: 'Social Media Strategist', layer: 'Business', objective: 'Multi-platform post scheduling and content viral hooks', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'seo-content-specialist', name: 'SEO Content Strategist', layer: 'Business', objective: 'Programmatic keyword clustering and SERP position audits', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'contract-legal-officer', name: 'Digital Contracts Guardian', layer: 'Business', objective: 'Cryptographic contract sealing and audit log verification', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'ad-spend-optimizer', name: 'Ad Spend Optimizer', layer: 'Business', objective: 'ROAS tracking and CAPI conversion event synchronization', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'affiliate-partner-liaison', name: 'Partner Affiliate Liaison', layer: 'Business', objective: 'Creator commission tiers and influencer link tracking', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'cross-border-trade-advisor', name: 'Cross-Border Trade Advisor', layer: 'Business', objective: 'Customs tariffs, FX rates, and international shipping bounds', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'feedback-csat-analyst', name: 'CSAT & Feedback Analyst', layer: 'Business', objective: 'NPS sentiment evaluation and user feature upvoting', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'privacy-compliance-officer', name: 'GDPR & Privacy Officer', layer: 'Business', objective: 'Tenant data erasure requests and PII masking audits', model: 'gemini-2.0-flash', status: 'Active' },

    // Engineering (21)
    { roleKey: 'dotnet-architect', name: '.NET 10 System Architect', layer: 'Engineering', objective: 'Modular monolith boundaries, CQRS, and dependency graphs', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'api-designer', name: 'Minimal API Contract Designer', layer: 'Engineering', objective: 'RFC 7807 problem details, OpenAPI v3, and rate limiting', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'ef-core-specialist', name: 'EF Core 10 Specialist', layer: 'Engineering', objective: 'Spatial GiST index queries, compiled models, and migrations', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'performance-analyst', name: 'Performance & Concurrency Analyst', layer: 'Engineering', objective: 'Memory allocation reduction, zero-alloc spans, and lock safety', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'security-auditor', name: 'Security & Cryptography Auditor', layer: 'Engineering', objective: 'AES-256-GCM vault inspection and OWASP compliance', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'test-engineer', name: 'Quality & TDD Test Engineer', layer: 'Engineering', objective: 'xUnit parallel test suites, WebApplicationFactory, and assertions', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'devops-engineer', name: 'Aspire & Cloud DevOps Engineer', layer: 'Engineering', objective: 'Multi-stage Docker builds, Kubernetes manifests, and CI/CD', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'frontend-architect', name: 'Angular 22 Signals Architect', layer: 'Engineering', objective: 'Zoneless change detection, computed signals, and LTR UX', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'code-reviewer', name: 'Holistic Code Reviewer', layer: 'Engineering', objective: 'Blast-radius analysis and convention enforcement', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'build-error-resolver', name: 'Build & Compilation Guardian', layer: 'Engineering', objective: 'Zero warning compilation and NuGet dependency resolution', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'refactor-cleaner', name: 'Refactoring & De-sloppifier', layer: 'Engineering', objective: 'Systematic dead code pruning and structural elegance', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'database-dba', name: 'PostgreSQL & PostGIS DBA', layer: 'Engineering', objective: 'Query plan analysis, WAL checkpointing, and GiST indexes', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'redis-cache-specialist', name: 'Distributed Cache Engineer', layer: 'Engineering', objective: 'Idempotency key TTLs, Redis pub/sub, and sliding caches', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'telemetry-sre', name: 'OpenTelemetry & SRE Engineer', layer: 'Engineering', objective: 'Distributed ActivitySource traces, metrics, and alerting', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'whatsapp-protocol-engineer', name: 'WhatsApp Protocol Specialist', layer: 'Engineering', objective: 'Noise XX handshakes, Signal ratchet E2EE, and WABinary codec', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'queue-worker-specialist', name: 'Background Jobs Specialist', layer: 'Engineering', objective: 'Worker daemon resilience, exponential backoff, and DLQ', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'mcp-server-engineer', name: 'Model Context Protocol Engineer', layer: 'Engineering', objective: 'Tool definitions, resource routing, and JSON-RPC transport', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'internationalization-lead', name: 'i18n & Localization Specialist', layer: 'Engineering', objective: 'Directional LTR layouts, translation schemas, and Unicode', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'accessibility-a11y-auditor', name: 'WCAG 2.2 Accessibility Auditor', layer: 'Engineering', objective: 'ARIA live regions, contrast ratios, and keyboard focus', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'resilience-polly-engineer', name: 'Chaos & Resilience Engineer', layer: 'Engineering', objective: 'Circuit breakers, bulkhead isolation, and fallback policies', model: 'gemini-2.0-flash', status: 'Active' },
    { roleKey: 'storage-media-specialist', name: 'S3 Object Storage Engineer', layer: 'Engineering', objective: 'Pre-signed URL generation, tamper hashes, and direct uploads', model: 'gemini-2.0-flash', status: 'Active' }
  ]);

  filteredPersonas = computed(() => {
    const l = this.filterLayer();
    const q = this.searchQuery.toLowerCase().trim();
    return this.personas().filter(p => {
      const matchLayer = l === 'ALL' || p.layer === l;
      const matchSearch = !q || p.name.toLowerCase().includes(q) || p.roleKey.toLowerCase().includes(q) || p.objective.toLowerCase().includes(q);
      return matchLayer && matchSearch;
    });
  });

  activeCount = computed(() => this.personas().filter(p => p.status === 'Active').length);
  anomaliesCount = computed(() => this.findings().filter(f => f.status !== 'Healthy').length);

  getLayerBadge(layer: string): string {
    return layer === 'Business'
      ? 'px-2 py-0.5 rounded text-[10px] font-semibold bg-cyan-500/10 text-cyan-400 border border-cyan-500/20'
      : 'px-2 py-0.5 rounded text-[10px] font-semibold bg-violet-500/10 text-violet-400 border border-violet-500/20';
  }

  getFindingBadge(status: string): string {
    switch (status) {
      case 'Healthy':
        return 'px-2 py-0.5 rounded text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20';
      case 'Warning':
        return 'px-2 py-0.5 rounded text-[10px] font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20';
      default:
        return 'px-2 py-0.5 rounded text-[10px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20';
    }
  }

  runHeartbeatSweep() {
    this.isSweeping.set(true);
    setTimeout(() => {
      this.isSweeping.set(false);
      this.findings.update(list => [
        {
          id: 'f-' + Math.random().toString(36).substring(2, 6),
          agentName: 'RevOps Strategist',
          status: 'Healthy',
          summary: 'Cycle complete. All 14 sales deals maintain healthy gross margins.',
          action: 'Audit logs synced to PostgreSQL.',
          timestamp: new Date().toTimeString().split(' ')[0].substring(0, 5)
        },
        ...list.slice(0, 3)
      ]);
    }, 1200);
  }

  runRemediation() {
    this.isRemediating.set(true);
    setTimeout(() => {
      this.isRemediating.set(false);
      this.findings.update(list => [
        {
          id: 'rem-' + Math.random().toString(36).substring(2, 6),
          agentName: 'Logistics Controller',
          status: 'Healthy',
          summary: 'Auto-Replenished Sfax Regional Depot with 100 units (Manifest TRF-AUTO-8921).',
          action: 'Buffer stock restored to 115 units.',
          timestamp: new Date().toTimeString().split(' ')[0].substring(0, 5)
        },
        ...list.slice(0, 3)
      ]);
    }, 900);
  }

  runAgentTask(agent: AgentPersona) {
    this.activeModalAgent.set(agent);
    this.taskOutput.set(null);
  }

  executeTask() {
    this.isTaskRunning.set(true);
    setTimeout(() => {
      this.isTaskRunning.set(false);
      const agent = this.activeModalAgent();
      this.taskOutput.set(`[${agent?.name}] Task execution complete: Evaluated "${this.taskPrompt}". Parameters validated, telemetry recorded in SGrowingTelemetry ActivitySource.`);
    }, 800);
  }
}
