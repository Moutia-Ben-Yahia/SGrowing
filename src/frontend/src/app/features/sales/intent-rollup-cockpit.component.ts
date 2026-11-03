import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface SignalItem {
  signalId: string;
  contactEmail: string;
  jobTitle: string;
  intentSource: string;
  intensityScore: number;
}

export interface AccountNodeItem {
  accountId: string;
  legalName: string;
  domain: string;
  parentCorporateEntity: string;
  globalUltimateDuns: string;
  totalEmployees: number;
  aggregateIntentScore: number;
  activeOutreachOwnerRepId: string;
  outreachLocked: boolean;
  committeeSignals: SignalItem[];
}

@Component({
  selector: 'app-intent-rollup-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="intent-rollup-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              Horizon 111 • Domain B19 & J17
            </span>
            <span class="text-xs text-slate-400">Buying Committee Rollup &amp; Intent De-Duplication</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">Buyer Intent De-Duplication &amp; Account Rollup</h1>
          <p class="text-slate-400 text-sm mt-1">
            Fuzzy parent-child corporate entity aggregation, intent signal de-duplication, and multi-rep collision prevention.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="simulateIntentSurge()"
            data-testid="simulate-surge-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Simulate Committee Surge
          </button>
          <button
            (click)="lockCorporateOutreach()"
            data-testid="lock-outreach-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-emerald-600/30 flex items-center gap-2">
            Lock Outreach Exclusivity
          </button>
        </div>
      </div>

      <!-- Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Monitored Enterprise Accounts</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-total-accounts">{{ totalAccounts() }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">Fuzzy Hierarchy Aggregation</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">High-Intent Surging Accounts</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-high-intent">{{ highIntentAccounts() }}</p>
          <span class="text-xs text-indigo-300 mt-1 block">Intent Score &ge; 70 / 100</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">De-Duplicated Intent Signals</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-deduped-signals">{{ deDupedSignals() | number }}</p>
          <span class="text-xs text-slate-400 mt-1 block">Zero Repetitive SDR Noise</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Rep Collision Prevention Rate</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-collision-rate">{{ collisionPreventionRate() }}%</p>
          <span class="text-xs text-emerald-300 mt-1 block">Mutual Exclusion Active</span>
        </div>
      </div>

      <!-- Accounts Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Consolidated Corporate Parent Hierarchies</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Account ID</th>
                <th class="py-3 px-4">Entity Legal Name</th>
                <th class="py-3 px-4">Parent Corporate Entity</th>
                <th class="py-3 px-4">DUNS Number</th>
                <th class="py-3 px-4 text-right">Intent Score</th>
                <th class="py-3 px-4">Assigned AE</th>
                <th class="py-3 px-4 text-right">Outreach Lock</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (acc of accounts(); track acc.accountId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-indigo-300">{{ acc.accountId }}</td>
                  <td class="py-3 px-4 font-semibold text-white">{{ acc.legalName }}</td>
                  <td class="py-3 px-4 text-slate-300">{{ acc.parentCorporateEntity }}</td>
                  <td class="py-3 px-4 font-mono text-xs text-slate-400">{{ acc.globalUltimateDuns }}</td>
                  <td class="py-3 px-4 text-right font-bold" [class]="acc.aggregateIntentScore >= 70 ? 'text-emerald-400' : 'text-slate-400'">
                    {{ acc.aggregateIntentScore }} / 100
                  </td>
                  <td class="py-3 px-4 text-slate-200">{{ acc.activeOutreachOwnerRepId || 'Unassigned' }}</td>
                  <td class="py-3 px-4 text-right">
                    <span [class]="acc.outreachLocked ? 'px-2 py-0.5 text-xs rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-medium' : 'px-2 py-0.5 text-xs rounded-full bg-slate-800 text-slate-400 font-medium'">
                      {{ acc.outreachLocked ? 'Locked (Protected)' : 'Open' }}
                    </span>
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
export class IntentRollupCockpitComponent {
  totalAccounts = signal<number>(3);
  highIntentAccounts = signal<number>(2);
  deDupedSignals = signal<number>(1420);
  collisionPreventionRate = signal<number>(99.4);

  accounts = signal<AccountNodeItem[]>([
    {
      accountId: 'ACC-CORP-01',
      legalName: 'Acme Global Holdings Inc.',
      domain: 'acme.corp',
      parentCorporateEntity: 'Acme Worldwide Ultimate Parent',
      globalUltimateDuns: 'DUNS-991204812',
      totalEmployees: 14500,
      aggregateIntentScore: 88,
      activeOutreachOwnerRepId: 'REP-SARAH-01',
      outreachLocked: true,
      committeeSignals: [
        { signalId: 'SIG-01', contactEmail: 'cto@acme.corp', jobTitle: 'Chief Technology Officer', intentSource: 'G2_BuyerIntent', intensityScore: 92 },
        { signalId: 'SIG-02', contactEmail: 'vp.eng@acme.eu', jobTitle: 'VP Engineering EMEA', intentSource: 'WebsitePricingPage', intensityScore: 85 }
      ]
    },
    {
      accountId: 'ACC-CORP-02',
      legalName: 'Vanguard Logistics International',
      domain: 'vanguardlogistics.com',
      parentCorporateEntity: 'Vanguard Global Group',
      globalUltimateDuns: 'DUNS-441029411',
      totalEmployees: 8200,
      aggregateIntentScore: 74,
      activeOutreachOwnerRepId: 'REP-MICHAEL-02',
      outreachLocked: false,
      committeeSignals: [
        { signalId: 'SIG-04', contactEmail: 'cio@vanguardlogistics.com', jobTitle: 'Chief Information Officer', intentSource: 'WebinarAttendance', intensityScore: 80 }
      ]
    },
    {
      accountId: 'ACC-CORP-03',
      legalName: 'Nexus Healthcare Systems',
      domain: 'nexushealth.org',
      parentCorporateEntity: 'Nexus Health Alliance',
      globalUltimateDuns: 'DUNS-110294821',
      totalEmployees: 12000,
      aggregateIntentScore: 42,
      activeOutreachOwnerRepId: '',
      outreachLocked: false,
      committeeSignals: [
        { signalId: 'SIG-06', contactEmail: 'it.admin@nexushealth.org', jobTitle: 'Lead Systems Architect', intentSource: 'WhitepaperDownload', intensityScore: 45 }
      ]
    }
  ]);

  simulateIntentSurge(): void {
    this.accounts.update(list =>
      list.map(a => a.accountId === 'ACC-CORP-03' ? { ...a, aggregateIntentScore: 85 } : a)
    );
    this.highIntentAccounts.set(3);
  }

  lockCorporateOutreach(): void {
    this.accounts.update(list =>
      list.map(a => a.accountId === 'ACC-CORP-02' ? { ...a, outreachLocked: true } : a)
    );
  }
}
