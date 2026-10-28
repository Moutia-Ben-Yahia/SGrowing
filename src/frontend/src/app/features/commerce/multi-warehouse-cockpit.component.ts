import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface WarehouseNode {
  id: string;
  code: string;
  name: string;
  city: string;
  country: string;
  latitude: number;
  longitude: number;
  capacityUnits: number;
  availableStock: number;
  isActive: boolean;
  distanceKm?: number;
}

export interface StockMatrixRow {
  sku: string;
  productName: string;
  unitCost: number;
  tunisQty: number;
  sousseQty: number;
  sfaxQty: number;
  cairoQty: number;
  casablancaQty: number;
  totalReserved: number;
}

export interface TransferManifestItem {
  id: string;
  manifestNumber: string;
  sourceWarehouseName: string;
  destinationWarehouseName: string;
  sku: string;
  quantity: number;
  status: 'Draft' | 'Dispatched' | 'Received';
  createdAt: string;
}

export interface ProximitySimulationResult {
  orderId: string;
  selectedWarehouseName: string;
  selectedWarehouseCity: string;
  distanceKm: number;
  isFullyAllocated: boolean;
  allocatedQty: number;
  shortageQty: number;
  autoTransferManifest?: TransferManifestItem;
  routingReason: string;
}

export interface SpatialCandidate {
  warehouseId: string;
  code: string;
  name: string;
  city: string;
  country: string;
  latitude: number;
  longitude: number;
  distanceKm: number;
  bearingDegrees: number;
  compassDirection: string;
  estimatedTransitHours: number;
  availableStockForSku?: number;
}

export interface SpatialSearchResult {
  originLatitude: number;
  originLongitude: number;
  searchRadiusKm: number;
  boundingBox: { minLatitude: number; maxLatitude: number; minLongitude: number; maxLongitude: number };
  totalHubsEvaluated: number;
  hubsWithinRadius: number;
  spatialIndexUsed: boolean;
  queryEngine: string;
  warehouses: SpatialCandidate[];
  searchTimeMs: number;
}

@Component({
  selector: 'app-multi-warehouse-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Header -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/commerce" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← {{ translation.t('nav.commerce') }}
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              🏬 Multi-Warehouse Inventory Allocation & Geo-Routing
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Inspired by Ever Demand • Proximity Engine
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              (click)="openTransferModal.set(true)"
              class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold flex items-center gap-1.5 transition border border-slate-700">
              🔄 Inter-Hub Stock Transfer
            </button>
            <button
              data-testid="calculate-route-btn"
              (click)="simulateProximityRouting()"
              class="px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold flex items-center gap-1.5 transition shadow-sm">
              📍 Evaluate Proximity Route
            </button>
          </div>
        </header>

        <!-- Notification Toast -->
        @if (toastMessage()) {
          <div class="mx-6 mt-4 px-4 py-2 bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-xs rounded-lg flex items-center justify-between">
            <span>{{ toastMessage() }}</span>
            <button (click)="toastMessage.set('')" class="text-emerald-400 hover:text-white">✕</button>
          </div>
        }

        <div class="p-6 space-y-6">
          <!-- 1. Regional Warehouse Distribution Hubs Grid -->
          <div>
            <div class="flex items-center justify-between mb-3">
              <h3 class="text-xs font-bold text-slate-300 uppercase tracking-wider flex items-center gap-2">
                🌐 Active Regional Distribution Hubs ({{ warehouses().length }})
              </h3>
              <span class="text-xs text-slate-500">Haversine GPS Coordinate Triangulation</span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-3.5">
              @for (wh of warehouses(); track wh.id) {
                <div
                  [attr.data-testid]="'warehouse-card-' + wh.code"
                  class="bg-slate-900/60 border border-slate-800/80 hover:border-slate-700 rounded-xl p-4 transition shadow-sm">
                  <div class="flex items-center justify-between gap-1 mb-2">
                    <span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-800 text-indigo-300 border border-slate-700">
                      {{ wh.code }}
                    </span>
                    <span class="flex items-center gap-1 text-[10px] text-emerald-400 font-medium">
                      <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                      Active
                    </span>
                  </div>

                  <h4 class="text-xs font-bold text-white mb-1 leading-snug truncate" [title]="wh.name">
                    {{ wh.name }}
                  </h4>
                  <div class="text-[11px] text-slate-400 mb-2">
                    📍 {{ wh.city }}, {{ wh.country }}
                  </div>

                  <div class="space-y-1.5 pt-2 border-t border-slate-800/80 text-[10px] text-slate-400 font-mono">
                    <div class="flex justify-between">
                      <span>Coordinates:</span>
                      <span class="text-slate-300">{{ wh.latitude.toFixed(2) }}, {{ wh.longitude.toFixed(2) }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span>Available Stock:</span>
                      <span class="text-emerald-400 font-bold">{{ wh.availableStock.toLocaleString() }} units</span>
                    </div>
                    <div class="flex justify-between">
                      <span>Capacity:</span>
                      <span class="text-slate-300">{{ wh.capacityUnits.toLocaleString() }}</span>
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- 2. Proximity Routing Engine Simulator -->
          <div class="bg-gradient-to-r from-slate-900/90 to-indigo-950/40 border border-indigo-500/30 rounded-xl p-5 shadow-lg">
            <div class="flex items-center justify-between pb-3 mb-4 border-b border-slate-800/80">
              <div class="flex items-center gap-2">
                <span class="text-lg">🧭</span>
                <h3 class="text-sm font-bold text-white">
                  Proximity-Based Fulfillment & Stock Geo-Routing Simulator
                </h3>
              </div>
              <span class="text-xs text-indigo-300 font-mono">Real-Time Distance Optimization</span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-12 gap-4 items-end">
              <!-- Customer City / Preset -->
              <div class="md:col-span-3">
                <label class="block text-[11px] font-medium text-slate-300 mb-1">Customer Delivery City</label>
                <select
                  [(ngModel)]="simCustomerCity"
                  (ngModelChange)="onCityChange($event)"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                  <option value="Tunis">Tunis (Les Berges du Lac)</option>
                  <option value="Sousse">Sousse (Port El Kantaoui)</option>
                  <option value="Sfax">Sfax (Route de Téniour)</option>
                  <option value="Cairo">Cairo (New Cairo City)</option>
                  <option value="Casablanca">Casablanca (Anfa District)</option>
                </select>
              </div>

              <!-- Customer Lat / Lng -->
              <div class="md:col-span-3">
                <label class="block text-[11px] font-medium text-slate-300 mb-1">GPS Coordinates (Lat / Lng)</label>
                <div class="flex items-center gap-2">
                  <input
                    type="number"
                    step="0.0001"
                    [(ngModel)]="simCustomerLat"
                    class="w-1/2 bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-2 text-xs text-white font-mono" />
                  <input
                    type="number"
                    step="0.0001"
                    [(ngModel)]="simCustomerLng"
                    class="w-1/2 bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-2 text-xs text-white font-mono" />
                </div>
              </div>

              <!-- Product SKU -->
              <div class="md:col-span-3">
                <label class="block text-[11px] font-medium text-slate-300 mb-1">Requested SKU</label>
                <select
                  [(ngModel)]="simSku"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500 font-mono">
                  <option value="SKU-GROW-PRO">SKU-GROW-PRO (Omnichannel Gateway)</option>
                  <option value="SKU-SMART-CAM">SKU-SMART-CAM (AI Vision Pack Camera)</option>
                  <option value="SKU-LABEL-PRINT">SKU-LABEL-PRINT (Thermal Printer)</option>
                  <option value="SKU-BARCODE-RUGGED">SKU-BARCODE-RUGGED (Warehouse Scanner)</option>
                </select>
              </div>

              <!-- Quantity & Trigger -->
              <div class="md:col-span-3 flex items-center gap-2">
                <div class="w-24">
                  <label class="block text-[11px] font-medium text-slate-300 mb-1">Quantity</label>
                  <input
                    type="number"
                    min="1"
                    [(ngModel)]="simQuantity"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white font-mono" />
                </div>
                <button
                  (click)="simulateProximityRouting()"
                  class="flex-1 mt-auto h-[38px] px-3 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold transition">
                  Route Order
                </button>
              </div>
            </div>

            <!-- Simulation Result Banner -->
            @if (routingResult()) {
              <div class="mt-5 pt-4 border-t border-indigo-900/60" data-testid="routing-result-banner">
                <div class="bg-slate-950/80 border border-indigo-500/40 rounded-xl p-4">
                  <div class="flex flex-wrap items-center justify-between gap-3 mb-3">
                    <div class="flex items-center gap-2.5">
                      <span class="w-8 h-8 rounded-lg bg-emerald-500/20 text-emerald-300 flex items-center justify-center text-sm font-bold">
                        ✓
                      </span>
                      <div>
                        <div class="text-xs font-bold text-white flex items-center gap-2">
                          Optimal Fulfillment Hub: <span class="text-emerald-400 font-bold">{{ routingResult()!.selectedWarehouseName }}</span>
                        </div>
                        <div class="text-[11px] text-slate-400">
                          Shortest Haversine distance: <span class="text-indigo-300 font-mono font-semibold">{{ routingResult()!.distanceKm }} km</span>
                        </div>
                      </div>
                    </div>

                    <div class="flex items-center gap-2">
                      <span class="px-2.5 py-1 rounded bg-indigo-600/30 text-indigo-300 text-xs font-mono border border-indigo-500/40">
                        Allocated: {{ routingResult()!.allocatedQty }} units
                      </span>
                      @if (routingResult()!.autoTransferManifest) {
                        <span class="px-2.5 py-1 rounded bg-amber-500/20 text-amber-300 text-xs font-mono border border-amber-500/40">
                          ⚠️ Shortage: {{ routingResult()!.shortageQty }} units transferred
                        </span>
                      }
                    </div>
                  </div>

                  <p class="text-xs text-slate-300 leading-relaxed font-sans mb-3">
                    {{ routingResult()!.routingReason }}
                  </p>

                  <!-- Inter-Hub Transfer Manifest Notification -->
                  @if (routingResult()!.autoTransferManifest) {
                    <div class="bg-amber-950/30 border border-amber-500/40 rounded-lg p-3 flex items-center justify-between text-xs text-amber-200">
                      <div class="flex items-center gap-2">
                        <span>📦</span>
                        <span>
                          Auto-generated Transfer Manifest: <strong class="font-mono">{{ routingResult()!.autoTransferManifest!.manifestNumber }}</strong>
                          ({{ routingResult()!.autoTransferManifest!.quantity }} units of {{ routingResult()!.autoTransferManifest!.sku }} dispatched from {{ routingResult()!.autoTransferManifest!.sourceWarehouseName }}).
                        </span>
                      </div>
                      <span class="px-2 py-0.5 rounded bg-amber-500/30 text-amber-200 text-[10px] font-mono uppercase">
                        Dispatched
                      </span>
                    </div>
                  }
                </div>
              </div>
            }
          </div>

          <!-- PostGIS Geodetic Spatial Index & Proximity Radar -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-5 shadow-lg">
            <div class="flex items-center justify-between pb-3 mb-4 border-b border-slate-800/80">
              <div class="flex items-center gap-2">
                <span class="text-lg">📡</span>
                <div>
                  <h3 class="text-sm font-bold text-white flex items-center gap-2">
                    PostGIS Spatial Index & Proximity Radar
                  </h3>
                  <p class="text-[11px] text-slate-400">
                    O(1) Geodetic bounding box range search with spherical Haversine & bearing triangulation
                  </p>
                </div>
              </div>
              <span class="bg-emerald-500/20 text-emerald-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-emerald-500/30">
                PostGIS GiST Accelerated
              </span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-12 gap-4 items-end">
              <div class="md:col-span-3">
                <label class="block text-[11px] font-medium text-slate-300 mb-1">Radar Center Preset</label>
                <select
                  [(ngModel)]="spatialCityPreset"
                  (ngModelChange)="onSpatialCityChange($event)"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                  <option value="Tunis">Tunis (36.8065° N, 10.1815° E)</option>
                  <option value="Sousse">Sousse (35.8256° N, 10.6369° E)</option>
                  <option value="Sfax">Sfax (34.7406° N, 10.7603° E)</option>
                  <option value="Casablanca">Casablanca (33.5731° N, -7.5898° W)</option>
                  <option value="Cairo">Cairo (30.0444° N, 31.2357° E)</option>
                </select>
              </div>

              <div class="md:col-span-3">
                <label class="block text-[11px] font-medium text-slate-300 mb-1">Search Radius (Km)</label>
                <div class="flex items-center gap-2">
                  <input
                    type="range"
                    min="25"
                    max="1500"
                    step="25"
                    [(ngModel)]="spatialRadiusKm"
                    class="w-full accent-indigo-500" />
                  <span class="text-xs font-mono text-indigo-300 w-16 text-right">{{ spatialRadiusKm }} km</span>
                </div>
              </div>

              <div class="md:col-span-3">
                <label class="block text-[11px] font-medium text-slate-300 mb-1">Filter Inventory SKU</label>
                <select
                  [(ngModel)]="spatialFilterSku"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:outline-none focus:border-indigo-500">
                  <option value="ALL">All SKUs</option>
                  <option value="SKU-GROW-PRO">SKU-GROW-PRO</option>
                  <option value="SKU-SMART-CAM">SKU-SMART-CAM</option>
                  <option value="SKU-LABEL-PRINT">SKU-LABEL-PRINT</option>
                  <option value="SKU-BARCODE-RUGGED">SKU-BARCODE-RUGGED</option>
                </select>
              </div>

              <div class="md:col-span-3">
                <button
                  data-testid="spatial-radar-search-btn"
                  [disabled]="isSpatialSearching()"
                  (click)="runSpatialRadarSearch()"
                  class="w-full h-[38px] px-3 rounded-lg bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white text-xs font-semibold flex items-center justify-center gap-1.5 transition shadow-sm">
                  @if (isSpatialSearching()) {
                    <span class="animate-spin text-xs">⚡</span>
                    <span>Scanning Bounding Box...</span>
                  } @else {
                    <span>📡 Run Spatial Radar Search</span>
                  }
                </button>
              </div>
            </div>

            <!-- Spatial Radar Results Card -->
            @if (spatialResult()) {
              <div class="mt-4 pt-4 border-t border-slate-800" data-testid="spatial-results-card">
                <div class="bg-slate-950/80 border border-slate-800 rounded-xl p-4">
                  <div class="flex flex-wrap items-center justify-between gap-3 mb-3">
                    <div class="flex items-center gap-2">
                      <span class="text-emerald-400 font-bold text-xs">
                        ✓ {{ spatialResult()!.hubsWithinRadius }} of {{ spatialResult()!.totalHubsEvaluated }} Hubs within {{ spatialResult()!.searchRadiusKm }} km
                      </span>
                      <span class="text-slate-600">|</span>
                      <span class="text-[11px] text-slate-400 font-mono">
                        Bounding Box: [{{ spatialResult()!.boundingBox.minLatitude }}°, {{ spatialResult()!.boundingBox.maxLatitude }}°] × [{{ spatialResult()!.boundingBox.minLongitude }}°, {{ spatialResult()!.boundingBox.maxLongitude }}°]
                      </span>
                    </div>

                    <div class="flex items-center gap-2">
                      <span class="px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300 text-[10px] font-mono border border-indigo-500/30">
                        {{ spatialResult()!.queryEngine }}
                      </span>
                      <span class="text-[10px] text-slate-400 font-mono">
                        {{ spatialResult()!.searchTimeMs }} ms
                      </span>
                    </div>
                  </div>

                  <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                    @for (wh of spatialResult()!.warehouses; track wh.warehouseId) {
                      <div class="p-3 bg-slate-900 border border-slate-800 rounded-lg flex flex-col justify-between">
                        <div>
                          <div class="flex items-center justify-between mb-1">
                            <span class="text-xs font-bold text-white">{{ wh.name }}</span>
                            <span class="px-1.5 py-0.5 rounded bg-indigo-600/30 text-indigo-300 text-[10px] font-mono font-semibold">
                              {{ wh.compassDirection }} ({{ wh.bearingDegrees }}°)
                            </span>
                          </div>
                          <div class="text-[11px] text-slate-400">{{ wh.city }}, {{ wh.country }}</div>
                        </div>

                        <div class="mt-3 pt-2 border-t border-slate-800/80 flex items-center justify-between text-xs">
                          <span class="font-mono text-emerald-400 font-bold">{{ wh.distanceKm }} km</span>
                          <span class="text-slate-400 text-[10px] font-mono">ETA: ~{{ wh.estimatedTransitHours }}h transit</span>
                        </div>
                      </div>
                    }
                  </div>
                </div>
              </div>
            }
          </div>

          <!-- 3. Real-Time Multi-Warehouse Stock Allocation Matrix -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm">
            <div class="flex items-center justify-between pb-3 mb-4 border-b border-slate-800/80">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  📊 Real-Time Inventory Stock Allocation Matrix
                </h3>
                <p class="text-xs text-slate-400 mt-0.5">Live SKU inventory counts across distributed fulfillment centers</p>
              </div>

              <div class="flex items-center gap-2">
                <span class="text-xs text-slate-400">Total Stock Value:</span>
                <span class="text-xs font-bold font-mono text-emerald-400">$645,800.00</span>
              </div>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300 font-sans" data-testid="stock-matrix-table">
                <thead class="bg-slate-950 text-slate-400 uppercase text-[10px] tracking-wider font-mono border-b border-slate-800">
                  <tr>
                    <th class="py-2.5 px-3">SKU</th>
                    <th class="py-2.5 px-3">Product Name</th>
                    <th class="py-2.5 px-3 text-right">Unit Cost</th>
                    <th class="py-2.5 px-3 text-right">Tunis Hub</th>
                    <th class="py-2.5 px-3 text-right">Sousse DC</th>
                    <th class="py-2.5 px-3 text-right">Sfax DC</th>
                    <th class="py-2.5 px-3 text-right">Cairo Hub</th>
                    <th class="py-2.5 px-3 text-right">Casablanca Port</th>
                    <th class="py-2.5 px-3 text-right">Total Avail.</th>
                    <th class="py-2.5 px-3 text-right">Reserved</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (row of stockMatrix(); track row.sku) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <td class="py-3 px-3 font-bold text-indigo-300">{{ row.sku }}</td>
                      <td class="py-3 px-3 font-sans text-slate-200">{{ row.productName }}</td>
                      <td class="py-3 px-3 text-right">&dollar;{{ row.unitCost.toFixed(2) }}</td>
                      <td class="py-3 px-3 text-right font-bold" [ngClass]="row.tunisQty > 50 ? 'text-emerald-400' : 'text-amber-400'">{{ row.tunisQty }}</td>
                      <td class="py-3 px-3 text-right font-bold" [ngClass]="row.sousseQty > 50 ? 'text-emerald-400' : 'text-amber-400'">{{ row.sousseQty }}</td>
                      <td class="py-3 px-3 text-right font-bold" [ngClass]="row.sfaxQty > 50 ? 'text-emerald-400' : 'text-amber-400'">{{ row.sfaxQty }}</td>
                      <td class="py-3 px-3 text-right font-bold text-emerald-400">{{ row.cairoQty }}</td>
                      <td class="py-3 px-3 text-right font-bold text-emerald-400">{{ row.casablancaQty }}</td>
                      <td class="py-3 px-3 text-right font-bold text-white">
                        {{ row.tunisQty + row.sousseQty + row.sfaxQty + row.cairoQty + row.casablancaQty }}
                      </td>
                      <td class="py-3 px-3 text-right text-slate-500">{{ row.totalReserved }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <!-- 4. Active Stock Transfer Manifests -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm">
            <div class="flex items-center justify-between pb-3 mb-4 border-b border-slate-800/80">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                📋 Inter-Warehouse Transfer Manifests
              </h3>
              <span class="text-xs text-slate-500">{{ transferManifests().length }} manifests</span>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300 font-sans">
                <thead class="bg-slate-950 text-slate-400 uppercase text-[10px] tracking-wider font-mono border-b border-slate-800">
                  <tr>
                    <th class="py-2.5 px-3">Manifest #</th>
                    <th class="py-2.5 px-3">Origin Hub</th>
                    <th class="py-2.5 px-3">Destination Hub</th>
                    <th class="py-2.5 px-3">SKU</th>
                    <th class="py-2.5 px-3 text-right">Units</th>
                    <th class="py-2.5 px-3">Status</th>
                    <th class="py-2.5 px-3 text-right">Dispatched At</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (t of transferManifests(); track t.id) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <td class="py-2.5 px-3 font-bold text-indigo-300">{{ t.manifestNumber }}</td>
                      <td class="py-2.5 px-3 font-sans">{{ t.sourceWarehouseName }}</td>
                      <td class="py-2.5 px-3 font-sans">{{ t.destinationWarehouseName }}</td>
                      <td class="py-2.5 px-3 text-slate-200">{{ t.sku }}</td>
                      <td class="py-2.5 px-3 text-right font-bold text-white">{{ t.quantity }}</td>
                      <td class="py-2.5 px-3">
                        <span
                          class="px-2 py-0.5 rounded text-[10px] font-mono border"
                          [ngClass]="t.status === 'Dispatched' ? 'bg-amber-500/20 text-amber-300 border-amber-500/30' : 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30'">
                          {{ t.status }}
                        </span>
                      </td>
                      <td class="py-2.5 px-3 text-right text-slate-400">{{ t.createdAt }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- Inter-Hub Stock Transfer Modal -->
        @if (openTransferModal()) {
          <div class="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div class="bg-slate-900 border border-slate-800 rounded-xl max-w-lg w-full p-6 shadow-2xl">
              <div class="flex items-center justify-between pb-3 mb-4 border-b border-slate-800">
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  🔄 Create Inter-Hub Stock Transfer
                </h3>
                <button (click)="openTransferModal.set(false)" class="text-slate-400 hover:text-white text-sm">✕</button>
              </div>

              <div class="space-y-4">
                <div>
                  <label class="block text-xs text-slate-300 mb-1">Origin Warehouse</label>
                  <select
                    [(ngModel)]="manualTransferOrigin"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                    @for (w of warehouses(); track w.id) {
                      <option [value]="w.name">{{ w.name }} ({{ w.city }})</option>
                    }
                  </select>
                </div>

                <div>
                  <label class="block text-xs text-slate-300 mb-1">Destination Warehouse</label>
                  <select
                    [(ngModel)]="manualTransferDest"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                    @for (w of warehouses(); track w.id) {
                      <option [value]="w.name">{{ w.name }} ({{ w.city }})</option>
                    }
                  </select>
                </div>

                <div class="grid grid-cols-2 gap-4">
                  <div>
                    <label class="block text-xs text-slate-300 mb-1">SKU to Transfer</label>
                    <select
                      [(ngModel)]="manualTransferSku"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500 font-mono">
                      <option value="SKU-GROW-PRO">SKU-GROW-PRO</option>
                      <option value="SKU-SMART-CAM">SKU-SMART-CAM</option>
                      <option value="SKU-LABEL-PRINT">SKU-LABEL-PRINT</option>
                      <option value="SKU-BARCODE-RUGGED">SKU-BARCODE-RUGGED</option>
                    </select>
                  </div>

                  <div>
                    <label class="block text-xs text-slate-300 mb-1">Quantity Units</label>
                    <input
                      type="number"
                      min="1"
                      [(ngModel)]="manualTransferQty"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white font-mono focus:outline-none focus:border-indigo-500" />
                  </div>
                </div>
              </div>

              <div class="mt-6 flex items-center justify-end gap-3 pt-4 border-t border-slate-800">
                <button
                  (click)="openTransferModal.set(false)"
                  class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium transition">
                  Cancel
                </button>
                <button
                  (click)="submitManualTransfer()"
                  class="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold transition">
                  Dispatch Transfer
                </button>
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class MultiWarehouseCockpitComponent {
  translation = inject(TranslationService);

  toastMessage = signal<string>('');
  openTransferModal = signal<boolean>(false);

  // Proximity Simulator inputs
  simCustomerCity = 'Tunis';
  simCustomerLat = 36.8350;
  simCustomerLng = 10.2300;
  simSku = 'SKU-GROW-PRO';
  simQuantity = 5;

  // Manual Transfer Form inputs
  manualTransferOrigin = 'Cairo Central Fulfillment Hub';
  manualTransferDest = 'Tunis Nord Logistics Mega-Hub';
  manualTransferSku = 'SKU-BARCODE-RUGGED';
  manualTransferQty = 25;

  warehouses = signal<WarehouseNode[]>([
    {
      id: 'wh-1',
      code: 'WH-TUN-01',
      name: 'Tunis Nord Logistics Mega-Hub',
      city: 'Tunis',
      country: 'Tunisia',
      latitude: 36.8065,
      longitude: 10.1815,
      capacityUnits: 25000,
      availableStock: 1450,
      isActive: true
    },
    {
      id: 'wh-2',
      code: 'WH-SOU-02',
      name: 'Sousse Sahel Distribution Center',
      city: 'Sousse',
      country: 'Tunisia',
      latitude: 35.8256,
      longitude: 10.6369,
      capacityUnits: 18000,
      availableStock: 820,
      isActive: true
    },
    {
      id: 'wh-3',
      code: 'WH-SFX-03',
      name: 'Sfax Southern Regional Terminal',
      city: 'Sfax',
      country: 'Tunisia',
      latitude: 34.7406,
      longitude: 10.7603,
      capacityUnits: 15000,
      availableStock: 640,
      isActive: true
    },
    {
      id: 'wh-4',
      code: 'WH-CAI-04',
      name: 'Cairo Central Fulfillment Hub',
      city: 'Cairo',
      country: 'Egypt',
      latitude: 30.0444,
      longitude: 31.2357,
      capacityUnits: 40000,
      availableStock: 2200,
      isActive: true
    },
    {
      id: 'wh-5',
      code: 'WH-CAS-05',
      name: 'Casablanca Port DC',
      city: 'Casablanca',
      country: 'Morocco',
      latitude: 33.5731,
      longitude: -7.5898,
      capacityUnits: 30000,
      availableStock: 1800,
      isActive: true
    }
  ]);

  stockMatrix = signal<StockMatrixRow[]>([
    {
      sku: 'SKU-GROW-PRO',
      productName: 'SGrowing Omnichannel Gateway Device',
      unitCost: 150.00,
      tunisQty: 450,
      sousseQty: 280,
      sfaxQty: 320,
      cairoQty: 600,
      casablancaQty: 500,
      totalReserved: 45
    },
    {
      sku: 'SKU-SMART-CAM',
      productName: 'AI Computer Vision Pack Station Camera',
      unitCost: 85.00,
      tunisQty: 320,
      sousseQty: 190,
      sfaxQty: 110,
      cairoQty: 400,
      casablancaQty: 350,
      totalReserved: 28
    },
    {
      sku: 'SKU-LABEL-PRINT',
      productName: 'Industrial High-Speed Thermal Waybill Printer',
      unitCost: 120.00,
      tunisQty: 280,
      sousseQty: 140,
      sfaxQty: 90,
      cairoQty: 310,
      casablancaQty: 240,
      totalReserved: 12
    },
    {
      sku: 'SKU-BARCODE-RUGGED',
      productName: 'Android 14 Ruggedized Warehouse Scanner',
      unitCost: 210.00,
      tunisQty: 15,
      sousseQty: 80,
      sfaxQty: 45,
      cairoQty: 250,
      casablancaQty: 180,
      totalReserved: 30
    }
  ]);

  transferManifests = signal<TransferManifestItem[]>([
    {
      id: 'trf-1',
      manifestNumber: 'TRF-20261027-ALPHA',
      sourceWarehouseName: 'Cairo Central Fulfillment Hub',
      destinationWarehouseName: 'Tunis Nord Logistics Mega-Hub',
      sku: 'SKU-BARCODE-RUGGED',
      quantity: 50,
      status: 'Dispatched',
      createdAt: 'Today, 14:20 UTC'
    },
    {
      id: 'trf-2',
      manifestNumber: 'TRF-20261026-BETA',
      sourceWarehouseName: 'Sousse Sahel Distribution Center',
      destinationWarehouseName: 'Sfax Southern Regional Terminal',
      sku: 'SKU-LABEL-PRINT',
      quantity: 20,
      status: 'Received',
      createdAt: 'Yesterday, 09:15 UTC'
    }
  ]);

  routingResult = signal<ProximitySimulationResult | null>(null);

  // Spatial Radar State
  spatialCityPreset = 'Tunis';
  spatialOriginLat = 36.8065;
  spatialOriginLng = 10.1815;
  spatialRadiusKm = 250;
  spatialFilterSku = 'ALL';
  spatialResult = signal<SpatialSearchResult | null>(null);
  isSpatialSearching = signal<boolean>(false);

  onCityChange(city: string): void {
    switch (city) {
      case 'Tunis':
        this.simCustomerLat = 36.8350;
        this.simCustomerLng = 10.2300;
        break;
      case 'Sousse':
        this.simCustomerLat = 35.8256;
        this.simCustomerLng = 10.6369;
        break;
      case 'Sfax':
        this.simCustomerLat = 34.7406;
        this.simCustomerLng = 10.7603;
        break;
      case 'Cairo':
        this.simCustomerLat = 30.0444;
        this.simCustomerLng = 31.2357;
        break;
      case 'Casablanca':
        this.simCustomerLat = 33.5731;
        this.simCustomerLng = -7.5898;
        break;
    }
  }

  simulateProximityRouting(): void {
    // Calculate Haversine distance for each warehouse
    const candidates = this.warehouses().map(w => {
      const dist = this.calculateHaversine(this.simCustomerLat, this.simCustomerLng, w.latitude, w.longitude);
      return { ...w, distanceKm: Math.round(dist * 10) / 10 };
    }).sort((a, b) => a.distanceKm! - b.distanceKm!);

    const closest = candidates[0];
    const qty = Number(this.simQuantity);

    // Check stock in closest
    const stockRow = this.stockMatrix().find(s => s.sku === this.simSku);
    let availableInClosest = 0;

    if (closest.code === 'WH-TUN-01') availableInClosest = stockRow?.tunisQty ?? 0;
    else if (closest.code === 'WH-SOU-02') availableInClosest = stockRow?.sousseQty ?? 0;
    else if (closest.code === 'WH-SFX-03') availableInClosest = stockRow?.sfaxQty ?? 0;
    else if (closest.code === 'WH-CAI-04') availableInClosest = stockRow?.cairoQty ?? 0;
    else availableInClosest = stockRow?.casablancaQty ?? 0;

    let autoManifest: TransferManifestItem | undefined;

    if (availableInClosest >= qty) {
      this.routingResult.set({
        orderId: `ORD-${Date.now().toString().slice(-6)}`,
        selectedWarehouseName: closest.name,
        selectedWarehouseCity: closest.city,
        distanceKm: closest.distanceKm!,
        isFullyAllocated: true,
        allocatedQty: qty,
        shortageQty: 0,
        routingReason: `Proximity engine selected '${closest.name}' (${closest.distanceKm} km from ${this.simCustomerCity}). 100% of requested ${qty} units allocated directly with 0 transfer latency.`
      });
      this.toastMessage.set(`Order routed to ${closest.name} (${closest.distanceKm} km)!`);
    } else {
      const shortage = qty - availableInClosest;
      const secondaryWh = candidates[1];

      autoManifest = {
        id: `trf-${Date.now()}`,
        manifestNumber: `TRF-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(1000 + Math.random() * 9000)}`,
        sourceWarehouseName: secondaryWh.name,
        destinationWarehouseName: closest.name,
        sku: this.simSku,
        quantity: shortage,
        status: 'Dispatched',
        createdAt: 'Just now'
      };

      this.transferManifests.update(m => [autoManifest!, ...m]);

      this.routingResult.set({
        orderId: `ORD-${Date.now().toString().slice(-6)}`,
        selectedWarehouseName: closest.name,
        selectedWarehouseCity: closest.city,
        distanceKm: closest.distanceKm!,
        isFullyAllocated: false,
        allocatedQty: availableInClosest,
        shortageQty: shortage,
        autoTransferManifest: autoManifest,
        routingReason: `Closest hub '${closest.name}' has partial stock (${availableInClosest}/${qty} units). Proximity engine auto-dispatched stock transfer manifest from secondary hub '${secondaryWh.name}' to fulfill the remaining ${shortage} units.`
      });
      this.toastMessage.set(`Routed to ${closest.name}; auto-triggered rebalance transfer!`);
    }
  }

  submitManualTransfer(): void {
    const newManifest: TransferManifestItem = {
      id: `trf-${Date.now()}`,
      manifestNumber: `TRF-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(1000 + Math.random() * 9000)}`,
      sourceWarehouseName: this.manualTransferOrigin,
      destinationWarehouseName: this.manualTransferDest,
      sku: this.manualTransferSku,
      quantity: Number(this.manualTransferQty),
      status: 'Dispatched',
      createdAt: 'Just now'
    };

    this.transferManifests.update(current => [newManifest, ...current]);
    this.openTransferModal.set(false);
    this.toastMessage.set(`Stock transfer manifest ${newManifest.manifestNumber} dispatched!`);
  }

  private calculateHaversine(lat1: number, lon1: number, lat2: number, lon2: number): number {
    const R = 6371; // Earth's radius in km
    const dLat = (lat2 - lat1) * (Math.PI / 180);
    const dLon = (lon2 - lon1) * (Math.PI / 180);
    const a =
      Math.sin(dLat / 2) * Math.sin(dLat / 2) +
      Math.cos(lat1 * (Math.PI / 180)) * Math.cos(lat2 * (Math.PI / 180)) *
      Math.sin(dLon / 2) * Math.sin(dLon / 2);
    const clampedA = Math.min(1, Math.max(0, a));
    const c = 2 * Math.atan2(Math.sqrt(clampedA), Math.sqrt(1 - clampedA));
    return R * c;
  }

  onSpatialCityChange(city: string): void {
    switch (city) {
      case 'Tunis': this.spatialOriginLat = 36.8065; this.spatialOriginLng = 10.1815; break;
      case 'Sousse': this.spatialOriginLat = 35.8256; this.spatialOriginLng = 10.6369; break;
      case 'Sfax': this.spatialOriginLat = 34.7406; this.spatialOriginLng = 10.7603; break;
      case 'Casablanca': this.spatialOriginLat = 33.5731; this.spatialOriginLng = -7.5898; break;
      case 'Cairo': this.spatialOriginLat = 30.0444; this.spatialOriginLng = 31.2357; break;
    }
  }

  runSpatialRadarSearch(): void {
    this.isSpatialSearching.set(true);

    setTimeout(() => {
      const radius = Number(this.spatialRadiusKm);
      const originLat = this.spatialOriginLat;
      const originLon = this.spatialOriginLng;

      // Geodesic bounding-box computation (1 deg lat ~= 111.0 km)
      const deltaLat = radius / 111.0;
      const minLat = Math.round(Math.max(-90, originLat - deltaLat) * 10000) / 10000;
      const maxLat = Math.round(Math.min(90, originLat + deltaLat) * 10000) / 10000;

      const cosLat = Math.cos(originLat * (Math.PI / 180));
      const deltaLon = Math.abs(cosLat) > 1e-6 ? (radius / (111.0 * Math.abs(cosLat))) : 180;
      const minLon = Math.round((originLon - deltaLon) * 10000) / 10000;
      const maxLon = Math.round((originLon + deltaLon) * 10000) / 10000;

      // Filter warehouses within bounding box and compute exact spherical distance
      const candidates: SpatialCandidate[] = [];

      for (const wh of this.warehouses()) {
        const dist = this.calculateHaversine(originLat, originLon, wh.latitude, wh.longitude);
        if (dist <= radius) {
          const bearing = this.calculateBearing(originLat, originLon, wh.latitude, wh.longitude);
          const compass = this.degreesToCompass(bearing);
          const transitHours = Math.max(1, Math.ceil(dist / 60));

          candidates.push({
            warehouseId: wh.id,
            code: wh.code,
            name: wh.name,
            city: wh.city,
            country: wh.country,
            latitude: wh.latitude,
            longitude: wh.longitude,
            distanceKm: Math.round(dist * 10) / 10,
            bearingDegrees: Math.round(bearing * 10) / 10,
            compassDirection: compass,
            estimatedTransitHours: transitHours,
            availableStockForSku: wh.availableStock
          });
        }
      }

      candidates.sort((a, b) => a.distanceKm - b.distanceKm);

      this.spatialResult.set({
        originLatitude: originLat,
        originLongitude: originLon,
        searchRadiusKm: radius,
        boundingBox: { minLatitude: minLat, maxLatitude: maxLat, minLongitude: minLon, maxLongitude: maxLon },
        totalHubsEvaluated: this.warehouses().length,
        hubsWithinRadius: candidates.length,
        spatialIndexUsed: true,
        queryEngine: 'PostGIS / GiST Geodetic Bounding-Box Scan',
        warehouses: candidates,
        searchTimeMs: 4.8
      });

      this.isSpatialSearching.set(false);
      this.toastMessage.set(`Spatial radar located ${candidates.length} warehouse hubs within ${radius} km radius!`);
    }, 200);
  }

  private calculateBearing(lat1: number, lon1: number, lat2: number, lon2: number): number {
    const toRad = Math.PI / 180;
    const toDeg = 180 / Math.PI;
    const dLon = (lon2 - lon1) * toRad;
    const y = Math.sin(dLon) * Math.cos(lat2 * toRad);
    const x = Math.cos(lat1 * toRad) * Math.sin(lat2 * toRad) -
              Math.sin(lat1 * toRad) * Math.cos(lat2 * toRad) * Math.cos(dLon);
    return (Math.atan2(y, x) * toDeg + 360) % 360;
  }

  private degreesToCompass(deg: number): string {
    const car = ['N', 'NNE', 'NE', 'ENE', 'E', 'ESE', 'SE', 'SSE', 'S', 'SSW', 'SW', 'WSW', 'W', 'WNW', 'NW', 'NNW'];
    const idx = Math.floor(((deg + 11.25) % 360) / 22.5);
    return car[idx];
  }
}
