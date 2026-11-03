import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface EscrowCampaign {
  campaignId: string;
  creatorHandle: string;
  platform: string;
  lockedEscrowUsd: number;
  gmvAttributedUsd: number;
  roasMultiplier: number;
  fraudRiskScore: number;
  status: 'LockedInEscrow' | 'MilestoneReleased' | 'SlashedFraud';
}

@Component({
  selector: 'app-influencer-escrow-v2-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="influencer-escrow-v2-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-amber-500/20 text-amber-400 border border-amber-500/30">Horizon 87</span>
            <span class="text-xs text-slate-400">Creator Performance & Attribution Settlement</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Influencer Affiliate Payout & Escrow</h1>
          <p class="text-sm text-slate-400">Smart-contract milestone disbursement, click-farm fraud prevention, and real-time ROAS attribution escrow.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-escrow-balance">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Escrow Balance</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">\${{ totalEscrowLocked() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Collateral held safely in smart contract</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-attributed-gmv">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Attributed GMV</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ totalGmv() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Omnichannel trackable sales</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-average-roas">
          <div class="text-xs font-medium text-slate-400 uppercase">Average Campaign ROAS</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ averageRoas() | number:'1.1-1' }}x</div>
          <div class="text-xs text-slate-400 mt-1">Target benchmark: > 3.0x</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-released-payouts">
          <div class="text-xs font-medium text-slate-400 uppercase">Released Payouts</div>
          <div class="text-2xl font-bold text-white mt-1">{{ releasedCampaignsCount() }} / {{ campaigns().length }}</div>
          <div class="text-xs text-slate-400 mt-1">Verified fraud-free milestones</div>
        </div>
      </section>

      <!-- Campaigns Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Creator Escrow Contracts</h2>
          <div class="space-y-3">
            @for (camp of campaigns(); track camp.campaignId) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedCampaign()?.campaignId === camp.campaignId"
                [class.ring-amber-500]="selectedCampaign()?.campaignId === camp.campaignId"
                (click)="selectCampaign(camp)"
                data-testid="escrow-campaign-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <div class="flex items-center gap-2">
                      <span class="font-semibold text-white text-base">{{ camp.creatorHandle }}</span>
                      <span class="text-xs px-2 py-0.5 rounded bg-slate-700 text-slate-300">{{ camp.platform }}</span>
                    </div>
                    <p class="text-xs text-slate-400 mt-0.5">Escrow Locked: \${{ camp.lockedEscrowUsd | number:'1.0-0' }} &bull; Attributed GMV: \${{ camp.gmvAttributedUsd | number:'1.0-0' }}</p>
                  </div>
                  <div class="text-right">
                    <span class="text-sm font-bold text-emerald-400">{{ camp.roasMultiplier }}x ROAS</span>
                    <div class="text-xs text-slate-400">Fraud Risk: {{ camp.fraudRiskScore }}%</div>
                  </div>
                </div>

                <div class="mt-4 flex items-center justify-between pt-2 border-t border-slate-700/60">
                  <div class="text-xs">
                    <span class="text-slate-400">Contract State:</span>
                    <span class="ml-1 font-semibold" [class.text-amber-400]="camp.status === 'LockedInEscrow'" [class.text-emerald-400]="camp.status === 'MilestoneReleased'">
                      {{ camp.status }}
                    </span>
                  </div>

                  @if (camp.status === 'MilestoneReleased') {
                    <span class="text-xs px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-medium" data-testid="escrow-released-status">
                      Funds Transferred
                    </span>
                  } @else {
                    <button
                      (click)="releaseEscrow(camp.campaignId); $event.stopPropagation()"
                      data-testid="btn-release-escrow"
                      class="px-3 py-1 bg-amber-600 hover:bg-amber-500 text-white text-xs font-semibold rounded shadow transition-colors"
                    >
                      Release Escrow
                    </button>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Escrow Contract Telemetry</h2>
            @if (selectedCampaign(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Creator Entity</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.creatorHandle }} ({{ sel.platform }})</div>
                  <div class="text-xs text-amber-400 mt-1">Smart Vault ID: {{ sel.campaignId }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Escrow Value:</span>
                    <span class="font-mono text-white">\${{ sel.lockedEscrowUsd | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Verified Revenue:</span>
                    <span class="font-mono text-emerald-400">\${{ sel.gmvAttributedUsd | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Attributed ROAS:</span>
                    <span class="font-mono text-cyan-300">{{ sel.roasMultiplier }}x</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Anti-Fraud Validation:</span>
                    <span class="font-mono text-emerald-400">Passed ({{ sel.fraudRiskScore }}% bot score)</span>
                  </div>
                </div>

                @if (sel.status !== 'MilestoneReleased') {
                  <button
                    (click)="releaseEscrow(sel.campaignId)"
                    class="w-full mt-4 py-2 bg-gradient-to-r from-amber-600 to-yellow-600 hover:from-amber-500 hover:to-yellow-500 text-white font-semibold rounded text-sm shadow transition-all"
                  >
                    Disburse Locked Milestone Payout
                  </button>
                }
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select a campaign to inspect escrow ledger.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-amber-400 font-semibold">Fraud Protection Guard:</span> Real-time click stream inspection blocks botnets and invalid traffic before release.
          </div>
        </div>
      </section>
    </div>
  `
})
export class InfluencerEscrowV2CockpitComponent {
  campaigns = signal<EscrowCampaign[]>([
    {
      campaignId: 'escrow_camp_01',
      creatorHandle: '@tech_reviewer_alex',
      platform: 'YouTube Tech',
      lockedEscrowUsd: 15000,
      gmvAttributedUsd: 68000,
      roasMultiplier: 4.5,
      fraudRiskScore: 3,
      status: 'LockedInEscrow'
    },
    {
      campaignId: 'escrow_camp_02',
      creatorHandle: '@growth_hacker_sarah',
      platform: 'LinkedIn Pulse',
      lockedEscrowUsd: 8500,
      gmvAttributedUsd: 34000,
      roasMultiplier: 4.0,
      fraudRiskScore: 1,
      status: 'LockedInEscrow'
    },
    {
      campaignId: 'escrow_camp_03',
      creatorHandle: '@saas_insider_mike',
      platform: 'Twitter / X Tech',
      lockedEscrowUsd: 12000,
      gmvAttributedUsd: 45600,
      roasMultiplier: 3.8,
      fraudRiskScore: 2,
      status: 'LockedInEscrow'
    }
  ]);

  selectedCampaign = signal<EscrowCampaign | null>(this.campaigns()[0]);

  totalEscrowLocked = computed(() =>
    this.campaigns()
      .filter(c => c.status === 'LockedInEscrow')
      .reduce((sum, c) => sum + c.lockedEscrowUsd, 0)
  );

  totalGmv = computed(() =>
    this.campaigns().reduce((sum, c) => sum + c.gmvAttributedUsd, 0)
  );

  averageRoas = computed(() => {
    const list = this.campaigns();
    if (!list.length) return 0;
    return list.reduce((sum, c) => sum + c.roasMultiplier, 0) / list.length;
  });

  releasedCampaignsCount = computed(() =>
    this.campaigns().filter(c => c.status === 'MilestoneReleased').length
  );

  selectCampaign(campaign: EscrowCampaign): void {
    this.selectedCampaign.set(campaign);
  }

  releaseEscrow(campaignId: string): void {
    this.campaigns.update(list =>
      list.map(c => c.campaignId === campaignId ? { ...c, status: 'MilestoneReleased' } : c)
    );
    if (this.selectedCampaign()?.campaignId === campaignId) {
      this.selectedCampaign.update(curr => curr ? { ...curr, status: 'MilestoneReleased' } : null);
    }
  }
}
