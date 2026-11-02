import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CreatorProfile {
  creatorId: string;
  handle: string;
  platform: string;
  followersCount: number;
  engagementRatePercent: number;
  authenticityScore: number;
  nicheCategory: string;
  isVerified: boolean;
}

export interface EscrowMilestone {
  milestoneId: string;
  title: string;
  payoutAmount: number;
  deliverableRequirement: string;
  targetViews: number;
  actualViews: number;
  isVerified: boolean;
  isReleased: boolean;
}

export interface EscrowContract {
  contractId: string;
  creatorId: string;
  campaignName: string;
  totalEscrowAmount: number;
  releasedAmount: number;
  remainingAmount: number;
  milestones: EscrowMilestone[];
  status: string;
  createdAtUtc: string;
}

@Component({
  selector: 'app-influencer-escrow-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="influencer-escrow-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-pink-400">
            <span class="inline-block w-2 h-2 rounded-full bg-pink-400 animate-pulse"></span>
            Creator Economy & Escrow • Horizon 51
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Influencer Outreach & Contract Escrow
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Milestone-gated escrow release, engagement authenticity verification & automated creator ROI attribution.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="simulateNewContract()"
            data-testid="create-contract-btn"
            class="px-4 py-2 bg-pink-600 hover:bg-pink-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-pink-600/20"
          >
            <span>+</span> Fund Creator Escrow
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Escrow Contracts</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-active-contracts">{{ contracts().length }}</div>
          <div class="text-emerald-400 text-xs mt-1">100% Escrow Funded</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Secured Escrow Capital</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-total-escrow">
            \${{ totalEscrowCapital() | number:'1.0-0' }}
          </div>
          <div class="text-slate-400 text-xs mt-1">\${{ totalReleasedCapital() | number:'1.0-0' }} Disbursed</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Attributed GMV</div>
          <div class="text-2xl font-bold text-pink-400 mt-1">\${{ (totalReleasedCapital() * 4.8) | number:'1.0-0' }}</div>
          <div class="text-emerald-400 text-xs mt-1">4.8x Creator ROAS</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Creator Authenticity Avg</div>
          <div class="text-2xl font-bold text-white mt-1">95.3%</div>
          <div class="text-slate-400 text-xs mt-1">Anti-Bot Audited</div>
        </div>
      </div>

      <!-- Main Stage: Contracts & Selected Contract Milestones -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Contracts Table (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Active Escrow Agreements</h2>
            <span class="text-xs text-slate-400">{{ contracts().length }} Campaigns</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (contract of contracts(); track contract.contractId) {
              <div
                (click)="selectContract(contract)"
                [class.bg-slate-700/40]="selectedContract()?.contractId === contract.contractId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'contract-row-' + contract.contractId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ contract.campaignName }}</span>
                    <span
                      [class]="contract.status === 'Completed' ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-pink-900/60 text-pink-300 border-pink-700/60'"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                    >
                      {{ contract.status }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Creator: <span class="text-white font-medium">{{ contract.creatorId }}</span> • Contract ID: <span class="font-mono text-slate-300">{{ contract.contractId }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Total: <strong class="text-white">\${{ contract.totalEscrowAmount | number:'1.0-0' }}</strong> • Released: \${{ contract.releasedAmount | number:'1.0-0' }} • Remaining:
                    <strong class="text-pink-400">\${{ contract.remainingAmount | number:'1.0-0' }}</strong>
                  </div>
                </div>

                <div class="flex flex-col items-end gap-1 self-end md:self-auto">
                  <div class="text-xs text-slate-400">Milestone Progress</div>
                  <div class="text-sm font-bold text-emerald-400">
                    {{ getCompletedMilestonesCount(contract) }} / {{ contract.milestones.length }}
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Milestones Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          @if (selectedContract(); as active) {
            <div class="space-y-4">
              <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
                <div>
                  <h3 class="font-bold text-white text-base">Deliverable Milestones</h3>
                  <p class="text-xs text-slate-400">{{ active.campaignName }}</p>
                </div>
                <span class="text-xs px-2.5 py-1 rounded bg-slate-900 border border-slate-700 text-slate-300 font-mono">
                  \${{ active.totalEscrowAmount | number:'1.0-0' }} Total
                </span>
              </div>

              <!-- Milestones List -->
              <div class="space-y-3">
                @for (ms of active.milestones; track ms.milestoneId) {
                  <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2">
                    <div class="flex items-center justify-between">
                      <div class="font-semibold text-xs text-white">{{ ms.title }}</div>
                      <span class="text-xs font-bold text-emerald-400">\${{ ms.payoutAmount | number:'1.0-0' }}</span>
                    </div>

                    <div class="text-[11px] text-slate-400">
                      Requirement: <span class="text-slate-300">{{ ms.deliverableRequirement }}</span>
                    </div>

                    <div class="flex items-center justify-between text-xs pt-1 border-t border-slate-800">
                      <div class="text-slate-400 text-[11px]">
                        Views: <strong class="text-white">{{ ms.actualViews | number:'1.0-0' }}</strong> / {{ ms.targetViews | number:'1.0-0' }}
                      </div>

                      @if (ms.isReleased) {
                        <span class="text-emerald-400 text-[11px] font-semibold flex items-center gap-1">
                          ✓ Payout Released
                        </span>
                      } @else {
                        <button
                          (click)="releaseMilestone(active, ms)"
                          data-testid="release-milestone-btn"
                          class="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-[11px] font-semibold transition-colors"
                        >
                          Verify & Release Payout
                        </button>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          } @else {
            <div class="text-center py-16 text-slate-400 text-xs">
              Select an escrow contract to inspect milestone deliverables and trigger fund release.
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class InfluencerEscrowCockpitComponent {
  readonly contracts = signal<EscrowContract[]>([
    {
      contractId: 'escrow_contract_01',
      creatorId: 'creator_sara_tech',
      campaignName: 'Growth OS Q4 Launch Blitz',
      totalEscrowAmount: 5000,
      releasedAmount: 2000,
      remainingAmount: 3000,
      status: 'MilestoneApproved',
      createdAtUtc: new Date(Date.now() - 10 * 86400000).toISOString(),
      milestones: [
        {
          milestoneId: 'ms_01',
          title: 'Deliverable 1: In-depth Platform Walkthrough Reel',
          payoutAmount: 2000,
          deliverableRequirement: 'Reel posted with custom bio link',
          targetViews: 50000,
          actualViews: 68000,
          isVerified: true,
          isReleased: true
        },
        {
          milestoneId: 'ms_02',
          title: 'Deliverable 2: Follow-up ROI Breakdown Story Series',
          payoutAmount: 3000,
          deliverableRequirement: '3x Stories with swipe-up UTM',
          targetViews: 75000,
          actualViews: 82000,
          isVerified: false,
          isReleased: false
        }
      ]
    }
  ]);

  readonly selectedContract = signal<EscrowContract | null>(this.contracts()[0]);

  readonly totalEscrowCapital = computed(() =>
    this.contracts().reduce((acc, c) => acc + c.totalEscrowAmount, 0)
  );

  readonly totalReleasedCapital = computed(() =>
    this.contracts().reduce((acc, c) => acc + c.releasedAmount, 0)
  );

  selectContract(c: EscrowContract): void {
    this.selectedContract.set(c);
  }

  getCompletedMilestonesCount(c: EscrowContract): number {
    return c.milestones.filter(m => m.isReleased).length;
  }

  simulateNewContract(): void {
    const newContract: EscrowContract = {
      contractId: `escrow_${Date.now()}`,
      creatorId: 'creator_dave_growth',
      campaignName: 'Black Friday SaaS Creator Blitz',
      totalEscrowAmount: 7500,
      releasedAmount: 0,
      remainingAmount: 7500,
      status: 'Funded',
      createdAtUtc: new Date().toISOString(),
      milestones: [
        {
          milestoneId: `ms_${Date.now()}_1`,
          title: 'Dedicated YouTube Product Deep-Dive',
          payoutAmount: 5000,
          deliverableRequirement: '12-minute integrated review video',
          targetViews: 100000,
          actualViews: 0,
          isVerified: false,
          isReleased: false
        },
        {
          milestoneId: `ms_${Date.now()}_2`,
          title: 'Community Discord Workshop AMA',
          payoutAmount: 2500,
          deliverableRequirement: 'Live demo with exclusive coupon code',
          targetViews: 15000,
          actualViews: 0,
          isVerified: false,
          isReleased: false
        }
      ]
    };

    this.contracts.update(list => [newContract, ...list]);
    this.selectedContract.set(newContract);
  }

  releaseMilestone(contract: EscrowContract, milestone: EscrowMilestone): void {
    const updatedMilestones = contract.milestones.map(m =>
      m.milestoneId === milestone.milestoneId ? { ...m, isVerified: true, isReleased: true } : m
    );
    const newReleased = contract.releasedAmount + milestone.payoutAmount;
    const newRemaining = contract.totalEscrowAmount - newReleased;
    const newStatus = newRemaining <= 0 ? 'Completed' : 'MilestoneApproved';

    const updatedContract: EscrowContract = {
      ...contract,
      milestones: updatedMilestones,
      releasedAmount: newReleased,
      remainingAmount: newRemaining,
      status: newStatus
    };

    this.contracts.update(list =>
      list.map(c => (c.contractId === contract.contractId ? updatedContract : c))
    );
    if (this.selectedContract()?.contractId === contract.contractId) {
      this.selectedContract.set(updatedContract);
    }
  }
}
