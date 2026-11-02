import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface FraudRuleUi {
  id: string;
  name: string;
  description: string;
  weight: number;
  isActive: boolean;
}

export interface TransactionEvaluationUi {
  id: string;
  amountUsd: number;
  customerEmail: string;
  riskScore: number;
  decision: 'Allow' | 'Challenge3DS' | 'ManualReview' | 'Block';
  triggeredRules: string[];
  evaluatedAt: string;
}

@Component({
  selector: 'app-fraud-firewall-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="fraud-firewall-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Autonomous Fraud Detection & Transaction Firewall</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-rose-100 text-rose-800">
              Bayesian Risk Engine Active
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Real-time IP velocity clustering, cross-border BIN anomaly detection, and automated 3D-Secure stepped authentication.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="simulateEvaluation()"
            class="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-evaluate-txn">
            + Ingest Live Transaction
          </button>
        </div>
      </div>

      <!-- Fraud KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="fraud-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Evaluated Transactions</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-total-evals">
            {{ evaluations().length }}
          </div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">Sub-25ms Processing</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Firewall Block Rate</div>
          <div class="text-2xl font-bold text-rose-600 mt-1 font-mono" data-testid="kpi-block-rate">
            {{ blockRate() }}%
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Auto-Quarantine Threshold</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Stepped 3DS Challenges</div>
          <div class="text-2xl font-bold text-amber-600 mt-1 font-mono" data-testid="kpi-challenged-count">
            {{ challengeCount() }}
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Risk Score 50 - 74</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Average Risk Index</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-avg-risk">
            {{ avgRiskScore() }} / 100
          </div>
          <div class="text-[11px] text-indigo-600 mt-1 font-semibold">Bayesian Multi-Factor</div>
        </div>
      </div>

      <!-- Main Columns -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Active Fraud Rules -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Firewall Heuristic Rules</h3>
            <span class="text-xs text-slate-400 font-mono">{{ rules().length }} active</span>
          </div>

          <div class="space-y-3" data-testid="rules-list">
            @for (rule of rules(); track rule.id) {
              <div class="p-3 border rounded-lg hover:border-slate-300 transition-colors space-y-2">
                <div class="flex justify-between items-center text-xs">
                  <span class="font-bold text-slate-800">{{ rule.name }}</span>
                  <input 
                    type="checkbox" 
                    [checked]="rule.isActive" 
                    (change)="toggleRule(rule.id)"
                    class="rounded text-rose-600 focus:ring-rose-500" 
                    [attr.data-testid]="'toggle-rule-' + rule.id" />
                </div>
                <p class="text-[11px] text-slate-500">{{ rule.description }}</p>
                <div class="flex justify-between items-center text-[10px] text-slate-400 pt-1 border-t">
                  <span>Risk Penalty: <strong class="text-rose-700 font-mono">+{{ rule.weight }}</strong></span>
                  <span class="font-mono text-emerald-700" [class.text-slate-400]="!rule.isActive">
                    {{ rule.isActive ? 'Active' : 'Disabled' }}
                  </span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Real-Time Evaluation Logs -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Real-Time Risk Decision Feed</h3>
            <span class="text-xs text-slate-400 font-mono">Stream Live</span>
          </div>

          <div class="overflow-x-auto" data-testid="evaluations-table">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-50 border-b text-slate-500">
                <tr>
                  <th class="p-3">Txn ID / Buyer</th>
                  <th class="p-3">Amount</th>
                  <th class="p-3">Risk Score</th>
                  <th class="p-3">Decision</th>
                  <th class="p-3">Triggered Heuristics</th>
                </tr>
              </thead>
              <tbody class="divide-y">
                @for (evalItem of evaluations(); track evalItem.id) {
                  <tr class="hover:bg-slate-50">
                    <td class="p-3">
                      <div class="font-mono font-bold text-slate-900">{{ evalItem.id }}</div>
                      <div class="text-[10px] text-slate-400">{{ evalItem.customerEmail }}</div>
                    </td>
                    <td class="p-3 font-mono font-bold text-slate-900">
                      \${{ evalItem.amountUsd | number:'1.2-2' }}
                    </td>
                    <td class="p-3 font-mono font-bold"
                      [class.text-emerald-600]="evalItem.riskScore < 30"
                      [class.text-amber-600]="evalItem.riskScore >= 30 && evalItem.riskScore < 75"
                      [class.text-rose-600]="evalItem.riskScore >= 75">
                      {{ evalItem.riskScore }} / 100
                    </td>
                    <td class="p-3">
                      <span 
                        [class.bg-emerald-100]="evalItem.decision === 'Allow'"
                        [class.text-emerald-800]="evalItem.decision === 'Allow'"
                        [class.bg-amber-100]="evalItem.decision === 'Challenge3DS' || evalItem.decision === 'ManualReview'"
                        [class.text-amber-800]="evalItem.decision === 'Challenge3DS' || evalItem.decision === 'ManualReview'"
                        [class.bg-rose-100]="evalItem.decision === 'Block'"
                        [class.text-rose-800]="evalItem.decision === 'Block'"
                        class="px-2 py-0.5 rounded text-[10px] font-bold font-mono">
                        {{ evalItem.decision }}
                      </span>
                    </td>
                    <td class="p-3 text-[11px] text-slate-500">
                      {{ evalItem.triggeredRules.length > 0 ? evalItem.triggeredRules.join(', ') : 'No anomalies detected' }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  `
})
export class FraudFirewallCockpitComponent {
  rules = signal<FraudRuleUi[]>([
    { id: 'rule_geo_mismatch', name: 'Cross-Border BIN Geo Mismatch', description: 'Triggers when card issuing country diverges from buyer billing address', weight: 35, isActive: true },
    { id: 'rule_high_value', name: 'High-Value Transaction Spike', description: 'Flags transactions exceeding $5,000 for elevated verification', weight: 25, isActive: true },
    { id: 'rule_temp_email', name: 'Disposable Burner Email Shield', description: 'Detects throwaway and burner email addresses', weight: 40, isActive: true },
    { id: 'rule_ip_velocity', name: 'IP Subnet Velocity Cluster', description: 'Detects rapid multi-card testing across the same subnet', weight: 30, isActive: true }
  ]);

  evaluations = signal<TransactionEvaluationUi[]>([
    { id: 'txn_89210', amountUsd: 1250.00, customerEmail: 'cfo@acme.corp', riskScore: 15, decision: 'Allow', triggeredRules: [], evaluatedAt: '12m ago' },
    { id: 'txn_89211', amountUsd: 7500.00, customerEmail: 'buyer@anonymous.com', riskScore: 60, decision: 'Challenge3DS', triggeredRules: ['High-Value Spike', 'Geo Mismatch'], evaluatedAt: '5m ago' },
    { id: 'txn_89212', amountUsd: 240.00, customerEmail: 'bot99@tempmail.com', riskScore: 70, decision: 'Challenge3DS', triggeredRules: ['Disposable Domain', 'IP Velocity'], evaluatedAt: '2m ago' }
  ]);

  blockRate = computed(() => {
    const list = this.evaluations();
    if (list.length === 0) return 0;
    const blocked = list.filter(e => e.decision === 'Block').length;
    return Math.round((blocked / list.length) * 100);
  });

  challengeCount = computed(() => {
    return this.evaluations().filter(e => e.decision === 'Challenge3DS').length;
  });

  avgRiskScore = computed(() => {
    const list = this.evaluations();
    if (list.length === 0) return 0;
    const avg = list.reduce((sum, e) => sum + e.riskScore, 0) / list.length;
    return Math.round(avg);
  });

  toggleRule(ruleId: string) {
    this.rules.update(list =>
      list.map(r => r.id === ruleId ? { ...r, isActive: !r.isActive } : r)
    );
  }

  simulateEvaluation() {
    const newEval: TransactionEvaluationUi = {
      id: 'txn_' + Math.floor(10000 + Math.random() * 90000),
      amountUsd: 14500.00,
      customerEmail: 'scammer@guerrillamail.com',
      riskScore: 85,
      decision: 'Block',
      triggeredRules: ['High-Value Spike ($14.5k)', 'Disposable Email', 'Geo Mismatch'],
      evaluatedAt: 'Just now'
    };

    this.evaluations.update(list => [newEval, ...list]);
  }
}
