import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CustomerSentimentIncident {
  incidentId: string;
  customerName: string;
  channel: 'Zendesk' | 'Intercom' | 'SlackConnect' | 'Phone';
  sentimentScore: number; // -1.0 to 1.0
  riskTier: 'Critical' | 'High' | 'Moderate' | 'Low';
  triggerReason: string;
  recommendedPlaybook: string;
  status: 'ActiveIntervention' | 'Deescalated' | 'Resolved';
  timestamp: string;
}

@Component({
  selector: 'app-sentiment-deescalation-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="sentiment-deescalation-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-rose-500/20 text-rose-400 border border-rose-500/30">Horizon 78</span>
            <span class="text-xs text-slate-400">Customer Sentiment & CSAT Mesh</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous CSAT & Sentiment De-escalation Mesh</h1>
          <p class="text-sm text-slate-400">Real-time omnichannel sentiment monitoring, churn risk detection, and autonomous VIP de-escalation routing.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="simulateCriticalIncident()" data-testid="simulate-incident-btn" class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded font-medium text-sm transition-colors shadow">
            Simulate Churn Risk Alert
          </button>
        </div>
      </header>

      <!-- KPI Metrics -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-critical-incidents">
          <div class="text-xs font-medium text-slate-400 uppercase">Critical / High Risk</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ criticalCount() }} Active</div>
          <div class="text-xs text-slate-400 mt-1">Immediate intervention required</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-avg-sentiment">
          <div class="text-xs font-medium text-slate-400 uppercase">Mesh Health Sentiment</div>
          <div class="text-2xl font-bold" [ngClass]="avgSentiment() < 0 ? 'text-amber-400' : 'text-emerald-400'" mt-1>
            {{ avgSentiment() > 0 ? '+' : '' }}{{ avgSentiment() }}
          </div>
          <div class="text-xs text-slate-400 mt-1">Score scale: -1.0 to +1.0</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-deescalation-rate">
          <div class="text-xs font-medium text-slate-400 uppercase">De-escalation Success Rate</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">94.2%</div>
          <div class="text-xs text-slate-400 mt-1">Average rescue time: 4.8 min</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-monitored">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Incidents Tracked</div>
          <div class="text-2xl font-bold text-white mt-1">{{ incidents().length }}</div>
          <div class="text-xs text-slate-400 mt-1">{{ resolvedCount() }} Resolved or Calmed</div>
        </div>
      </section>

      <!-- Incident Stream -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-base font-semibold text-white">Live Customer Sentiment Interventions</h2>
          <div class="flex items-center gap-2">
            <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            <span class="text-xs text-slate-400">Autonomous Listening Active</span>
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="incidents-table">
            <thead class="text-xs text-slate-400 uppercase border-b border-slate-700/60 bg-slate-800/40">
              <tr>
                <th class="p-3">Customer</th>
                <th class="p-3">Channel</th>
                <th class="p-3">Sentiment</th>
                <th class="p-3">Risk Tier</th>
                <th class="p-3">Reason</th>
                <th class="p-3">Recommended Playbook</th>
                <th class="p-3">Status</th>
                <th class="p-3">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-700/40">
              @for (inc of incidents(); track inc.incidentId) {
                <tr class="hover:bg-slate-700/20" [attr.data-testid]="'incident-row-' + inc.incidentId">
                  <td class="p-3 font-medium text-white">{{ inc.customerName }}</td>
                  <td class="p-3 text-xs text-slate-300">{{ inc.channel }}</td>
                  <td class="p-3 font-mono text-xs" [ngClass]="inc.sentimentScore < 0 ? 'text-rose-400 font-bold' : 'text-emerald-400'">
                    {{ inc.sentimentScore > 0 ? '+' : '' }}{{ inc.sentimentScore }}
                  </td>
                  <td class="p-3">
                    <span [ngClass]="{
                      'bg-rose-500/20 text-rose-300 border-rose-500/30': inc.riskTier === 'Critical',
                      'bg-amber-500/20 text-amber-300 border-amber-500/30': inc.riskTier === 'High',
                      'bg-blue-500/20 text-blue-300 border-blue-500/30': inc.riskTier === 'Moderate',
                      'bg-slate-700/50 text-slate-300 border-slate-600': inc.riskTier === 'Low'
                    }" class="px-2 py-0.5 text-xs rounded border font-medium">
                      {{ inc.riskTier }}
                    </span>
                  </td>
                  <td class="p-3 text-xs text-slate-300 max-w-xs truncate">{{ inc.triggerReason }}</td>
                  <td class="p-3 text-xs text-indigo-300">{{ inc.recommendedPlaybook }}</td>
                  <td class="p-3">
                    <span [ngClass]="{
                      'bg-rose-500/20 text-rose-400': inc.status === 'ActiveIntervention',
                      'bg-emerald-500/20 text-emerald-400': inc.status === 'Resolved' || inc.status === 'Deescalated'
                    }" class="px-2 py-0.5 text-xs rounded font-medium">
                      {{ inc.status }}
                    </span>
                  </td>
                  <td class="p-3">
                    @if (inc.status === 'ActiveIntervention') {
                      <button (click)="applyPlaybook(inc)" [attr.data-testid]="'apply-playbook-btn-' + inc.incidentId" class="text-xs px-2.5 py-1 bg-indigo-600 hover:bg-indigo-500 text-white rounded font-medium transition-colors">
                        Execute Playbook
                      </button>
                    } @else {
                      <span class="text-xs text-emerald-400 font-medium">Stabilized</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class SentimentDeescalationCockpitComponent {
  incidents = signal<CustomerSentimentIncident[]>([
    {
      incidentId: 'inc-sd-501',
      customerName: 'Enterprise FinServ Corp',
      channel: 'SlackConnect',
      sentimentScore: -0.82,
      riskTier: 'Critical',
      triggerReason: 'Repeated billing invoice discrepancy and delayed webhook sync',
      recommendedPlaybook: 'Auto-credit $500 goodwill balance & page Tier-3 Engineering Lead',
      status: 'ActiveIntervention',
      timestamp: '2026-11-03T00:45:00Z'
    },
    {
      incidentId: 'inc-sd-502',
      customerName: 'OmniRetail Europe',
      channel: 'Intercom',
      sentimentScore: -0.45,
      riskTier: 'High',
      triggerReason: 'Unclear customs HS code duty calculation on cross-border shipment',
      recommendedPlaybook: 'Dispatch automated customs specialist guidance & schedule CS consultation',
      status: 'ActiveIntervention',
      timestamp: '2026-11-03T01:05:00Z'
    },
    {
      incidentId: 'inc-sd-503',
      customerName: 'Nexus AI Cloud',
      channel: 'Zendesk',
      sentimentScore: 0.65,
      riskTier: 'Low',
      triggerReason: 'Feature inquiry regarding zero-trust ephemeral vault permissions',
      recommendedPlaybook: 'Send JIT credential documentation link',
      status: 'Resolved',
      timestamp: '2026-11-03T01:12:00Z'
    }
  ]);

  criticalCount = computed(() => this.incidents()
    .filter(i => (i.riskTier === 'Critical' || i.riskTier === 'High') && i.status === 'ActiveIntervention')
    .length);

  resolvedCount = computed(() => this.incidents()
    .filter(i => i.status === 'Resolved' || i.status === 'Deescalated')
    .length);

  avgSentiment = computed(() => {
    const list = this.incidents();
    if (list.length === 0) return 0;
    const total = list.reduce((sum, i) => sum + i.sentimentScore, 0);
    return Math.round((total / list.length) * 100) / 100;
  });

  applyPlaybook(incident: CustomerSentimentIncident) {
    this.incidents.update(list => list.map(i => {
      if (i.incidentId === incident.incidentId) {
        return {
          ...i,
          sentimentScore: 0.45,
          riskTier: 'Low',
          status: 'Deescalated'
        };
      }
      return i;
    }));
  }

  simulateCriticalIncident() {
    const newInc: CustomerSentimentIncident = {
      incidentId: `inc-sd-${Date.now().toString().slice(-4)}`,
      customerName: 'Pacific Commerce Ltd',
      channel: 'Phone',
      sentimentScore: -0.91,
      riskTier: 'Critical',
      triggerReason: 'High rate of 429 rate-limiting during seasonal flash sale launch',
      recommendedPlaybook: 'Dynamic rate limit burst quota provisioned & VIP VP escalation',
      status: 'ActiveIntervention',
      timestamp: new Date().toISOString()
    };

    this.incidents.update(list => [newInc, ...list]);
  }
}
