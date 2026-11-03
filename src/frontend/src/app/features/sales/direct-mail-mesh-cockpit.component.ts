import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DirectMailCampaign {
  campaignId: string;
  campaignName: string;
  targetTier: string;
  giftType: string;
  unitCostUsd: number;
  triggerStage: string;
  status: string;
}

export interface DispatchedPackageRecord {
  trackingNumber: string;
  campaignId: string;
  recipientExecutiveName: string;
  companyName: string;
  carrier: string;
  deliveryStatus: string;
}

@Component({
  selector: 'app-direct-mail-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="direct-mail-mesh-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-amber-500 animate-pulse"></span>
            ABM Direct Mail & Executive Gifting Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Programmatic Physical Gifting Triggers, Corporate Anti-Bribery Compliance & Pipeline Influence Attribution
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="dispatchGift()" class="px-4 py-2 bg-amber-600 hover:bg-amber-500 text-white font-medium rounded-lg shadow-sm transition">
            Dispatch Executive Gift Box
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Campaigns</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ campaigns().length }}</p>
          <span class="text-xs text-amber-400">Fortune 500 & Mid-Market</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Packages Dispatched</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">{{ dispatches().length }}</p>
          <span class="text-xs text-slate-400">FedEx / UPS Priority Overnight</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Delivery Rate</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">{{ deliverySuccessRatePct() }}%</p>
          <span class="text-xs text-slate-400">Signed Direct-to-Desk</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Pipeline Influence</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">{{ pipelineInfluenceRatePct() }}%</p>
          <span class="text-xs text-emerald-400">+3.4x Meeting Conversion Lift</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Campaigns -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Curated Gift Tiers</h2>
          <div class="space-y-3">
            <div *ngFor="let c of campaigns()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-medium text-slate-200 text-sm">{{ c.campaignName }}</span>
                <span class="px-2 py-0.5 text-xs bg-amber-500/20 text-amber-300 rounded font-semibold font-mono">\${{ c.unitCostUsd }}</span>
              </div>
              <p class="text-xs text-slate-400 mb-2">{{ c.giftType }}</p>
              <div class="text-[10px] font-mono text-indigo-400 bg-slate-950 p-1.5 rounded">
                Trigger: {{ c.triggerStage }}
              </div>
            </div>
          </div>
        </section>

        <!-- Dispatches Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Real-Time Delivery & Attribution Ledger</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Tracking Number</th>
                  <th class="pb-3 font-medium">Recipient Executive</th>
                  <th class="pb-3 font-medium">Company</th>
                  <th class="pb-3 font-medium">Carrier</th>
                  <th class="pb-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let d of dispatches()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-amber-300 font-semibold">{{ d.trackingNumber }}</td>
                  <td class="py-3 font-medium text-slate-200">{{ d.recipientExecutiveName }}</td>
                  <td class="py-3 text-slate-400">{{ d.companyName }}</td>
                  <td class="py-3 text-slate-400 text-xs">{{ d.carrier }}</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-xs rounded font-semibold font-mono"
                          [ngClass]="d.deliveryStatus.includes('DELIVERED') ? 'bg-emerald-500/20 text-emerald-300' : 'bg-indigo-500/20 text-indigo-300'">
                      {{ d.deliveryStatus }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  `
})
export class DirectMailMeshCockpitComponent {
  campaigns = signal<DirectMailCampaign[]>([
    { campaignId: 'CMP-DIR-01', campaignName: 'Fortune 500 CIO VIP Concierge Box', targetTier: 'ENTERPRISE_TIER_1', giftType: 'Custom Hardcover Growth Playbook + Smart Ember Mug', unitCostUsd: 145.00, triggerStage: 'OPPORTUNITY_QUALIFIED_DISCOVERY', status: 'ACTIVE' },
    { campaignId: 'CMP-DIR-02', campaignName: 'Mid-Market VP Engineering Welcome Kit', targetTier: 'GROWTH_TIER_2', giftType: 'Bespoke Mechanical Keycap Set + Founder Letter', unitCostUsd: 85.00, triggerStage: 'TECHNICAL_VALIDATION_PASSED', status: 'ACTIVE' }
  ]);

  dispatches = signal<DispatchedPackageRecord[]>([
    { trackingNumber: 'FDX-9988-7711-22', campaignId: 'CMP-DIR-01', recipientExecutiveName: 'Sarah Jenkins (CIO)', companyName: 'Apex Global Financial', carrier: 'FedEx Priority Overnight', deliveryStatus: 'DELIVERED_SIGNED' },
    { trackingNumber: 'UPS-1122-3344-55', campaignId: 'CMP-DIR-02', recipientExecutiveName: 'Marcus Vance (VP Eng)', companyName: 'CloudSphere Systems', carrier: 'UPS 2nd Day Air', deliveryStatus: 'IN_TRANSIT_OUT_FOR_DELIVERY' }
  ]);

  deliverySuccessRatePct = signal(98.6);
  pipelineInfluenceRatePct = signal(42.8);

  dispatchGift() {
    const newDispatch: DispatchedPackageRecord = {
      trackingNumber: `FDX-${Date.now().toString().slice(-6)}`,
      campaignId: 'CMP-DIR-01',
      recipientExecutiveName: 'Elena Rostova (CTO)',
      companyName: 'Vanguard Logistics AI',
      carrier: 'FedEx Priority Overnight',
      deliveryStatus: 'LABEL_CREATED_SCHEDULED_PICKUP'
    };
    this.dispatches.update(list => [newDispatch, ...list]);
  }
}
