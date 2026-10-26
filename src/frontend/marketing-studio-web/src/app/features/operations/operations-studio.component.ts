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
        </div>
      </main>
    </div>
  `
})
export class OperationsStudioComponent {
  activeTab = signal<'confirmation' | 'logistics' | 'wms' | 'cadences' | 'rma' | 'remittance' | 'conversions'>('confirmation');
  audioAlertsEnabled = signal<boolean>(true);

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
}
