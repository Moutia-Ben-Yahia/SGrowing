import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ProviderMetricView {
  provider: string;
  name: string;
  matchRate: number;
  latencyMs: number;
  queriesToday: number;
}

export interface EnrichedLeadView {
  domain: string;
  legalName: string;
  industry: string;
  employeeRange: string;
  annualRevenue: string;
  contactEmail: string;
  contactName: string;
  jobTitle: string;
  seniority: string;
  winningProvider: string;
  deliverability: 'DeliverableWithMx' | 'Valid' | 'Risky' | 'Bounced';
  techStack: string[];
}

@Component({
  selector: 'app-waterfall-enrichment-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="waterfall-enrichment-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-cyan-400">
            <span class="inline-block w-2 h-2 rounded-full bg-cyan-400 animate-pulse"></span>
            Waterfall Lead Enrichment & Deliverability • Horizon 61
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous B2B Lead Enrichment & Waterfall Aggregator
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Cascading enrichment across Clearbit, Apollo, ZoomInfo & PDL with conflict arbitration & SMTP MX verification.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="triggerWaterfallEnrichment()"
            data-testid="enrich-lead-btn"
            class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-cyan-600/20"
          >
            <span>⚡</span> Trigger Waterfall Enrichment
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Cascaded Match Rate</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-match-rate">
            {{ overallMatchRate() }}%
          </div>
          <div class="text-slate-400 text-xs mt-1">+34% vs Single-Provider Monolith</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">P95 Cascade Latency</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-avg-latency">
            {{ avgLatency() }} ms
          </div>
          <div class="text-cyan-400 text-xs mt-1">Parallel Sub-second Fallbacks</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Verified Deliverable Contacts</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-verified-count">
            {{ verifiedCount() }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">Zero Bounce SLA Verified</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Waterfall Providers</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">4 Tiers</div>
          <div class="text-slate-400 text-xs mt-1">Clearbit • Apollo • ZoomInfo • PDL</div>
        </div>
      </div>

      <!-- Main Stage: Providers Health & Enriched Leads Viewport -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Providers Status (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Enrichment Waterfall Health</h3>
              <p class="text-xs text-slate-400">Cascading Failover Priorities</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-cyan-950 text-cyan-300 border border-cyan-800">
              Active Mesh
            </span>
          </div>

          <div class="space-y-3">
            @for (p of providers(); track p.provider) {
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-1.5 text-xs">
                <div class="flex items-center justify-between">
                  <span class="font-bold text-white">{{ p.name }}</span>
                  <span class="text-emerald-400 font-bold font-mono">{{ (p.matchRate * 100) | number:'1.0-0' }}% Match</span>
                </div>
                <div class="flex items-center justify-between text-slate-400 text-[11px]">
                  <span>Latency: <strong class="text-slate-300 font-mono">{{ p.latencyMs }} ms</strong></span>
                  <span>{{ p.queriesToday | number:'1.0-0' }} calls today</span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Enriched Lead Profiles (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Verified Enriched Profiles</h2>
            <span class="text-xs text-slate-400 font-mono">{{ leads().length }} Profiles</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (lead of leads(); track lead.domain) {
              <div
                class="p-4 hover:bg-slate-700/30 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-3"
                data-testid="profile-card"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ lead.legalName }}</span>
                    <span class="bg-cyan-900/60 text-cyan-300 border border-cyan-700/60 px-2 py-0.5 rounded text-[10px] font-semibold">
                      Via {{ lead.winningProvider }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Contact: <strong class="text-white">{{ lead.contactName }}</strong> ({{ lead.jobTitle }}) • <span class="font-mono text-cyan-300">{{ lead.contactEmail }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Stack: <span class="text-slate-300">{{ lead.techStack.join(' • ') }}</span>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto space-y-1">
                  <div class="text-xs font-semibold text-emerald-400 flex items-center gap-1 justify-end">
                    ✓ {{ lead.deliverability }}
                  </div>
                  <button
                    (click)="verifyMx(lead)"
                    data-testid="verify-mx-btn"
                    class="px-2.5 py-1 bg-slate-700 hover:bg-slate-600 text-slate-200 rounded text-[11px] transition-colors"
                  >
                    Re-Verify MX
                  </button>
                </div>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class WaterfallEnrichmentCockpitComponent {
  readonly providers = signal<ProviderMetricView[]>([
    { provider: 'Clearbit', name: 'Clearbit Enterprise API', matchRate: 0.88, latencyMs: 120.5, queriesToday: 4120 },
    { provider: 'Apollo', name: 'Apollo.io B2B Graph', matchRate: 0.82, latencyMs: 145.0, queriesToday: 3890 },
    { provider: 'ZoomInfo', name: 'ZoomInfo Enterprise Data', matchRate: 0.79, latencyMs: 180.2, queriesToday: 2150 },
    { provider: 'PeopleDataLabs', name: 'People Data Labs (PDL)', matchRate: 0.76, latencyMs: 195.4, queriesToday: 1820 }
  ]);

  readonly leads = signal<EnrichedLeadView[]>([
    {
      domain: 'stripe.com',
      legalName: 'Stripe Inc.',
      industry: 'FinTech / Payments Infrastructure',
      employeeRange: '5,000+ employees',
      annualRevenue: '$1B+',
      contactEmail: 'sarah.growth@stripe.com',
      contactName: 'Sarah Jenkins',
      jobTitle: 'Head of Growth Architecture',
      seniority: 'Director',
      winningProvider: 'Clearbit',
      deliverability: 'DeliverableWithMx',
      techStack: ['PostgreSQL', 'Angular', 'Kafka', 'Redis']
    },
    {
      domain: 'snowflake.com',
      legalName: 'Snowflake Inc.',
      industry: 'Cloud Data Platform',
      employeeRange: '5,000+ employees',
      annualRevenue: '$2B+',
      contactEmail: 'marcus.data@snowflake.com',
      contactName: 'Marcus Vance',
      jobTitle: 'VP of Product Marketing',
      seniority: 'VP',
      winningProvider: 'Apollo',
      deliverability: 'DeliverableWithMx',
      techStack: ['Kubernetes', 'Go', 'React', 'Snowflake']
    }
  ]);

  readonly overallMatchRate = computed(() => 94);

  readonly avgLatency = computed(() => {
    const list = this.providers();
    if (!list.length) return 0;
    return Math.round(list.reduce((acc, p) => acc + p.latencyMs, 0) / list.length);
  });

  readonly verifiedCount = computed(() =>
    this.leads().filter(l => l.deliverability === 'DeliverableWithMx' || l.deliverability === 'Valid').length
  );

  triggerWaterfallEnrichment(): void {
    const newLead: EnrichedLeadView = {
      domain: 'databricks.com',
      legalName: 'Databricks Inc.',
      industry: 'Data & AI Infrastructure',
      employeeRange: '5,000+ employees',
      annualRevenue: '$1.5B+',
      contactEmail: 'jordan.ae@databricks.com',
      contactName: 'Jordan Reed',
      jobTitle: 'VP of Commercial Operations',
      seniority: 'VP',
      winningProvider: 'ZoomInfo',
      deliverability: 'DeliverableWithMx',
      techStack: ['Spark', 'Delta Lake', 'PostgreSQL', 'TypeScript']
    };

    this.leads.update(list => [newLead, ...list]);
  }

  verifyMx(lead: EnrichedLeadView): void {
    this.leads.update(list =>
      list.map(l => l.domain === lead.domain ? { ...l, deliverability: 'DeliverableWithMx' } : l)
    );
  }
}
