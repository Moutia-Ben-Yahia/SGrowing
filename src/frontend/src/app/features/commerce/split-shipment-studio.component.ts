import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface WarehouseCard {
  id: string;
  name: string;
  type: string;
  city: string;
  capacity: string;
  activeOrders: number;
}

export interface RoutedPackageRow {
  tracking: string;
  warehouse: string;
  items: string;
  cost: number;
  eta: string;
}

@Component({
  selector: 'app-split-shipment-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-blue-500/10 text-blue-400 rounded-lg border border-blue-500/20 text-xl font-bold">📦</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Multi-Warehouse Split-Shipment Routing Studio</h1>
              <p class="text-slate-400 text-sm">Spree-inspired proximity inventory allocation, split-package optimization & stock rebalance mesh</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="triggerRebalance()" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-sm font-medium border border-slate-700 transition">
            🔄 Auto-Rebalance Stock
          </button>
          <button (click)="simulateRoute()" class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-sm font-medium shadow-lg shadow-blue-900/30 transition">
            + Simulate Order Route
          </button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Orders Routed (Today)</div>
          <div class="text-3xl font-extrabold text-blue-400">1,240</div>
          <div class="text-xs text-slate-400 mt-1">Across 3 regional hubs</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Single-Package Efficiency</div>
          <div class="text-3xl font-extrabold text-emerald-400">88.2%</div>
          <div class="text-xs text-emerald-400 mt-1">Only 11.8% split rate</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Average Transit Distance</div>
          <div class="text-3xl font-extrabold text-cyan-400">42.5 km</div>
          <div class="text-xs text-cyan-400 mt-1">↓ 32% vs single warehouse</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Average Shipping Cost</div>
          <div class="text-3xl font-extrabold text-amber-400">7.80 TND</div>
          <div class="text-xs text-emerald-400 mt-1">Optimized courier rate</div>
        </div>
      </div>

      <!-- Main Layout -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Active Warehouse Nodes -->
        <div class="lg:col-span-2 space-y-6">
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-4">Fulfillment Node Network</h2>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div *ngFor="let node of nodes()" class="p-4 bg-slate-800/80 border border-slate-700 rounded-lg hover:border-slate-600 transition">
                <div class="flex items-center justify-between mb-2">
                  <span class="text-sm font-bold text-white">{{ node.name }}</span>
                  <span class="text-xs px-2 py-0.5 rounded bg-blue-500/20 text-blue-300 font-semibold">{{ node.type }}</span>
                </div>
                <div class="text-xs text-slate-400 space-y-1">
                  <div>Location: <strong class="text-slate-200">{{ node.city }}</strong></div>
                  <div>Daily Capacity: {{ node.capacity }} orders</div>
                  <div>Active Load: <strong class="text-emerald-400">{{ node.activeOrders }} orders</strong></div>
                </div>
              </div>
            </div>
          </div>

          <!-- Live Split-Shipment Routing Preview -->
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-4">Latest Split-Shipment Route Execution (#ORD-9021)</h2>
            <div class="space-y-3">
              <div *ngFor="let pkg of packages()" class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg flex items-center justify-between gap-4">
                <div>
                  <div class="flex items-center gap-2 mb-1">
                    <span class="font-mono text-xs font-bold text-blue-400">{{ pkg.tracking }}</span>
                    <span class="text-xs px-1.5 py-0.5 rounded bg-emerald-500/20 text-emerald-300">{{ pkg.warehouse }}</span>
                  </div>
                  <div class="text-xs text-slate-300">Items: {{ pkg.items }}</div>
                  <div class="text-[11px] text-slate-500 mt-1">Estimated delivery: {{ pkg.eta }}</div>
                </div>
                <div class="text-right">
                  <div class="text-sm font-bold text-white">{{ pkg.cost | number:'1.2-2' }} TND</div>
                  <div class="text-[11px] text-emerald-400">Carrier Allocated</div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Sidebar: Proximity Rules & Rebalance Manifest -->
        <div class="space-y-6">
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-3">Geospatial Routing Policy</h2>
            <div class="space-y-3 text-xs text-slate-300">
              <div class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg">
                <div class="font-semibold text-white mb-1">Single-Warehouse Threshold</div>
                <p class="text-slate-400">If nearest warehouse holds 100% of order items, single package is enforced to minimize base carrier drop fees.</p>
              </div>
              <div class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg">
                <div class="font-semibold text-white mb-1">Split-Shipment Fallback</div>
                <p class="text-slate-400">If stock is fractured across regional nodes, order splits into 2 discrete carrier manifests with synchronized customer tracking.</p>
              </div>
            </div>
          </div>

          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-2">Automated Hub Rebalancing</h2>
            <p class="text-xs text-slate-400 leading-relaxed mb-3">
              When regional micro-hub safety stock dips below 10 units, nightly stock transfer manifests are automatically dispatched from the Central Mega-Hub.
            </p>
            <div class="p-3 bg-blue-500/10 border border-blue-500/20 rounded-lg text-xs text-blue-300">
              🚚 <strong>Active Transfer Manifest:</strong> 50 units (SKU-ELEC-01) en route from Tunis to Sfax.
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class SplitShipmentStudioComponent {
  nodes = signal<WarehouseCard[]>([
    { id: '1', name: 'Tunis Central Mega-Hub', type: 'CentralHub', city: 'Tunis (Postal: 1000)', capacity: '2,500', activeOrders: 1420 },
    { id: '2', name: 'Sfax Regional Center', type: 'RegionalHub', city: 'Sfax (Postal: 3000)', capacity: '800', activeOrders: 310 },
    { id: '3', name: 'Sousse Micro-Hub', type: 'UrbanMicroHub', city: 'Sousse (Postal: 4000)', capacity: '400', activeOrders: 125 }
  ]);

  packages = signal<RoutedPackageRow[]>([
    { tracking: 'SPLIT-89104', warehouse: 'Tunis Central Hub', items: '2x Wireless Earbuds (SKU-ELEC-01)', cost: 7.00, eta: 'Tomorrow, 12:00' },
    { tracking: 'SPLIT-89105', warehouse: 'Sfax Regional Hub', items: '1x Ceramic Table Lamp (SKU-HOME-03)', cost: 7.00, eta: 'Tomorrow, 16:00' }
  ]);

  triggerRebalance(): void {
    // Demonstration action
  }

  simulateRoute(): void {
    // Demonstration action
  }
}
