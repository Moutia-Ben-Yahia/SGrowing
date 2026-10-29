import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface CallCenterOrderUi {
  id: string;
  orderNumber: string;
  customerName: string;
  phone: string;
  wilaya: string;
  totalTnd: number;
  productCost: number;
  shippingFee: number;
  isLocked: boolean;
  lockedByAgentName: string | null;
  lockExpiresInSec: number;
  callStatus: 'PendingConfirmation' | 'Confirmed' | 'Canceled' | 'CallLater';
}

@Component({
  selector: 'app-call-center-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <!-- Sidebar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-amber-500 to-rose-500 flex items-center justify-center font-bold text-white shadow-lg shadow-amber-500/20">
              📞
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-amber-400 font-medium">Call Center Anti-Collision</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <div class="space-y-1">
              <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📥 Omnichannel Inbox</span>
              </a>
              <a routerLink="/commerce/callcenter" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-amber-500/20 text-amber-300 border border-amber-500/30 font-medium">
                <span>📞 Call Center & COD Ledger</span>
              </a>
              <a routerLink="/commerce/warehouses" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span>🏬 Multi-Warehouse Allocation</span>
              </a>
              <a routerLink="/organizations/margin" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span>🏢 Organization & Margin</span>
              </a>
              <a routerLink="/accounts/snapshots" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-sky-400 hover:bg-sky-500/10 border border-sky-500/20 transition">
                <span>🏢 Agency Snapshots & Rebilling</span>
              </a>
            </div>
          </div>

          <!-- Operator Info -->
          <div class="p-3 bg-slate-900 border border-slate-800 rounded-xl space-y-2">
            <div class="flex items-center justify-between text-xs">
              <span class="text-slate-400">Current Operator:</span>
              <span class="font-semibold text-amber-400">{{ currentAgentName() }}</span>
            </div>
            <div class="flex items-center justify-between text-xs">
              <span class="text-slate-400">Station Status:</span>
              <span class="flex items-center gap-1.5 text-emerald-400 font-medium">
                <span class="h-2 w-2 rounded-full bg-emerald-500 animate-pulse"></span> Ready to Dial
              </span>
            </div>
          </div>
        </div>

        <div class="p-3 bg-amber-950/20 border border-amber-800/40 rounded-xl text-xs space-y-1">
          <div class="text-amber-400 font-semibold flex items-center gap-1.5">
            <span>🛡️ Sliding Anti-Collision Lock</span>
          </div>
          <p class="text-slate-400 text-[11px] leading-relaxed">
            Orders are locked for 180s on pickup. Other operators attempting to dial receive instant conflict alerts.
          </p>
        </div>
      </aside>

      <!-- Main Content Area -->
      <main class="flex-1 flex flex-col overflow-y-auto bg-slate-950">
        <!-- Top Header Bar -->
        <header class="border-b border-slate-800 bg-slate-900/50 px-8 py-5 flex items-center justify-between backdrop-blur sticky top-0 z-10">
          <div>
            <h2 class="text-lg font-bold text-white flex items-center gap-2.5">
              <span>Tele-Operator Dialing Queue & COD Net Margin Ledger</span>
              <span class="text-xs px-2.5 py-0.5 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20">
                Live Sliding-Window Anti-Collision
              </span>
            </h2>
            <p class="text-xs text-slate-400 mt-0.5">
              Prevent duplicate operator calls, resolve order conflicts, and inspect true net profit margins before delivery confirmation.
            </p>
          </div>

          <!-- Tab Selector -->
          <div class="flex items-center gap-2 bg-slate-900 border border-slate-800 p-1 rounded-xl">
            <button
              (click)="activeTab.set('queue')"
              [class]="activeTab() === 'queue' ? 'bg-amber-600 text-white font-semibold' : 'text-slate-400 hover:text-white'"
              class="px-4 py-1.5 rounded-lg text-xs transition">
              📋 Live Order Dialing Queue
            </button>
            <button
              (click)="activeTab.set('calculator')"
              [class]="activeTab() === 'calculator' ? 'bg-amber-600 text-white font-semibold' : 'text-slate-400 hover:text-white'"
              class="px-4 py-1.5 rounded-lg text-xs transition">
              🧮 True COD Margin Simulator
            </button>
          </div>
        </header>

        <!-- Collision Alert Toast -->
        @if (collisionAlert()) {
          <div class="mx-8 mt-4 p-4 bg-rose-950/40 border border-rose-800/80 rounded-xl flex items-center justify-between text-rose-200 text-xs animate-shake">
            <div class="flex items-center gap-3">
              <span class="text-lg">⚠️</span>
              <div>
                <span class="font-bold">Order Lock Collision Detected:</span> {{ collisionAlert() }}
              </div>
            </div>
            <button (click)="collisionAlert.set(null)" class="text-slate-400 hover:text-white text-xs">Dismiss</button>
          </div>
        }

        <!-- Tab 1: Live Dialing Queue -->
        @if (activeTab() === 'queue') {
          <div class="p-8 space-y-6">
            <!-- Telemetry Cards -->
            <div class="grid grid-cols-4 gap-4">
              <div class="p-4 bg-slate-900/60 border border-slate-800 rounded-xl">
                <span class="text-xs text-slate-400">Pending Orders</span>
                <div class="text-2xl font-bold text-white mt-1">{{ orders().length }}</div>
                <span class="text-[11px] text-amber-400">Awaiting phone confirmation</span>
              </div>
              <div class="p-4 bg-slate-900/60 border border-slate-800 rounded-xl">
                <span class="text-xs text-slate-400">Currently Locked</span>
                <div class="text-2xl font-bold text-amber-400 mt-1">{{ lockedCount() }}</div>
                <span class="text-[11px] text-slate-400">Under operator call</span>
              </div>
              <div class="p-4 bg-slate-900/60 border border-slate-800 rounded-xl">
                <span class="text-xs text-slate-400">Lock Window (TTL)</span>
                <div class="text-2xl font-bold text-indigo-400 mt-1">180s</div>
                <span class="text-[11px] text-slate-400">Auto-release on disconnect</span>
              </div>
              <div class="p-4 bg-slate-900/60 border border-slate-800 rounded-xl">
                <span class="text-xs text-slate-400">Average Confirmation Rate</span>
                <div class="text-2xl font-bold text-emerald-400 mt-1">78.4%</div>
                <span class="text-[11px] text-emerald-400">COD phone verified</span>
              </div>
            </div>

            <!-- Orders Table -->
            <div class="bg-slate-900/50 border border-slate-800 rounded-xl overflow-hidden">
              <div class="px-6 py-4 border-b border-slate-800 flex items-center justify-between">
                <h3 class="text-xs font-semibold text-white uppercase tracking-wider">Unconfirmed Orders Ready for Confirmation</h3>
                <span class="text-xs text-slate-400">Click "Dial & Lock" to open order</span>
              </div>

              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-900/90 text-slate-400 border-b border-slate-800">
                    <tr>
                      <th class="px-4 py-3">Order #</th>
                      <th class="px-4 py-3">Customer</th>
                      <th class="px-4 py-3">Phone</th>
                      <th class="px-4 py-3">Destination</th>
                      <th class="px-4 py-3">Total (TND)</th>
                      <th class="px-4 py-3">Lock Status</th>
                      <th class="px-4 py-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (order of orders(); track order.id) {
                      <tr [class.bg-amber-950/10]="order.isLocked" class="hover:bg-slate-800/30 transition">
                        <td class="px-4 py-3.5 font-mono text-amber-400 font-semibold">{{ order.orderNumber }}</td>
                        <td class="px-4 py-3.5 font-medium text-white">{{ order.customerName }}</td>
                        <td class="px-4 py-3.5 text-slate-300">{{ order.phone }}</td>
                        <td class="px-4 py-3.5 text-slate-400">{{ order.wilaya }}</td>
                        <td class="px-4 py-3.5 font-semibold text-emerald-400">{{ order.totalTnd }} TND</td>
                        <td class="px-4 py-3.5">
                          @if (order.isLocked) {
                            @if (order.lockedByAgentName === currentAgentName()) {
                              <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-emerald-500/10 text-emerald-300 border border-emerald-500/30 text-[11px] font-medium">
                                <span class="h-1.5 w-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                                Locked by You ({{ order.lockExpiresInSec }}s)
                              </span>
                            } @else {
                              <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-rose-500/10 text-rose-300 border border-rose-500/30 text-[11px] font-medium">
                                <span class="h-1.5 w-1.5 rounded-full bg-rose-400"></span>
                                Locked by {{ order.lockedByAgentName }} ({{ order.lockExpiresInSec }}s)
                              </span>
                            }
                          } @else {
                            <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-slate-800 text-slate-400 text-[11px]">
                              Available
                            </span>
                          }
                        </td>
                        <td class="px-4 py-3.5 text-right space-x-2">
                          @if (!order.isLocked) {
                            <button
                              (click)="acquireLock(order)"
                              class="px-3 py-1.5 bg-gradient-to-r from-amber-600 to-rose-600 hover:from-amber-500 hover:to-rose-500 text-white rounded-lg font-semibold text-xs transition">
                              📞 Dial & Lock
                            </button>
                          } @else if (order.lockedByAgentName === currentAgentName()) {
                            <button
                              (click)="renewLock(order)"
                              class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-amber-300 border border-amber-500/30 rounded-lg text-xs transition">
                              🔄 Renew (+180s)
                            </button>
                            <button
                              (click)="releaseLock(order)"
                              class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs transition">
                              Release
                            </button>
                            <button
                              (click)="confirmOrder(order)"
                              class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold rounded-lg text-xs transition">
                              ✅ Confirm
                            </button>
                          } @else {
                            <button
                              (click)="attemptCollision(order)"
                              class="px-3 py-1 bg-slate-800 text-slate-500 cursor-not-allowed rounded-lg text-xs">
                              🔒 In Dialing
                            </button>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        }

        <!-- Tab 2: True COD Margin Simulator -->
        @if (activeTab() === 'calculator') {
          <div class="p-8 max-w-4xl space-y-6">
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-6 space-y-5">
              <div>
                <h3 class="text-sm font-semibold text-white">Cash-on-Delivery Net Margin Ledger Formula</h3>
                <p class="text-xs text-slate-400 mt-1">
                  Accurately calculates real cash-in-hand net profit accounting for return rate penalties:
                  <code class="text-amber-400 font-mono">NetMargin = OrderTotal - (COGS + Shipping + CallCenter + (ReturnRate% * ReturnPenaltyFee))</code>
                </p>
              </div>

              <!-- Input Grid -->
              <div class="grid grid-cols-3 gap-4 pt-2">
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1.5">Collected Order Total (TND)</label>
                  <input
                    type="number"
                    [(ngModel)]="simOrderTotal"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1.5">Product Cost / COGS (TND)</label>
                  <input
                    type="number"
                    [(ngModel)]="simProductCost"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1.5">Courier One-Way Shipping (TND)</label>
                  <input
                    type="number"
                    [(ngModel)]="simShippingFee"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1.5">Call Center Commission (TND)</label>
                  <input
                    type="number"
                    [(ngModel)]="simCallCenterFee"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1.5">Historical Return Rate (%)</label>
                  <input
                    type="number"
                    [(ngModel)]="simReturnRate"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1.5">Two-Way Return Penalty Fee (TND)</label>
                  <input
                    type="number"
                    [(ngModel)]="simReturnPenalty"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500" />
                </div>
              </div>

              <!-- Computed Breakdown -->
              <div class="pt-4 border-t border-slate-800 grid grid-cols-2 gap-6">
                <div class="space-y-3 bg-slate-950/70 p-4 rounded-xl border border-slate-800/80">
                  <h4 class="text-xs font-semibold text-slate-300">Cost Structure Breakdown</h4>
                  <div class="flex justify-between text-xs">
                    <span class="text-slate-400">Cost of Goods Sold (COGS):</span>
                    <span class="text-white font-medium">{{ simProductCost }} TND</span>
                  </div>
                  <div class="flex justify-between text-xs">
                    <span class="text-slate-400">One-Way Courier Freight:</span>
                    <span class="text-white font-medium">{{ simShippingFee }} TND</span>
                  </div>
                  <div class="flex justify-between text-xs">
                    <span class="text-slate-400">Call Center Confirmation Fee:</span>
                    <span class="text-white font-medium">{{ simCallCenterFee }} TND</span>
                  </div>
                  <div class="flex justify-between text-xs">
                    <span class="text-slate-400">Statistical Return Risk Cost:</span>
                    <span class="text-rose-400 font-medium">+{{ computedReturnRiskCost() }} TND</span>
                  </div>
                  <div class="flex justify-between text-xs pt-2 border-t border-slate-800 font-semibold">
                    <span class="text-slate-300">Total Operational Cost:</span>
                    <span class="text-amber-400">{{ computedTotalCosts() }} TND</span>
                  </div>
                </div>

                <div class="flex flex-col justify-between bg-gradient-to-br from-slate-900 to-slate-950 p-5 rounded-xl border border-amber-500/30">
                  <div>
                    <span class="text-xs font-semibold text-amber-400 uppercase tracking-wider">Derived Cash Net Margin</span>
                    <div class="text-3xl font-extrabold text-white mt-2">
                      {{ computedNetMargin() }} <span class="text-lg font-normal text-slate-400">TND</span>
                    </div>
                    <div class="text-sm font-semibold mt-1" [class]="computedNetMargin() > 0 ? 'text-emerald-400' : 'text-rose-400'">
                      {{ computedMarginPercent() }}% Net Margin
                    </div>
                  </div>

                  <div class="pt-4">
                    @if (computedMarginPercent() >= 30) {
                      <div class="px-3 py-2 rounded-lg bg-emerald-500/10 border border-emerald-500/30 text-emerald-300 text-xs">
                        🚀 High Margin: Order is highly viable for expedited fulfillment.
                      </div>
                    } @else if (computedMarginPercent() > 0) {
                      <div class="px-3 py-2 rounded-lg bg-amber-500/10 border border-amber-500/30 text-amber-300 text-xs">
                        ⚠️ Moderate Margin: Verify delivery address to avoid return penalty risk.
                      </div>
                    } @else {
                      <div class="px-3 py-2 rounded-lg bg-rose-500/10 border border-rose-500/30 text-rose-300 text-xs">
                        🛑 Loss Warning: Return risk exceeds projected profit. Re-negotiate pricing or upsell.
                      </div>
                    }
                  </div>
                </div>
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class CallCenterCockpitComponent implements OnInit {
  activeTab = signal<'queue' | 'calculator'>('queue');
  currentAgentName = signal('Operator Sarah');
  collisionAlert = signal<string | null>(null);

  simOrderTotal = 165.0;
  simProductCost = 55.0;
  simShippingFee = 11.5;
  simCallCenterFee = 3.5;
  simReturnRate = 18.0; // 18%
  simReturnPenalty = 16.0; // 16 TND return handling

  orders = signal<CallCenterOrderUi[]>([
    {
      id: 'ord-101',
      orderNumber: 'ORD-202610-8801',
      customerName: 'Moncef Ben Salem',
      phone: '+216 98 445 566',
      wilaya: 'Sousse',
      totalTnd: 185.0,
      productCost: 65.0,
      shippingFee: 12.0,
      isLocked: false,
      lockedByAgentName: null,
      lockExpiresInSec: 0,
      callStatus: 'PendingConfirmation'
    },
    {
      id: 'ord-102',
      orderNumber: 'ORD-202610-8802',
      customerName: 'Mariem Jaziri',
      phone: '+216 22 113 344',
      wilaya: 'Nabeul',
      totalTnd: 120.0,
      productCost: 40.0,
      shippingFee: 10.0,
      isLocked: true,
      lockedByAgentName: 'Operator Ahmed',
      lockExpiresInSec: 142,
      callStatus: 'PendingConfirmation'
    },
    {
      id: 'ord-103',
      orderNumber: 'ORD-202610-8803',
      customerName: 'Hichem Khelifi',
      phone: '+216 50 889 900',
      wilaya: 'Tunis',
      totalTnd: 240.0,
      productCost: 85.0,
      shippingFee: 9.0,
      isLocked: false,
      lockedByAgentName: null,
      lockExpiresInSec: 0,
      callStatus: 'PendingConfirmation'
    },
    {
      id: 'ord-104',
      orderNumber: 'ORD-202610-8804',
      customerName: 'Samira Dridi',
      phone: '+216 29 778 899',
      wilaya: 'Sfax',
      totalTnd: 95.0,
      productCost: 35.0,
      shippingFee: 13.0,
      isLocked: false,
      lockedByAgentName: null,
      lockExpiresInSec: 0,
      callStatus: 'PendingConfirmation'
    }
  ]);

  lockedCount = computed(() => this.orders().filter(o => o.isLocked).length);

  computedReturnRiskCost = computed(() => {
    return Math.round((this.simReturnRate / 100.0) * this.simReturnPenalty * 100) / 100;
  });

  computedTotalCosts = computed(() => {
    return Math.round((this.simProductCost + this.simShippingFee + this.simCallCenterFee + this.computedReturnRiskCost()) * 100) / 100;
  });

  computedNetMargin = computed(() => {
    return Math.round((this.simOrderTotal - this.computedTotalCosts()) * 100) / 100;
  });

  computedMarginPercent = computed(() => {
    if (this.simOrderTotal <= 0) return 0;
    return Math.round((this.computedNetMargin() / this.simOrderTotal) * 1000) / 10;
  });

  ngOnInit() {
    // Countdown timer interval for active locks
    setInterval(() => {
      this.orders.update(list => {
        return list.map(o => {
          if (o.isLocked && o.lockExpiresInSec > 0) {
            const nextSec = o.lockExpiresInSec - 1;
            if (nextSec === 0) {
              return { ...o, isLocked: false, lockedByAgentName: null, lockExpiresInSec: 0 };
            }
            return { ...o, lockExpiresInSec: nextSec };
          }
          return o;
        });
      });
    }, 1000);
  }

  acquireLock(order: CallCenterOrderUi) {
    this.collisionAlert.set(null);
    order.isLocked = true;
    order.lockedByAgentName = this.currentAgentName();
    order.lockExpiresInSec = 180;
    this.orders.update(l => [...l]);
  }

  renewLock(order: CallCenterOrderUi) {
    order.lockExpiresInSec = 180;
    this.orders.update(l => [...l]);
  }

  releaseLock(order: CallCenterOrderUi) {
    order.isLocked = false;
    order.lockedByAgentName = null;
    order.lockExpiresInSec = 0;
    this.orders.update(l => [...l]);
  }

  confirmOrder(order: CallCenterOrderUi) {
    order.callStatus = 'Confirmed';
    this.releaseLock(order);
    this.orders.update(l => l.filter(o => o.id !== order.id));
  }

  attemptCollision(order: CallCenterOrderUi) {
    this.collisionAlert.set(`Order ${order.orderNumber} is actively locked by ${order.lockedByAgentName}. Call collision avoided!`);
  }
}
