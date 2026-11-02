import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AccountIntentView {
  accountId: string;
  domain: string;
  companyName: string;
  industry: string;
  employeeTier: string;
  totalVisits: number;
  activeScore: number;
  stage: 'ReadyToBuy' | 'HighIntentDecision' | 'Consideration' | 'Awareness';
  visitedPages: string[];
  assignedRep: string;
}

@Component({
  selector: 'app-de-anonymization-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="de-anonymization-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-amber-400">
            <span class="inline-block w-2 h-2 rounded-full bg-amber-400 animate-pulse"></span>
            B2B Buyer Intelligence & Reverse IP • Horizon 56
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous B2B Buyer Intent De-Anonymization
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Reverse-IP subnet lookup, corporate firmographics resolution, and real-time buying stage scoring for anonymous web visitors.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="simulateDeAnonymization()"
            data-testid="simulate-lookup-btn"
            class="px-4 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-amber-600/20"
          >
            <span>🔍</span> De-Anonymize Traffic
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">High-Intent Accounts</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-active-accounts">{{ accounts().length }}</div>
          <div class="text-amber-400 text-xs mt-1">Enterprise Corporate ASN Matches</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Ready-To-Buy Surges</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-ready-to-buy">{{ readyToBuyCount() }}</div>
          <div class="text-slate-400 text-xs mt-1">Score >= 85 • Pricing Visited</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Average Intent Score</div>
          <div class="text-2xl font-bold text-amber-400 mt-1" data-testid="stat-avg-intent">{{ avgScore() }} / 100</div>
          <div class="text-emerald-400 text-xs mt-1">+18.4% WoW Intent Velocity</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">De-Anonymization Rate</div>
          <div class="text-2xl font-bold text-sky-400 mt-1">38.6%</div>
          <div class="text-slate-400 text-xs mt-1">Filtered Residential ISP Noise</div>
        </div>
      </div>

      <!-- Main Stage: De-Anonymized Feed & Intent Details -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Accounts Feed (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Live B2B Visitor Accounts</h2>
            <span class="text-xs text-slate-400 font-mono">{{ accounts().length }} Identified</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (acc of accounts(); track acc.accountId) {
              <div
                (click)="selectAccount(acc)"
                [class.bg-slate-700/40]="selectedAccount()?.accountId === acc.accountId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'account-row-' + acc.accountId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ acc.companyName }}</span>
                    <span
                      [class]="acc.stage === 'ReadyToBuy' ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-amber-900/60 text-amber-300 border-amber-700/60'"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                    >
                      {{ acc.stage }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Domain: <span class="font-mono text-slate-300">{{ acc.domain }}</span> • Industry: <span class="text-slate-300">{{ acc.industry }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Team: <strong class="text-slate-200">{{ acc.employeeTier }}</strong> • Total Visits: <strong class="text-amber-400">{{ acc.totalVisits }}</strong>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">Buying Intent</div>
                  <div
                    [class]="acc.activeScore >= 85 ? 'text-emerald-400' : 'text-amber-400'"
                    class="text-lg font-extrabold font-mono"
                  >
                    {{ acc.activeScore }}/100
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Account Deep Dive & Alert Dispatch Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Account Buying Telemetry</h3>
              <p class="text-xs text-slate-400">Assigned Rep: {{ selectedAccount()?.assignedRep }}</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-amber-950 text-amber-300 border border-amber-800">
              Verified Corporate IP
            </span>
          </div>

          @if (selectedAccount(); as active) {
            <div class="space-y-4 text-xs">
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2">
                <div class="text-slate-400 font-bold">Visited URL Footprint:</div>
                <div class="space-y-1">
                  @for (page of active.visitedPages; track page) {
                    <div class="p-1.5 bg-slate-950 rounded font-mono text-[11px] text-amber-300 flex items-center justify-between">
                      <span>{{ page }}</span>
                      <span class="text-slate-500 text-[10px]">High Intent</span>
                    </div>
                  }
                </div>
              </div>

              <!-- Real-Time AE Alert Dispatcher -->
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-3">
                <div class="font-bold text-white text-xs">Instant AE Outreach Alert</div>
                <p class="text-[11px] text-slate-400">
                  Notify assigned Account Executive in Slack/WhatsApp to trigger high-velocity personalized outreach.
                </p>

                <button
                  (click)="dispatchAlert(active)"
                  data-testid="dispatch-alert-btn"
                  class="w-full py-2 bg-amber-600 hover:bg-amber-500 text-white font-semibold rounded-lg text-xs shadow transition-colors"
                >
                  Dispatch High-Intent Alert
                </button>

                @if (lastAlert(); as alt) {
                  <div class="p-2.5 bg-slate-950 rounded-lg border border-amber-900/60 text-amber-200 text-[11px] leading-relaxed" data-testid="alert-preview-card">
                    {{ alt }}
                  </div>
                }
              </div>
            </div>
          } @else {
            <div class="text-center py-16 text-slate-400 text-xs">
              Select an identified enterprise account to review page journey and trigger rep alerts.
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class DeAnonymizationCockpitComponent {
  readonly accounts = signal<AccountIntentView[]>([
    {
      accountId: 'acc_stripe',
      domain: 'stripe.com',
      companyName: 'Stripe Inc.',
      industry: 'FinTech / Payments Infrastructure',
      employeeTier: '5,000+ employees',
      totalVisits: 14,
      activeScore: 92,
      stage: 'ReadyToBuy',
      visitedPages: ['/pricing', '/enterprise-sla', '/docs/api/growth-os'],
      assignedRep: 'sarah.cro@sgrowing.io'
    },
    {
      accountId: 'acc_shopify',
      domain: 'shopify.com',
      companyName: 'Shopify Inc.',
      industry: 'E-Commerce Infrastructure',
      employeeTier: '10,000+ employees',
      totalVisits: 8,
      activeScore: 78,
      stage: 'HighIntentDecision',
      visitedPages: ['/integrations/commerce', '/pricing/volume'],
      assignedRep: 'alex.ae@sgrowing.io'
    }
  ]);

  readonly selectedAccount = signal<AccountIntentView | null>(this.accounts()[0]);
  readonly lastAlert = signal<string | null>(null);

  readonly readyToBuyCount = computed(() =>
    this.accounts().filter(a => a.stage === 'ReadyToBuy').length
  );

  readonly avgScore = computed(() => {
    const list = this.accounts();
    if (!list.length) return 0;
    return Math.round(list.reduce((acc, curr) => acc + curr.activeScore, 0) / list.length);
  });

  selectAccount(acc: AccountIntentView): void {
    this.selectedAccount.set(acc);
  }

  simulateDeAnonymization(): void {
    const newAccount: AccountIntentView = {
      accountId: `acc_databricks_${Date.now()}`,
      domain: 'databricks.com',
      companyName: 'Databricks Inc.',
      industry: 'Cloud Data & AI',
      employeeTier: '5,000+ employees',
      totalVisits: 6,
      activeScore: 89,
      stage: 'ReadyToBuy',
      visitedPages: ['/pricing', '/security/soc2'],
      assignedRep: 'jordan.ae@sgrowing.io'
    };

    this.accounts.update(list => [newAccount, ...list]);
    this.selectedAccount.set(newAccount);
  }

  dispatchAlert(acc: AccountIntentView): void {
    this.lastAlert.set(
      `✓ Alert Dispatched: High-intent surge for ${acc.companyName} (${acc.domain}) routed to ${acc.assignedRep} via Slack #b2b-leads.`
    );
  }
}
