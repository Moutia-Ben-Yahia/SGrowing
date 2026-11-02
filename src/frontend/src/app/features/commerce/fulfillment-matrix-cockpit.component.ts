import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface WarehouseUi {
  id: string;
  code: string;
  name: string;
  country: string;
  stock: number;
  buffer: number;
  status: string;
}

export interface FulfillmentPlanUi {
  id: string;
  orderId: string;
  destination: string;
  assignedWarehouse: string;
  distanceKm: number;
  estShippingUsd: number;
  isSplit: boolean;
}

@Component({
  selector: 'app-fulfillment-matrix-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="fulfillment-matrix-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Multi-Warehouse Proximity Order Allocation</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              Single-Package Split Shield Active
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Haversine geodetic distance routing, automated split-shipment prevention, and inter-hub safety stock rebalancing.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="planNewOrder()"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-plan-order">
            + Simulate Order Allocation
          </button>
        </div>
      </div>

      <!-- Fulfillment KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="fulfillment-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Regional Warehouses</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-warehouses-count">{{ warehouses().length }} Hubs</div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">US, EU & APAC Distributed</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Split Shipment Rate</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono" data-testid="kpi-split-rate">0.8%</div>
          <div class="text-[11px] text-slate-400 mt-1">&darr; 94% Package Consolidation</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Average Transit Distance</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono">142 km</div>
          <div class="text-[11px] text-slate-400 mt-1">Haversine Geodetic Routing</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Total Live Inventory Units</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-total-stock">
            {{ totalStockUnits() | number:'1.0-0' }}
          </div>
          <div class="text-[11px] text-indigo-600 mt-1 font-semibold">Safety Stock Preserved</div>
        </div>
      </div>

      <!-- Main Workbench -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Distribution Hubs Radar -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Regional Fulfillment Facilities</h3>
            <span class="text-xs text-slate-400 font-mono">{{ warehouses().length }} facilities</span>
          </div>

          <div class="space-y-3" data-testid="warehouses-list">
            @for (wh of warehouses(); track wh.id) {
              <div class="p-4 border rounded-lg hover:border-slate-300 transition-colors space-y-2">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-slate-800 text-sm">{{ wh.name }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-slate-100 text-slate-700">
                      {{ wh.code }} &bull; {{ wh.country }}
                    </span>
                  </div>
                  <span class="px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
                    {{ wh.status }}
                  </span>
                </div>

                <div class="grid grid-cols-2 gap-4 pt-2 border-t text-xs">
                  <div>
                    <span class="text-slate-400 block text-[11px]">Current Available Stock</span>
                    <span class="font-bold text-slate-900 font-mono">{{ wh.stock | number:'1.0-0' }} units</span>
                  </div>
                  <div>
                    <span class="text-slate-400 block text-[11px]">Safety Buffer Threshold</span>
                    <span class="font-bold text-amber-700 font-mono">{{ wh.buffer | number:'1.0-0' }} units</span>
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Recent Order Allocation Plans -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Proximity Order Plans</h3>
          <p class="text-xs text-slate-500">
            Real-time geodetic allocation minimizing shipping zone transit times and courier costs.
          </p>

          <div class="space-y-3" data-testid="recent-plans-list">
            @for (plan of plans(); track plan.id) {
              <div class="p-3 border rounded-lg text-xs space-y-1.5">
                <div class="flex justify-between items-center">
                  <span class="font-mono font-bold text-slate-800">{{ plan.orderId }}</span>
                  <span class="font-mono font-semibold text-emerald-600">\${{ plan.estShippingUsd | number:'1.2-2' }}</span>
                </div>
                <div class="text-[11px] text-slate-600">
                  Dest: <strong>{{ plan.destination }}</strong> &rarr; Hub: <strong>{{ plan.assignedWarehouse }}</strong>
                </div>
                <div class="flex justify-between items-center text-[10px] text-slate-400 font-mono">
                  <span>{{ plan.distanceKm }} km Haversine</span>
                  <span class="text-emerald-700 font-bold">Single Package</span>
                </div>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class FulfillmentMatrixCockpitComponent {
  warehouses = signal<WarehouseUi[]>([
    { id: 'wh_1', code: 'EWR-01', name: 'Newark Regional Hub', country: 'US', stock: 18500, buffer: 1000, status: 'Active' },
    { id: 'wh_2', code: 'RNO-02', name: 'Reno Pacific Distribution', country: 'US', stock: 14200, buffer: 800, status: 'Active' },
    { id: 'wh_3', code: 'FRA-01', name: 'Frankfurt Continental Hub', country: 'DE', stock: 22000, buffer: 1500, status: 'Active' },
    { id: 'wh_4', code: 'SIN-01', name: 'Singapore Southeast Gateway', country: 'SG', stock: 16800, buffer: 1200, status: 'Active' }
  ]);

  plans = signal<FulfillmentPlanUi[]>([
    { id: 'p1', orderId: 'ORD-9841', destination: 'Brooklyn, NY, USA', assignedWarehouse: 'EWR-01 (Newark)', distanceKm: 24.5, estShippingUsd: 12.70, isSplit: false },
    { id: 'p2', orderId: 'ORD-9842', destination: 'Munich, Germany', assignedWarehouse: 'FRA-01 (Frankfurt)', distanceKm: 304.2, estShippingUsd: 14.95, isSplit: false }
  ]);

  totalStockUnits = computed(() => {
    return this.warehouses().reduce((sum, w) => sum + w.stock, 0);
  });

  planNewOrder() {
    const newPlan: FulfillmentPlanUi = {
      id: 'p_' + Date.now(),
      orderId: 'ORD-' + Math.floor(1000 + Math.random() * 9000),
      destination: 'San Francisco, CA, USA',
      assignedWarehouse: 'RNO-02 (Reno)',
      distanceKm: 312.0,
      estShippingUsd: 15.20,
      isSplit: false
    };

    this.plans.update(list => [newPlan, ...list]);
  }
}
