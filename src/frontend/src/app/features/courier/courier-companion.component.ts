import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface MobileStopUi {
  id: string;
  orderNumber: string;
  sequence: number;
  customerName: string;
  customerPhone: string;
  address: string;
  amount: number;
  status: 'Pending' | 'Delivered' | 'Failed';
  mapsUrl: string;
}

@Component({
  selector: 'app-courier-companion',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex flex-col min-h-screen bg-slate-950 text-slate-100 font-sans max-w-md mx-auto border-x border-slate-800 shadow-2xl">
      <!-- Top Driver App Bar -->
      <header class="p-4 bg-slate-900 border-b border-slate-800 sticky top-0 z-30 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="h-10 w-10 rounded-xl bg-amber-500/20 text-amber-400 flex items-center justify-center font-bold text-lg border border-amber-500/30">
            🛵
          </div>
          <div>
            <h1 class="text-sm font-bold text-white leading-tight">Driver Companion</h1>
            <div class="flex items-center gap-1.5 text-[11px] text-slate-400 font-mono">
              <span class="h-2 w-2 rounded-full bg-emerald-400 animate-pulse"></span>
              <span>Walid M. (215 TN 4821)</span>
            </div>
          </div>
        </div>

        <a routerLink="/operations" class="px-2.5 py-1 rounded-lg bg-slate-800 text-[11px] text-slate-300 font-medium hover:bg-slate-700 transition">
          Exit to Desk
        </a>
      </header>

      <!-- Cash in Hand & Daily Progress Card -->
      <section class="p-4 bg-gradient-to-b from-slate-900/90 to-slate-950 border-b border-slate-800 space-y-3">
        <div class="grid grid-cols-2 gap-3">
          <div class="p-3 bg-slate-950 rounded-xl border border-amber-500/30">
            <span class="text-[10px] text-slate-400 uppercase font-mono tracking-wider block">Cash In Hand</span>
            <div class="text-xl font-bold font-mono text-amber-400 mt-0.5">{{ cashInHand() | number:'1.2-2' }} TND</div>
            <span class="text-[10px] text-slate-500 block mt-0.5">Physical drawer balance</span>
          </div>
          <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
            <span class="text-[10px] text-slate-400 uppercase font-mono tracking-wider block">Run-Sheet Stops</span>
            <div class="text-xl font-bold font-mono text-emerald-400 mt-0.5">{{ completedCount() }} / {{ stops().length }}</div>
            <span class="text-[10px] text-slate-500 block mt-0.5">{{ pendingCount() }} pending delivery</span>
          </div>
        </div>

        <!-- Quick Actions Row -->
        <div class="flex gap-2">
          <button
            (click)="triggerSync()"
            class="flex-1 py-2 px-3 rounded-lg bg-slate-800/80 hover:bg-slate-800 border border-slate-700 text-xs text-slate-200 flex items-center justify-center gap-2 transition font-medium">
            <span>🔄</span> Sync Route Stops
          </button>
          <button
            (click)="triggerEndDayReconciliation()"
            class="py-2 px-3 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-medium transition shadow-md shadow-emerald-600/20">
            💵 Handover Vault Cash
          </button>
        </div>
      </section>

      <!-- Route Delivery Stops List -->
      <main class="flex-1 p-4 space-y-3 overflow-y-auto">
        <div class="flex items-center justify-between text-xs text-slate-400 font-mono">
          <span class="uppercase font-semibold tracking-wider">Today's Assigned Route</span>
          <span class="text-amber-400 font-bold">Zone: Grand Tunis</span>
        </div>

        @for (stop of stops(); track stop.id) {
          <div
            class="p-4 rounded-xl border transition-all space-y-3"
            [ngClass]="{
              'bg-slate-900/80 border-slate-800': stop.status === 'Pending',
              'bg-emerald-950/20 border-emerald-500/30 opacity-75': stop.status === 'Delivered',
              'bg-rose-950/20 border-rose-500/30': stop.status === 'Failed'
            }">
            <div class="flex items-start justify-between">
              <div class="flex items-center gap-2.5">
                <span class="h-6 w-6 rounded-full bg-slate-800 text-slate-300 flex items-center justify-center text-xs font-mono font-bold">
                  {{ stop.sequence }}
                </span>
                <div>
                  <h3 class="text-sm font-bold text-white">{{ stop.customerName }}</h3>
                  <span class="text-xs text-cyan-400 font-mono">{{ stop.orderNumber }}</span>
                </div>
              </div>

              <div class="text-right">
                <span class="text-sm font-bold font-mono text-emerald-400">{{ stop.amount }} TND</span>
                <span class="text-[10px] uppercase font-bold block"
                  [ngClass]="{
                    'text-amber-400': stop.status === 'Pending',
                    'text-emerald-400': stop.status === 'Delivered',
                    'text-rose-400': stop.status === 'Failed'
                  }">
                  {{ stop.status }}
                </span>
              </div>
            </div>

            <!-- Address and 1-tap Nav -->
            <div class="p-2.5 rounded-lg bg-slate-950/70 border border-slate-800/80 text-xs text-slate-300 flex items-center justify-between">
              <div class="flex items-center gap-2">
                <span>📍</span>
                <span class="leading-relaxed">{{ stop.address }}</span>
              </div>
              <a
                [href]="stop.mapsUrl"
                target="_blank"
                class="px-2 py-1 rounded bg-blue-600/20 text-blue-400 border border-blue-500/30 text-[11px] font-medium whitespace-nowrap">
                Waze / Maps
              </a>
            </div>

            <!-- Action buttons for driver -->
            @if (stop.status === 'Pending') {
              <div class="grid grid-cols-2 gap-2 pt-1">
                <a
                  [href]="'tel:' + stop.customerPhone"
                  class="py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium flex items-center justify-center gap-1.5 transition">
                  <span>📞</span> Call Client
                </a>
                <button
                  (click)="openPodModal(stop)"
                  class="py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold transition shadow-md shadow-emerald-600/20 flex items-center justify-center gap-1.5">
                  <span>📸</span> Record POD
                </button>
              </div>
            } @else {
              <div class="text-[11px] text-slate-400 font-mono flex items-center gap-1.5">
                <span>✓</span> Doorstep Digital Proof of Delivery Saved (Cash Logged)
              </div>
            }
          </div>
        }
      </main>

      <!-- Doorstep Proof-of-Delivery Modal -->
      @if (activeModalStop()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-end sm:items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-sm p-5 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-sm font-bold text-white">Doorstep POD & Cash Collection</h3>
                <span class="text-xs text-cyan-400 font-mono">{{ activeModalStop()!.orderNumber }}</span>
              </div>
              <button (click)="activeModalStop.set(null)" class="text-slate-400 hover:text-white text-lg">✕</button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="text-slate-400 block mb-1">Cash Collected (TND)</label>
                <input
                  type="number"
                  [(ngModel)]="podCashAmount"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-white font-mono font-bold text-base focus:border-emerald-500 focus:outline-none" />
              </div>

              <div>
                <label class="text-slate-400 block mb-1">Recipient Name</label>
                <input
                  type="text"
                  [(ngModel)]="podRecipientName"
                  placeholder="e.g. Kais Saidi (Self)"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-white focus:outline-none" />
              </div>

              <!-- Camera upload simulation -->
              <div class="p-3 bg-slate-950 rounded-xl border border-dashed border-slate-700 text-center space-y-1">
                <span class="text-lg">📷</span>
                <p class="text-[11px] text-slate-300 font-medium">Doorstep Parcel Photo Attached</p>
                <span class="text-[10px] text-emerald-400 font-mono">GPS: 36.8782°N, 10.3247°E verified</span>
              </div>

              <!-- Digital signature canvas simulation -->
              <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1 text-center">
                <span class="text-[10px] text-slate-400 uppercase font-mono block">Customer Digital Signature</span>
                <div class="h-16 w-full bg-slate-900 rounded-lg flex items-center justify-center font-mono italic text-emerald-400 text-xs border border-slate-800">
                  ✍️ Signature Verified on Glass
                </div>
              </div>
            </div>

            <button
              (click)="confirmPodSubmission()"
              class="w-full py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold transition shadow-lg shadow-emerald-600/30">
              Confirm Delivery & Log {{ podCashAmount }} TND
            </button>
          </div>
        </div>
      }
    </div>
  `
})
export class CourierCompanionComponent {
  cashInHand = signal(425.5);

  stops = signal<MobileStopUi[]>([
    {
      id: 'stop-1',
      orderNumber: 'ORD-202610-8819',
      sequence: 1,
      customerName: 'Kais Saidi',
      customerPhone: '+21698223344',
      address: '12 Rue Sidi Bou Said, La Marsa, Tunis',
      amount: 157.0,
      status: 'Pending',
      mapsUrl: 'https://maps.google.com/?q=La+Marsa+Tunis'
    },
    {
      id: 'stop-2',
      orderNumber: 'ORD-202610-8820',
      sequence: 2,
      customerName: 'Sarra Ben Ahmed',
      customerPhone: '+21622334455',
      address: '45 Avenue Habib Bourguiba, Carthage, Tunis',
      amount: 120.0,
      status: 'Pending',
      mapsUrl: 'https://maps.google.com/?q=Carthage+Tunis'
    },
    {
      id: 'stop-3',
      orderNumber: 'ORD-202610-8821',
      sequence: 3,
      customerName: 'Mehdi Khelil',
      customerPhone: '+21655667788',
      address: '8 Rue Ibn Khaldoun, El Menzah 6, Ariana',
      amount: 148.5,
      status: 'Delivered',
      mapsUrl: 'https://maps.google.com/?q=El+Menzah+Ariana'
    }
  ]);

  completedCount = signal(1);
  pendingCount = signal(2);

  activeModalStop = signal<MobileStopUi | null>(null);
  podCashAmount = 0;
  podRecipientName = '';

  openPodModal(stop: MobileStopUi) {
    this.activeModalStop.set(stop);
    this.podCashAmount = stop.amount;
    this.podRecipientName = stop.customerName;
  }

  confirmPodSubmission() {
    const s = this.activeModalStop();
    if (!s) return;

    s.status = 'Delivered';
    this.cashInHand.update(val => val + this.podCashAmount);
    this.completedCount.update(c => c + 1);
    this.pendingCount.update(p => Math.max(0, p - 1));
    this.activeModalStop.set(null);

    alert(`Doorstep Delivery for ${s.orderNumber} successfully registered! Collected ${this.podCashAmount} TND. Cash drawer updated.`);
  }

  triggerSync() {
    alert('Synchronized with SGrowing Central Dispatch: Run-sheet route up to date.');
  }

  triggerEndDayReconciliation() {
    alert(`Handed over ${this.cashInHand()} TND to central hub vault. Cash drawer zeroed out. Reconciliation receipt generated.`);
    this.cashInHand.set(0);
  }
}
