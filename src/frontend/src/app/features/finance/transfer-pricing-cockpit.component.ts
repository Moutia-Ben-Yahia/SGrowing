import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface IntercompanyEntityItem {
  entityId: string;
  legalName: string;
  jurisdiction: string;
  taxIdentificationNumber: string;
  functionalProfile: string;
  functionalCurrency: string;
}

export interface IntercompanyInvoiceItem {
  invoiceId: string;
  agreementId: string;
  billingPeriod: string;
  baseCostAmount: number;
  appliedMarkupPct: number;
  totalInvoiceAmount: number;
  currency: string;
  settlementStatus: string;
}

@Component({
  selector: 'app-transfer-pricing-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="transfer-pricing-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              Horizon 109 • Domain H18 & G14
            </span>
            <span class="text-xs text-slate-400">OECD BEPS Arm's Length Pricing &amp; Intercompany Netting</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">Cross-Border Transfer Pricing &amp; Intercompany Ledger</h1>
          <p class="text-slate-400 text-sm mt-1">
            Automated arm's length markup calculations, intercompany invoice generation, and multilateral FX netting.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="recalculateMarkups()"
            data-testid="recalculate-markups-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Audit Arm's Length Range
          </button>
          <button
            (click)="executeFxNetting()"
            data-testid="execute-netting-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-emerald-600/30 flex items-center gap-2">
            Execute Monthly FX Netting
          </button>
        </div>
      </div>

      <!-- Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Operating Entities</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-total-entities">{{ totalEntities() }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">US, Ireland, Singapore, UK</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Quarterly Intercompany Volume</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-volume">\${{ totalVolume() | number:'1.2-2' }}</p>
          <span class="text-xs text-indigo-300 mt-1 block">Gross Cross-Border Billings</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">BEPS Compliance Rate</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-compliance">{{ complianceRate() }}%</p>
          <span class="text-xs text-slate-400 mt-1 block">Interquartile Range Confirmed</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Net Settlement Due</span>
          <p class="text-2xl font-bold text-amber-400 mt-1" data-testid="metric-net-settlement">\${{ netSettlement() | number:'1.2-2' }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">72% Liquidity Netted</span>
        </div>
      </div>

      <!-- Entities Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Multi-Entity Legal Architecture &amp; Profiles</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Entity Code</th>
                <th class="py-3 px-4">Legal Entity Name</th>
                <th class="py-3 px-4">Jurisdiction</th>
                <th class="py-3 px-4">Tax ID</th>
                <th class="py-3 px-4">Functional Profile</th>
                <th class="py-3 px-4 text-right">Currency</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (ent of entities(); track ent.entityId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-indigo-300">{{ ent.entityId }}</td>
                  <td class="py-3 px-4 font-semibold text-white">{{ ent.legalName }}</td>
                  <td class="py-3 px-4 text-slate-300">{{ ent.jurisdiction }}</td>
                  <td class="py-3 px-4 font-mono text-xs text-slate-400">{{ ent.taxIdentificationNumber }}</td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 text-xs rounded bg-slate-800 text-slate-200 border border-slate-700">
                      {{ ent.functionalProfile }}
                    </span>
                  </td>
                  <td class="py-3 px-4 text-right font-bold text-white">{{ ent.functionalCurrency }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>

      <!-- Invoices Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Intercompany Invoicing &amp; Settlement Ledger</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Invoice #</th>
                <th class="py-3 px-4">Agreement</th>
                <th class="py-3 px-4">Billing Period</th>
                <th class="py-3 px-4 text-right">Base Cost</th>
                <th class="py-3 px-4 text-right">Markup %</th>
                <th class="py-3 px-4 text-right">Total Invoice</th>
                <th class="py-3 px-4 text-right">Settlement State</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (inv of invoices(); track inv.invoiceId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-slate-400">{{ inv.invoiceId }}</td>
                  <td class="py-3 px-4 font-mono text-xs text-indigo-300">{{ inv.agreementId }}</td>
                  <td class="py-3 px-4 text-slate-300">{{ inv.billingPeriod }}</td>
                  <td class="py-3 px-4 text-right text-slate-300">\${{ inv.baseCostAmount | number:'1.2-2' }}</td>
                  <td class="py-3 px-4 text-right text-indigo-400 font-semibold">+{{ inv.appliedMarkupPct }}%</td>
                  <td class="py-3 px-4 text-right font-bold text-white">\${{ inv.totalInvoiceAmount | number:'1.2-2' }}</td>
                  <td class="py-3 px-4 text-right">
                    <span [class]="inv.settlementStatus.includes('Settled') ? 'px-2 py-0.5 text-xs rounded bg-emerald-500/20 text-emerald-300 font-medium' : 'px-2 py-0.5 text-xs rounded bg-amber-500/20 text-amber-300 font-medium'">
                      {{ inv.settlementStatus }}
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
export class TransferPricingCockpitComponent {
  totalEntities = signal<number>(4);
  totalVolume = signal<number>(2885450);
  complianceRate = signal<number>(100.0);
  netSettlement = signal<number>(807926);

  entities = signal<IntercompanyEntityItem[]>([
    { entityId: 'ENT-US-HQ', legalName: 'SGrowing Technologies Inc.', jurisdiction: 'UnitedStates_Delaware', taxIdentificationNumber: 'EIN-12-9844102', functionalProfile: 'Principal_RiskTaker', functionalCurrency: 'USD' },
    { entityId: 'ENT-IE-OPS', legalName: 'SGrowing International Operations Ltd.', jurisdiction: 'Ireland_Dublin', taxIdentificationNumber: 'IE-9941028-V', functionalProfile: 'Distributor_LimitedRisk', functionalCurrency: 'EUR' },
    { entityId: 'ENT-SG-APAC', legalName: 'SGrowing APAC Hub Pte. Ltd.', jurisdiction: 'Singapore', taxIdentificationNumber: 'UEN-202611984K', functionalProfile: 'SharedServices', functionalCurrency: 'SGD' },
    { entityId: 'ENT-UK-RND', legalName: 'SGrowing R&D UK Limited', jurisdiction: 'UnitedKingdom_London', taxIdentificationNumber: 'GB-771928410', functionalProfile: 'RndServiceProvider', functionalCurrency: 'GBP' }
  ]);

  invoices = signal<IntercompanyInvoiceItem[]>([
    { invoiceId: 'INV-IC-2026-001', agreementId: 'AGR-TP-01', billingPeriod: '2026-M03', baseCostAmount: 850000, appliedMarkupPct: 8.5, totalInvoiceAmount: 922250, currency: 'USD', settlementStatus: 'Settled_NettingPool' },
    { invoiceId: 'INV-IC-2026-002', agreementId: 'AGR-TP-02', billingPeriod: '2026-M03', baseCostAmount: 320000, appliedMarkupPct: 6.0, totalInvoiceAmount: 339200, currency: 'USD', settlementStatus: 'Settled_NettingPool' },
    { invoiceId: 'INV-IC-2026-003', agreementId: 'AGR-TP-03', billingPeriod: '2026-M03', baseCostAmount: 1450000, appliedMarkupPct: 12.0, totalInvoiceAmount: 1624000, currency: 'EUR', settlementStatus: 'Approved_PendingSettlement' }
  ]);

  recalculateMarkups(): void {
    this.complianceRate.set(100.0);
  }

  executeFxNetting(): void {
    this.invoices.update(list =>
      list.map(inv => ({ ...inv, settlementStatus: 'Settled_NettingPool' }))
    );
    this.netSettlement.set(0);
  }
}
