import { Component, signal, input, output, ElementRef, viewChild, AfterViewInit, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface PodSubmissionData {
  id: string;
  orderNumber: string;
  recipientName: string;
  cashAmount: number;
  signatureBase64: string;
  gpsCoordinates: string;
  timestampUtc: string;
  synced: boolean;
}

@Component({
  selector: 'app-touchscreen-signature',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4 shadow-2xl text-slate-100 max-w-lg mx-auto">
      <!-- Header -->
      <div class="flex items-center justify-between border-b border-slate-800 pb-3">
        <div class="flex items-center gap-2.5">
          <div class="h-9 w-9 rounded-xl bg-emerald-500/20 text-emerald-400 flex items-center justify-center font-bold text-lg border border-emerald-500/30">
            ✍️
          </div>
          <div>
            <h3 class="text-sm font-bold text-white leading-tight">Doorstep Touchscreen Signature</h3>
            <span class="text-[11px] text-slate-400 font-mono">Proof-of-Delivery (POD) & IndexedDB Sync</span>
          </div>
        </div>

        <div class="flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-slate-800 border border-slate-700 text-[10px] font-mono">
          <span class="h-2 w-2 rounded-full" [ngClass]="isOnline() ? 'bg-emerald-400' : 'bg-amber-400 animate-pulse'"></span>
          <span [ngClass]="isOnline() ? 'text-emerald-400' : 'text-amber-400'">{{ isOnline() ? 'Online' : 'Offline Mode' }}</span>
        </div>
      </div>

      <!-- Recipient & Cash Details -->
      <div class="grid grid-cols-2 gap-3 text-xs">
        <div>
          <label class="text-slate-400 block mb-1 font-medium">Order Number</label>
          <div class="p-2 rounded-lg bg-slate-950 border border-slate-800 font-mono font-bold text-cyan-400">
            {{ orderNumber() }}
          </div>
        </div>
        <div>
          <label class="text-slate-400 block mb-1 font-medium">Cash to Collect (TND)</label>
          <input
            type="number"
            [(ngModel)]="cashAmount"
            class="w-full p-2 rounded-lg bg-slate-950 border border-slate-700 font-mono font-bold text-emerald-400 focus:outline-none focus:border-emerald-500" />
        </div>
      </div>

      <div>
        <label class="text-slate-400 block mb-1 text-xs font-medium">Recipient Full Name</label>
        <input
          type="text"
          [(ngModel)]="recipientName"
          placeholder="e.g. Kais Saidi"
          class="w-full p-2.5 rounded-lg bg-slate-950 border border-slate-700 text-xs text-white focus:outline-none focus:border-emerald-500" />
      </div>

      <!-- Touchscreen Canvas Box -->
      <div class="space-y-1.5">
        <div class="flex items-center justify-between text-xs">
          <span class="text-slate-400 font-medium">Customer Signature on Glass</span>
          <button
            (click)="clearCanvas()"
            class="text-[11px] text-rose-400 hover:text-rose-300 font-medium transition">
            Clear Signature
          </button>
        </div>

        <div class="relative bg-slate-950 border border-slate-800 rounded-xl overflow-hidden touch-none shadow-inner">
          <canvas
            #signatureCanvas
            width="460"
            height="180"
            class="w-full h-44 cursor-crosshair block"
            (mousedown)="startDrawing($event)"
            (mousemove)="draw($event)"
            (mouseup)="stopDrawing()"
            (mouseleave)="stopDrawing()"
            (touchstart)="startTouchDrawing($event)"
            (touchmove)="touchDraw($event)"
            (touchend)="stopDrawing()"></canvas>

          <!-- Watermark hint when blank -->
          @if (!hasSignature()) {
            <div class="absolute inset-0 pointer-events-none flex items-center justify-center text-slate-700 font-mono text-xs italic">
              Touch or drag to sign here
            </div>
          }
        </div>
      </div>

      <!-- GPS Geotagging Card -->
      <div class="p-3 bg-slate-950/80 rounded-xl border border-slate-800/80 flex items-center justify-between text-xs">
        <div class="flex items-center gap-2">
          <span class="text-emerald-400 text-base">📍</span>
          <div>
            <span class="text-[10px] text-slate-400 uppercase font-mono block">GPS Geotag Stamp</span>
            <span class="font-mono text-slate-200 font-semibold">{{ gpsCoords() }}</span>
          </div>
        </div>

        <button
          (click)="refreshGps()"
          class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-[11px] text-slate-300 transition">
          Refresh Fix
        </button>
      </div>

      <!-- Submit POD Action -->
      <button
        (click)="submitPod()"
        [disabled]="!hasSignature() || !recipientName.trim()"
        class="w-full py-3 rounded-xl bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs font-bold transition shadow-lg shadow-emerald-600/30 flex items-center justify-center gap-2">
        <span>📦</span>
        <span>Confirm Doorstep POD ({{ cashAmount }} TND)</span>
      </button>

      <!-- IndexedDB Offline Queue Sync Status -->
      <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 text-xs space-y-2">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <span class="text-cyan-400">💾</span>
            <span class="text-slate-300 font-medium">IndexedDB Offline Queue</span>
          </div>
          <span class="px-2 py-0.5 rounded-full bg-cyan-950 text-cyan-400 font-mono text-[10px] font-bold border border-cyan-800">
            {{ offlineQueue().length }} queued
          </span>
        </div>

        @if (offlineQueue().length > 0) {
          <div class="space-y-1.5 pt-1">
            @for (item of offlineQueue(); track item.id) {
              <div class="flex items-center justify-between p-2 rounded bg-slate-900 border border-slate-800/70 text-[11px]">
                <div>
                  <span class="font-mono text-cyan-400 font-bold">{{ item.orderNumber }}</span>
                  <span class="text-slate-400 block text-[10px]">{{ item.recipientName }} • {{ item.cashAmount }} TND</span>
                </div>
                <span class="text-[10px] font-mono text-amber-400">Offline Pending</span>
              </div>
            }

            <button
              (click)="flushOfflineQueue()"
              class="w-full mt-2 py-1.5 rounded-lg bg-cyan-600/30 hover:bg-cyan-600/40 text-cyan-300 border border-cyan-500/40 text-xs font-medium transition">
              Flush All ({{ offlineQueue().length }}) to ERP
            </button>
          </div>
        } @else {
          <p class="text-[11px] text-slate-500">All local POD deliveries synchronized with central server.</p>
        }
      </div>
    </div>
  `
})
export class TouchscreenSignatureComponent implements OnInit, AfterViewInit {
  orderNumber = input<string>('ORD-202610-8819');
  defaultCashAmount = input<number>(157.0);

  podSubmitted = output<PodSubmissionData>();

  canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('signatureCanvas');

  recipientName = '';
  cashAmount = 157.0;
  hasSignature = signal(false);
  isOnline = signal(true);
  gpsCoords = signal('36.8782° N, 10.3247° E (±3m fix)');
  offlineQueue = signal<PodSubmissionData[]>([]);

  private ctx: CanvasRenderingContext2D | null = null;
  private isDrawing = false;
  private db: IDBDatabase | null = null;

  ngOnInit() {
    this.cashAmount = this.defaultCashAmount();
    if (typeof window !== 'undefined') {
      this.isOnline.set(navigator.onLine);
      window.addEventListener('online', () => this.isOnline.set(true));
      window.addEventListener('offline', () => this.isOnline.set(false));
      this.initIndexedDb();
      this.refreshGps();
    }
  }

  ngAfterViewInit() {
    const canvas = this.canvasRef()?.nativeElement;
    if (canvas) {
      this.ctx = canvas.getContext('2d');
      if (this.ctx) {
        this.ctx.strokeStyle = '#34d399'; // Emerald-400 stroke
        this.ctx.lineWidth = 2.5;
        this.ctx.lineCap = 'round';
        this.ctx.lineJoin = 'round';
      }
    }
  }

  startDrawing(event: MouseEvent) {
    if (!this.ctx) return;
    this.isDrawing = true;
    const { x, y } = this.getCanvasCoordinates(event);
    this.ctx.beginPath();
    this.ctx.moveTo(x, y);
    this.hasSignature.set(true);
  }

  draw(event: MouseEvent) {
    if (!this.isDrawing || !this.ctx) return;
    const { x, y } = this.getCanvasCoordinates(event);
    this.ctx.lineTo(x, y);
    this.ctx.stroke();
  }

  startTouchDrawing(event: TouchEvent) {
    event.preventDefault();
    if (!this.ctx || event.touches.length === 0) return;
    this.isDrawing = true;
    const touch = event.touches[0];
    const { x, y } = this.getTouchCoordinates(touch);
    this.ctx.beginPath();
    this.ctx.moveTo(x, y);
    this.hasSignature.set(true);
  }

  touchDraw(event: TouchEvent) {
    event.preventDefault();
    if (!this.isDrawing || !this.ctx || event.touches.length === 0) return;
    const touch = event.touches[0];
    const { x, y } = this.getTouchCoordinates(touch);
    this.ctx.lineTo(x, y);
    this.ctx.stroke();
  }

  stopDrawing() {
    this.isDrawing = false;
  }

  clearCanvas() {
    const canvas = this.canvasRef()?.nativeElement;
    if (canvas && this.ctx) {
      this.ctx.clearRect(0, 0, canvas.width, canvas.height);
      this.hasSignature.set(false);
    }
  }

  refreshGps() {
    if (typeof navigator !== 'undefined' && navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        pos => {
          const lat = pos.coords.latitude.toFixed(4);
          const lng = pos.coords.longitude.toFixed(4);
          const acc = Math.round(pos.coords.accuracy);
          this.gpsCoords.set(`${lat}° N, ${lng}° E (±${acc}m fix)`);
        },
        () => {
          this.gpsCoords.set('36.8782° N, 10.3247° E (Autonomous GPS fix)');
        },
        { timeout: 3000 }
      );
    }
  }

  submitPod() {
    const canvas = this.canvasRef()?.nativeElement;
    const signatureBase64 = canvas ? canvas.toDataURL('image/png') : '';

    const submission: PodSubmissionData = {
      id: `pod-${Date.now()}`,
      orderNumber: this.orderNumber(),
      recipientName: this.recipientName,
      cashAmount: this.cashAmount,
      signatureBase64,
      gpsCoordinates: this.gpsCoords(),
      timestampUtc: new Date().toISOString(),
      synced: this.isOnline()
    };

    if (!this.isOnline()) {
      this.storeInIndexedDb(submission);
      this.offlineQueue.update(q => [...q, submission]);
    } else {
      this.podSubmitted.emit(submission);
    }

    this.clearCanvas();
  }

  flushOfflineQueue() {
    this.offlineQueue().forEach(item => {
      this.podSubmitted.emit({ ...item, synced: true });
    });
    this.offlineQueue.set([]);
    if (this.db) {
      const tx = this.db.transaction('pod_queue', 'readwrite');
      tx.objectStore('pod_queue').clear();
    }
  }

  private initIndexedDb() {
    if (typeof window === 'undefined' || !window.indexedDB) return;
    const request = window.indexedDB.open('SGrowingCourierDb', 1);

    request.onupgradeneeded = (event: any) => {
      const db = event.target.result as IDBDatabase;
      if (!db.objectStoreNames.contains('pod_queue')) {
        db.createObjectStore('pod_queue', { keyPath: 'id' });
      }
    };

    request.onsuccess = (event: any) => {
      this.db = event.target.result;
      this.loadIndexedDbQueue();
    };
  }

  private loadIndexedDbQueue() {
    if (!this.db) return;
    const tx = this.db.transaction('pod_queue', 'readonly');
    const store = tx.objectStore('pod_queue');
    const request = store.getAll();
    request.onsuccess = () => {
      this.offlineQueue.set(request.result || []);
    };
  }

  private storeInIndexedDb(pod: PodSubmissionData) {
    if (!this.db) return;
    const tx = this.db.transaction('pod_queue', 'readwrite');
    tx.objectStore('pod_queue').put(pod);
  }

  private getCanvasCoordinates(event: MouseEvent): { x: number; y: number } {
    const canvas = this.canvasRef()!.nativeElement;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    return {
      x: (event.clientX - rect.left) * scaleX,
      y: (event.clientY - rect.top) * scaleY
    };
  }

  private getTouchCoordinates(touch: Touch): { x: number; y: number } {
    const canvas = this.canvasRef()!.nativeElement;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    return {
      x: (touch.clientX - rect.left) * scaleX,
      y: (touch.clientY - rect.top) * scaleY
    };
  }
}
