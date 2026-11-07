import { Component, signal, computed, inject, Optional, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export type LoyaltyMembershipTier = 'Bronze' | 'Silver' | 'Gold' | 'VipDiamond';
export type LoyaltyTransactionType =
  | 'EarnPurchase'
  | 'EarnReview'
  | 'EarnReferral'
  | 'BurnRedemption'
  | 'ManualAdjustment';

export interface LoyaltyPointLedgerEntry {
  entryId: string;
  tenantId: string;
  contactId: string;
  type: LoyaltyTransactionType;
  points: number;
  balanceAfter: number;
  referenceId: string;
  description: string;
  createdAt: string;
}

export interface LoyaltyAccountSummary {
  contactId: string;
  tenantId: string;
  contactName: string;
  contactEmail: string;
  currentPointsBalance: number;
  lifetimePointsEarned: number;
  currentTier: LoyaltyMembershipTier;
  nextTierPointsRemaining: number;
  referralCode: string;
  activeTierPerks: string[];
  recentTransactions: LoyaltyPointLedgerEntry[];
}

export interface ReferralAttributionRecord {
  referralId: string;
  tenantId: string;
  referrerContactId: string;
  refereeContactId: string;
  referralCode: string;
  referrerPointsAwarded: number;
  refereeDiscountPercent: number;
  attributedAt: string;
}

export function calculateTier(lifetimePoints: number): LoyaltyMembershipTier {
  if (lifetimePoints >= 5000) return 'VipDiamond';
  if (lifetimePoints >= 2000) return 'Gold';
  if (lifetimePoints >= 500) return 'Silver';
  return 'Bronze';
}

export function calculateNextTierPointsRemaining(lifetimePoints: number): number {
  if (lifetimePoints >= 5000) return 0;
  if (lifetimePoints >= 2000) return 5000 - lifetimePoints;
  if (lifetimePoints >= 500) return 2000 - lifetimePoints;
  return 500 - lifetimePoints;
}

export function getPerksForTier(tier: LoyaltyMembershipTier): string[] {
  switch (tier) {
    case 'VipDiamond':
      return [
        '2.0x Double Point Earning on every SAR spent',
        'Dedicated VIP Concierge & Personal Account Manager',
        'Zero-Fee Instant Returns & GCC Priority Exchanges',
        'Complimentary White-Glove VIP Express Courier',
        'Annual Invitation to Exclusive SGrowing Diamond Gala'
      ];
    case 'Gold':
      return [
        '1.5x Points Multiplier on all marketplace orders',
        'Complimentary Express Same-Day Delivery in KSA',
        'Priority WhatsApp Support Hotline (24/7)',
        'Exclusive Seasonal VIP Mystery Gift Box'
      ];
    case 'Silver':
      return [
        '1.25x Points Multiplier on GCC orders',
        'Free Standard Delivery on orders over SAR 150',
        'Early Access (12h) to Flash Sales and Launches'
      ];
    case 'Bronze':
    default:
      return [
        'Standard 1x Points Earning on all orders (1 pt / SAR)',
        'Birthday Bonus Celebration Points (100 pts)',
        'Members-Only Monthly Newsletter & Secret Deals'
      ];
  }
}

@Component({
  selector: 'app-loyalty-referral-studio',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Header -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/marketing" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Marketing
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              💎 Omnichannel Loyalty & Referral Rewards Ledger
            </span>
            <span class="bg-amber-500/20 text-amber-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-amber-500/30">
              PRD Domain H4 • Double-Entry Ledger • Tier Thresholds
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <div class="text-xs text-slate-400 bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
              <span>Active Member: <strong class="text-white">{{ account().contactName }}</strong></span>
            </div>
          </div>
        </header>

        <!-- KPI Metrics Ribbon -->
        <section class="grid grid-cols-1 md:grid-cols-4 gap-4 p-6 border-b border-slate-800/80 bg-slate-900/40">
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Total Active Members</div>
            <div class="text-2xl font-bold text-indigo-400 mt-1" data-testid="kpi-active-members">{{ activeMembersCount() | number }}</div>
            <div class="text-[11px] text-slate-500 mt-1">+14.2% month-over-month</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Points in Circulation</div>
            <div class="text-2xl font-bold text-amber-400 mt-1" data-testid="kpi-points-circulation">{{ pointsInCirculation() | number }} pts</div>
            <div class="text-[11px] text-slate-500 mt-1">Multi-tenant ledger backed</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Points Redeemed (SAR Value)</div>
            <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="kpi-points-redeemed">SAR {{ redeemedSarValue() | number:'1.0-0' }}</div>
            <div class="text-[11px] text-slate-500 mt-1">10 pts = SAR 1.00 store credit</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Referral Conversion Rate</div>
            <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="kpi-referral-conversion">{{ referralConversionRate() }}</div>
            <div class="text-[11px] text-slate-500 mt-1">Dual-sided reward incentive</div>
          </div>
        </section>

        <!-- Main Content Area -->
        <div class="p-6 space-y-6">
          <!-- Notification Banners -->
          @if (statusMessage()) {
            <div class="p-3.5 rounded-lg bg-emerald-500/10 border border-emerald-500/30 text-emerald-300 text-sm flex items-center justify-between" data-testid="status-banner">
              <span>✓ {{ statusMessage() }}</span>
              <button (click)="clearStatus()" class="text-emerald-400 hover:text-emerald-200 text-xs">✕</button>
            </div>
          }

          @if (errorMessage()) {
            <div class="p-3.5 rounded-lg bg-rose-500/10 border border-rose-500/30 text-rose-300 text-sm flex items-center justify-between" data-testid="error-banner">
              <span>⚠ {{ errorMessage() }}</span>
              <button (click)="clearError()" class="text-rose-400 hover:text-rose-200 text-xs">✕</button>
            </div>
          }

          <!-- Member Profile & Tier Gauge Card -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Account Standing Card -->
            <div class="lg:col-span-1 rounded-xl bg-slate-900 border border-slate-800 p-5 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h2 class="text-lg font-bold text-white">{{ account().contactName }}</h2>
                  <p class="text-xs text-slate-400">{{ account().contactEmail }}</p>
                </div>
                <div [ngClass]="tierBadgeClass(account().currentTier)" class="px-3 py-1 rounded-full text-xs font-bold border" data-testid="member-tier-badge">
                  {{ tierDisplayName(account().currentTier) }}
                </div>
              </div>

              <!-- Points Balance Highlights -->
              <div class="grid grid-cols-2 gap-3 pt-2">
                <div class="p-3 rounded-lg bg-slate-950/70 border border-slate-800">
                  <div class="text-[11px] text-slate-400">Current Balance</div>
                  <div class="text-2xl font-black text-amber-400" data-testid="account-current-balance">
                    {{ account().currentPointsBalance | number }}
                  </div>
                  <div class="text-[10px] text-slate-500">Available to spend</div>
                </div>
                <div class="p-3 rounded-lg bg-slate-950/70 border border-slate-800">
                  <div class="text-[11px] text-slate-400">Lifetime Earned</div>
                  <div class="text-2xl font-black text-indigo-400" data-testid="account-lifetime-points">
                    {{ account().lifetimePointsEarned | number }}
                  </div>
                  <div class="text-[10px] text-slate-500">Qualifying points</div>
                </div>
              </div>

              <!-- Referral Code & Share Link -->
              <div class="pt-2 border-t border-slate-800 space-y-2">
                <div class="text-xs font-semibold text-slate-300">Member Referral Code</div>
                <div class="flex items-center gap-2">
                  <div class="flex-1 bg-slate-950 border border-slate-700 font-mono text-xs text-amber-300 px-3 py-2 rounded-lg truncate" data-testid="referral-link-display">
                    {{ referralShareUrl() }}
                  </div>
                  <button
                    (click)="copyReferralLink()"
                    data-testid="copy-referral-btn"
                    class="px-3 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition shrink-0">
                    {{ linkCopied() ? '✓ Copied' : 'Copy Link' }}
                  </button>
                </div>
                <div class="text-[11px] text-slate-500">
                  Friends get 15% off first order; member earns 250+ bonus points.
                </div>
              </div>
            </div>

            <!-- Tier Progression Gauge & Perks -->
            <div class="lg:col-span-2 rounded-xl bg-slate-900 border border-slate-800 p-5 space-y-4">
              <div class="flex items-center justify-between">
                <h3 class="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2">
                  <span>🏆</span> Tier Progression & Member Perks
                </h3>
                <span class="text-xs text-slate-400">
                  @if (account().nextTierPointsRemaining > 0) {
                    <strong class="text-amber-400">{{ account().nextTierPointsRemaining | number }} pts</strong> to next tier
                  } @else {
                    <span class="text-emerald-400 font-bold">★ Highest Tier Achieved</span>
                  }
                </span>
              </div>

              <!-- Tier Stepper Gauge -->
              <div class="grid grid-cols-4 gap-2 pt-2">
                @for (tier of tiersList; track tier.name) {
                  <div
                    [ngClass]="getTierStepClass(tier.key, account().currentTier)"
                    class="p-2.5 rounded-lg border text-center transition">
                    <div class="text-[10px] font-semibold text-slate-400">{{ tier.range }}</div>
                    <div class="text-xs font-bold mt-0.5">{{ tier.name }}</div>
                    <div class="text-[10px] mt-1 text-slate-400">{{ tier.multiplier }} pts</div>
                  </div>
                }
              </div>

              <!-- Visual Progress Bar -->
              <div class="space-y-1.5 pt-2">
                <div class="flex justify-between text-xs text-slate-400">
                  <span>Progress to Next Tier</span>
                  <span data-testid="tier-progress-percent">{{ tierProgressPercent() }}%</span>
                </div>
                <div class="w-full bg-slate-950 h-3 rounded-full overflow-hidden border border-slate-800">
                  <div
                    class="h-full bg-gradient-to-r from-indigo-500 via-amber-400 to-emerald-400 transition-all duration-500 rounded-full"
                    [style.width.%]="tierProgressPercent()"
                    data-testid="tier-progress-bar"></div>
                </div>
              </div>

              <!-- Active Tier Perks List -->
              <div class="pt-2 border-t border-slate-800 space-y-2">
                <div class="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                  <span>✨</span> Active Perks for {{ tierDisplayName(account().currentTier) }}
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-2" data-testid="active-perks-list">
                  @for (perk of account().activeTierPerks; track perk) {
                    <div class="flex items-start gap-2 text-xs text-slate-300 bg-slate-950/60 p-2 rounded-lg border border-slate-800/80">
                      <span class="text-emerald-400 shrink-0">✓</span>
                      <span>{{ perk }}</span>
                    </div>
                  }
                </div>
              </div>
            </div>
          </div>

          <!-- Bottom Grid: Simulation Panel & Double-Entry Ledger -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Quick Simulation Control Center -->
            <div class="lg:col-span-1 rounded-xl bg-slate-900 border border-slate-800 p-5 space-y-5">
              <h3 class="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2">
                <span>⚡</span> Quick Test Simulation Panel
              </h3>

              <!-- Earn Simulation Sub-panel -->
              <div class="p-3.5 rounded-lg bg-slate-950/80 border border-slate-800 space-y-3">
                <div class="text-xs font-bold text-emerald-400 flex items-center gap-1">
                  <span>+</span> Credit Points (Earn Action)
                </div>
                <div class="space-y-2">
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-1">Transaction Type</label>
                    <select
                      [(ngModel)]="earnType"
                      data-testid="earn-type-select"
                      class="w-full bg-slate-900 border border-slate-700 text-xs rounded-lg px-2.5 py-1.5 text-slate-200">
                      <option value="EarnPurchase">EarnPurchase (Order Checkout)</option>
                      <option value="EarnReview">EarnReview (Product Review)</option>
                      <option value="EarnReferral">EarnReferral (Friend Invited)</option>
                      <option value="ManualAdjustment">ManualAdjustment (Support Credit)</option>
                    </select>
                  </div>

                  <div class="grid grid-cols-2 gap-2">
                    <div>
                      <label class="text-[10px] text-slate-400 block mb-1">Points Amount</label>
                      <input
                        type="number"
                        [(ngModel)]="earnAmount"
                        data-testid="earn-amount-input"
                        class="w-full bg-slate-900 border border-slate-700 text-xs rounded-lg px-2.5 py-1.5 text-slate-200"
                        min="1" />
                    </div>
                    <div>
                      <label class="text-[10px] text-slate-400 block mb-1">Reference ID</label>
                      <input
                        type="text"
                        [(ngModel)]="earnReference"
                        data-testid="earn-reference-input"
                        class="w-full bg-slate-900 border border-slate-700 text-xs rounded-lg px-2.5 py-1.5 text-slate-200" />
                    </div>
                  </div>

                  <div>
                    <label class="text-[10px] text-slate-400 block mb-1">Description</label>
                    <input
                      type="text"
                      [(ngModel)]="earnDescription"
                      data-testid="earn-description-input"
                      class="w-full bg-slate-900 border border-slate-700 text-xs rounded-lg px-2.5 py-1.5 text-slate-200" />
                  </div>

                  <button
                    (click)="simulateEarn()"
                    data-testid="simulate-earn-btn"
                    class="w-full py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition flex items-center justify-center gap-1.5 shadow-md shadow-emerald-900/30">
                    <span>🚀</span> Credit Points to Ledger
                  </button>
                </div>
              </div>

              <!-- Redeem Simulation Sub-panel -->
              <div class="p-3.5 rounded-lg bg-slate-950/80 border border-slate-800 space-y-3">
                <div class="text-xs font-bold text-amber-400 flex items-center gap-1">
                  <span>-</span> Burn Points (Redemption Action)
                </div>
                <div class="space-y-2">
                  <div class="grid grid-cols-2 gap-2">
                    <div>
                      <label class="text-[10px] text-slate-400 block mb-1">Points to Burn</label>
                      <input
                        type="number"
                        [(ngModel)]="redeemAmount"
                        data-testid="redeem-amount-input"
                        class="w-full bg-slate-900 border border-slate-700 text-xs rounded-lg px-2.5 py-1.5 text-slate-200"
                        min="1" />
                    </div>
                    <div>
                      <label class="text-[10px] text-slate-400 block mb-1">Order Ref ID</label>
                      <input
                        type="text"
                        [(ngModel)]="redeemOrderRef"
                        data-testid="redeem-reference-input"
                        class="w-full bg-slate-900 border border-slate-700 text-xs rounded-lg px-2.5 py-1.5 text-slate-200" />
                    </div>
                  </div>

                  <button
                    (click)="simulateRedeem()"
                    data-testid="simulate-redeem-btn"
                    class="w-full py-2 bg-amber-600 hover:bg-amber-500 text-white rounded-lg text-xs font-semibold transition flex items-center justify-center gap-1.5 shadow-md shadow-amber-900/30">
                    <span>🔥</span> Burn Points with Guard Check
                  </button>
                </div>
              </div>

              <!-- Referral Attribution Simulator -->
              <div class="p-3.5 rounded-lg bg-slate-950/80 border border-slate-800 space-y-3">
                <div class="text-xs font-bold text-cyan-400 flex items-center gap-1">
                  <span>🤝</span> Simulate Referral Attribution
                </div>
                <div class="space-y-2">
                  <div class="grid grid-cols-2 gap-2">
                    <div>
                      <label class="text-[10px] text-slate-400 block mb-1">Referee Contact</label>
                      <input
                        type="text"
                        [(ngModel)]="refereeContactInput"
                        data-testid="referee-contact-input"
                        class="w-full bg-slate-900 border border-slate-700 text-xs rounded-lg px-2.5 py-1.5 text-slate-200" />
                    </div>
                    <div>
                      <label class="text-[10px] text-slate-400 block mb-1">Order Total (SAR)</label>
                      <input
                        type="number"
                        [(ngModel)]="refereeOrderTotalSar"
                        data-testid="referee-order-total-input"
                        class="w-full bg-slate-900 border border-slate-700 text-xs rounded-lg px-2.5 py-1.5 text-slate-200"
                        min="1" />
                    </div>
                  </div>

                  <button
                    (click)="simulateReferralAttribution()"
                    data-testid="simulate-referral-btn"
                    class="w-full py-2 bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg text-xs font-semibold transition flex items-center justify-center gap-1.5 shadow-md shadow-cyan-900/30">
                    <span>🎁</span> Attribute Referee Order
                  </button>
                </div>
              </div>
            </div>

            <!-- Double-Entry Points Ledger Feed -->
            <div class="lg:col-span-2 rounded-xl bg-slate-900 border border-slate-800 p-5 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2">
                    <span>📜</span> Double-Entry Points Ledger Feed
                  </h3>
                  <p class="text-xs text-slate-400">
                    Audit log enforcing: <code class="text-indigo-300 font-mono">BalanceAfter = previous balance + Points</code>
                  </p>
                </div>
                <span class="text-xs text-slate-500">
                  {{ account().recentTransactions.length }} transactions logged
                </span>
              </div>

              <!-- Transaction Table / Feed -->
              <div class="overflow-x-auto rounded-lg border border-slate-800">
                <table class="w-full text-left text-xs text-slate-300" data-testid="ledger-transactions-table">
                  <thead class="bg-slate-950/80 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800">
                    <tr>
                      <th class="px-3.5 py-2.5">Type & Ref</th>
                      <th class="px-3.5 py-2.5">Description</th>
                      <th class="px-3.5 py-2.5 text-right">Points</th>
                      <th class="px-3.5 py-2.5 text-right">Balance After</th>
                      <th class="px-3.5 py-2.5 text-right">Timestamp</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (tx of account().recentTransactions; track tx.entryId) {
                      <tr class="hover:bg-slate-800/40 transition">
                        <td class="px-3.5 py-2.5 whitespace-nowrap">
                          <div class="font-semibold text-slate-200">{{ tx.type }}</div>
                          <div class="text-[10px] font-mono text-slate-400">{{ tx.referenceId }}</div>
                        </td>
                        <td class="px-3.5 py-2.5">
                          <div class="text-slate-300 truncate max-w-xs">{{ tx.description }}</div>
                        </td>
                        <td class="px-3.5 py-2.5 text-right whitespace-nowrap font-bold">
                          @if (tx.points >= 0) {
                            <span class="text-emerald-400" data-testid="tx-points-earn">+{{ tx.points | number }} pts</span>
                          } @else {
                            <span class="text-amber-400" data-testid="tx-points-burn">{{ tx.points | number }} pts</span>
                          }
                        </td>
                        <td class="px-3.5 py-2.5 text-right whitespace-nowrap font-mono font-semibold text-slate-200" data-testid="tx-balance-after">
                          {{ tx.balanceAfter | number }}
                        </td>
                        <td class="px-3.5 py-2.5 text-right whitespace-nowrap text-slate-500 text-[11px]">
                          {{ tx.createdAt | date:'short' }}
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="5" class="px-4 py-8 text-center text-slate-500">
                          No loyalty transactions recorded yet.
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class LoyaltyReferralStudioComponent {
  public readonly translation: TranslationService;

  // Tiers definition for visual gauge
  public readonly tiersList = [
    { key: 'Bronze', name: 'Bronze', range: '0 - 499', multiplier: '1.0x' },
    { key: 'Silver', name: 'Silver', range: '500 - 1,999', multiplier: '1.25x' },
    { key: 'Gold', name: 'Gold', range: '2,000 - 4,999', multiplier: '1.5x' },
    { key: 'VipDiamond', name: 'VIP Diamond', range: '5,000+', multiplier: '2.0x' }
  ];

  // Primary Account State (Seeded: Sultan Al-Dosari, VIP Diamond, 4850 pts, referral code: "SULTAN-VIP")
  public account = signal<LoyaltyAccountSummary>({
    contactId: '11111111-1111-1111-1111-111111111111',
    tenantId: '00000000-0000-0000-0000-000000000001',
    contactName: 'Sultan Al-Dosari',
    contactEmail: 'sultan.dosari@growthksa.com',
    currentPointsBalance: 4850,
    lifetimePointsEarned: 5350,
    currentTier: 'VipDiamond',
    nextTierPointsRemaining: 0,
    referralCode: 'SULTAN-VIP',
    activeTierPerks: getPerksForTier('VipDiamond'),
    recentTransactions: [
      {
        entryId: 'tx-sultan-4',
        tenantId: '00000000-0000-0000-0000-000000000001',
        contactId: '11111111-1111-1111-1111-111111111111',
        type: 'BurnRedemption',
        points: -500,
        balanceAfter: 4850,
        referenceId: 'RED-SA-1004',
        description: 'Redeemed SAR 50 Store Voucher',
        createdAt: new Date(Date.now() - 86400000).toISOString()
      },
      {
        entryId: 'tx-sultan-3',
        tenantId: '00000000-0000-0000-0000-000000000001',
        contactId: '11111111-1111-1111-1111-111111111111',
        type: 'EarnReferral',
        points: 350,
        balanceAfter: 5350,
        referenceId: 'REF-SA-4421',
        description: 'Referral bonus for referee Fahad Al-Harbi',
        createdAt: new Date(Date.now() - 86400000 * 3).toISOString()
      },
      {
        entryId: 'tx-sultan-2',
        tenantId: '00000000-0000-0000-0000-000000000001',
        contactId: '11111111-1111-1111-1111-111111111111',
        type: 'EarnPurchase',
        points: 3000,
        balanceAfter: 5000,
        referenceId: 'ORD-SA-9855',
        description: 'Earned from Premium Executive Package',
        createdAt: new Date(Date.now() - 86400000 * 7).toISOString()
      },
      {
        entryId: 'tx-sultan-1',
        tenantId: '00000000-0000-0000-0000-000000000001',
        contactId: '11111111-1111-1111-1111-111111111111',
        type: 'EarnPurchase',
        points: 2000,
        balanceAfter: 2000,
        referenceId: 'ORD-SA-9801',
        description: 'Earned from Luxury Oud Collection',
        createdAt: new Date(Date.now() - 86400000 * 14).toISOString()
      }
    ]
  });

  // Top KPIs
  public activeMembersCount = signal<number>(12480);
  public referralConversionRate = signal<string>('34.8%');
  public totalRedeemedPointsCounter = signal<number>(1485200);

  public pointsInCirculation = computed(() => {
    const delta = this.account().currentPointsBalance - 4850;
    return 1485200 + delta;
  });

  public redeemedSarValue = computed(() => {
    return Math.round(this.totalRedeemedPointsCounter() / 10);
  });

  // Tier progress percentage (0 - 100)
  public tierProgressPercent = computed(() => {
    const lifetime = this.account().lifetimePointsEarned;
    if (lifetime >= 5000) return 100;
    if (lifetime >= 2000) {
      return Math.min(100, Math.round(((lifetime - 2000) / 3000) * 100));
    }
    if (lifetime >= 500) {
      return Math.min(100, Math.round(((lifetime - 500) / 1500) * 100));
    }
    return Math.min(100, Math.round((lifetime / 500) * 100));
  });

  public referralShareUrl = computed(() => {
    return `https://sgrowing.io/ref/${this.account().referralCode}`;
  });

  // UI state messages
  public statusMessage = signal<string | null>(null);
  public errorMessage = signal<string | null>(null);
  public linkCopied = signal<boolean>(false);

  // Simulation form inputs
  public earnType = signal<LoyaltyTransactionType>('EarnPurchase');
  public earnAmount = signal<number>(250);
  public earnReference = signal<string>('ORD-LIVE-102');
  public earnDescription = signal<string>('Ramadan Special Order points');

  public redeemAmount = signal<number>(200);
  public redeemOrderRef = signal<string>('ORD-BURN-77');

  public refereeContactInput = signal<string>('Fawaz Al-Zahrani');
  public refereeOrderTotalSar = signal<number>(450);

  constructor(@Optional() translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }
  }

  public copyReferralLink(): void {
    const link = this.referralShareUrl();
    if (navigator?.clipboard?.writeText) {
      navigator.clipboard.writeText(link).catch(() => {});
    }
    this.linkCopied.set(true);
    setTimeout(() => this.linkCopied.set(false), 2500);
  }

  public simulateEarn(): void {
    this.clearMessages();
    const points = Number(this.earnAmount());
    if (points <= 0) {
      this.errorMessage.set('Points to earn must be greater than zero.');
      return;
    }

    const current = this.account();
    const previousBalance = current.currentPointsBalance;
    const newBalance = previousBalance + points;
    const newLifetime = current.lifetimePointsEarned + points;
    const newTier = calculateTier(newLifetime);
    const nextRemaining = calculateNextTierPointsRemaining(newLifetime);
    const perks = getPerksForTier(newTier);

    const newTx: LoyaltyPointLedgerEntry = {
      entryId: `tx-${Date.now()}`,
      tenantId: current.tenantId,
      contactId: current.contactId,
      type: this.earnType(),
      points,
      balanceAfter: newBalance,
      referenceId: this.earnReference() || `REF-${Date.now()}`,
      description: this.earnDescription() || 'Earned loyalty points',
      createdAt: new Date().toISOString()
    };

    this.account.set({
      ...current,
      currentPointsBalance: newBalance,
      lifetimePointsEarned: newLifetime,
      currentTier: newTier,
      nextTierPointsRemaining: nextRemaining,
      activeTierPerks: perks,
      recentTransactions: [newTx, ...current.recentTransactions]
    });

    this.statusMessage.set(
      `Successfully credited ${points} points! New balance: ${newBalance} pts (Double-entry verified: ${previousBalance} + ${points} = ${newBalance}).`
    );
  }

  public simulateRedeem(): void {
    this.clearMessages();
    const pointsToRedeem = Number(this.redeemAmount());
    if (pointsToRedeem <= 0) {
      this.errorMessage.set('Points to redeem must be greater than zero.');
      return;
    }

    const current = this.account();
    // Balance check guard
    if (pointsToRedeem > current.currentPointsBalance) {
      this.errorMessage.set(
        `Insufficient points balance! Requested: ${pointsToRedeem} pts, Available: ${current.currentPointsBalance} pts.`
      );
      return;
    }

    const previousBalance = current.currentPointsBalance;
    const pointsBurn = -pointsToRedeem;
    const newBalance = previousBalance + pointsBurn;

    const newTx: LoyaltyPointLedgerEntry = {
      entryId: `tx-${Date.now()}`,
      tenantId: current.tenantId,
      contactId: current.contactId,
      type: 'BurnRedemption',
      points: pointsBurn,
      balanceAfter: newBalance,
      referenceId: this.redeemOrderRef() || `ORD-BURN-${Date.now()}`,
      description: `Redeemed ${pointsToRedeem} points for order discount`,
      createdAt: new Date().toISOString()
    };

    this.account.set({
      ...current,
      currentPointsBalance: newBalance,
      recentTransactions: [newTx, ...current.recentTransactions]
    });

    this.totalRedeemedPointsCounter.update(c => c + pointsToRedeem);

    this.statusMessage.set(
      `Redeemed ${pointsToRedeem} points! New balance: ${newBalance} pts (Double-entry verified: ${previousBalance} + (${pointsBurn}) = ${newBalance}).`
    );
  }

  public simulateReferralAttribution(): void {
    this.clearMessages();
    const referee = this.refereeContactInput().trim();
    if (!referee) {
      this.errorMessage.set('Please provide referee contact information.');
      return;
    }

    const orderSar = Number(this.refereeOrderTotalSar());
    const bonusFromSpend = Math.round(orderSar * 0.1);
    const referrerAward = 250 + Math.max(0, bonusFromSpend);

    const current = this.account();
    const previousBalance = current.currentPointsBalance;
    const newBalance = previousBalance + referrerAward;
    const newLifetime = current.lifetimePointsEarned + referrerAward;
    const newTier = calculateTier(newLifetime);
    const nextRemaining = calculateNextTierPointsRemaining(newLifetime);

    const newTx: LoyaltyPointLedgerEntry = {
      entryId: `tx-ref-${Date.now()}`,
      tenantId: current.tenantId,
      contactId: current.contactId,
      type: 'EarnReferral',
      points: referrerAward,
      balanceAfter: newBalance,
      referenceId: `REF-${referee.replace(/\s+/g, '').slice(0, 6).toUpperCase()}`,
      description: `Referral award for referee ${referee} (Order SAR ${orderSar})`,
      createdAt: new Date().toISOString()
    };

    this.account.set({
      ...current,
      currentPointsBalance: newBalance,
      lifetimePointsEarned: newLifetime,
      currentTier: newTier,
      nextTierPointsRemaining: nextRemaining,
      activeTierPerks: getPerksForTier(newTier),
      recentTransactions: [newTx, ...current.recentTransactions]
    });

    this.statusMessage.set(
      `Referral converted! Credited +${referrerAward} points to ${current.contactName}. Referee received 15% discount voucher.`
    );
  }

  public clearStatus(): void {
    this.statusMessage.set(null);
  }

  public clearError(): void {
    this.errorMessage.set(null);
  }

  private clearMessages(): void {
    this.statusMessage.set(null);
    this.errorMessage.set(null);
  }

  public tierDisplayName(tier: LoyaltyMembershipTier): string {
    switch (tier) {
      case 'VipDiamond': return 'VIP Diamond';
      case 'Gold': return 'Gold';
      case 'Silver': return 'Silver';
      case 'Bronze': default: return 'Bronze';
    }
  }

  public tierBadgeClass(tier: LoyaltyMembershipTier): string {
    switch (tier) {
      case 'VipDiamond':
        return 'bg-purple-500/20 text-purple-300 border-purple-500/40 shadow-sm shadow-purple-900/30';
      case 'Gold':
        return 'bg-amber-500/20 text-amber-300 border-amber-500/40 shadow-sm shadow-amber-900/30';
      case 'Silver':
        return 'bg-slate-400/20 text-slate-200 border-slate-400/40';
      case 'Bronze':
      default:
        return 'bg-amber-700/20 text-amber-500 border-amber-700/40';
    }
  }

  public getTierStepClass(stepKey: string, currentTier: LoyaltyMembershipTier): string {
    const tierOrder: Record<string, number> = {
      Bronze: 0,
      Silver: 1,
      Gold: 2,
      VipDiamond: 3
    };

    const currentRank = tierOrder[currentTier] ?? 0;
    const stepRank = tierOrder[stepKey] ?? 0;

    if (stepRank === currentRank) {
      return 'bg-indigo-600/30 border-indigo-500 text-white shadow-md shadow-indigo-950/50';
    } else if (stepRank < currentRank) {
      return 'bg-slate-900 border-emerald-500/50 text-emerald-400';
    } else {
      return 'bg-slate-950/60 border-slate-800 text-slate-500';
    }
  }
}
