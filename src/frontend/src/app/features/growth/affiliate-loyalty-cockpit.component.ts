import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AffiliatePartnerUi {
  id: string;
  name: string;
  code: string;
  tier1Rate: number;
  tier2Rate: number;
  totalEarned: number;
  pendingPayout: number;
  parentPartner?: string;
  conversionsCount: number;
}

export interface LoyaltyTierInfo {
  tier: string;
  pointsRequired: number;
  benefits: string;
  color: string;
}

@Component({
  selector: 'app-affiliate-loyalty-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="affiliate-loyalty-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Viral Referral & Multi-Tier Affiliate Network</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-violet-100 text-violet-800">
              Anti-Fraud Cycle Defense Active
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Automate cookieless affiliate tracking, parent-child multi-tier revenue splits, and customer loyalty tier ledgers.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="showRegisterModal.set(true)"
            class="px-4 py-2 bg-violet-600 hover:bg-violet-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-register-partner">
            + Register Affiliate Partner
          </button>
        </div>
      </div>

      <!-- Network Performance KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="affiliate-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Active Partners</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-partners-count">{{ partners().length }}</div>
          <div class="text-[11px] text-violet-600 mt-1 font-semibold">Tier 1 & Tier 2 Active</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Total Paid Commissions</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-total-earned">
            \${{ totalCommissionsEarned() | number:'1.2-2' }}
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Stripe Connect & PayPal</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Pending Payout Batch</div>
          <div class="text-2xl font-bold text-amber-600 mt-1 font-mono">
            \${{ pendingPayoutTotal() | number:'1.2-2' }}
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Net-30 Settlement</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Fraud Detection Status</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono">100% Clean</div>
          <div class="text-[11px] text-slate-400 mt-1">0 Self-Referral Collisions</div>
        </div>
      </div>

      <!-- Main Workbench -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Partner Network Table -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Affiliate Partner Network</h3>
            <span class="text-xs text-slate-400 font-mono">{{ partners().length }} active partners</span>
          </div>

          <div class="space-y-3" data-testid="partners-list">
            @for (partner of partners(); track partner.id) {
              <div class="p-4 border rounded-lg hover:border-slate-300 transition-colors space-y-2">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-slate-800 text-sm">{{ partner.name }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-violet-50 text-violet-700 border border-violet-200">
                      {{ partner.code }}
                    </span>
                  </div>
                  <div class="text-right">
                    <span class="font-mono font-bold text-sm text-slate-900">\${{ partner.totalEarned | number:'1.0-0' }}</span>
                    <span class="text-[11px] text-slate-400 block">Pending: \${{ partner.pendingPayout | number:'1.0-0' }}</span>
                  </div>
                </div>

                <div class="flex items-center justify-between pt-2 border-t text-xs text-slate-500">
                  <div class="flex items-center gap-3">
                    <span>Direct: <strong class="text-slate-800">{{ partner.tier1Rate }}%</strong></span>
                    <span>Parent Tier 2: <strong class="text-slate-800">{{ partner.tier2Rate }}%</strong></span>
                    @if (partner.parentPartner) {
                      <span class="text-violet-600 font-medium">Sub-Affiliate of {{ partner.parentPartner }}</span>
                    }
                  </div>
                  <span class="font-mono text-slate-600">{{ partner.conversionsCount }} orders referred</span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Customer Loyalty Tiers Radar -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Customer Loyalty Tiers</h3>
          <p class="text-xs text-slate-500">
            Points accrue automatically on invoice settlement (1 pt / \$1 spend).
          </p>

          <div class="space-y-3" data-testid="loyalty-tiers-list">
            @for (t of loyaltyTiers; track t.tier) {
              <div class="p-3 border rounded-lg space-y-1">
                <div class="flex justify-between items-center text-xs">
                  <span class="font-bold text-slate-800">{{ t.tier }} Tier</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold"
                    [ngClass]="t.color">
                    {{ t.pointsRequired }}+ pts
                  </span>
                </div>
                <div class="text-[11px] text-slate-500">{{ t.benefits }}</div>
              </div>
            }
          </div>

          <!-- Quick Referral Simulation Box -->
          <div class="pt-3 border-t space-y-2">
            <h4 class="font-bold text-slate-800 text-xs">Simulate Conversion & Points</h4>
            <div class="flex gap-2">
              <input 
                type="number" 
                [(ngModel)]="simulatedOrderAmount" 
                placeholder="Order Amount"
                class="w-full px-3 py-1.5 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-violet-500 focus:outline-none"
                data-testid="input-sim-order" />
              <button 
                (click)="simulateConversion()"
                class="px-3 py-1.5 bg-violet-600 hover:bg-violet-700 text-white rounded-lg text-xs font-medium whitespace-nowrap shadow-sm"
                data-testid="btn-sim-conversion">
                Test Payout
              </button>
            </div>
            @if (simulatedResult()) {
              <div class="text-[11px] text-emerald-600 font-medium font-mono" data-testid="sim-result-text">
                {{ simulatedResult() }}
              </div>
            }
          </div>
        </div>
      </div>

      <!-- Register Modal -->
      @if (showRegisterModal()) {
        <div class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-white rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-xl border">
            <div class="flex items-center justify-between border-b pb-3">
              <h3 class="font-bold text-slate-900 text-base">Register Affiliate Partner</h3>
              <button (click)="showRegisterModal.set(false)" class="text-slate-400 hover:text-slate-600">&times;</button>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Partner / Agency Name</label>
                <input 
                  type="text" 
                  [(ngModel)]="newPartnerName" 
                  class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-violet-500 focus:outline-none"
                  data-testid="input-partner-name" />
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Referral Code Prefix</label>
                <input 
                  type="text" 
                  [(ngModel)]="newPartnerCode" 
                  class="w-full px-3 py-2 border rounded-lg text-xs font-mono uppercase focus:ring-2 focus:ring-violet-500 focus:outline-none"
                  data-testid="input-partner-code" />
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-xs font-semibold text-slate-700 mb-1">Direct Commission (%)</label>
                  <input 
                    type="number" 
                    [(ngModel)]="newTier1Rate" 
                    class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-violet-500 focus:outline-none"
                    data-testid="input-tier1-rate" />
                </div>
                <div>
                  <label class="block text-xs font-semibold text-slate-700 mb-1">Parent Tier 2 (%)</label>
                  <input 
                    type="number" 
                    [(ngModel)]="newTier2Rate" 
                    class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-violet-500 focus:outline-none"
                    data-testid="input-tier2-rate" />
                </div>
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-3 border-t">
              <button 
                (click)="showRegisterModal.set(false)"
                class="px-3 py-1.5 border rounded-lg text-xs font-medium text-slate-600 hover:bg-slate-50">
                Cancel
              </button>
              <button 
                (click)="registerPartner()"
                class="px-4 py-1.5 bg-violet-600 hover:bg-violet-700 text-white rounded-lg text-xs font-medium shadow-sm"
                data-testid="btn-submit-partner">
                Create Partner Account
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AffiliateLoyaltyCockpitComponent {
  showRegisterModal = signal(false);

  partners = signal<AffiliatePartnerUi[]>([
    {
      id: 'aff_1',
      name: 'Alpha Growth Agency',
      code: 'ALPHA20',
      tier1Rate: 20.0,
      tier2Rate: 5.0,
      totalEarned: 18450.00,
      pendingPayout: 3200.00,
      conversionsCount: 42
    },
    {
      id: 'aff_2',
      name: 'DevRel Tech Media',
      code: 'DEVREL10',
      tier1Rate: 15.0,
      tier2Rate: 5.0,
      totalEarned: 4620.00,
      pendingPayout: 890.00,
      parentPartner: 'Alpha Growth Agency',
      conversionsCount: 19
    }
  ]);

  loyaltyTiers: LoyaltyTierInfo[] = [
    { tier: 'Bronze', pointsRequired: 0, benefits: 'Standard support, 1x referral points multiplier', color: 'bg-amber-100 text-amber-800' },
    { tier: 'Silver', pointsRequired: 5000, benefits: 'Priority queue, 1.25x points, quarterly strategy review', color: 'bg-slate-200 text-slate-800' },
    { tier: 'Gold', pointsRequired: 15000, benefits: 'Dedicated Customer Success Manager, 1.5x points, API rate limit 2x', color: 'bg-yellow-100 text-yellow-800' },
    { tier: 'Platinum', pointsRequired: 50000, benefits: 'Air-gapped deployment option, 2.0x points, executive briefings', color: 'bg-indigo-100 text-indigo-800' }
  ];

  totalCommissionsEarned = computed(() => {
    return this.partners().reduce((sum, p) => sum + p.totalEarned, 0);
  });

  pendingPayoutTotal = computed(() => {
    return this.partners().reduce((sum, p) => sum + p.pendingPayout, 0);
  });

  simulatedOrderAmount = 2500;
  simulatedResult = signal<string | null>(null);

  newPartnerName = 'ScaleOps Ventures';
  newPartnerCode = 'SCALEOPS';
  newTier1Rate = 20.0;
  newTier2Rate = 5.0;

  simulateConversion() {
    const commission = this.simulatedOrderAmount * 0.20;
    this.simulatedResult.set(`✓ Simulated conversion for $${this.simulatedOrderAmount}: $${commission} direct commission + ${this.simulatedOrderAmount} loyalty pts.`);
  }

  registerPartner() {
    const created: AffiliatePartnerUi = {
      id: 'aff_' + Date.now(),
      name: this.newPartnerName,
      code: this.newPartnerCode.toUpperCase(),
      tier1Rate: this.newTier1Rate,
      tier2Rate: this.newTier2Rate,
      totalEarned: 0,
      pendingPayout: 0,
      conversionsCount: 0
    };

    this.partners.update(list => [created, ...list]);
    this.showRegisterModal.set(false);
  }
}
