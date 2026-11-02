import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CartRescueView {
  rescueId: string;
  cartId: string;
  email: string;
  value: number;
  status: 'Staged' | 'DispatchedWhatsApp' | 'Recovered' | 'Expired';
  incentive: string;
  minutesAgo: number;
}

@Component({
  selector: 'app-cart-rescue-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="cart-rescue-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-orange-400">
            <span class="inline-block w-2 h-2 rounded-full bg-orange-400 animate-pulse"></span>
            Cart Abandonment & Dynamic Incentive Escrow • Horizon 63
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Margin-Preserving Cart Rescue
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Dynamic incentive bundling (gift items, priority shipping) over margin-destroying discounts with WhatsApp conversational nudges.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="evaluateCartRescue()"
            data-testid="evaluate-cart-btn"
            class="px-4 py-2 bg-orange-600 hover:bg-orange-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-orange-600/20"
          >
            <span>🛒</span> Evaluate Cart Elasticity
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Recoverable Pipeline GMV</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-recoverable-gmv">
            \${{ totalGmv() | number:'1.0-0' }}
          </div>
          <div class="text-orange-400 text-xs mt-1">Active Abandoned Carts</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Rescues</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-active-rescues">
            {{ activeCount() }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Staged & Dispatched Nudges</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Preserved Gross Margin</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-avg-margin">
            58.4%
          </div>
          <div class="text-slate-400 text-xs mt-1">Zero Blind Discount Bleed</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Recovery Conversion Rate</div>
          <div class="text-2xl font-bold text-sky-400 mt-1">36.8%</div>
          <div class="text-emerald-400 text-xs mt-1">3.4x vs Standard Static Email</div>
        </div>
      </div>

      <!-- Main Stage: Abandoned Cart Pipelines & WhatsApp Nudge Console -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Carts Table (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Active Abandoned Carts</h2>
            <span class="text-xs text-slate-400 font-mono">{{ rescues().length }} Carts</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (cart of rescues(); track cart.rescueId) {
              <div
                (click)="selectRescue(cart)"
                [class.bg-slate-700/40]="selectedRescue()?.rescueId === cart.rescueId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'cart-row-' + cart.rescueId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">\${{ cart.value | number:'1.2-2' }}</span>
                    <span
                      [class]="cart.status === 'Recovered' ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-orange-900/60 text-orange-300 border-orange-700/60'"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                    >
                      {{ cart.status }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Customer: <span class="text-white">{{ cart.email }}</span> • Cart ID: <span class="font-mono text-slate-300">{{ cart.cartId }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Incentive: <strong class="text-orange-300">{{ cart.incentive }}</strong>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">Abandoned</div>
                  <div class="text-sm font-bold text-slate-300 font-mono">{{ cart.minutesAgo }}m ago</div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Nudge Dispatcher & Incentive Preview Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Conversational WhatsApp Nudge</h3>
              <p class="text-xs text-slate-400">Margin-Preserving Rescue Engine</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-orange-950 text-orange-300 border border-orange-800">
              Live Incentive
            </span>
          </div>

          @if (selectedRescue(); as active) {
            <div class="space-y-4 text-xs">
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2">
                <div class="text-slate-400 font-bold">Selected Incentive Strategy:</div>
                <div class="text-white font-semibold">{{ active.incentive }}</div>
                <p class="text-slate-400 text-[11px] leading-relaxed">
                  Calculated based on item price elasticity. Protects 100% of product GMV by substituting non-cash high-perceived-value bundles.
                </p>
              </div>

              <button
                (click)="dispatchNudge(active)"
                data-testid="dispatch-nudge-btn"
                class="w-full py-2 bg-orange-600 hover:bg-orange-500 text-white font-semibold rounded-lg text-xs shadow transition-colors"
              >
                Dispatch Conversational WhatsApp Nudge
              </button>

              @if (nudgePreview(); as prev) {
                <div class="p-3 bg-slate-900 rounded-xl border border-orange-900/60 text-orange-200 text-xs leading-relaxed" data-testid="nudge-preview-box">
                  {{ prev }}
                </div>
              }
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class CartRescueCockpitComponent {
  readonly rescues = signal<CartRescueView[]>([
    {
      rescueId: 'rescue_cart_8801',
      cartId: 'cart_nordic_apparel_99',
      email: 'elena@nordicstyle.se',
      value: 340.0,
      status: 'DispatchedWhatsApp',
      incentive: 'Free Expedited Priority Courier Shipping',
      minutesAgo: 45
    },
    {
      rescueId: 'rescue_cart_8802',
      cartId: 'cart_tech_gadgets_44',
      email: 'marcus@berlintech.de',
      value: 890.0,
      status: 'Recovered',
      incentive: 'High-Margin Premium Carry Case Bundle',
      minutesAgo: 120
    }
  ]);

  readonly selectedRescue = signal<CartRescueView | null>(this.rescues()[0]);
  readonly nudgePreview = signal<string | null>(null);

  readonly totalGmv = computed(() =>
    this.rescues().reduce((acc, c) => acc + c.value, 0)
  );

  readonly activeCount = computed(() =>
    this.rescues().filter(c => c.status !== 'Expired').length
  );

  selectRescue(cart: CartRescueView): void {
    this.selectedRescue.set(cart);
  }

  evaluateCartRescue(): void {
    const newCart: CartRescueView = {
      rescueId: `rescue_${Date.now()}`,
      cartId: 'cart_enterprise_bundle_12',
      email: 'sarah@globalcorp.com',
      value: 650.0,
      status: 'Staged',
      incentive: 'Complimentary High-Margin Accessory Gift',
      minutesAgo: 15
    };

    this.rescues.update(list => [newCart, ...list]);
    this.selectedRescue.set(newCart);
  }

  dispatchNudge(cart: CartRescueView): void {
    this.nudgePreview.set(
      `✓ WhatsApp Dispatched to ${cart.email}: "Hi! We saved your cart ($${cart.value.toFixed(2)}). Complete in the next 2 hours and unlock: ${cart.incentive}."`
    );
  }
}
