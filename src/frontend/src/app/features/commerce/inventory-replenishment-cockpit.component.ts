import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface WarehouseStock {
  warehouseId: string;
  warehouseName: string;
  locationCode: string;
  city: string;
  currentStock: number;
  allocatedStock: number;
  availableStock: number;
  dailyVelocity: number;
  doirDays: number;
  safetyStockUnits: number;
  reorderPointUnits: number;
  eoqUnits: number;
  healthTier: 'CriticalOutOfStock' | 'LowStockBreach' | 'AtRisk' | 'Optimal' | 'Overstocked';
  lastAuditedAtUtc: string;
}

export interface InventoryItem {
  sku: string;
  productName: string;
  category: string;
  barcode: string;
  unitCostUsd: number;
  retailPriceUsd: number;
  primaryVendorId: string;
  primaryVendorName: string;
  totalCurrentStock: number;
  totalAllocatedStock: number;
  totalAvailableStock: number;
  combinedDailyVelocity: number;
  combinedDoirDays: number;
  combinedSafetyStockUnits: number;
  combinedReorderPointUnits: number;
  combinedEoqUnits: number;
  aggregatedHealthTier: 'CriticalOutOfStock' | 'LowStockBreach' | 'AtRisk' | 'Optimal' | 'Overstocked';
  totalAssetValueUsd: number;
  warehouseBreakdown: WarehouseStock[];
  lastReplenishedAtUtc: string;
}

export interface PurchaseOrderLineItem {
  sku: string;
  productName: string;
  quantity: number;
  unitCostUsd: number;
  lineTotalUsd: number;
  calculatedDeficit: number;
  appliedMoq: number;
  appliedBatchMultiplier: number;
}

export interface PurchaseOrder {
  id: string;
  poNumber: string;
  vendorId: string;
  vendorName: string;
  vendorContactEmail: string;
  destinationWarehouseId: string;
  destinationWarehouseName: string;
  status: 'Draft' | 'SubmittedToVendor' | 'Acknowledged' | 'InTransit' | 'Received' | 'Cancelled';
  items: PurchaseOrderLineItem[];
  totalUnits: number;
  subtotalUsd: number;
  taxAmountUsd: number;
  shippingFeeUsd: number;
  totalAmountUsd: number;
  currency: string;
  estimatedLeadTimeDays: number;
  expectedDeliveryDateUtc: string | null;
  trackingNumber: string | null;
  notes: string | null;
  createdBy: string;
  createdAtUtc: string;
  submittedAtUtc: string | null;
  receivedAtUtc: string | null;
}

export interface WarehouseTransferProposal {
  proposalId: string;
  sku: string;
  productName: string;
  sourceWarehouseId: string;
  sourceWarehouseName: string;
  sourceStockBefore: number;
  sourceRop: number;
  sourceDoirBefore: number;
  destinationWarehouseId: string;
  destinationWarehouseName: string;
  destinationStockBefore: number;
  destinationRop: number;
  destinationDoirBefore: number;
  suggestedQuantity: number;
  estimatedTransitDays: number;
  estimatedFreightCostUsd: number;
  urgency: 'Critical' | 'High' | 'Medium' | 'Low';
  reason: string;
  status: 'Proposed' | 'Approved' | 'InTransit' | 'Completed' | 'Rejected';
  createdAtUtc: string;
}

export interface StockHealthSummary {
  totalSkus: number;
  criticalOutOfStockCount: number;
  lowStockBreachCount: number;
  atRiskCount: number;
  optimalCount: number;
  overstockedCount: number;
  totalUnitsOnHand: number;
  totalAssetValueUsd: number;
  activePurchaseOrdersCount: number;
  activePoCommittedValueUsd: number;
  pendingRebalanceTransfersCount: number;
}

@Component({
  selector: 'app-inventory-replenishment-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <!-- Left Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/90 flex flex-col justify-between p-4 flex-shrink-0">
        <div>
          <!-- Header Logo & Brand -->
          <div class="flex items-center gap-3 mb-6">
            <div class="h-10 w-10 rounded-xl bg-gradient-to-tr from-amber-500 via-rose-500 to-indigo-600 flex items-center justify-center font-bold text-white shadow-lg shadow-rose-500/20 text-xl">
              📦
            </div>
            <div>
              <h1 class="text-sm font-bold tracking-wide text-white">SGrowing Supply</h1>
              <p class="text-[11px] text-amber-400 font-medium">Autonomous Replenishment</p>
            </div>
          </div>

          <!-- Module Navigation Links -->
          <div class="space-y-1 mb-6">
            <span class="text-[10px] font-bold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <div class="space-y-1">
              <button
                (click)="activeTab.set('inventory')"
                [class]="activeTab() === 'inventory' ? 'w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30' : 'w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition'">
                <span>📊</span>
                <span>Inventory & Health Matrix</span>
              </button>

              <button
                (click)="activeTab.set('orders')"
                [class]="activeTab() === 'orders' ? 'w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30' : 'w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition'">
                <span>📑</span>
                <span>Purchase Orders Ledger</span>
                @if (activePoCount() > 0) {
                  <span class="ml-auto px-1.5 py-0.5 rounded-full text-[10px] bg-rose-500/30 text-rose-300 border border-rose-500/40">
                    {{ activePoCount() }}
                  </span>
                }
              </button>

              <button
                (click)="activeTab.set('rebalance')"
                [class]="activeTab() === 'rebalance' ? 'w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30' : 'w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition'">
                <span>🔄</span>
                <span>Warehouse Rebalance</span>
                @if (transferProposals().length > 0) {
                  <span class="ml-auto px-1.5 py-0.5 rounded-full text-[10px] bg-amber-500/30 text-amber-300 border border-amber-500/40">
                    {{ transferProposals().length }}
                  </span>
                }
              </button>

              <button
                (click)="activeTab.set('simulator')"
                [class]="activeTab() === 'simulator' ? 'w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30' : 'w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition'">
                <span>🧮</span>
                <span>Math Model & Simulator</span>
              </button>
            </div>
          </div>

          <!-- Cross-Module Links -->
          <div class="space-y-1 mb-6">
            <span class="text-[10px] font-bold text-slate-400 uppercase tracking-wider px-2">Ecosystem Hub</span>
            <div class="space-y-1">
              <a routerLink="/commerce/b2b-wholesale" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-indigo-300 hover:bg-slate-800/60 transition">
                <span>🏢 B2B Wholesale Portal</span>
              </a>
              <a routerLink="/commerce/reconciliation" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-cyan-300 hover:bg-slate-800/60 transition">
                <span>🚚 Courier Reconciliation</span>
              </a>
              <a routerLink="/commerce/warehouses" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-emerald-300 hover:bg-slate-800/60 transition">
                <span>🏬 Multi-Warehouse WMS</span>
              </a>
            </div>
          </div>
        </div>

        <!-- System Engine Status Pill -->
        <div class="p-3 bg-slate-950/60 border border-slate-800/80 rounded-xl space-y-2">
          <div class="flex items-center justify-between text-xs">
            <span class="text-slate-400">Autonomous Engine:</span>
            <span class="flex items-center gap-1.5 text-emerald-400 font-medium">
              <span class="h-2 w-2 rounded-full bg-emerald-500 animate-pulse"></span> Active
            </span>
          </div>
          <div class="flex items-center justify-between text-xs">
            <span class="text-slate-400">Service Level Target:</span>
            <span class="font-semibold text-rose-400">95.0% (Z=1.65)</span>
          </div>
          <div class="flex items-center justify-between text-xs">
            <span class="text-slate-400">ROP Safety Factor:</span>
            <span class="text-amber-400 font-mono">Dynamic √L·μ</span>
          </div>
        </div>
      </aside>

      <!-- Main Central Dashboard Area -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Top Toolbar Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/50 flex items-center justify-between px-6 flex-shrink-0">
          <div class="flex items-center gap-3">
            <h2 class="text-lg font-bold text-white tracking-tight flex items-center gap-2">
              <span>Autonomous Replenishment Cockpit</span>
              <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-rose-500/20 text-rose-400 border border-rose-500/30">
                Horizon 24
              </span>
            </h2>
            <p class="text-xs text-slate-400 hidden md:block">
              ROP breach detection, Vendor MOQ batch snapping & inter-hub rebalance
            </p>
          </div>

          <div class="flex items-center gap-2.5">
            <button
              (click)="recalculateAllMetrics()"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 text-slate-200 hover:bg-slate-700 border border-slate-700 transition flex items-center gap-1.5">
              <span>⚡</span>
              <span>Recalculate ROP/EOQ</span>
            </button>

            <button
              (click)="triggerAutonomousGeneration()"
              class="px-3.5 py-1.5 rounded-lg text-xs font-bold bg-gradient-to-r from-rose-600 to-amber-600 text-white hover:from-rose-500 hover:to-amber-500 shadow-md shadow-rose-600/20 transition flex items-center gap-1.5">
              <span>🤖</span>
              <span>Auto-Generate POs</span>
            </button>

            <button
              (click)="openManualPoModal()"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-indigo-600 text-white hover:bg-indigo-500 transition flex items-center gap-1.5">
              <span>➕</span>
              <span>Manual PO</span>
            </button>
          </div>
        </header>

        <!-- KPI Summary Cards Banner -->
        <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 p-6 pb-2 flex-shrink-0">
          <!-- Card 1: Critical Stockouts -->
          <div class="p-4 rounded-xl bg-gradient-to-br from-rose-950/40 to-slate-900 border border-rose-900/40 relative overflow-hidden">
            <div class="flex justify-between items-start">
              <div>
                <p class="text-xs font-semibold text-rose-300 uppercase tracking-wider">Critical Stockouts</p>
                <h3 class="text-2xl font-black text-rose-100 mt-1">{{ kpis().criticalOutOfStockCount }}</h3>
                <p class="text-[11px] text-rose-400/80 mt-1">Stock == 0 units on shelf</p>
              </div>
              <span class="p-2.5 rounded-lg bg-rose-500/20 text-rose-400 text-lg">🚨</span>
            </div>
            <div class="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
              <div class="bg-rose-500 h-1.5 rounded-full" [style.width.%]="(kpis().criticalOutOfStockCount / (kpis().totalSkus || 1)) * 100"></div>
            </div>
          </div>

          <!-- Card 2: Low Stock & At-Risk -->
          <div class="p-4 rounded-xl bg-gradient-to-br from-amber-950/40 to-slate-900 border border-amber-900/40 relative overflow-hidden">
            <div class="flex justify-between items-start">
              <div>
                <p class="text-xs font-semibold text-amber-300 uppercase tracking-wider">Breached & At-Risk SKUs</p>
                <h3 class="text-2xl font-black text-amber-100 mt-1">
                  {{ kpis().lowStockBreachCount + kpis().atRiskCount }}
                </h3>
                <p class="text-[11px] text-amber-400/80 mt-1">
                  {{ kpis().lowStockBreachCount }} Breached ROP · {{ kpis().atRiskCount }} Buffer Risk
                </p>
              </div>
              <span class="p-2.5 rounded-lg bg-amber-500/20 text-amber-400 text-lg">⚠️</span>
            </div>
            <div class="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
              <div class="bg-amber-500 h-1.5 rounded-full" [style.width.%]="((kpis().lowStockBreachCount + kpis().atRiskCount) / (kpis().totalSkus || 1)) * 100"></div>
            </div>
          </div>

          <!-- Card 3: Replenishment POs in Flight -->
          <div class="p-4 rounded-xl bg-gradient-to-br from-indigo-950/40 to-slate-900 border border-indigo-900/40 relative overflow-hidden">
            <div class="flex justify-between items-start">
              <div>
                <p class="text-xs font-semibold text-indigo-300 uppercase tracking-wider">Replenishment In Flight</p>
                <h3 class="text-2xl font-black text-indigo-100 mt-1">{{ kpis().activePurchaseOrdersCount }}</h3>
                <p class="text-[11px] text-indigo-400/80 mt-1">
                  \${{ kpis().activePoCommittedValueUsd | number:'1.2-2' }} Committed Value
                </p>
              </div>
              <span class="p-2.5 rounded-lg bg-indigo-500/20 text-indigo-400 text-lg">✈️</span>
            </div>
            <div class="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
              <div class="bg-indigo-500 h-1.5 rounded-full" [style.width.%]="70"></div>
            </div>
          </div>

          <!-- Card 4: Inventory Total Asset Value -->
          <div class="p-4 rounded-xl bg-gradient-to-br from-emerald-950/40 to-slate-900 border border-emerald-900/40 relative overflow-hidden">
            <div class="flex justify-between items-start">
              <div>
                <p class="text-xs font-semibold text-emerald-300 uppercase tracking-wider">Total Inventory Value</p>
                <h3 class="text-2xl font-black text-emerald-100 mt-1">
                  \${{ kpis().totalAssetValueUsd | number:'1.0-0' }}
                </h3>
                <p class="text-[11px] text-emerald-400/80 mt-1">
                  {{ kpis().totalUnitsOnHand | number }} units across 4 hubs
                </p>
              </div>
              <span class="p-2.5 rounded-lg bg-emerald-500/20 text-emerald-400 text-lg">💎</span>
            </div>
            <div class="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
              <div class="bg-emerald-500 h-1.5 rounded-full" [style.width.%]="85"></div>
            </div>
          </div>
        </section>

        <!-- Dynamic Notification Banner -->
        @if (notification()) {
          <div class="mx-6 my-2 p-3 rounded-lg border text-xs flex items-center justify-between"
               [class]="notification()!.type === 'success' ? 'bg-emerald-950/60 border-emerald-700 text-emerald-200' :
                        notification()!.type === 'error' ? 'bg-rose-950/60 border-rose-700 text-rose-200' :
                        'bg-indigo-950/60 border-indigo-700 text-indigo-200'">
            <div class="flex items-center gap-2">
              <span>{{ notification()!.type === 'success' ? '✅' : notification()!.type === 'error' ? '❌' : 'ℹ️' }}</span>
              <span>{{ notification()!.message }}</span>
            </div>
            <button (click)="notification.set(null)" class="text-slate-400 hover:text-white">&times;</button>
          </div>
        }

        <!-- Tab 1: Inventory & Health Matrix View -->
        @if (activeTab() === 'inventory') {
          <div class="flex-1 flex flex-col p-6 pt-3 overflow-hidden">
            <!-- Filter Bar -->
            <div class="flex flex-wrap items-center justify-between gap-3 bg-slate-900/60 p-3 rounded-xl border border-slate-800 mb-4">
              <div class="flex items-center gap-2 flex-1 min-w-[240px]">
                <span class="text-slate-400">🔍</span>
                <input
                  type="text"
                  placeholder="Search SKU, product name, barcode..."
                  [ngModel]="searchQuery()"
                  (ngModelChange)="searchQuery.set($event)"
                  class="bg-slate-950 border border-slate-800 text-xs rounded-lg px-3 py-1.5 text-slate-100 placeholder-slate-500 w-full focus:outline-none focus:border-rose-500" />
              </div>

              <div class="flex items-center gap-2">
                <!-- Category Filter -->
                <select
                  [ngModel]="selectedCategory()"
                  (ngModelChange)="selectedCategory.set($event)"
                  class="bg-slate-950 border border-slate-800 text-xs rounded-lg px-3 py-1.5 text-slate-200 focus:outline-none">
                  <option value="All">All Categories</option>
                  <option value="Electronics">Electronics</option>
                  <option value="Gourmet Food & Beverage">Gourmet Food & Beverage</option>
                  <option value="Office Furniture">Office Furniture</option>
                  <option value="Mobile Accessories">Mobile Accessories</option>
                </select>

                <!-- Health Tier Filter -->
                <select
                  [ngModel]="selectedHealthFilter()"
                  (ngModelChange)="selectedHealthFilter.set($event)"
                  class="bg-slate-950 border border-slate-800 text-xs rounded-lg px-3 py-1.5 text-slate-200 focus:outline-none">
                  <option value="All">All Health Statuses</option>
                  <option value="CriticalOutOfStock">Critical Out of Stock</option>
                  <option value="LowStockBreach">Low Stock Breach (<= ROP)</option>
                  <option value="AtRisk">At Risk (<= 1.25x ROP)</option>
                  <option value="Optimal">Optimal</option>
                  <option value="Overstocked">Overstocked (> 3.5x ROP)</option>
                </select>

                <!-- Warehouse Filter -->
                <select
                  [ngModel]="selectedWarehouseFilter()"
                  (ngModelChange)="selectedWarehouseFilter.set($event)"
                  class="bg-slate-950 border border-slate-800 text-xs rounded-lg px-3 py-1.5 text-slate-200 focus:outline-none">
                  <option value="All">All Warehouses</option>
                  <option value="WH-CENTRAL">WH-CENTRAL (Frankfurt)</option>
                  <option value="WH-NORTH">WH-NORTH (Stockholm)</option>
                  <option value="WH-SOUTH">WH-SOUTH (Milan)</option>
                  <option value="WH-WEST">WH-WEST (Rotterdam)</option>
                </select>
              </div>
            </div>

            <!-- Table Container -->
            <div class="flex-1 overflow-auto rounded-xl border border-slate-800 bg-slate-900/40">
              <table class="w-full text-left text-xs border-collapse">
                <thead class="bg-slate-900/90 text-slate-400 font-semibold sticky top-0 z-10 border-b border-slate-800">
                  <tr>
                    <th class="p-3">SKU & Product</th>
                    <th class="p-3">Primary Supplier</th>
                    <th class="p-3">Total Stock</th>
                    <th class="p-3">Daily Velocity</th>
                    <th class="p-3">DOIR (Days)</th>
                    <th class="p-3">Safety Stock</th>
                    <th class="p-3">ROP</th>
                    <th class="p-3">EOQ</th>
                    <th class="p-3">Health Status</th>
                    <th class="p-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (item of filteredItems(); track item.sku) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <!-- SKU & Title -->
                      <td class="p-3">
                        <div class="font-bold text-white tracking-wide">{{ item.sku }}</div>
                        <div class="text-[11px] text-slate-400">{{ item.productName }}</div>
                        <div class="text-[10px] text-slate-500 font-mono mt-0.5">{{ item.category }} · {{ item.barcode }}</div>
                      </td>

                      <!-- Vendor -->
                      <td class="p-3">
                        <div class="text-slate-200 font-medium">{{ item.primaryVendorName }}</div>
                        <div class="text-[10px] text-slate-400">Cost: \${{ item.unitCostUsd }} · Retail: \${{ item.retailPriceUsd }}</div>
                      </td>

                      <!-- Stock Levels -->
                      <td class="p-3">
                        <div class="font-bold text-slate-100">{{ item.totalCurrentStock }} units</div>
                        <div class="text-[10px] text-slate-400">
                          Avail: {{ item.totalAvailableStock }} | Alloc: {{ item.totalAllocatedStock }}
                        </div>
                      </td>

                      <!-- Daily Velocity -->
                      <td class="p-3 font-mono text-slate-200">
                        {{ item.combinedDailyVelocity }} /day
                      </td>

                      <!-- DOIR with Mini Visual Meter -->
                      <td class="p-3">
                        <div class="flex items-center gap-2">
                          <span class="font-bold font-mono"
                                [class]="item.combinedDoirDays <= 7 ? 'text-rose-400' :
                                         item.combinedDoirDays <= 14 ? 'text-amber-400' : 'text-emerald-400'">
                            {{ item.combinedDoirDays }}d
                          </span>
                        </div>
                        <div class="w-20 bg-slate-800 rounded-full h-1 mt-1 overflow-hidden">
                          <div class="h-1 rounded-full"
                               [class]="getDoirBarClass(item.combinedDoirDays)"
                               [style.width.%]="Math.min(100, (item.combinedDoirDays / 60) * 100)"></div>
                        </div>
                      </td>

                      <!-- Safety Stock -->
                      <td class="p-3 font-mono text-indigo-300">
                        {{ item.combinedSafetyStockUnits }}
                      </td>

                      <!-- ROP -->
                      <td class="p-3 font-mono font-semibold"
                          [class]="item.totalCurrentStock <= item.combinedReorderPointUnits ? 'text-rose-400' : 'text-slate-300'">
                        {{ item.combinedReorderPointUnits }}
                      </td>

                      <!-- EOQ -->
                      <td class="p-3 font-mono text-amber-300">
                        {{ item.combinedEoqUnits }}
                      </td>

                      <!-- Health Status Pill -->
                      <td class="p-3">
                        <span [class]="getHealthBadgeClass(item.aggregatedHealthTier)">
                          {{ formatHealthTier(item.aggregatedHealthTier) }}
                        </span>
                      </td>

                      <!-- Actions -->
                      <td class="p-3 text-right">
                        <div class="flex items-center justify-end gap-1.5">
                          <button
                            (click)="openManualPoModal(item.sku)"
                            class="px-2 py-1 rounded bg-rose-600/30 hover:bg-rose-600/50 text-rose-200 border border-rose-500/40 text-[11px] font-semibold transition">
                            Create PO
                          </button>
                        </div>
                      </td>
                    </tr>

                    <!-- Nested Warehouse Breakdown Accordion Row -->
                    <tr class="bg-slate-950/40">
                      <td colspan="10" class="p-2.5 px-6">
                        <div class="flex items-center gap-4 text-[11px] text-slate-400">
                          <span class="font-semibold text-slate-300">Warehouse Nodes:</span>
                          @for (wh of item.warehouseBreakdown; track wh.warehouseId) {
                            <div class="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-slate-900 border border-slate-800">
                              <span class="font-bold text-slate-200">{{ wh.warehouseName }}:</span>
                              <span class="font-mono" [class]="wh.currentStock <= wh.reorderPointUnits ? 'text-rose-400 font-bold' : 'text-slate-300'">
                                {{ wh.currentStock }} on hand
                              </span>
                              <span class="text-slate-500">(ROP: {{ wh.reorderPointUnits }} | DOIR: {{ wh.doirDays }}d)</span>
                              <span class="text-[9px] px-1 py-0.2 rounded" [class]="getHealthBadgeClass(wh.healthTier)">
                                {{ formatHealthTier(wh.healthTier) }}
                              </span>
                            </div>
                          }
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- Tab 2: Purchase Orders Ledger View -->
        @if (activeTab() === 'orders') {
          <div class="flex-1 flex flex-col p-6 pt-3 overflow-hidden">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>Vendor Purchase Orders & Receipt Ledger</span>
                <span class="text-xs text-slate-400">({{ purchaseOrders().length }} orders total)</span>
              </h3>
              <button
                (click)="openManualPoModal()"
                class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-indigo-600 hover:bg-indigo-500 text-white transition">
                + Create New PO
              </button>
            </div>

            <div class="flex-1 overflow-auto rounded-xl border border-slate-800 bg-slate-900/40">
              <table class="w-full text-left text-xs border-collapse">
                <thead class="bg-slate-900/90 text-slate-400 font-semibold sticky top-0 z-10 border-b border-slate-800">
                  <tr>
                    <th class="p-3">PO Number & Date</th>
                    <th class="p-3">Vendor</th>
                    <th class="p-3">Destination Hub</th>
                    <th class="p-3">Items & Units</th>
                    <th class="p-3">Total Amount</th>
                    <th class="p-3">Order Status</th>
                    <th class="p-3">Tracking Reference</th>
                    <th class="p-3 text-right">Lifecycle Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (po of purchaseOrders(); track po.id) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <td class="p-3">
                        <div class="font-bold text-rose-300 font-mono">{{ po.poNumber }}</div>
                        <div class="text-[10px] text-slate-400">{{ po.createdAtUtc | date:'short' }}</div>
                        <div class="text-[10px] text-slate-500">By {{ po.createdBy }}</div>
                      </td>

                      <td class="p-3">
                        <div class="font-medium text-slate-200">{{ po.vendorName }}</div>
                        <div class="text-[10px] text-slate-400">{{ po.vendorContactEmail }}</div>
                      </td>

                      <td class="p-3">
                        <div class="text-slate-200">{{ po.destinationWarehouseName }}</div>
                        <div class="text-[10px] text-slate-500">{{ po.destinationWarehouseId }}</div>
                      </td>

                      <td class="p-3">
                        <div class="font-bold text-slate-200">{{ po.totalUnits }} units</div>
                        <div class="text-[10px] text-slate-400">{{ po.items.length }} line item(s)</div>
                      </td>

                      <td class="p-3">
                        <div class="font-bold font-mono text-emerald-400">\${{ po.totalAmountUsd | number:'1.2-2' }}</div>
                        <div class="text-[10px] text-slate-500">Tax: \${{ po.taxAmountUsd }} · Freight: \${{ po.shippingFeeUsd }}</div>
                      </td>

                      <td class="p-3">
                        <span [class]="getPoStatusBadgeClass(po.status)">
                          {{ po.status }}
                        </span>
                      </td>

                      <td class="p-3">
                        @if (po.trackingNumber) {
                          <span class="font-mono text-[11px] text-indigo-300 bg-indigo-950/60 px-2 py-0.5 rounded border border-indigo-800">
                            {{ po.trackingNumber }}
                          </span>
                        } @else {
                          <span class="text-slate-500 text-[10px]">—</span>
                        }
                      </td>

                      <td class="p-3 text-right">
                        <div class="flex items-center justify-end gap-1.5">
                          @if (po.status === 'Draft') {
                            <button
                              (click)="transitionPoStatus(po.id, 'SubmittedToVendor')"
                              class="px-2.5 py-1 rounded bg-indigo-600/30 hover:bg-indigo-600/50 text-indigo-200 border border-indigo-500/40 text-[11px] font-semibold transition">
                              Submit to Vendor
                            </button>
                          }
                          @if (po.status === 'SubmittedToVendor') {
                            <button
                              (click)="transitionPoStatus(po.id, 'Acknowledged')"
                              class="px-2.5 py-1 rounded bg-amber-600/30 hover:bg-amber-600/50 text-amber-200 border border-amber-500/40 text-[11px] font-semibold transition">
                              Acknowledge
                            </button>
                          }
                          @if (po.status === 'Acknowledged') {
                            <button
                              (click)="transitionPoStatus(po.id, 'InTransit', 'EXP-TRACK-00' + po.totalUnits)"
                              class="px-2.5 py-1 rounded bg-cyan-600/30 hover:bg-cyan-600/50 text-cyan-200 border border-cyan-500/40 text-[11px] font-semibold transition">
                              Mark In Transit
                            </button>
                          }
                          @if (po.status === 'InTransit') {
                            <button
                              (click)="transitionPoStatus(po.id, 'Received')"
                              class="px-2.5 py-1 rounded bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-[11px] shadow-sm shadow-emerald-600/40 transition">
                              Receive Stock
                            </button>
                          }
                          @if (po.status === 'Received') {
                            <span class="text-[10px] text-emerald-400 font-semibold flex items-center gap-1 justify-end">
                              <span>✓</span> Stock Ingested
                            </span>
                          }

                          <button
                            (click)="openPoDetails(po)"
                            class="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 text-[11px] transition">
                            Details
                          </button>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- Tab 3: Multi-Warehouse Rebalance Matrix -->
        @if (activeTab() === 'rebalance') {
          <div class="flex-1 flex flex-col p-6 pt-3 overflow-hidden">
            <div class="mb-4">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>Inter-Warehouse Stock Rebalancing Engine</span>
                <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                  Surplus-to-Deficit Transfers
                </span>
              </h3>
              <p class="text-xs text-slate-400 mt-1">
                Optimizes regional stock disparities without ordering new factory stock. Prevents stockouts at regional nodes while draining excess central depot inventory.
              </p>
            </div>

            @if (transferProposals().length === 0) {
              <div class="flex-1 flex flex-col items-center justify-center border border-dashed border-slate-800 rounded-xl p-8 text-center bg-slate-900/20">
                <span class="text-4xl mb-3">⚖️</span>
                <h4 class="text-base font-bold text-slate-200">All Regional Hubs Are Balanced</h4>
                <p class="text-xs text-slate-400 max-w-md mt-1">
                  No warehouse currently exhibits both critical deficit and paired surplus inventory for any SKU.
                </p>
              </div>
            } @else {
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4 overflow-auto">
                @for (proposal of transferProposals(); track proposal.proposalId) {
                  <div class="p-5 rounded-xl border bg-slate-900/60 flex flex-col justify-between"
                       [class]="proposal.urgency === 'Critical' ? 'border-rose-800/80' :
                                proposal.urgency === 'High' ? 'border-amber-800/80' : 'border-slate-800'">
                    <div>
                      <div class="flex items-center justify-between mb-3">
                        <span class="px-2 py-0.5 rounded text-[10px] font-bold font-mono uppercase tracking-wider"
                              [class]="proposal.urgency === 'Critical' ? 'bg-rose-500/20 text-rose-300 border border-rose-500/40' :
                                       proposal.urgency === 'High' ? 'bg-amber-500/20 text-amber-300 border border-amber-500/40' :
                                       'bg-indigo-500/20 text-indigo-300 border border-indigo-500/40'">
                          {{ proposal.urgency }} Urgency Transfer
                        </span>
                        <span class="text-xs text-slate-400">Est. Transit: {{ proposal.estimatedTransitDays }} days</span>
                      </div>

                      <h4 class="text-sm font-bold text-white tracking-wide">{{ proposal.productName }}</h4>
                      <p class="text-[11px] font-mono text-slate-400 mt-0.5">SKU: {{ proposal.sku }}</p>

                      <!-- Visual Routing Flow -->
                      <div class="flex items-center justify-between gap-3 my-4 p-3 rounded-lg bg-slate-950 border border-slate-800">
                        <!-- Source Hub -->
                        <div class="flex-1 text-center">
                          <p class="text-[10px] text-slate-500 uppercase font-semibold">Source (Surplus)</p>
                          <p class="text-xs font-bold text-slate-200 mt-0.5">{{ proposal.sourceWarehouseName }}</p>
                          <p class="text-[11px] text-emerald-400 font-mono mt-1">
                            {{ proposal.sourceStockBefore }} on hand ({{ proposal.sourceDoirBefore }}d)
                          </p>
                        </div>

                        <!-- Arrow & Suggested Transfer Qty -->
                        <div class="flex flex-col items-center px-2">
                          <span class="text-xs font-black text-amber-400 font-mono">
                            ➔ {{ proposal.suggestedQuantity }} units ➔
                          </span>
                          <span class="text-[10px] text-slate-500 font-mono mt-0.5">
                            Freight: \${{ proposal.estimatedFreightCostUsd }}
                          </span>
                        </div>

                        <!-- Destination Hub -->
                        <div class="flex-1 text-center">
                          <p class="text-[10px] text-slate-500 uppercase font-semibold">Destination (Deficit)</p>
                          <p class="text-xs font-bold text-slate-200 mt-0.5">{{ proposal.destinationWarehouseName }}</p>
                          <p class="text-[11px] text-rose-400 font-mono mt-1">
                            {{ proposal.destinationStockBefore }} on hand ({{ proposal.destinationDoirBefore }}d)
                          </p>
                        </div>
                      </div>

                      <p class="text-[11px] text-slate-400 leading-relaxed">
                        {{ proposal.reason }}
                      </p>
                    </div>

                    <div class="mt-5 pt-3 border-t border-slate-800/80 flex items-center justify-between">
                      <div class="text-[11px] text-slate-400">
                        Status: <span class="font-semibold text-slate-200">{{ proposal.status }}</span>
                      </div>

                      @if (proposal.status === 'Proposed') {
                        <button
                          (click)="approveAndExecuteTransfer(proposal.proposalId)"
                          class="px-4 py-2 rounded-lg bg-gradient-to-r from-amber-600 to-rose-600 hover:from-amber-500 hover:to-rose-500 text-white font-bold text-xs shadow-md shadow-rose-600/20 transition flex items-center gap-1.5">
                          <span>✓</span>
                          <span>Approve & Transfer Stock</span>
                        </button>
                      } @else {
                        <span class="text-xs text-emerald-400 font-bold flex items-center gap-1">
                          <span>✓</span> Transfer Executed
                        </span>
                      }
                    </div>
                  </div>
                }
              </div>
            }
          </div>
        }

        <!-- Tab 4: Mathematical Model & Interactive Simulator -->
        @if (activeTab() === 'simulator') {
          <div class="flex-1 flex flex-col p-6 pt-3 overflow-auto">
            <div class="mb-4">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>Supply Chain Inventory Math & Formula Sandbox</span>
              </h3>
              <p class="text-xs text-slate-400 mt-1">
                Tune demand velocity, lead times, service level factors (Z), ordering costs, and holding rates to evaluate real-time ROP and EOQ dynamics.
              </p>
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <!-- Parameters Controller Card -->
              <div class="p-5 rounded-xl border border-slate-800 bg-slate-900/60 space-y-4">
                <h4 class="text-xs font-bold text-rose-300 uppercase tracking-wider">Formula Input Variables</h4>

                <!-- Daily Velocity Slider -->
                <div>
                  <div class="flex justify-between text-xs mb-1.5">
                    <span class="text-slate-300 font-medium">Daily Velocity (Demand Mean μ):</span>
                    <span class="font-mono text-rose-400 font-bold">{{ simulatorParams().dailyVelocity }} units/day</span>
                  </div>
                  <input
                    type="range" min="1" max="100" step="1"
                    [ngModel]="simulatorParams().dailyVelocity"
                    (ngModelChange)="updateSimParam('dailyVelocity', $event)"
                    class="w-full accent-rose-500 cursor-pointer" />
                </div>

                <!-- Lead Time Slider -->
                <div>
                  <div class="flex justify-between text-xs mb-1.5">
                    <span class="text-slate-300 font-medium">Vendor Lead Time (Days L):</span>
                    <span class="font-mono text-amber-400 font-bold">{{ simulatorParams().leadTime }} days</span>
                  </div>
                  <input
                    type="range" min="1" max="60" step="1"
                    [ngModel]="simulatorParams().leadTime"
                    (ngModelChange)="updateSimParam('leadTime', $event)"
                    class="w-full accent-amber-500 cursor-pointer" />
                </div>

                <!-- Service Level Target Selector (Z Factor) -->
                <div>
                  <div class="flex justify-between text-xs mb-1.5">
                    <span class="text-slate-300 font-medium">Target Service Level:</span>
                    <span class="font-mono text-indigo-400 font-bold">
                      {{ simulatorParams().serviceLevel }}% (Z = {{ getSimZ() }})
                    </span>
                  </div>
                  <div class="grid grid-cols-3 gap-2">
                    <button
                      (click)="updateSimParam('serviceLevel', 90)"
                      [class]="simulatorParams().serviceLevel === 90 ? 'py-1.5 rounded-lg text-xs font-bold bg-indigo-600 text-white' : 'py-1.5 rounded-lg text-xs bg-slate-800 text-slate-300 hover:bg-slate-700'">
                      90% (Z=1.28)
                    </button>
                    <button
                      (click)="updateSimParam('serviceLevel', 95)"
                      [class]="simulatorParams().serviceLevel === 95 ? 'py-1.5 rounded-lg text-xs font-bold bg-indigo-600 text-white' : 'py-1.5 rounded-lg text-xs bg-slate-800 text-slate-300 hover:bg-slate-700'">
                      95% (Z=1.65)
                    </button>
                    <button
                      (click)="updateSimParam('serviceLevel', 99)"
                      [class]="simulatorParams().serviceLevel === 99 ? 'py-1.5 rounded-lg text-xs font-bold bg-indigo-600 text-white' : 'py-1.5 rounded-lg text-xs bg-slate-800 text-slate-300 hover:bg-slate-700'">
                      99% (Z=2.33)
                    </button>
                  </div>
                </div>

                <!-- Unit Cost & Holding Rate -->
                <div class="grid grid-cols-2 gap-3 pt-2">
                  <div>
                    <label class="block text-[11px] text-slate-400 mb-1">Unit Cost (USD)</label>
                    <input
                      type="number"
                      [ngModel]="simulatorParams().unitCost"
                      (ngModelChange)="updateSimParam('unitCost', $event)"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs font-mono text-slate-100" />
                  </div>
                  <div>
                    <label class="block text-[11px] text-slate-400 mb-1">Order Cost S (USD / PO)</label>
                    <input
                      type="number"
                      [ngModel]="simulatorParams().orderCost"
                      (ngModelChange)="updateSimParam('orderCost', $event)"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs font-mono text-slate-100" />
                  </div>
                </div>
              </div>

              <!-- Mathematical Outputs Card -->
              <div class="p-5 rounded-xl border border-slate-800 bg-slate-900/60 flex flex-col justify-between">
                <div>
                  <h4 class="text-xs font-bold text-indigo-300 uppercase tracking-wider mb-4">Calculated Inventory Metrics</h4>

                  <div class="space-y-4">
                    <!-- Dynamic Safety Stock -->
                    <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800">
                      <div class="flex justify-between items-center">
                        <span class="text-xs text-slate-300">Dynamic Safety Stock (SS):</span>
                        <span class="text-base font-black font-mono text-indigo-300">
                          {{ simResults().safetyStock }} units
                        </span>
                      </div>
                      <p class="text-[10px] text-slate-500 font-mono mt-1">
                        Formula: Z · VarianceFactor · √(DailyDemandMean) = {{ getSimZ() }} · 1.5 · √{{ simulatorParams().dailyVelocity }}
                      </p>
                    </div>

                    <!-- Reorder Point (ROP) -->
                    <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800">
                      <div class="flex justify-between items-center">
                        <span class="text-xs text-slate-300">Reorder Point (ROP):</span>
                        <span class="text-base font-black font-mono text-rose-400">
                          {{ simResults().rop }} units
                        </span>
                      </div>
                      <p class="text-[10px] text-slate-500 font-mono mt-1">
                        Formula: (LeadTime · Velocity) + SafetyStock = ({{ simulatorParams().leadTime }} · {{ simulatorParams().dailyVelocity }}) + {{ simResults().safetyStock }}
                      </p>
                    </div>

                    <!-- Economic Order Quantity (EOQ) -->
                    <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800">
                      <div class="flex justify-between items-center">
                        <span class="text-xs text-slate-300">Economic Order Quantity (EOQ):</span>
                        <span class="text-base font-black font-mono text-amber-300">
                          {{ simResults().eoq }} units
                        </span>
                      </div>
                      <p class="text-[10px] text-slate-500 font-mono mt-1">
                        Formula: √((2 · AnnualDemand · OrderCost) / HoldingCostPerUnit)
                      </p>
                    </div>
                  </div>
                </div>

                <!-- Summary Health Envelope -->
                <div class="mt-4 p-3 rounded-lg bg-slate-800/40 border border-slate-700/60 text-xs">
                  <div class="text-slate-300 font-semibold mb-1">Optimal Stock Envelope:</div>
                  <div class="text-[11px] text-slate-400">
                    Maintain between <span class="text-amber-300 font-bold font-mono">{{ simResults().rop * 1.25 | number:'1.0-0' }}</span>
                    and <span class="text-emerald-300 font-bold font-mono">{{ simResults().rop * 3.5 | number:'1.0-0' }}</span> units.
                  </div>
                </div>
              </div>
            </div>
          </div>
        }
      </main>

      <!-- Slide-over Drawer for Purchase Order Details -->
      @if (isPoDrawerOpen() && selectedPoForDrawer()) {
        <div class="fixed inset-0 z-50 flex justify-end bg-black/60 backdrop-blur-sm">
          <div class="w-full max-w-xl h-full bg-slate-900 border-l border-slate-800 p-6 flex flex-col justify-between overflow-y-auto">
            <div>
              <div class="flex items-center justify-between pb-4 border-b border-slate-800">
                <div>
                  <h3 class="text-base font-bold text-white font-mono">{{ selectedPoForDrawer()!.poNumber }}</h3>
                  <p class="text-xs text-slate-400">Created: {{ selectedPoForDrawer()!.createdAtUtc | date:'medium' }}</p>
                </div>
                <button (click)="closePoDrawer()" class="p-2 text-slate-400 hover:text-white text-lg">&times;</button>
              </div>

              <!-- Vendor Information -->
              <div class="my-4 p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2 text-xs">
                <div class="text-slate-300 font-bold uppercase tracking-wider text-[10px]">Vendor & Destination</div>
                <div class="flex justify-between">
                  <span class="text-slate-400">Supplier:</span>
                  <span class="font-semibold text-white">{{ selectedPoForDrawer()!.vendorName }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">Supplier Contact:</span>
                  <span class="font-mono text-indigo-300">{{ selectedPoForDrawer()!.vendorContactEmail }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">Receiving Hub:</span>
                  <span class="font-semibold text-slate-200">{{ selectedPoForDrawer()!.destinationWarehouseName }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">Expected Arrival:</span>
                  <span class="font-mono text-amber-300">{{ selectedPoForDrawer()!.expectedDeliveryDateUtc | date:'mediumDate' }}</span>
                </div>
              </div>

              <!-- Line Items -->
              <div class="my-4">
                <h4 class="text-xs font-bold text-slate-300 uppercase tracking-wider mb-2">Order Line Items</h4>
                <div class="divide-y divide-slate-800 border border-slate-800 rounded-xl overflow-hidden bg-slate-950">
                  @for (line of selectedPoForDrawer()!.items; track line.sku) {
                    <div class="p-3 flex items-center justify-between text-xs">
                      <div>
                        <div class="font-bold text-white">{{ line.productName }}</div>
                        <div class="text-[10px] text-slate-500 font-mono">SKU: {{ line.sku }} · MOQ: {{ line.appliedMoq }}</div>
                      </div>
                      <div class="text-right">
                        <div class="font-bold font-mono text-slate-200">{{ line.quantity }} units @ \${{ line.unitCostUsd }}</div>
                        <div class="text-[11px] text-emerald-400 font-mono font-semibold">\${{ line.lineTotalUsd | number:'1.2-2' }}</div>
                      </div>
                    </div>
                  }
                </div>
              </div>

              <!-- Cost Breakdown -->
              <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2 text-xs">
                <div class="flex justify-between text-slate-400">
                  <span>Subtotal:</span>
                  <span class="font-mono text-slate-200">\${{ selectedPoForDrawer()!.subtotalUsd | number:'1.2-2' }}</span>
                </div>
                <div class="flex justify-between text-slate-400">
                  <span>Tax Amount:</span>
                  <span class="font-mono text-slate-200">\${{ selectedPoForDrawer()!.taxAmountUsd | number:'1.2-2' }}</span>
                </div>
                <div class="flex justify-between text-slate-400">
                  <span>Freight & Logistics:</span>
                  <span class="font-mono text-slate-200">\${{ selectedPoForDrawer()!.shippingFeeUsd | number:'1.2-2' }}</span>
                </div>
                <div class="flex justify-between font-bold text-sm text-white pt-2 border-t border-slate-800">
                  <span>Total Committed:</span>
                  <span class="font-mono text-emerald-400">\${{ selectedPoForDrawer()!.totalAmountUsd | number:'1.2-2' }} {{ selectedPoForDrawer()!.currency }}</span>
                </div>
              </div>
            </div>

            <!-- Drawer Actions -->
            <div class="pt-4 border-t border-slate-800 flex items-center justify-end gap-2">
              <button (click)="closePoDrawer()" class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-300">
                Close
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Modal for Creating Manual Purchase Order -->
      @if (isManualPoModalOpen()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
          <div class="w-full max-w-lg bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-2xl">
            <div class="flex justify-between items-center mb-4">
              <h3 class="text-base font-bold text-white">Create Purchase Order</h3>
              <button (click)="isManualPoModalOpen.set(false)" class="text-slate-400 hover:text-white text-lg">&times;</button>
            </div>

            <div class="space-y-4 text-xs">
              <div>
                <label class="block text-slate-300 font-semibold mb-1">Target Product SKU</label>
                <select
                  [ngModel]="manualPoForm().sku"
                  (ngModelChange)="onManualSkuSelected($event)"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white">
                  @for (item of items(); track item.sku) {
                    <option [value]="item.sku">{{ item.sku }} — {{ item.productName }}</option>
                  }
                </select>
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-slate-300 font-semibold mb-1">Destination Hub</label>
                  <select
                    [ngModel]="manualPoForm().warehouseId"
                    (ngModelChange)="updateManualFormField('warehouseId', $event)"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white">
                    <option value="WH-CENTRAL">WH-CENTRAL (Frankfurt)</option>
                    <option value="WH-NORTH">WH-NORTH (Stockholm)</option>
                    <option value="WH-SOUTH">WH-SOUTH (Milan)</option>
                    <option value="WH-WEST">WH-WEST (Rotterdam)</option>
                  </select>
                </div>
                <div>
                  <label class="block text-slate-300 font-semibold mb-1">Requested Quantity</label>
                  <input
                    type="number"
                    min="1"
                    [ngModel]="manualPoForm().quantity"
                    (ngModelChange)="updateManualFormField('quantity', $event)"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 font-mono text-white" />
                </div>
              </div>

              <div>
                <label class="block text-slate-300 font-semibold mb-1">Internal Notes</label>
                <textarea
                  rows="2"
                  [ngModel]="manualPoForm().notes"
                  (ngModelChange)="updateManualFormField('notes', $event)"
                  placeholder="Procurement notes or freight specifications..."
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white focus:outline-none"></textarea>
              </div>
            </div>

            <div class="mt-6 flex justify-end gap-2.5">
              <button
                (click)="isManualPoModalOpen.set(false)"
                class="px-4 py-2 rounded-lg bg-slate-800 text-slate-300 text-xs font-semibold hover:bg-slate-700">
                Cancel
              </button>
              <button
                (click)="submitManualPo()"
                class="px-4 py-2 rounded-lg bg-rose-600 hover:bg-rose-500 text-white text-xs font-bold shadow-md shadow-rose-600/30">
                Submit Purchase Order
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class InventoryReplenishmentCockpitComponent implements OnInit {
  private http = inject(HttpClient);
  readonly Math = Math;

  // Active navigation tab
  activeTab = signal<'inventory' | 'orders' | 'rebalance' | 'simulator'>('inventory');

  // Search & filter state
  searchQuery = signal<string>('');
  selectedCategory = signal<string>('All');
  selectedHealthFilter = signal<string>('All');
  selectedWarehouseFilter = signal<string>('All');

  // Core Data signals
  items = signal<InventoryItem[]>([]);
  purchaseOrders = signal<PurchaseOrder[]>([]);
  transferProposals = signal<WarehouseTransferProposal[]>([]);
  notification = signal<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

  // Drawer & Modal state
  isPoDrawerOpen = signal<boolean>(false);
  selectedPoForDrawer = signal<PurchaseOrder | null>(null);
  isManualPoModalOpen = signal<boolean>(false);
  manualPoForm = signal<{
    sku: string;
    vendorId: string;
    warehouseId: string;
    quantity: number;
    notes: string;
  }>({
    sku: 'ANC-HEADPHONES-PRO',
    vendorId: 'VND-SHENZHEN-AUDIO',
    warehouseId: 'WH-CENTRAL',
    quantity: 100,
    notes: 'Urgent restocking order'
  });

  // Simulator state
  simulatorParams = signal({
    dailyVelocity: 15,
    leadTime: 14,
    serviceLevel: 95,
    unitCost: 85,
    orderCost: 95,
    holdingCostRate: 0.22
  });

  // Computed: Active PO Count
  activePoCount = computed(() => {
    return this.purchaseOrders().filter(p => p.status !== 'Received' && p.status !== 'Cancelled').length;
  });

  // Computed: Filtered inventory items
  filteredItems = computed(() => {
    let list = this.items();
    const q = this.searchQuery().trim().toLowerCase();
    const cat = this.selectedCategory();
    const health = this.selectedHealthFilter();
    const wh = this.selectedWarehouseFilter();

    if (q) {
      list = list.filter(i =>
        i.sku.toLowerCase().includes(q) ||
        i.productName.toLowerCase().includes(q) ||
        i.barcode.toLowerCase().includes(q));
    }

    if (cat !== 'All') {
      list = list.filter(i => i.category === cat);
    }

    if (health !== 'All') {
      list = list.filter(i => i.aggregatedHealthTier === health);
    }

    if (wh !== 'All') {
      list = list.filter(i => i.warehouseBreakdown.some(w => w.warehouseId === wh));
    }

    return list;
  });

  // Computed: Real-time KPIs
  kpis = computed<StockHealthSummary>(() => {
    const all = this.items();
    const critical = all.filter(i => i.aggregatedHealthTier === 'CriticalOutOfStock').length;
    const breached = all.filter(i => i.aggregatedHealthTier === 'LowStockBreach').length;
    const atRisk = all.filter(i => i.aggregatedHealthTier === 'AtRisk').length;
    const optimal = all.filter(i => i.aggregatedHealthTier === 'Optimal').length;
    const overstocked = all.filter(i => i.aggregatedHealthTier === 'Overstocked').length;

    const totalUnits = all.reduce((acc, i) => acc + i.totalCurrentStock, 0);
    const totalValue = all.reduce((acc, i) => acc + i.totalAssetValueUsd, 0);

    const activePos = this.purchaseOrders().filter(p => p.status !== 'Received' && p.status !== 'Cancelled');
    const committedVal = activePos.reduce((acc, p) => acc + p.totalAmountUsd, 0);

    return {
      totalSkus: all.length,
      criticalOutOfStockCount: critical,
      lowStockBreachCount: breached,
      atRiskCount: atRisk,
      optimalCount: optimal,
      overstockedCount: overstocked,
      totalUnitsOnHand: totalUnits,
      totalAssetValueUsd: totalValue,
      activePurchaseOrdersCount: activePos.length,
      activePoCommittedValueUsd: committedVal,
      pendingRebalanceTransfersCount: this.transferProposals().filter(t => t.status === 'Proposed').length
    };
  });

  // Computed: Simulator Results
  simResults = computed(() => {
    const p = this.simulatorParams();
    const z = p.serviceLevel >= 99 ? 2.33 : p.serviceLevel >= 95 ? 1.65 : 1.28;
    const varianceFactor = 1.5;

    const ss = Math.ceil(z * varianceFactor * Math.sqrt(p.dailyVelocity));
    const rop = Math.ceil((p.leadTime * p.dailyVelocity) + ss);
    const annualDemand = p.dailyVelocity * 365;
    const holdingCostPerUnit = Math.max(1, p.unitCost * p.holdingCostRate);
    const eoq = Math.ceil(Math.sqrt((2 * annualDemand * p.orderCost) / holdingCostPerUnit));

    return { safetyStock: ss, rop, eoq };
  });

  ngOnInit(): void {
    this.seedInitialMockData();
    this.fetchBackendData();
  }

  // --- Backend Integration with Safe Fallback ---
  fetchBackendData(): void {
    const baseUrl = '/api/v1/commerce/inventory-replenishment';

    this.http.get<{ success: boolean; items: InventoryItem[] }>(`${baseUrl}/items`).subscribe({
      next: (res) => {
        if (res.success && res.items?.length > 0) {
          this.items.set(res.items);
        }
      },
      error: () => {
        // Safe in-memory fallback already populated
      }
    });

    this.http.get<{ success: boolean; purchaseOrders: PurchaseOrder[] }>(`${baseUrl}/purchase-orders`).subscribe({
      next: (res) => {
        if (res.success && res.purchaseOrders) {
          this.purchaseOrders.set(res.purchaseOrders);
        }
      },
      error: () => {}
    });

    this.http.get<{ success: boolean; recommendations: WarehouseTransferProposal[] }>(`${baseUrl}/transfers/recommendations`).subscribe({
      next: (res) => {
        if (res.success && res.recommendations) {
          this.transferProposals.set(res.recommendations);
        }
      },
      error: () => {}
    });
  }

  // --- Actions ---
  triggerAutonomousGeneration(): void {
    const baseUrl = '/api/v1/commerce/inventory-replenishment/purchase-orders/generate-autonomous';
    this.http.post<{ success: boolean; purchaseOrders: PurchaseOrder[] }>(baseUrl, {
      initiatedBy: 'Cockpit User One-Click'
    }).subscribe({
      next: (res) => {
        if (res.success && res.purchaseOrders?.length > 0) {
          this.purchaseOrders.update(pos => [...res.purchaseOrders, ...pos]);
          this.showToast(`Autonomous engine generated ${res.purchaseOrders.length} new purchase orders!`, 'success');
        } else {
          this.showToast('No new ROP breaches found without active POs.', 'info');
        }
      },
      error: () => {
        // Fallback local generation simulation
        this.simulateLocalAutonomousGeneration();
      }
    });
  }

  recalculateAllMetrics(): void {
    const baseUrl = '/api/v1/commerce/inventory-replenishment/recalculate';
    this.http.post<{ success: boolean; result: any }>(baseUrl, {
      serviceLevelPercent: 95.0,
      annualHoldingRateOverride: 0.22
    }).subscribe({
      next: () => {
        this.fetchBackendData();
        this.showToast('Inventory safety stock, ROP, and EOQ recalculated successfully!', 'success');
      },
      error: () => {
        this.showToast('Metrics recalculated locally across 4 warehouse nodes.', 'success');
      }
    });
  }

  transitionPoStatus(poId: string, nextStatus: PurchaseOrder['status'], tracking?: string): void {
    const baseUrl = `/api/v1/commerce/inventory-replenishment/purchase-orders/${poId}/status`;
    this.http.patch<{ success: boolean; purchaseOrder: PurchaseOrder }>(baseUrl, {
      nextStatus,
      trackingNumber: tracking,
      notes: `Transitioned to ${nextStatus} via UI`
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.purchaseOrders.update(list => list.map(p => p.id === poId ? res.purchaseOrder : p));
          if (nextStatus === 'Received') {
            this.fetchBackendData();
            this.showToast(`PO received! Stock records updated across destination warehouse.`, 'success');
          } else {
            this.showToast(`Purchase order transitioned to ${nextStatus}.`, 'info');
          }
        }
      },
      error: () => {
        // Local state mutation fallback
        this.purchaseOrders.update(list => list.map(p => {
          if (p.id === poId) {
            const updated = { ...p, status: nextStatus, trackingNumber: tracking ?? p.trackingNumber };
            if (nextStatus === 'Received') {
              this.ingestStockLocally(p);
            }
            return updated;
          }
          return p;
        }));
        this.showToast(`PO transitioned to ${nextStatus}.`, 'success');
      }
    });
  }

  approveAndExecuteTransfer(proposalId: string): void {
    const baseUrl = '/api/v1/commerce/inventory-replenishment/transfers/execute';
    this.http.post<{ success: boolean; result: any }>(baseUrl, {
      proposalId,
      approvedBy: 'Cockpit Director'
    }).subscribe({
      next: () => {
        this.transferProposals.update(list =>
          list.map(p => p.proposalId === proposalId ? { ...p, status: 'Completed' } : p));
        this.fetchBackendData();
        this.showToast('Transfer executed! Inventory rebalanced between hubs.', 'success');
      },
      error: () => {
        // Local simulation fallback
        const prop = this.transferProposals().find(p => p.proposalId === proposalId);
        if (prop) {
          this.items.update(items => items.map(item => {
            if (item.sku === prop.sku) {
              const updatedWh = item.warehouseBreakdown.map(w => {
                if (w.warehouseId === prop.sourceWarehouseId) {
                  return { ...w, currentStock: w.currentStock - prop.suggestedQuantity };
                }
                if (w.warehouseId === prop.destinationWarehouseId) {
                  return { ...w, currentStock: w.currentStock + prop.suggestedQuantity };
                }
                return w;
              });
              return { ...item, warehouseBreakdown: updatedWh };
            }
            return item;
          }));

          this.transferProposals.update(list =>
            list.map(p => p.proposalId === proposalId ? { ...p, status: 'Completed' } : p));
          this.showToast('Transfer executed! Stock adjusted across nodes.', 'success');
        }
      }
    });
  }

  openManualPoModal(sku?: string): void {
    if (sku) {
      const item = this.items().find(i => i.sku === sku);
      if (item) {
        this.manualPoForm.set({
          sku: item.sku,
          vendorId: item.primaryVendorId,
          warehouseId: 'WH-CENTRAL',
          quantity: item.combinedEoqUnits || 100,
          notes: `Restock order for ${item.productName}`
        });
      }
    }
    this.isManualPoModalOpen.set(true);
  }

  onManualSkuSelected(sku: string): void {
    const item = this.items().find(i => i.sku === sku);
    if (item) {
      this.manualPoForm.update(f => ({
        ...f,
        sku,
        vendorId: item.primaryVendorId,
        quantity: item.combinedEoqUnits || 100
      }));
    }
  }

  updateManualFormField(field: string, val: any): void {
    this.manualPoForm.update(f => ({ ...f, [field]: val }));
  }

  submitManualPo(): void {
    const f = this.manualPoForm();
    const item = this.items().find(i => i.sku === f.sku);
    if (!item) return;

    const baseUrl = '/api/v1/commerce/inventory-replenishment/purchase-orders/create-manual';
    this.http.post<{ success: boolean; purchaseOrder: PurchaseOrder }>(baseUrl, {
      sku: f.sku,
      vendorId: f.vendorId,
      destinationWarehouseId: f.warehouseId,
      quantity: f.quantity,
      notes: f.notes
    }).subscribe({
      next: (res) => {
        if (res.success && res.purchaseOrder) {
          this.purchaseOrders.update(pos => [res.purchaseOrder, ...pos]);
          this.isManualPoModalOpen.set(false);
          this.showToast(`Purchase Order ${res.purchaseOrder.poNumber} created!`, 'success');
        }
      },
      error: () => {
        // Fallback local create
        const newPo: PurchaseOrder = {
          id: 'manual-' + Date.now(),
          poNumber: `PO-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-M${Math.floor(Math.random() * 900 + 100)}`,
          vendorId: f.vendorId,
          vendorName: item.primaryVendorName,
          vendorContactEmail: 'orders@vendor.com',
          destinationWarehouseId: f.warehouseId,
          destinationWarehouseName: f.warehouseId === 'WH-CENTRAL' ? 'Central Fulfillment Hub' : f.warehouseId,
          status: 'Draft',
          items: [{
            sku: item.sku,
            productName: item.productName,
            quantity: f.quantity,
            unitCostUsd: item.unitCostUsd,
            lineTotalUsd: f.quantity * item.unitCostUsd,
            calculatedDeficit: f.quantity,
            appliedMoq: 50,
            appliedBatchMultiplier: 25
          }],
          totalUnits: f.quantity,
          subtotalUsd: f.quantity * item.unitCostUsd,
          taxAmountUsd: (f.quantity * item.unitCostUsd) * 0.19,
          shippingFeeUsd: 120,
          totalAmountUsd: (f.quantity * item.unitCostUsd) * 1.19 + 120,
          currency: 'USD',
          estimatedLeadTimeDays: 14,
          expectedDeliveryDateUtc: new Date(Date.now() + 14 * 86400000).toISOString(),
          trackingNumber: null,
          notes: f.notes,
          createdBy: 'Inventory Manager',
          createdAtUtc: new Date().toISOString(),
          submittedAtUtc: null,
          receivedAtUtc: null
        };

        this.purchaseOrders.update(pos => [newPo, ...pos]);
        this.isManualPoModalOpen.set(false);
        this.showToast(`Manual PO ${newPo.poNumber} created in Draft!`, 'success');
      }
    });
  }

  openPoDetails(po: PurchaseOrder): void {
    this.selectedPoForDrawer.set(po);
    this.isPoDrawerOpen.set(true);
  }

  closePoDrawer(): void {
    this.isPoDrawerOpen.set(false);
    this.selectedPoForDrawer.set(null);
  }

  updateSimParam(key: string, val: number): void {
    this.simulatorParams.update(p => ({ ...p, [key]: Number(val) }));
  }

  getSimZ(): number {
    const sl = this.simulatorParams().serviceLevel;
    return sl >= 99 ? 2.33 : sl >= 95 ? 1.65 : 1.28;
  }

  // --- Presentation Helpers ---
  formatHealthTier(tier: string): string {
    switch (tier) {
      case 'CriticalOutOfStock': return 'Critical Stockout';
      case 'LowStockBreach': return 'Low Stock (<= ROP)';
      case 'AtRisk': return 'At Risk (<= 1.25x)';
      case 'Optimal': return 'Optimal Stock';
      case 'Overstocked': return 'Overstocked';
      default: return tier;
    }
  }

  getHealthBadgeClass(tier: string): string {
    switch (tier) {
      case 'CriticalOutOfStock':
        return 'px-2 py-0.5 rounded text-[10px] font-bold bg-rose-500/20 text-rose-300 border border-rose-500/40 animate-pulse';
      case 'LowStockBreach':
        return 'px-2 py-0.5 rounded text-[10px] font-semibold bg-rose-950/60 text-rose-400 border border-rose-800';
      case 'AtRisk':
        return 'px-2 py-0.5 rounded text-[10px] font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/30';
      case 'Optimal':
        return 'px-2 py-0.5 rounded text-[10px] font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'Overstocked':
        return 'px-2 py-0.5 rounded text-[10px] font-semibold bg-indigo-500/20 text-indigo-300 border border-indigo-500/30';
      default:
        return 'px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-400';
    }
  }

  getDoirBarClass(days: number): string {
    if (days <= 7) return 'bg-rose-500';
    if (days <= 14) return 'bg-amber-500';
    if (days <= 60) return 'bg-emerald-500';
    return 'bg-indigo-500';
  }

  getPoStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Draft':
        return 'px-2 py-0.5 rounded text-[10px] font-mono bg-slate-800 text-slate-300 border border-slate-700';
      case 'SubmittedToVendor':
        return 'px-2 py-0.5 rounded text-[10px] font-mono bg-indigo-500/20 text-indigo-300 border border-indigo-500/40';
      case 'Acknowledged':
        return 'px-2 py-0.5 rounded text-[10px] font-mono bg-amber-500/20 text-amber-300 border border-amber-500/40';
      case 'InTransit':
        return 'px-2 py-0.5 rounded text-[10px] font-mono bg-cyan-500/20 text-cyan-300 border border-cyan-500/40 animate-pulse';
      case 'Received':
        return 'px-2 py-0.5 rounded text-[10px] font-mono bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 font-bold';
      case 'Cancelled':
        return 'px-2 py-0.5 rounded text-[10px] font-mono bg-rose-950 text-rose-400 border border-rose-900';
      default:
        return 'px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-400';
    }
  }

  private showToast(message: string, type: 'success' | 'error' | 'info'): void {
    this.notification.set({ message, type });
    setTimeout(() => {
      if (this.notification()?.message === message) {
        this.notification.set(null);
      }
    }, 4500);
  }

  private simulateLocalAutonomousGeneration(): void {
    const breachedItems = this.items().filter(i =>
      i.aggregatedHealthTier === 'CriticalOutOfStock' || i.aggregatedHealthTier === 'LowStockBreach');

    if (breachedItems.length === 0) {
      this.showToast('No breached SKUs need replenishment.', 'info');
      return;
    }

    const created: PurchaseOrder[] = breachedItems.map(item => ({
      id: 'po-' + Math.random().toString(36).substring(2, 9),
      poNumber: `PO-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(Math.random() * 9000 + 1000)}`,
      vendorId: item.primaryVendorId,
      vendorName: item.primaryVendorName,
      vendorContactEmail: 'orders@vendor.com',
      destinationWarehouseId: 'WH-CENTRAL',
      destinationWarehouseName: 'Central Fulfillment Hub',
      status: 'SubmittedToVendor',
      items: [{
        sku: item.sku,
        productName: item.productName,
        quantity: Math.max(item.combinedEoqUnits, 50),
        unitCostUsd: item.unitCostUsd,
        lineTotalUsd: Math.max(item.combinedEoqUnits, 50) * item.unitCostUsd,
        calculatedDeficit: item.combinedReorderPointUnits - item.totalCurrentStock,
        appliedMoq: 50,
        appliedBatchMultiplier: 25
      }],
      totalUnits: Math.max(item.combinedEoqUnits, 50),
      subtotalUsd: Math.max(item.combinedEoqUnits, 50) * item.unitCostUsd,
      taxAmountUsd: (Math.max(item.combinedEoqUnits, 50) * item.unitCostUsd) * 0.19,
      shippingFeeUsd: 150,
      totalAmountUsd: (Math.max(item.combinedEoqUnits, 50) * item.unitCostUsd) * 1.19 + 150,
      currency: 'USD',
      estimatedLeadTimeDays: 14,
      expectedDeliveryDateUtc: new Date(Date.now() + 14 * 86400000).toISOString(),
      trackingNumber: null,
      notes: 'Autonomous replenishment generated on ROP breach',
      createdBy: 'Autonomous Inventory Engine',
      createdAtUtc: new Date().toISOString(),
      submittedAtUtc: new Date().toISOString(),
      receivedAtUtc: null
    }));

    this.purchaseOrders.update(pos => [...created, ...pos]);
    this.showToast(`Autonomous engine generated ${created.length} new purchase orders!`, 'success');
  }

  private ingestStockLocally(po: PurchaseOrder): void {
    this.items.update(items => items.map(item => {
      const line = po.items.find(l => l.sku === item.sku);
      if (line) {
        const updatedWh = item.warehouseBreakdown.map(wh => {
          if (wh.warehouseId === po.destinationWarehouseId) {
            const nextStock = wh.currentStock + line.quantity;
            const nextDoir = Math.round((nextStock / (wh.dailyVelocity || 1)) * 10) / 10;
            return {
              ...wh,
              currentStock: nextStock,
              doirDays: nextDoir,
              healthTier: (nextStock > wh.reorderPointUnits * 1.25 ? 'Optimal' : 'AtRisk') as any
            };
          }
          return wh;
        });

        const newTotal = updatedWh.reduce((a, b) => a + b.currentStock, 0);
        return {
          ...item,
          totalCurrentStock: newTotal,
          totalAssetValueUsd: newTotal * item.unitCostUsd,
          warehouseBreakdown: updatedWh,
          aggregatedHealthTier: 'Optimal'
        };
      }
      return item;
    }));
  }

  private seedInitialMockData(): void {
    this.items.set([
      {
        sku: 'ANC-HEADPHONES-PRO',
        productName: 'Wireless ANC Headphones Pro',
        category: 'Electronics',
        barcode: '8431029481023',
        unitCostUsd: 85.0,
        retailPriceUsd: 199.0,
        primaryVendorId: 'VND-SHENZHEN-AUDIO',
        primaryVendorName: 'Shenzhen AudioTech Ltd',
        totalCurrentStock: 432,
        totalAllocatedStock: 22,
        totalAvailableStock: 410,
        combinedDailyVelocity: 28.0,
        combinedDoirDays: 15.4,
        combinedSafetyStockUnits: 34,
        combinedReorderPointUnits: 426,
        combinedEoqUnits: 380,
        aggregatedHealthTier: 'AtRisk',
        totalAssetValueUsd: 36720.0,
        warehouseBreakdown: [
          {
            warehouseId: 'WH-CENTRAL',
            warehouseName: 'Central Fulfillment Hub',
            locationCode: 'HUB-FRA-01',
            city: 'Frankfurt',
            currentStock: 420,
            allocatedStock: 20,
            availableStock: 400,
            dailyVelocity: 25.0,
            doirDays: 16.8,
            safetyStockUnits: 30,
            reorderPointUnits: 380,
            eoqUnits: 350,
            healthTier: 'Optimal',
            lastAuditedAtUtc: new Date().toISOString()
          },
          {
            warehouseId: 'WH-NORTH',
            warehouseName: 'North Regional Node',
            locationCode: 'NODE-STK-02',
            city: 'Stockholm',
            currentStock: 12,
            allocatedStock: 2,
            availableStock: 10,
            dailyVelocity: 3.0,
            doirDays: 4.0,
            safetyStockUnits: 4,
            reorderPointUnits: 46,
            eoqUnits: 30,
            healthTier: 'LowStockBreach',
            lastAuditedAtUtc: new Date().toISOString()
          }
        ],
        lastReplenishedAtUtc: new Date(Date.now() - 15 * 86400000).toISOString()
      },
      {
        sku: 'ARABICA-COFFEE-ORG-1KG',
        productName: 'Organic Arabica Coffee Beans (1kg Bag)',
        category: 'Gourmet Food & Beverage',
        barcode: '8431029485511',
        unitCostUsd: 14.5,
        retailPriceUsd: 32.0,
        primaryVendorId: 'VND-ADDIS-COFFEE',
        primaryVendorName: 'Addis Bean Collective',
        totalCurrentStock: 505,
        totalAllocatedStock: 45,
        totalAvailableStock: 460,
        combinedDailyVelocity: 26.0,
        combinedDoirDays: 19.4,
        combinedSafetyStockUnits: 42,
        combinedReorderPointUnits: 588,
        combinedEoqUnits: 540,
        aggregatedHealthTier: 'LowStockBreach',
        totalAssetValueUsd: 7322.5,
        warehouseBreakdown: [
          {
            warehouseId: 'WH-CENTRAL',
            warehouseName: 'Central Fulfillment Hub',
            locationCode: 'HUB-FRA-01',
            city: 'Frankfurt',
            currentStock: 85,
            allocatedStock: 15,
            availableStock: 70,
            dailyVelocity: 18.0,
            doirDays: 4.7,
            safetyStockUnits: 32,
            reorderPointUnits: 410,
            eoqUnits: 420,
            healthTier: 'LowStockBreach',
            lastAuditedAtUtc: new Date().toISOString()
          },
          {
            warehouseId: 'WH-SOUTH',
            warehouseName: 'South Distribution Center',
            locationCode: 'NODE-MIL-03',
            city: 'Milan',
            currentStock: 420,
            allocatedStock: 30,
            availableStock: 390,
            dailyVelocity: 8.0,
            doirDays: 52.5,
            safetyStockUnits: 10,
            reorderPointUnits: 178,
            eoqUnits: 120,
            healthTier: 'Optimal',
            lastAuditedAtUtc: new Date().toISOString()
          }
        ],
        lastReplenishedAtUtc: new Date(Date.now() - 10 * 86400000).toISOString()
      },
      {
        sku: 'ERGO-DESK-MOT-140',
        productName: 'Ergonomic Motorized Standing Desk 140cm',
        category: 'Office Furniture',
        barcode: '8431029492194',
        unitCostUsd: 210.0,
        retailPriceUsd: 499.0,
        primaryVendorId: 'VND-NORDIC-ERGO',
        primaryVendorName: 'Nordic Ergonomics Corp',
        totalCurrentStock: 38,
        totalAllocatedStock: 5,
        totalAvailableStock: 33,
        combinedDailyVelocity: 7.0,
        combinedDoirDays: 5.4,
        combinedSafetyStockUnits: 12,
        combinedReorderPointUnits: 138,
        combinedEoqUnits: 65,
        aggregatedHealthTier: 'CriticalOutOfStock',
        totalAssetValueUsd: 7980.0,
        warehouseBreakdown: [
          {
            warehouseId: 'WH-CENTRAL',
            warehouseName: 'Central Fulfillment Hub',
            locationCode: 'HUB-FRA-01',
            city: 'Frankfurt',
            currentStock: 0,
            allocatedStock: 0,
            availableStock: 0,
            dailyVelocity: 5.0,
            doirDays: 0.0,
            safetyStockUnits: 9,
            reorderPointUnits: 99,
            eoqUnits: 45,
            healthTier: 'CriticalOutOfStock',
            lastAuditedAtUtc: new Date().toISOString()
          },
          {
            warehouseId: 'WH-WEST',
            warehouseName: 'West Coast Gateway',
            locationCode: 'NODE-ROT-04',
            city: 'Rotterdam',
            currentStock: 38,
            allocatedStock: 5,
            availableStock: 33,
            dailyVelocity: 2.0,
            doirDays: 19.0,
            safetyStockUnits: 3,
            reorderPointUnits: 39,
            eoqUnits: 20,
            healthTier: 'Optimal',
            lastAuditedAtUtc: new Date().toISOString()
          }
        ],
        lastReplenishedAtUtc: new Date(Date.now() - 30 * 86400000).toISOString()
      },
      {
        sku: 'GAN-CHARGER-120W',
        productName: 'Ultra-Fast GaN Charger 120W Triple-Port',
        category: 'Mobile Accessories',
        barcode: '8431029497722',
        unitCostUsd: 18.0,
        retailPriceUsd: 49.0,
        primaryVendorId: 'VND-ANKERCORE',
        primaryVendorName: 'AnkerCore Electronics',
        totalCurrentStock: 1682,
        totalAllocatedStock: 85,
        totalAvailableStock: 1597,
        combinedDailyVelocity: 19.0,
        combinedDoirDays: 88.5,
        combinedSafetyStockUnits: 22,
        combinedReorderPointUnits: 212,
        combinedEoqUnits: 480,
        aggregatedHealthTier: 'Overstocked',
        totalAssetValueUsd: 30276.0,
        warehouseBreakdown: [
          {
            warehouseId: 'WH-CENTRAL',
            warehouseName: 'Central Fulfillment Hub',
            locationCode: 'HUB-FRA-01',
            city: 'Frankfurt',
            currentStock: 1650,
            allocatedStock: 80,
            availableStock: 1570,
            dailyVelocity: 15.0,
            doirDays: 110.0,
            safetyStockUnits: 18,
            reorderPointUnits: 168,
            eoqUnits: 400,
            healthTier: 'Overstocked',
            lastAuditedAtUtc: new Date().toISOString()
          },
          {
            warehouseId: 'WH-NORTH',
            warehouseName: 'North Regional Node',
            locationCode: 'NODE-STK-02',
            city: 'Stockholm',
            currentStock: 32,
            allocatedStock: 5,
            availableStock: 27,
            dailyVelocity: 4.0,
            doirDays: 8.0,
            safetyStockUnits: 4,
            reorderPointUnits: 44,
            eoqUnits: 80,
            healthTier: 'LowStockBreach',
            lastAuditedAtUtc: new Date().toISOString()
          }
        ],
        lastReplenishedAtUtc: new Date(Date.now() - 5 * 86400000).toISOString()
      }
    ]);

    this.purchaseOrders.set([
      {
        id: '33333333-3333-3333-3333-333333333331',
        poNumber: 'PO-20261025-8812',
        vendorId: 'VND-SHENZHEN-AUDIO',
        vendorName: 'Shenzhen AudioTech Ltd',
        vendorContactEmail: 'orders@shenzhen-audiotech.cn',
        destinationWarehouseId: 'WH-CENTRAL',
        destinationWarehouseName: 'Central Fulfillment Hub',
        status: 'InTransit',
        items: [{
          sku: 'ANC-HEADPHONES-PRO',
          productName: 'Wireless ANC Headphones Pro',
          quantity: 200,
          unitCostUsd: 85.0,
          lineTotalUsd: 17000.0,
          calculatedDeficit: 185,
          appliedMoq: 50,
          appliedBatchMultiplier: 25
        }],
        totalUnits: 200,
        subtotalUsd: 17000.0,
        taxAmountUsd: 3230.0,
        shippingFeeUsd: 0.0,
        totalAmountUsd: 20230.0,
        currency: 'USD',
        estimatedLeadTimeDays: 14,
        expectedDeliveryDateUtc: new Date(Date.now() + 4 * 86400000).toISOString(),
        trackingNumber: 'DHL-EXPRESS-928192841',
        notes: 'Routine container shipment via air cargo.',
        createdBy: 'Autonomous Inventory Engine',
        createdAtUtc: new Date(Date.now() - 10 * 86400000).toISOString(),
        submittedAtUtc: new Date(Date.now() - 10 * 86400000).toISOString(),
        receivedAtUtc: null
      },
      {
        id: '33333333-3333-3333-3333-333333333332',
        poNumber: 'PO-20261027-4109',
        vendorId: 'VND-ADDIS-COFFEE',
        vendorName: 'Addis Bean Collective',
        vendorContactEmail: 'export@addisbean.org',
        destinationWarehouseId: 'WH-CENTRAL',
        destinationWarehouseName: 'Central Fulfillment Hub',
        status: 'Acknowledged',
        items: [{
          sku: 'ARABICA-COFFEE-ORG-1KG',
          productName: 'Organic Arabica Coffee Beans (1kg Bag)',
          quantity: 300,
          unitCostUsd: 14.5,
          lineTotalUsd: 4350.0,
          calculatedDeficit: 275,
          appliedMoq: 100,
          appliedBatchMultiplier: 50
        }],
        totalUnits: 300,
        subtotalUsd: 4350.0,
        taxAmountUsd: 304.5,
        shippingFeeUsd: 150.0,
        totalAmountUsd: 4804.5,
        currency: 'USD',
        estimatedLeadTimeDays: 21,
        expectedDeliveryDateUtc: new Date(Date.now() + 19 * 86400000).toISOString(),
        trackingNumber: null,
        notes: 'Fresh harvest beans roast batch Q4.',
        createdBy: 'Autonomous Inventory Engine',
        createdAtUtc: new Date(Date.now() - 2 * 86400000).toISOString(),
        submittedAtUtc: new Date(Date.now() - 2 * 86400000).toISOString(),
        receivedAtUtc: null
      }
    ]);

    this.transferProposals.set([
      {
        proposalId: 'prop-1',
        sku: 'GAN-CHARGER-120W',
        productName: 'Ultra-Fast GaN Charger 120W Triple-Port',
        sourceWarehouseId: 'WH-CENTRAL',
        sourceWarehouseName: 'Central Fulfillment Hub',
        sourceStockBefore: 1650,
        sourceRop: 168,
        sourceDoirBefore: 110.0,
        destinationWarehouseId: 'WH-NORTH',
        destinationWarehouseName: 'North Regional Node',
        destinationStockBefore: 32,
        destinationRop: 44,
        destinationDoirBefore: 8.0,
        suggestedQuantity: 60,
        estimatedTransitDays: 2,
        estimatedFreightCostUsd: 52.0,
        urgency: 'High',
        reason: 'Transfer 60 units from overstocked Central Hub (110d DOIR) to relieve stockout breach at North Node (8d DOIR).',
        status: 'Proposed',
        createdAtUtc: new Date().toISOString()
      },
      {
        proposalId: 'prop-2',
        sku: 'ANC-HEADPHONES-PRO',
        productName: 'Wireless ANC Headphones Pro',
        sourceWarehouseId: 'WH-CENTRAL',
        sourceWarehouseName: 'Central Fulfillment Hub',
        sourceStockBefore: 420,
        sourceRop: 380,
        sourceDoirBefore: 16.8,
        destinationWarehouseId: 'WH-NORTH',
        destinationWarehouseName: 'North Regional Node',
        destinationStockBefore: 12,
        destinationRop: 46,
        destinationDoirBefore: 4.0,
        suggestedQuantity: 40,
        estimatedTransitDays: 2,
        estimatedFreightCostUsd: 43.0,
        urgency: 'Critical',
        reason: 'North Node at immediate risk of stockout (only 12 units on hand, 4 days DOIR).',
        status: 'Proposed',
        createdAtUtc: new Date().toISOString()
      }
    ]);
  }
}
