import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface CodOrderUi {
  id: string;
  orderNumber: string;
  customerName: string;
  phone: string;
  address: string;
  governorate: string;
  amount: number;
  currency: string;
  status: 'Pending' | 'Confirmed' | 'Dispatched' | 'Delivered' | 'Cancelled';
  riskScore: number;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Fraudulent';
  lockHolder?: string;
  isLockedByMe: boolean;
}

export interface WmsStockUi {
  sku: string;
  name: string;
  physical: number;
  reserved: number;
  committed: number;
  inTransit: number;
  quarantine: number;
  damaged: number;
  available: number;
}

export interface SdrTaskUi {
  id: string;
  leadName: string;
  phone: string;
  company: string;
  taskType: 'PhoneCall' | 'LinkedIn';
  instruction: string;
  isCompleted: boolean;
}

export interface RmaRequestUi {
  rmaNumber: string;
  orderNumber: string;
  phone: string;
  reason: string;
  status: 'Requested' | 'Approved' | 'ReverseDispatched';
}

@Component({
  selector: 'app-operations-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Sidebar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-emerald-500 to-teal-400 flex items-center justify-center font-bold text-white shadow-lg shadow-emerald-500/20">
              ⚡
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-slate-400">Commerce & Logistics OS</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Operations Studio</span>
            <button
              (click)="activeTab.set('confirmation')"
              [class.bg-slate-800]="activeTab() === 'confirmation'"
              [class.text-emerald-400]="activeTab() === 'confirmation'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">📞 Confirmation Workbench</span>
              <span class="bg-emerald-500/20 text-emerald-400 text-[10px] px-1.5 py-0.5 rounded-full font-bold">{{ pendingCount() }}</span>
            </button>
            <button
              (click)="activeTab.set('logistics')"
              [class.bg-slate-800]="activeTab() === 'logistics'"
              [class.text-cyan-400]="activeTab() === 'logistics'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🚚 Courier & Manifests</span>
            </button>
            <button
              (click)="activeTab.set('wms')"
              [class.bg-slate-800]="activeTab() === 'wms'"
              [class.text-amber-400]="activeTab() === 'wms'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">📦 Defensive WMS Ledger</span>
            </button>
            <button
              (click)="activeTab.set('cadences')"
              [class.bg-slate-800]="activeTab() === 'cadences'"
              [class.text-purple-400]="activeTab() === 'cadences'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🎯 SDR Cadences</span>
            </button>
            <button
              (click)="activeTab.set('rma')"
              [class.bg-slate-800]="activeTab() === 'rma'"
              [class.text-rose-400]="activeTab() === 'rma'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🔄 Customer 360 & RMA</span>
            </button>
            <button
              (click)="activeTab.set('remittance')"
              [class.bg-slate-800]="activeTab() === 'remittance'"
              [class.text-emerald-400]="activeTab() === 'remittance'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">💰 Courier Cash Clearing</span>
              <span class="bg-rose-500/20 text-rose-400 text-[10px] px-1.5 py-0.5 rounded-full font-bold">1 Alert</span>
            </button>
            <button
              (click)="activeTab.set('conversions')"
              [class.bg-slate-800]="activeTab() === 'conversions'"
              [class.text-sky-400]="activeTab() === 'conversions'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">📡 Ad Tracking & CAPI Mesh</span>
            </button>
            <button
              (click)="activeTab.set('scanner')"
              [class.bg-slate-800]="activeTab() === 'scanner'"
              [class.text-amber-400]="activeTab() === 'scanner'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">📱 Barcode Fulfillment Desk</span>
              <span class="bg-amber-500/20 text-amber-300 text-[10px] px-1.5 py-0.5 rounded-full font-bold">Live</span>
            </button>
            <button
              (click)="activeTab.set('dialer')"
              [class.bg-slate-800]="activeTab() === 'dialer'"
              [class.text-emerald-400]="activeTab() === 'dialer'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🤖 Voice AI Confirmation</span>
              <span class="bg-emerald-500/20 text-emerald-300 text-[10px] px-1.5 py-0.5 rounded-full font-bold">IVR</span>
            </button>
            <button
              (click)="activeTab.set('reputation')"
              [class.bg-slate-800]="activeTab() === 'reputation'"
              [class.text-indigo-400]="activeTab() === 'reputation'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🛡️ Customer Reputation</span>
            </button>
            <button
              (click)="activeTab.set('routing')"
              [class.bg-slate-800]="activeTab() === 'routing'"
              [class.text-cyan-400]="activeTab() === 'routing'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🎯 Smart Carrier Routing</span>
              <span class="bg-cyan-500/20 text-cyan-300 text-[10px] px-1.5 py-0.5 rounded-full font-bold">AI</span>
            </button>
            <button
              (click)="activeTab.set('livecommerce')"
              [class.bg-slate-800]="activeTab() === 'livecommerce'"
              [class.text-rose-400]="activeTab() === 'livecommerce'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🔴 Live Social Commerce</span>
              <span class="bg-rose-500/20 text-rose-300 text-[10px] px-1.5 py-0.5 rounded-full font-bold">Live</span>
            </button>
          </div>
        </div>

        <div class="border-t border-slate-800 pt-3">
          <a
            routerLink="/"
            class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs text-slate-400 hover:bg-slate-800 transition">
            <span class="flex items-center gap-2">📥 Back to Inbox</span>
          </a>
        </div>
      </aside>

      <!-- Main Panel -->
      <main class="flex-1 flex flex-col h-full bg-slate-950 overflow-y-auto">
        <!-- Top Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/50 px-6 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <h2 class="text-base font-semibold text-white">
              @if (activeTab() === 'confirmation') { COD Confirmation & Anti-Collision Workbench }
              @if (activeTab() === 'logistics') { Multi-Courier Logistics & Manifest Hub }
              @if (activeTab() === 'wms') { Defensive Multi-State WMS Stock Ledger }
              @if (activeTab() === 'cadences') { Sales Outbound Cadences & Stop-On-Reply SDR }
              @if (activeTab() === 'rma') { Customer 360 Identity Graph & Self-Service RMA }
              @if (activeTab() === 'remittance') { Courier Cash Remittance & Financial Clearing House }
              @if (activeTab() === 'conversions') { Server-Side Conversion Mesh & In-Chat Commerce }
            </h2>
            <span class="px-2 py-0.5 rounded text-[11px] font-medium bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              Live Engine Active
            </span>
          </div>
          <div class="flex items-center gap-3 text-xs">
            <button
              (click)="toggleAudioAlert()"
              [class.bg-emerald-500/20]="audioAlertsEnabled()"
              [class.text-emerald-400]="audioAlertsEnabled()"
              class="px-3 py-1.5 rounded-lg border border-slate-700 hover:bg-slate-800 transition flex items-center gap-1.5">
              <span>🔔</span> Ka-Ching Audio: {{ audioAlertsEnabled() ? 'ON' : 'OFF' }}
            </button>
            <span class="text-slate-400">Agent: <strong class="text-slate-200">Sarah (Call Center)</strong></span>
          </div>
        </header>

        <!-- Content Area -->
        <div class="p-6 space-y-6">
          <!-- TAB 1: Confirmation Workbench -->
          @if (activeTab() === 'confirmation') {
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
              <!-- Orders Column -->
              <div class="lg:col-span-2 space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-slate-300">Orders Awaiting Confirmation</h3>
                  <span class="text-xs text-slate-400">Anti-collision optimistic concurrency active</span>
                </div>

                <div class="space-y-3">
                  @for (order of orders(); track order.id) {
                    <div class="p-4 rounded-xl border bg-slate-900/60 space-y-3"
                         [class.border-emerald-500/40]="order.status === 'Confirmed'"
                         [class.border-slate-800]="order.status !== 'Confirmed'">
                      <div class="flex items-center justify-between">
                        <div class="flex items-center gap-2">
                          <span class="font-bold text-sm text-white">{{ order.orderNumber }}</span>
                          <span class="text-xs px-2 py-0.5 rounded-full font-medium"
                                [class.bg-amber-500/20]="order.status === 'Pending'"
                                [class.text-amber-400]="order.status === 'Pending'"
                                [class.bg-emerald-500/20]="order.status === 'Confirmed'"
                                [class.text-emerald-400]="order.status === 'Confirmed'">
                            {{ order.status }}
                          </span>
                          <span class="text-xs px-2 py-0.5 rounded font-mono"
                                [class.bg-emerald-500/20]="order.riskLevel === 'Low'"
                                [class.text-emerald-400]="order.riskLevel === 'Low'"
                                [class.bg-rose-500/20]="order.riskLevel === 'High' || order.riskLevel === 'Fraudulent'"
                                [class.text-rose-400]="order.riskLevel === 'High' || order.riskLevel === 'Fraudulent'">
                            Risk: {{ order.riskScore }} ({{ order.riskLevel }})
                          </span>
                        </div>
                        <span class="text-sm font-bold text-emerald-400">{{ order.amount }} {{ order.currency }}</span>
                      </div>

                      <div class="text-xs text-slate-400 grid grid-cols-2 gap-2">
                        <div>👤 <strong>{{ order.customerName }}</strong> ({{ order.phone }})</div>
                        <div>📍 {{ order.address }}, {{ order.governorate }}</div>
                      </div>

                      @if (order.lockHolder && !order.isLockedByMe) {
                        <div class="p-2 rounded bg-amber-500/10 border border-amber-500/30 text-xs text-amber-400 flex items-center gap-2">
                          <span>🔒</span>
                          <span>Collision Guard: <strong>{{ order.lockHolder }}</strong> is currently calling this customer.</span>
                        </div>
                      }

                      <!-- Actions -->
                      <div class="flex flex-wrap items-center gap-2 pt-2 border-t border-slate-800">
                        @if (!order.isLockedByMe && order.status === 'Pending') {
                          <button
                            (click)="acquireLock(order)"
                            class="px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-xs font-semibold text-white transition">
                            🔒 Lock & Call Customer
                          </button>
                        } @else if (order.isLockedByMe && order.status === 'Pending') {
                          <button
                            (click)="confirmOrder(order)"
                            class="px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-xs font-semibold text-white transition">
                            ✅ Confirm Order
                          </button>
                          <button
                            (click)="postponeOrder(order)"
                            class="px-3 py-1.5 rounded-lg bg-amber-600 hover:bg-amber-500 text-xs font-semibold text-white transition">
                            ⏰ Postpone / Callback
                          </button>
                          <button
                            (click)="cancelOrder(order)"
                            class="px-3 py-1.5 rounded-lg bg-rose-600 hover:bg-rose-500 text-xs font-semibold text-white transition">
                            ❌ Cancel Order
                          </button>
                          <button
                            (click)="releaseLock(order)"
                            class="px-2 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 transition">
                            Release Lock
                          </button>
                        }
                        <button
                          (click)="sendInteractiveWa(order)"
                          class="px-3 py-1.5 rounded-lg bg-teal-600/30 border border-teal-500/40 hover:bg-teal-600/50 text-xs text-teal-300 font-medium transition">
                          💬 1-Click WhatsApp
                        </button>
                      </div>
                    </div>
                  }
                </div>
              </div>

              <!-- Quick Stats & Cascades -->
              <div class="space-y-4">
                <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-3">
                  <h3 class="text-sm font-semibold text-slate-200">Confirmation Health</h3>
                  <div class="space-y-2 text-xs">
                    <div class="flex justify-between text-slate-400">
                      <span>Confirmation Rate:</span>
                      <strong class="text-emerald-400">78.4%</strong>
                    </div>
                    <div class="flex justify-between text-slate-400">
                      <span>Avg Call Duration:</span>
                      <strong class="text-slate-200">1m 18s</strong>
                    </div>
                    <div class="flex justify-between text-slate-400">
                      <span>WhatsApp 1-Click Share:</span>
                      <strong class="text-teal-400">32.6%</strong>
                    </div>
                  </div>
                </div>

                <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-3">
                  <h3 class="text-sm font-semibold text-slate-200">Tunisia Governorate Cascade</h3>
                  <p class="text-xs text-slate-400">Instant client-side zero-network cascade active for 24 governorates & 264 delegations.</p>
                  <div class="p-2 rounded bg-slate-950 text-xs font-mono text-cyan-400">
                    Tunis ➔ Bab Bhar, La Marsa, Carthage, Menzah
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- TAB 2: Logistics & Manifests Hub -->
          @if (activeTab() === 'logistics') {
            <div class="space-y-6">
              <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60">
                  <div class="text-xs text-slate-400">Ready to Dispatch</div>
                  <div class="text-2xl font-bold text-white mt-1">12 Orders</div>
                </div>
                <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60">
                  <div class="text-xs text-slate-400">In Transit with Couriers</div>
                  <div class="text-2xl font-bold text-cyan-400 mt-1">45 Parcels</div>
                </div>
                <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60">
                  <div class="text-xs text-slate-400">Cash Collected (Today)</div>
                  <div class="text-2xl font-bold text-emerald-400 mt-1">4,280 TND</div>
                </div>
              </div>

              <!-- Carrier Integration Cards -->
              <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-slate-200">Active Carrier Dispatchers</h3>
                  <button
                    (click)="generateManifest()"
                    class="px-3 py-1.5 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-xs font-semibold text-white transition">
                    📄 Print Bordereau Manifest
                  </button>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
                  <div class="p-3 rounded-lg border border-slate-800 bg-slate-950 flex items-center justify-between">
                    <div>
                      <div class="font-bold text-slate-200">Aramex Tunisia</div>
                      <div class="text-slate-400 text-[11px]">API: Online | AWB Barcode: Active</div>
                    </div>
                    <span class="text-emerald-400 font-bold">24 Govs</span>
                  </div>
                  <div class="p-3 rounded-lg border border-slate-800 bg-slate-950 flex items-center justify-between">
                    <div>
                      <div class="font-bold text-slate-200">Bosta Egypt</div>
                      <div class="text-slate-400 text-[11px]">API: Online | Zone Pricing: Active</div>
                    </div>
                    <span class="text-emerald-400 font-bold">27 Govs</span>
                  </div>
                  <div class="p-3 rounded-lg border border-slate-800 bg-slate-950 flex items-center justify-between">
                    <div>
                      <div class="font-bold text-slate-200">Yalidine Algeria</div>
                      <div class="text-slate-400 text-[11px]">API: Online | Stopdesk Support</div>
                    </div>
                    <span class="text-emerald-400 font-bold">58 Wilayas</span>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- TAB 3: Defensive WMS Stock Ledger -->
          @if (activeTab() === 'wms') {
            <div class="space-y-6">
              <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="text-sm font-semibold text-slate-200">Multi-State Defensive COD Inventory Model</h3>
                    <p class="text-xs text-slate-400">Available = Physical - Reserved - Committed (prevents overselling & artificial stockouts)</p>
                  </div>
                  <button
                    (click)="simulateBarcodeScan()"
                    class="px-3 py-1.5 rounded-lg bg-amber-600 hover:bg-amber-500 text-xs font-semibold text-white transition">
                    🔍 Barcode Scanner: Restock Quarantine Item
                  </button>
                </div>

                <div class="overflow-x-auto">
                  <table class="w-full text-left text-xs">
                    <thead class="border-b border-slate-800 text-slate-400">
                      <tr>
                        <th class="py-2">SKU</th>
                        <th class="py-2">Product</th>
                        <th class="py-2 text-center text-slate-200">Physical</th>
                        <th class="py-2 text-center text-amber-400">Reserved</th>
                        <th class="py-2 text-center text-indigo-400">Committed</th>
                        <th class="py-2 text-center text-cyan-400">In-Transit</th>
                        <th class="py-2 text-center text-rose-400">Quarantine</th>
                        <th class="py-2 text-right text-emerald-400 font-bold">Available to Promise</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800">
                      @for (item of wmsStock(); track item.sku) {
                        <tr class="hover:bg-slate-800/40 transition">
                          <td class="py-2.5 font-mono text-slate-300">{{ item.sku }}</td>
                          <td class="py-2.5 text-white font-medium">{{ item.name }}</td>
                          <td class="py-2.5 text-center">{{ item.physical }}</td>
                          <td class="py-2.5 text-center text-amber-400 font-bold">{{ item.reserved }}</td>
                          <td class="py-2.5 text-center text-indigo-400">{{ item.committed }}</td>
                          <td class="py-2.5 text-center text-cyan-400">{{ item.inTransit }}</td>
                          <td class="py-2.5 text-center text-rose-400">{{ item.quarantine }}</td>
                          <td class="py-2.5 text-right font-bold text-emerald-400">{{ item.available }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          }

          <!-- TAB 4: SDR Cadences -->
          @if (activeTab() === 'cadences') {
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <!-- Cadences -->
              <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <h3 class="text-sm font-semibold text-slate-200">Active Multi-Channel Sequences</h3>
                <div class="p-3 rounded-lg border border-slate-800 bg-slate-950 space-y-2 text-xs">
                  <div class="flex items-center justify-between">
                    <span class="font-bold text-white">Enterprise B2B Cold Outreach</span>
                    <span class="px-2 py-0.5 rounded bg-purple-500/20 text-purple-400">Stop-On-Reply Active</span>
                  </div>
                  <div class="text-slate-400">
                    Day 1: Personalized Email ➔ Day 3: Softphone Discovery Call ➔ Day 5: WhatsApp Case Study
                  </div>
                </div>
              </div>

              <!-- SDR Daily Task Checklist -->
              <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-slate-200">SDR Daily Action List</h3>
                  <span class="text-xs text-slate-400">{{ sdrTasks().length }} Tasks Due</span>
                </div>

                <div class="space-y-2">
                  @for (task of sdrTasks(); track task.id) {
                    <div class="p-3 rounded-lg border border-slate-800 bg-slate-950 flex items-center justify-between text-xs">
                      <div>
                        <div class="font-bold text-white">{{ task.leadName }} ({{ task.company }})</div>
                        <div class="text-slate-400">{{ task.instruction }}</div>
                      </div>
                      <button
                        (click)="completeTask(task)"
                        class="px-2.5 py-1 rounded bg-emerald-600 hover:bg-emerald-500 text-white font-medium transition">
                        Done
                      </button>
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- TAB 5: Customer 360 & RMA -->
          @if (activeTab() === 'rma') {
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <!-- Customer 360 Graph -->
              <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <h3 class="text-sm font-semibold text-slate-200">Unified Identity Resolution</h3>
                <div class="p-3 rounded-lg border border-slate-800 bg-slate-950 space-y-2 text-xs">
                  <div class="font-bold text-white">Yassine Mansour</div>
                  <div class="text-slate-400">Phone (E.164): <span class="text-cyan-400">+21698123456</span></div>
                  <div class="text-slate-400">Email: <span class="text-cyan-400">yassine&#64;gmail.com</span></div>
                  <div class="text-slate-400">Instagram: <span class="text-fuchsia-400">&#64;yassine_m</span></div>
                  <div class="text-slate-400">WhatsApp: <span class="text-emerald-400">+21698123456</span></div>
                  <div class="text-slate-400">Delivery Success: <strong class="text-emerald-400">100%</strong></div>
                </div>
              </div>

              <!-- RMA Returns List -->
              <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-slate-200">Return (RMA) Requests</h3>
                  <span class="text-xs text-slate-400">Self-Service Portal</span>
                </div>

                <div class="space-y-2">
                  @for (rma of rmaRequests(); track rma.rmaNumber) {
                    <div class="p-3 rounded-lg border border-slate-800 bg-slate-950 flex items-center justify-between text-xs">
                      <div>
                        <div class="font-bold text-white">{{ rma.rmaNumber }} - {{ rma.orderNumber }}</div>
                        <div class="text-slate-400">Reason: {{ rma.reason }} | Phone: {{ rma.phone }}</div>
                      </div>
                      <button
                        (click)="approveRma(rma)"
                        class="px-2.5 py-1 rounded bg-rose-600 hover:bg-rose-500 text-white font-medium transition">
                        Approve & Reverse Pickup
                      </button>
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- TAB 6: Courier Cash Remittance & Financial Clearing House -->
          @if (activeTab() === 'remittance') {
            <div class="space-y-6">
              <!-- Summary KPI Cards -->
              <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
                <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60">
                  <div class="text-xs text-slate-400">Total Cash Collected</div>
                  <div class="text-xl font-bold text-white mt-1">12,450 TND</div>
                  <span class="text-[11px] text-emerald-400">Aramex + Express Courrier</span>
                </div>
                <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60">
                  <div class="text-xs text-slate-400">Courier Fees Deducted</div>
                  <div class="text-xl font-bold text-rose-400 mt-1">-1,120 TND</div>
                  <span class="text-[11px] text-slate-400">Avg 8.00 TND / delivery</span>
                </div>
                <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60">
                  <div class="text-xs text-slate-400">Net Cash Remitted</div>
                  <div class="text-xl font-bold text-emerald-400 mt-1">11,330 TND</div>
                  <span class="text-[11px] text-emerald-400">Deposited into Bank</span>
                </div>
                <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60">
                  <div class="text-xs text-slate-400">Net Merchant Profitability</div>
                  <div class="text-xl font-bold text-cyan-400 mt-1">5,145 TND</div>
                  <span class="text-[11px] text-cyan-400">41.3% Net Margin</span>
                </div>
              </div>

              <!-- Discrepancy Reconciliation Table -->
              <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="text-sm font-semibold text-slate-200">Weekly Courier Settlement Reconciliation</h3>
                    <p class="text-xs text-slate-400">Automated 3-way matching against system delivered orders</p>
                  </div>
                  <button
                    (click)="simulateReconciliation()"
                    class="px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-xs font-semibold text-white transition">
                    🔄 Run Auto-Reconciliation
                  </button>
                </div>

                <div class="overflow-x-auto">
                  <table class="w-full text-left text-xs">
                    <thead class="border-b border-slate-800 text-slate-400">
                      <tr>
                        <th class="py-2">Tracking #</th>
                        <th class="py-2">Courier</th>
                        <th class="py-2 text-right">Expected Cash</th>
                        <th class="py-2 text-right">Reported Cash</th>
                        <th class="py-2 text-right">Difference</th>
                        <th class="py-2 text-center">Status</th>
                        <th class="py-2 text-right">Action</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800">
                      @for (item of remittanceDiscrepancies(); track item.tracking) {
                        <tr class="hover:bg-slate-800/40 transition">
                          <td class="py-2.5 font-mono text-slate-200">{{ item.tracking }}</td>
                          <td class="py-2.5 text-slate-300">{{ item.courier }}</td>
                          <td class="py-2.5 text-right font-mono">{{ item.expected }} TND</td>
                          <td class="py-2.5 text-right font-mono">{{ item.reported }} TND</td>
                          <td class="py-2.5 text-right font-mono font-bold"
                              [class.text-emerald-400]="item.diff === 0"
                              [class.text-rose-400]="item.diff < 0">
                            {{ item.diff }} TND
                          </td>
                          <td class="py-2.5 text-center">
                            <span class="px-2 py-0.5 rounded text-[11px] font-medium"
                                  [class.bg-emerald-500/20]="item.status === 'ExactMatch'"
                                  [class.text-emerald-400]="item.status === 'ExactMatch'"
                                  [class.bg-rose-500/20]="item.status === 'ShortPayment'"
                                  [class.text-rose-400]="item.status === 'ShortPayment'">
                              {{ item.status }}
                            </span>
                          </td>
                          <td class="py-2.5 text-right">
                            @if (item.status === 'ShortPayment') {
                              <button
                                (click)="resolveDiscrepancy(item)"
                                class="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 transition text-[11px]">
                                Resolve Dispute
                              </button>
                            } @else {
                              <span class="text-slate-500">Cleared</span>
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

          <!-- TAB 7: Server-Side Conversion Mesh & In-Chat Commerce -->
          @if (activeTab() === 'conversions') {
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <!-- Meta CAPI & TikTok Events -->
              <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-slate-200">Server-Side Conversion Mesh</h3>
                  <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 text-xs font-bold">
                    EMQ: 9.4 / 10.0
                  </span>
                </div>
                <p class="text-xs text-slate-400">
                  Bypassing iOS 14.5 ad-blockers with SHA-256 server-to-server event dispatching for Meta Graph API & TikTok Events API.
                </p>

                <div class="space-y-2 text-xs font-mono">
                  <div class="p-2.5 rounded bg-slate-950 border border-slate-800 text-slate-300 flex items-center justify-between">
                    <div>
                      <div class="text-sky-400 font-bold">Meta CAPI: Purchase (180 TND)</div>
                      <div class="text-slate-500 text-[10px]">SHA256(phone)=a94a8fe5... | fbp=fb.1.1689...</div>
                    </div>
                    <span class="text-emerald-400 font-bold">Dispatched</span>
                  </div>
                  <div class="p-2.5 rounded bg-slate-950 border border-slate-800 text-slate-300 flex items-center justify-between">
                    <div>
                      <div class="text-fuchsia-400 font-bold">TikTok Events: CompletePayment (89 TND)</div>
                      <div class="text-slate-500 text-[10px]">SHA256(email)=e3b0c442... | deduplication_id=EVT-881</div>
                    </div>
                    <span class="text-emerald-400 font-bold">Dispatched</span>
                  </div>
                </div>

                <div class="pt-2 border-t border-slate-800 flex items-center justify-between text-xs text-slate-400">
                  <span>Shopify Bi-Directional Webhook: <strong class="text-emerald-400">Connected</strong></span>
                  <span>Google Sheets Stream: <strong class="text-emerald-400">48 Rows Appended</strong></span>
                </div>
              </div>

              <!-- In-Chat Conversational Cart Builder -->
              <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <h3 class="text-sm font-semibold text-slate-200">Conversational In-Chat Cart Builder</h3>
                <p class="text-xs text-slate-400">Extracts quantities, governorate shipping fees, and customer phone directly from chat.</p>

                <div class="space-y-3">
                  <div class="p-3 rounded-lg border border-slate-800 bg-slate-950 text-xs space-y-1.5">
                    <div class="text-slate-400 font-sans">Simulated WhatsApp Customer Message:</div>
                    <div class="p-2 rounded bg-slate-900 text-slate-200 italic font-mono">
                      "Bonjour, je veux 2 bouteilles de Royal Oud pour Sousse au +21698112233"
                    </div>
                    <div class="text-emerald-400 font-sans font-medium pt-1">
                      ➔ Detected: Product="Royal Oud" | Qty=2 | Gov="Sousse" | Shipping=7.0 TND | Total=167 TND
                    </div>
                  </div>

                  <button
                    (click)="simulateChatOrder()"
                    class="w-full py-2 rounded-lg bg-teal-600 hover:bg-teal-500 text-xs font-semibold text-white transition">
                    ⚡ Generate 1-Click WhatsApp Order Card & Reserve Stock
                  </button>
                </div>
              </div>
            </div>
          }

          <!-- 8. Mobile Barcode Fulfillment Desk Tab -->
          @if (activeTab() === 'scanner') {
            <div class="space-y-6">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-base font-semibold text-slate-100">📱 Mobile Barcode Fulfillment Desk</h3>
                  <p class="text-xs text-slate-400">High-speed camera viewfinder, handheld laser wedge, Pick-Pack-Ship validation and RMA returns.</p>
                </div>
                <div class="flex items-center gap-2">
                  <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                    <span class="h-1.5 w-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                    Laser Wedge Active
                  </span>
                </div>
              </div>

              <!-- Barcode Scanner Viewfinder & Laser Box -->
              <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div class="md:col-span-1 rounded-xl border border-slate-800 bg-slate-900/40 p-4 flex flex-col items-center justify-center text-center relative overflow-hidden group">
                  <div class="w-full h-48 rounded-lg bg-slate-950 border border-slate-700/60 relative flex items-center justify-center overflow-hidden">
                    <!-- Scanner Laser Line Animation -->
                    <div class="absolute left-0 right-0 h-0.5 bg-gradient-to-r from-transparent via-red-500 to-transparent shadow-[0_0_12px_#ef4444] animate-bounce"></div>
                    <div class="absolute inset-4 border-2 border-dashed border-red-500/30 rounded flex items-center justify-center pointer-events-none">
                      <span class="text-[10px] text-red-400/80 tracking-widest uppercase font-mono">Align Barcode / QR</span>
                    </div>
                    <span class="text-4xl text-slate-700">📷</span>
                  </div>
                  <p class="text-xs text-slate-400 mt-3">Camera Viewfinder Active (Code128 / EAN13 / QR)</p>
                </div>

                <div class="md:col-span-2 rounded-xl border border-slate-800 bg-slate-900/40 p-5 space-y-4">
                  <h4 class="text-xs font-semibold text-slate-300 uppercase tracking-wider">Fast Scan Input (Laser Scanner / Keyboard Wedge)</h4>
                  <div class="flex gap-2">
                    <input
                      #barcodeInput
                      type="text"
                      placeholder="Scan SKU barcode or paste tracking (Press Enter)..."
                      (keydown.enter)="handleBarcodeScan(barcodeInput.value); barcodeInput.value = ''"
                      class="flex-1 bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 placeholder-slate-500 font-mono focus:border-amber-500 outline-none" />
                    <button
                      (click)="handleBarcodeScan(barcodeInput.value); barcodeInput.value = ''"
                      class="px-4 py-2 rounded-lg bg-amber-600 hover:bg-amber-500 text-xs font-semibold text-white transition">
                      Scan
                    </button>
                  </div>

                  @if (scanFeedback()) {
                    <div
                      [class.bg-emerald-500/10]="scanFeedback()!.success"
                      [class.border-emerald-500/30]="scanFeedback()!.success"
                      [class.text-emerald-400]="scanFeedback()!.success"
                      [class.bg-rose-500/10]="!scanFeedback()!.success"
                      [class.border-rose-500/30]="!scanFeedback()!.success"
                      [class.text-rose-400]="!scanFeedback()!.success"
                      class="p-3 rounded-lg border text-xs font-mono flex items-center justify-between">
                      <span>{{ scanFeedback()!.message }}</span>
                      <span class="text-[10px] font-sans font-bold px-2 py-0.5 rounded bg-slate-950/60">
                        {{ scanFeedback()!.success ? 'VERIFIED' : 'MISMATCH' }}
                      </span>
                    </div>
                  }

                  <!-- Active Pick Slip Summary -->
                  <div class="border-t border-slate-800/80 pt-4">
                    <div class="flex items-center justify-between mb-2">
                      <span class="text-xs font-medium text-slate-200">Pick Slip #{{ activePickSlip().pickSlipNumber }} (Order: {{ activePickSlip().orderNumber }})</span>
                      <span class="text-xs text-amber-400 font-bold">{{ pickedCount() }}/{{ activePickSlip().items.length }} Items Picked</span>
                    </div>
                    <div class="space-y-2">
                      @for (item of activePickSlip().items; track item.sku) {
                        <div class="flex items-center justify-between p-2.5 rounded-lg bg-slate-950/60 border border-slate-800">
                          <div class="flex items-center gap-2">
                            <span class="text-xs font-mono text-slate-400">{{ item.sku }}</span>
                            <span class="text-xs text-slate-200 font-medium">({{ item.productName }})</span>
                          </div>
                          <div class="flex items-center gap-3">
                            <span class="text-xs font-mono text-slate-400">Barcode: {{ item.barcode }}</span>
                            <span
                              [class.bg-emerald-500/20]="item.isVerified"
                              [class.text-emerald-400]="item.isVerified"
                              [class.bg-slate-800]="!item.isVerified"
                              [class.text-slate-400]="!item.isVerified"
                              class="text-[10px] px-2 py-0.5 rounded font-bold">
                              {{ item.isVerified ? '✓ Picked' : 'Pending' }}
                            </span>
                          </div>
                        </div>
                      }
                    </div>

                    <button
                      (click)="completeFulfillmentPack()"
                      [disabled]="pickedCount() < activePickSlip().items.length"
                      class="w-full mt-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 disabled:pointer-events-none text-xs font-semibold text-white transition flex items-center justify-center gap-2">
                      📦 Pack Box, Assign AWB & Dispatch to WMS In-Transit
                    </button>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- 9. Autonomous Voice AI Confirmation Tab -->
          @if (activeTab() === 'dialer') {
            <div class="space-y-6">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-base font-semibold text-slate-100">🤖 Autonomous Voice AI Confirmation Dialer</h3>
                  <p class="text-xs text-slate-400">Outbound conversational IVR with Tunisian Derja, French & Arabic speech intent recognition.</p>
                </div>
                <div class="flex items-center gap-3">
                  <span class="text-xs text-slate-400 font-mono">Active Campaign: <strong class="text-emerald-400">Tunisia COD Fast Confirm (ar-TN)</strong></span>
                  <button
                    (click)="triggerVoiceDialerCall()"
                    class="px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-xs font-semibold text-white transition flex items-center gap-1.5">
                    📞 Queue Auto Call
                  </button>
                </div>
              </div>

              <!-- Live Call Detail Records (CDR) Table -->
              <div class="rounded-xl border border-slate-800 bg-slate-900/40 overflow-hidden">
                <table class="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr class="border-b border-slate-800 bg-slate-900/80 text-slate-400 uppercase tracking-wider text-[10px]">
                      <th class="p-3">Order</th>
                      <th class="p-3">Customer & Phone</th>
                      <th class="p-3">Spoken Transcript</th>
                      <th class="p-3">DTMF</th>
                      <th class="p-3">Call State</th>
                      <th class="p-3">WMS Stock Sync</th>
                      <th class="p-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-mono">
                    @for (call of dialerAttempts(); track call.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="p-3 font-semibold text-slate-200">{{ call.orderNumber }}</td>
                        <td class="p-3 font-sans">
                          <div class="text-slate-100 font-medium">{{ call.customerName }}</div>
                          <div class="text-slate-400 text-[10px] font-mono">{{ call.customerPhone }}</div>
                        </td>
                        <td class="p-3 font-sans text-slate-300 italic">"{{ call.speechTranscript }}"</td>
                        <td class="p-3 text-slate-400">{{ call.dtmfDigit || '-' }}</td>
                        <td class="p-3">
                          <span
                            [class.bg-emerald-500/20]="call.state === 'Confirmed'"
                            [class.text-emerald-400]="call.state === 'Confirmed'"
                            [class.bg-rose-500/20]="call.state === 'Cancelled'"
                            [class.text-rose-400]="call.state === 'Cancelled'"
                            [class.bg-amber-500/20]="call.state === 'Queued'"
                            [class.text-amber-400]="call.state === 'Queued'"
                            class="px-2 py-0.5 rounded text-[10px] font-sans font-bold">
                            {{ call.state }}
                          </span>
                        </td>
                        <td class="p-3 font-sans">
                          <span class="text-xs text-cyan-400">{{ call.wmsEffect }}</span>
                        </td>
                        <td class="p-3 text-right font-sans">
                          @if (call.state === 'Queued') {
                            <button
                              (click)="simulateSpeechResponse(call.id, 'نعم نأكد')"
                              class="px-2.5 py-1 rounded bg-emerald-600/30 hover:bg-emerald-600/50 text-emerald-300 text-[10px] font-semibold transition mr-1">
                              Simulate 'نعم نأكد'
                            </button>
                            <button
                              (click)="simulateSpeechResponse(call.id, 'Annuler')"
                              class="px-2.5 py-1 rounded bg-rose-600/30 hover:bg-rose-600/50 text-rose-300 text-[10px] font-semibold transition">
                              Simulate 'Annuler'
                            </button>
                          } @else {
                            <span class="text-[10px] text-slate-500">Processed</span>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- 10. Customer Anti-Fraud Reputation Tab -->
          @if (activeTab() === 'reputation') {
            <div class="space-y-6">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-base font-semibold text-slate-100">🛡️ Customer Reputation & Anti-Fraud Scorecard</h3>
                  <p class="text-xs text-slate-400">Cross-storefront delivery trust scorecard, return risk detection, and smart micro-deposit guarantees.</p>
                </div>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                @for (c of customerScorecards(); track c.phone) {
                  <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/40 space-y-3">
                    <div class="flex items-center justify-between">
                      <div>
                        <h4 class="text-sm font-semibold text-slate-100">{{ c.name }}</h4>
                        <span class="text-xs font-mono text-slate-400">{{ c.phone }}</span>
                      </div>
                      <div class="flex items-center gap-2">
                        <span
                          [class.bg-emerald-500/20]="c.tier === 'Trusted'"
                          [class.text-emerald-400]="c.tier === 'Trusted'"
                          [class.bg-sky-500/20]="c.tier === 'Standard'"
                          [class.text-sky-400]="c.tier === 'Standard'"
                          [class.bg-amber-500/20]="c.tier === 'HighRisk'"
                          [class.text-amber-400]="c.tier === 'HighRisk'"
                          [class.bg-rose-500/20]="c.tier === 'Blacklisted'"
                          [class.text-rose-400]="c.tier === 'Blacklisted'"
                          class="px-2.5 py-1 rounded-full text-xs font-bold">
                          {{ c.tier }} (Trust: {{ c.score }}/100)
                        </span>
                      </div>
                    </div>

                    <div class="grid grid-cols-3 gap-2 py-2 border-y border-slate-800/80 text-center font-mono">
                      <div>
                        <span class="text-[10px] text-slate-400 uppercase">Delivered</span>
                        <div class="text-xs font-bold text-emerald-400">{{ c.delivered }}</div>
                      </div>
                      <div>
                        <span class="text-[10px] text-slate-400 uppercase">Returns</span>
                        <div class="text-xs font-bold text-rose-400">{{ c.returns }}</div>
                      </div>
                      <div>
                        <span class="text-[10px] text-slate-400 uppercase">Success Rate</span>
                        <div class="text-xs font-bold text-cyan-400">{{ c.successRate }}%</div>
                      </div>
                    </div>

                    <div class="flex items-center justify-between pt-1">
                      <span class="text-xs text-slate-400">{{ c.recommendation }}</span>
                      @if (c.requiresDeposit && !c.depositPaid) {
                        <button
                          (click)="settleDepositUi(c)"
                          class="px-3 py-1.5 rounded-lg bg-amber-600 hover:bg-amber-500 text-xs font-semibold text-white transition">
                          💳 Pay 10 DT Micro-Deposit
                        </button>
                      } @else if (c.depositPaid) {
                        <span class="text-xs text-emerald-400 font-semibold font-mono">✓ 10 DT Deposit Deducted</span>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- 11. Smart Carrier Routing Tab -->
          @if (activeTab() === 'routing') {
            <div class="space-y-6">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-base font-semibold text-slate-100">🎯 Smart Multi-Carrier Rate & Delivery Success Routing</h3>
                  <p class="text-xs text-slate-400">Algorithmic carrier ranking maximizing delivery success rate and net profit margin per governorate.</p>
                </div>
                <div class="flex items-center gap-2">
                  <span class="text-xs font-mono text-cyan-400 bg-cyan-500/10 border border-cyan-500/20 px-2.5 py-1 rounded-full">
                    Active Zone: Tunis & Sousse
                  </span>
                </div>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                @for (rec of carrierRankings(); track rec.courierName) {
                  <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/40 space-y-3">
                    <div class="flex items-center justify-between">
                      <div>
                        <h4 class="text-sm font-semibold text-slate-100">{{ rec.courierName }}</h4>
                        <span class="text-xs font-mono text-slate-400">Base Shipping: {{ rec.shippingCost }} TND</span>
                      </div>
                      <span
                        [class.bg-emerald-500/20]="rec.isRecommended"
                        [class.text-emerald-400]="rec.isRecommended"
                        [class.bg-slate-800]="!rec.isRecommended"
                        [class.text-slate-400]="!rec.isRecommended"
                        class="px-2.5 py-1 rounded-full text-xs font-bold">
                        {{ rec.isRecommended ? '★ Recommended (#1)' : 'Alternative' }}
                      </span>
                    </div>

                    <div class="grid grid-cols-2 gap-3 py-2 border-y border-slate-800/80 font-mono text-xs">
                      <div>
                        <span class="text-[10px] text-slate-400 uppercase">Historical Delivery</span>
                        <div class="text-emerald-400 font-bold">{{ rec.deliveryRate }}%</div>
                      </div>
                      <div>
                        <span class="text-[10px] text-slate-400 uppercase">Expected Net Margin</span>
                        <div class="text-cyan-400 font-bold">+{{ rec.expectedNetMargin }} TND</div>
                      </div>
                    </div>

                    <p class="text-xs text-slate-400 font-sans italic">{{ rec.reasoning }}</p>

                    <button
                      (click)="autoAssignCarrierUi(rec)"
                      class="w-full py-1.5 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-xs font-semibold text-white transition">
                      Auto-Assign {{ rec.courierName }} to Pending Orders
                    </button>
                  </div>
                }
              </div>
            </div>
          }

          <!-- 12. Live Social Commerce Tab -->
          @if (activeTab() === 'livecommerce') {
            <div class="space-y-6">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-base font-semibold text-slate-100">🔴 Live Social Commerce & Stream Overlay (TikTok & FB Live)</h3>
                  <p class="text-xs text-slate-400">Comment keyword-to-cart parser with real-time OBS browser source overlay.</p>
                </div>
                <div class="flex items-center gap-2">
                  <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-rose-500/10 text-rose-400 border border-rose-500/20">
                    <span class="h-2 w-2 rounded-full bg-rose-500 animate-ping"></span>
                    ON AIR (TikTok Live)
                  </span>
                </div>
              </div>

              <!-- OBS Browser Overlay Preview Widget -->
              <div class="rounded-xl border border-slate-800 bg-slate-900/40 p-5 space-y-4">
                <div class="flex items-center justify-between">
                  <h4 class="text-xs font-semibold text-slate-300 uppercase tracking-wider">Live Stream Broadcast Overlay (OBS Browser Source)</h4>
                  <span class="text-xs font-mono text-slate-500">Overlay URL: /api/channels/live/session-1/overlay</span>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  @for (deal of liveDeals(); track deal.sku) {
                    <div class="p-4 rounded-xl bg-slate-950 border border-rose-500/30 flex items-center justify-between relative overflow-hidden">
                      <div class="space-y-1">
                        <span class="text-[10px] font-bold uppercase tracking-wider text-rose-400 font-mono">FLASH DEAL • COMMENT "{{ deal.keyword }}"</span>
                        <h4 class="text-sm font-bold text-white">{{ deal.name }}</h4>
                        <div class="flex items-baseline gap-2 font-mono">
                          <span class="text-base font-black text-emerald-400">{{ deal.flashPrice }} TND</span>
                          <span class="text-xs text-slate-500 line-through">{{ deal.originalPrice }} TND</span>
                        </div>
                      </div>

                      <div class="text-right">
                        <span class="text-[10px] text-slate-400 uppercase font-mono">Stock Remaining</span>
                        <div class="text-xl font-black text-rose-400 font-mono">{{ deal.remaining }} / {{ deal.max }}</div>
                        <span class="text-[10px] text-amber-400 font-bold">🔥 1-Click DM Card</span>
                      </div>
                    </div>
                  }
                </div>

                <div class="flex items-center gap-2 pt-2 border-t border-slate-800/80">
                  <span class="text-xs text-slate-400">Simulate Viewer Comment:</span>
                  <button
                    (click)="simulateLiveComment('ACHETER')"
                    class="px-3 py-1.5 rounded-lg bg-rose-600 hover:bg-rose-500 text-xs font-semibold text-white transition">
                    💬 Comment "ACHETER"
                  </button>
                  <button
                    (click)="simulateLiveComment('PROMO50')"
                    class="px-3 py-1.5 rounded-lg bg-amber-600 hover:bg-amber-500 text-xs font-semibold text-white transition">
                    💬 Comment "PROMO50"
                  </button>
                </div>
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class OperationsStudioComponent {
  activeTab = signal<'confirmation' | 'logistics' | 'wms' | 'cadences' | 'rma' | 'remittance' | 'conversions' | 'scanner' | 'dialer' | 'reputation' | 'routing' | 'livecommerce'>('confirmation');
  audioAlertsEnabled = signal<boolean>(true);

  // Barcode Fulfillment Desk Signals
  scanFeedback = signal<{ success: boolean; message: string } | null>(null);
  activePickSlip = signal({
    pickSlipNumber: 'PICK-20261026-9901',
    orderNumber: 'ORD-TN-7701',
    items: [
      { sku: 'SLIM-JACKET-BLACK-XL', productName: 'Men Slim Leather Jacket', barcode: '6191234567890', isVerified: false },
      { sku: 'PERFUME-OUD-100ML', productName: 'Royal Oud Fragrance', barcode: '6199876543210', isVerified: false }
    ]
  });
  pickedCount = signal(0);

  // Voice AI Dialer Signals
  dialerAttempts = signal([
    {
      id: 'call-1',
      orderNumber: 'ORD-TN-7701',
      customerName: 'Mohamed Salah Trabelsi',
      customerPhone: '+21698112233',
      speechTranscript: 'نعم نأكد الطلب',
      dtmfDigit: '1',
      state: 'Confirmed',
      wmsEffect: '✓ Reserved ➔ Committed'
    },
    {
      id: 'call-2',
      orderNumber: 'ORD-TN-7702',
      customerName: 'Aymen Gharbi',
      customerPhone: '+21622334455',
      speechTranscript: 'Awaiting customer response...',
      dtmfDigit: '',
      state: 'Queued',
      wmsEffect: 'Stock in Reserved state'
    }
  ]);

  // Anti-Fraud Customer Reputation Scorecards
  customerScorecards = signal([
    {
      phone: '+21698112233',
      name: 'Mohamed Salah Trabelsi',
      score: 95,
      tier: 'Trusted',
      delivered: 12,
      returns: 0,
      successRate: 100,
      requiresDeposit: false,
      depositPaid: false,
      recommendation: 'Auto-approve without call'
    },
    {
      phone: '+21622334455',
      name: 'Aymen Gharbi',
      score: 35,
      tier: 'HighRisk',
      delivered: 2,
      returns: 3,
      successRate: 40,
      requiresDeposit: true,
      depositPaid: false,
      recommendation: 'Requires 10 TND Micro-Deposit'
    }
  ]);

  orders = signal<CodOrderUi[]>([
    {
      id: '1',
      orderNumber: 'ORD-TN-7701',
      customerName: 'Mohamed Salah Trabelsi',
      phone: '+21698112233',
      address: 'Avenue Habib Bourguiba',
      governorate: 'Tunis',
      amount: 149,
      currency: 'TND',
      status: 'Pending',
      riskScore: 12,
      riskLevel: 'Low',
      isLockedByMe: false
    },
    {
      id: '2',
      orderNumber: 'ORD-TN-7702',
      customerName: 'Aymen Gharbi',
      phone: '+21622334455',
      address: 'Route de Teniour km 4',
      governorate: 'Sfax',
      amount: 89,
      currency: 'TND',
      status: 'Pending',
      riskScore: 25,
      riskLevel: 'Medium',
      isLockedByMe: false
    },
    {
      id: '3',
      orderNumber: 'ORD-TN-7703',
      customerName: 'Sarra Ben Ahmed',
      phone: '+21650998877',
      address: 'Kantaoui Sousse',
      governorate: 'Sousse',
      amount: 220,
      currency: 'TND',
      status: 'Confirmed',
      riskScore: 5,
      riskLevel: 'Low',
      isLockedByMe: false
    }
  ]);

  wmsStock = signal<WmsStockUi[]>([
    {
      sku: 'SLIM-JACKET-BLACK-XL',
      name: 'Men Slim Leather Jacket',
      physical: 45,
      reserved: 2,
      committed: 3,
      inTransit: 5,
      quarantine: 2,
      damaged: 1,
      available: 40
    },
    {
      sku: 'PERFUME-OUD-100ML',
      name: 'Royal Oud Fragrance',
      physical: 120,
      reserved: 4,
      committed: 6,
      inTransit: 10,
      quarantine: 1,
      damaged: 0,
      available: 110
    }
  ]);

  sdrTasks = signal<SdrTaskUi[]>([
    {
      id: 'task-1',
      leadName: 'Karim Ayari',
      phone: '+21698765432',
      company: 'TechCorp TN',
      taskType: 'PhoneCall',
      instruction: 'Call prospect regarding cloud migration and interactive CPQ.',
      isCompleted: false
    }
  ]);

  rmaRequests = signal<RmaRequestUi[]>([
    {
      rmaNumber: 'RMA-ORD-TN-7701-01',
      orderNumber: 'ORD-TN-7701',
      phone: '+21698112233',
      reason: 'Size exchange (Need L instead of XL)',
      status: 'Requested'
    }
  ]);

  pendingCount(): number {
    return this.orders().filter(o => o.status === 'Pending').length;
  }

  toggleAudioAlert() {
    this.audioAlertsEnabled.update(v => !v);
  }

  acquireLock(order: CodOrderUi) {
    this.orders.update(list => list.map(o => o.id === order.id ? { ...o, lockHolder: 'Sarah', isLockedByMe: true } : o));
  }

  releaseLock(order: CodOrderUi) {
    this.orders.update(list => list.map(o => o.id === order.id ? { ...o, lockHolder: undefined, isLockedByMe: false } : o));
  }

  confirmOrder(order: CodOrderUi) {
    this.orders.update(list => list.map(o => o.id === order.id ? { ...o, status: 'Confirmed', isLockedByMe: false } : o));
  }

  cancelOrder(order: CodOrderUi) {
    this.orders.update(list => list.map(o => o.id === order.id ? { ...o, status: 'Cancelled', isLockedByMe: false } : o));
  }

  postponeOrder(order: CodOrderUi) {
    this.releaseLock(order);
  }

  sendInteractiveWa(order: CodOrderUi) {
    alert(`Interactive WhatsApp confirmation sent to ${order.customerName} (${order.phone})`);
  }

  generateManifest() {
    alert('Shipping Manifest (Bordereau) generated for 12 orders. Total Cash to Collect: 1,840 TND');
  }

  simulateBarcodeScan() {
    this.wmsStock.update(list => list.map(item => item.sku === 'SLIM-JACKET-BLACK-XL' ? {
      ...item,
      quarantine: Math.max(0, item.quarantine - 1),
      physical: item.physical + 1,
      available: item.available + 1
    } : item));
    alert('Barcode scanned: SKU verified and restocked to physical inventory.');
  }

  completeTask(task: SdrTaskUi) {
    this.sdrTasks.update(list => list.filter(t => t.id !== task.id));
  }

  approveRma(rma: RmaRequestUi) {
    this.rmaRequests.update(list => list.filter(r => r.rmaNumber !== rma.rmaNumber));
    alert(`RMA ${rma.rmaNumber} approved! Reverse courier pickup dispatched.`);
  }

  remittanceDiscrepancies = signal([
    {
      tracking: 'ARX-TN-9901-22',
      courier: 'Aramex Tunisia',
      expected: 149,
      reported: 149,
      diff: 0,
      status: 'ExactMatch'
    },
    {
      tracking: 'EXP-TN-8802-11',
      courier: 'Express Courrier',
      expected: 220,
      reported: 180,
      diff: -40,
      status: 'ShortPayment'
    }
  ]);

  simulateReconciliation() {
    alert('Reconciliation complete: 2 statements processed, 1 short-payment dispute flagged.');
  }

  resolveDiscrepancy(item: any) {
    this.remittanceDiscrepancies.update(list => list.map(i => i.tracking === item.tracking ? { ...i, status: 'ExactMatch', diff: 0 } : i));
    alert(`Dispute on ${item.tracking} marked resolved.`);
  }

  simulateChatOrder() {
    alert('Order #CHAT-261026-8821 generated for Sousse (167 TND). Stock reserved in WMS. WhatsApp interactive button card dispatched.');
  }

  handleBarcodeScan(input: string) {
    if (!input || !input.trim()) return;
    const clean = input.trim();
    const item = this.activePickSlip().items.find(i => i.barcode === clean || i.sku === clean);

    if (item) {
      item.isVerified = true;
      const count = this.activePickSlip().items.filter(i => i.isVerified).length;
      this.pickedCount.set(count);
      this.scanFeedback.set({
        success: true,
        message: `✓ Scanned item '${item.productName}'. (${count}/${this.activePickSlip().items.length})`
      });
    } else {
      this.scanFeedback.set({
        success: false,
        message: `❌ Mismatch! Barcode '${clean}' is not in Pick Slip #${this.activePickSlip().pickSlipNumber}.`
      });
    }
  }

  completeFulfillmentPack() {
    alert(`Pick Slip #${this.activePickSlip().pickSlipNumber} packed! Assigned Courier Tracking #YAL-TN-${Date.now().toString().slice(-6)}. Defensive WMS updated to InTransit.`);
    this.wmsStock.update(list => list.map(item => ({
      ...item,
      committed: Math.max(0, item.committed - 1),
      inTransit: item.inTransit + 1
    })));
  }

  triggerVoiceDialerCall() {
    const newCall = {
      id: `call-${Date.now()}`,
      orderNumber: 'ORD-TN-7703',
      customerName: 'Sarra Ben Ahmed',
      customerPhone: '+21650998877',
      speechTranscript: 'Calling customer via SIP...',
      dtmfDigit: '',
      state: 'Queued',
      wmsEffect: 'Stock in Reserved state'
    };
    this.dialerAttempts.update(list => [newCall, ...list]);
    alert('Outbound Voice AI call queued for Sarra Ben Ahmed (+21650998877).');
  }

  simulateSpeechResponse(callId: string, speech: string) {
    const isConfirm = speech.includes('نعم') || speech.toLowerCase().includes('oui');
    this.dialerAttempts.update(list => list.map(c => {
      if (c.id === callId) {
        return {
          ...c,
          speechTranscript: speech,
          dtmfDigit: isConfirm ? '1' : '3',
          state: isConfirm ? 'Confirmed' : 'Cancelled',
          wmsEffect: isConfirm ? '✓ Promoted to Committed' : 'Released back to Available'
        };
      }
      return c;
    }));
  }

  settleDepositUi(c: any) {
    c.depositPaid = true;
    c.tier = 'Standard';
    alert(`Micro-Deposit of 10 TND settled via Flouci for ${c.name}. Remaining COD reduced by 10 TND. Order auto-confirmed.`);
  }

  // Smart Carrier Routing Signals & Actions
  carrierRankings = signal([
    {
      courierName: 'Yalidine Express',
      shippingCost: 7.0,
      deliveryRate: 95.0,
      expectedNetMargin: 87.8,
      isRecommended: true,
      reasoning: 'Highest delivery success (95.0%) and fastest transit (1.2 days) in Greater Tunis.'
    },
    {
      courierName: 'Aramex Tunisia',
      shippingCost: 8.0,
      deliveryRate: 88.0,
      expectedNetMargin: 79.4,
      isRecommended: false,
      reasoning: 'Alternative carrier with strong coastal reach, but higher return rate in Tunis.'
    }
  ]);

  autoAssignCarrierUi(rec: any) {
    alert(`Smart Routing applied: ${rec.courierName} assigned to all eligible pending orders. Expected net margin: +${rec.expectedNetMargin} TND.`);
  }

  // Live Social Commerce Signals & Actions
  liveDeals = signal([
    {
      sku: 'FLASH-SERUM-VITC',
      name: 'Sérum Éclat Vitamine C 30ml',
      keyword: 'ACHETER',
      flashPrice: 65,
      originalPrice: 95,
      max: 20,
      remaining: 14
    },
    {
      sku: 'FLASH-PARFUM-OUD',
      name: 'Oud Carthage Extrait 50ml',
      keyword: 'PROMO50',
      flashPrice: 85,
      originalPrice: 120,
      max: 15,
      remaining: 8
    }
  ]);

  simulateLiveComment(keyword: string) {
    const deal = this.liveDeals().find(d => d.keyword === keyword);
    if (!deal) return;

    if (deal.remaining > 0) {
      this.liveDeals.update(list => list.map(d => d.keyword === keyword ? { ...d, remaining: d.remaining - 1 } : d));
      alert(`Live comment detected ("${keyword}")! Stock reserved. 1-Click DM checkout dispatched to @viewer_tunisie!`);
    } else {
      alert(`Flash deal for ${deal.name} is SOLD OUT!`);
    }
  }
}
