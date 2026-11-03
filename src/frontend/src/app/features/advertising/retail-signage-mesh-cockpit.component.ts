import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface InStoreScreenProfile {
  screenId: string;
  storeLocation: string;
  displayLocation: string;
  screenResolution: string;
  hourlyFootfallCount: number;
  currentSponsoredBrand: string;
}

export interface ShelfInventoryRecord {
  shelfId: string;
  associatedScreenId: string;
  productName: string;
  currentUnitsInStock: number;
  minimumStockThreshold: number;
  isAdActive: boolean;
}

@Component({
  selector: 'app-retail-signage-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="retail-signage-mesh-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-cyan-500 animate-pulse"></span>
            Retail Media In-Store Digital Signage & Smart Shelf Bidding Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-Time Shelf Weight & RFID Inventory-Triggered Programmatic Bidding Engine with Instant OOS Ad Suppression
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="restockShelf()" class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white font-medium rounded-lg shadow-sm transition">
            Simulate Shelf Restock Event
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">In-Store Screens</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ screens().length }}</p>
          <span class="text-xs text-cyan-400">4K Endcaps & Coolers</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Planogram Shelves</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">{{ shelves().length }}</p>
          <span class="text-xs text-slate-400">RFID / Weight Monitored</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">OOS Suppression</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">100.0%</p>
          <span class="text-xs text-slate-400">Zero Wasted Brand Ad Spend</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Ad Fill Rate</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">{{ screenAdFillRatePct() }}%</p>
          <span class="text-xs text-emerald-400">Dynamic CPM Maximizer</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Screens Table -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Connected Digital Displays</h2>
          <div class="space-y-3">
            <div *ngFor="let s of screens()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-medium text-slate-200 text-sm">{{ s.displayLocation }}</span>
                <span class="px-2 py-0.5 text-xs bg-slate-700 text-slate-300 rounded font-mono">{{ s.screenResolution }}</span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Location: {{ s.storeLocation }} | Footfall: {{ s.hourlyFootfallCount }}/hr</p>
              <div class="p-2 bg-slate-950 rounded border border-slate-800 text-xs flex justify-between items-center">
                <span class="text-slate-400">Active Brand Sponsor:</span>
                <span class="text-emerald-400 font-semibold">{{ s.currentSponsoredBrand }}</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Shelves Table -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Planogram Smart Shelf Sensors</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Shelf / Product</th>
                  <th class="pb-3 font-medium">Stock Level</th>
                  <th class="pb-3 font-medium">Threshold</th>
                  <th class="pb-3 font-medium">Ad Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let sh of shelves()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3">
                    <span class="font-medium text-slate-200 block">{{ sh.productName }}</span>
                    <span class="font-mono text-[10px] text-slate-400">{{ sh.shelfId }}</span>
                  </td>
                  <td class="py-3 font-mono font-bold" [ngClass]="sh.currentUnitsInStock < sh.minimumStockThreshold ? 'text-rose-400' : 'text-emerald-400'">
                    {{ sh.currentUnitsInStock }} units
                  </td>
                  <td class="py-3 font-mono text-slate-400 text-xs">{{ sh.minimumStockThreshold }}</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-[10px] rounded font-semibold font-mono"
                          [ngClass]="sh.isAdActive ? 'bg-emerald-500/20 text-emerald-300' : 'bg-rose-500/20 text-rose-300'">
                      {{ sh.isAdActive ? 'SPONSORED_ACTIVE' : 'OOS_SUPPRESSED' }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  `
})
export class RetailSignageMeshCockpitComponent {
  screens = signal<InStoreScreenProfile[]>([
    { screenId: 'SCR-NYC-FLAGSHIP-01', storeLocation: 'Manhattan Flagship Store #104', displayLocation: 'Premium Endcap Aisle 4 (Organic Beverages)', screenResolution: '3840x2160 4K HDR', hourlyFootfallCount: 420, currentSponsoredBrand: 'Electrolyte Hydration Labs' },
    { screenId: 'SCR-LDN-OXFORD-02', storeLocation: 'London Oxford Street #202', displayLocation: 'Checkout Lane 3 Digital Merchandiser', screenResolution: '1920x1080 FHD', hourlyFootfallCount: 680, currentSponsoredBrand: 'BioActive Snack Bars' }
  ]);

  shelves = signal<ShelfInventoryRecord[]>([
    { shelfId: 'SHLF-BEV-01', associatedScreenId: 'SCR-NYC-FLAGSHIP-01', productName: 'Sparkling Organic Electrolyte 500ml', currentUnitsInStock: 24, minimumStockThreshold: 5, isAdActive: true },
    { shelfId: 'SHLF-SNK-02', associatedScreenId: 'SCR-LDN-OXFORD-02', productName: 'Salted Caramel Protein Crisps 60g', currentUnitsInStock: 3, minimumStockThreshold: 5, isAdActive: false }
  ]);

  screenAdFillRatePct = signal(96.5);

  restockShelf() {
    this.shelves.update(list => list.map(item =>
      item.shelfId === 'SHLF-SNK-02'
        ? { ...item, currentUnitsInStock: 25, isAdActive: true }
        : item
    ));
  }
}
