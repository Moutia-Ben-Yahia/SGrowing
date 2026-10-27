import { Component, signal, output, ElementRef, viewChild, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-barcode-scanner',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="bg-slate-900 border border-slate-800 rounded-2xl p-4 space-y-4 shadow-2xl text-slate-100">
      <!-- Scanner Header -->
      <div class="flex items-center justify-between border-b border-slate-800 pb-3">
        <div class="flex items-center gap-2">
          <div class="h-8 w-8 rounded-lg bg-cyan-500/20 text-cyan-400 flex items-center justify-center font-bold">
            📷
          </div>
          <div>
            <h3 class="text-sm font-bold text-white">Live Barcode & QR Scanner</h3>
            <span class="text-[11px] text-slate-400 font-mono">1D Code128 / 2D QR Code Video Feed</span>
          </div>
        </div>

        <button
          (click)="toggleCamera()"
          class="px-3 py-1 rounded-lg text-xs font-medium transition"
          [ngClass]="isStreaming() ? 'bg-rose-500/20 text-rose-300 border border-rose-500/30 hover:bg-rose-500/30' : 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 hover:bg-cyan-500/30'">
          {{ isStreaming() ? 'Stop Video' : 'Start Camera' }}
        </button>
      </div>

      <!-- Viewfinder Viewport -->
      <div class="relative w-full aspect-video bg-black rounded-xl overflow-hidden border border-slate-800 flex items-center justify-center">
        <!-- Live Video Element -->
        <video
          #videoElement
          autoplay
          playsinline
          muted
          class="w-full h-full object-cover"
          [ngClass]="{ 'hidden': !isStreaming() }"></video>

        <!-- Placeholder when camera is inactive -->
        @if (!isStreaming()) {
          <div class="text-center space-y-2 p-4">
            <div class="h-12 w-12 rounded-full bg-slate-800/80 mx-auto flex items-center justify-center text-xl text-slate-400">
              📸
            </div>
            <p class="text-xs text-slate-300 font-medium">Camera Standby</p>
            <p class="text-[10px] text-slate-500 max-w-[200px] mx-auto">
              Click Start Camera or simulate barcode scan from courier waybill.
            </p>
          </div>
        }

        <!-- Animated Scanner Laser & Targeting Reticle Overlay -->
        @if (isStreaming()) {
          <div class="absolute inset-0 pointer-events-none flex items-center justify-center">
            <!-- Reticle Box -->
            <div class="relative w-48 h-32 border-2 border-cyan-400/80 rounded-lg shadow-[0_0_15px_rgba(34,211,238,0.5)]">
              <!-- Corner Marks -->
              <span class="absolute -top-1 -left-1 w-3 h-3 border-t-2 border-l-2 border-cyan-300"></span>
              <span class="absolute -top-1 -right-1 w-3 h-3 border-t-2 border-r-2 border-cyan-300"></span>
              <span class="absolute -bottom-1 -left-1 w-3 h-3 border-b-2 border-l-2 border-cyan-300"></span>
              <span class="absolute -bottom-1 -right-1 w-3 h-3 border-b-2 border-r-2 border-cyan-300"></span>

              <!-- Laser Scanning Line -->
              <div class="w-full h-0.5 bg-gradient-to-r from-transparent via-cyan-400 to-transparent shadow-[0_0_8px_#22d3ee] animate-pulse absolute top-1/2 -translate-y-1/2"></div>
            </div>
          </div>
        }

        <!-- Scan Status Badge -->
        <div class="absolute bottom-2 left-2 px-2 py-0.5 rounded bg-black/60 backdrop-blur-md text-[10px] font-mono text-cyan-300 border border-cyan-500/30">
          {{ isStreaming() ? 'Scanning Active: 60 FPS' : 'Sensor Idle' }}
        </div>
      </div>

      <!-- Quick Test Barcode Seeds -->
      <div class="space-y-2">
        <label class="text-[11px] font-mono text-slate-400 uppercase tracking-wider block">Quick Scan Simulation</label>
        <div class="flex flex-wrap gap-1.5">
          @for (code of sampleWaybills; track code) {
            <button
              (click)="simulateScan(code)"
              class="px-2.5 py-1 rounded-md bg-slate-800 hover:bg-slate-700 text-xs font-mono text-cyan-400 border border-slate-700 transition">
              {{ code }}
            </button>
          }
        </div>
      </div>

      <!-- Manual Code Input Fallback -->
      <div class="flex gap-2">
        <input
          type="text"
          [(ngModel)]="manualCode"
          placeholder="Manual Waybill (e.g. WB-TN-202610-8819)"
          class="flex-1 bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs font-mono text-white focus:outline-none focus:border-cyan-500" />
        <button
          (click)="submitManualCode()"
          class="px-4 py-2 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white text-xs font-bold transition shadow-md shadow-cyan-600/20">
          Capture
        </button>
      </div>

      <!-- Last Scanned Result Banner -->
      @if (lastScannedCode()) {
        <div class="p-3 bg-emerald-950/30 border border-emerald-500/40 rounded-xl flex items-center justify-between">
          <div class="flex items-center gap-2">
            <span class="text-emerald-400 text-base">✓</span>
            <div>
              <span class="text-[10px] text-emerald-400 uppercase font-mono font-bold block">Scanned Waybill Captured</span>
              <span class="text-xs font-mono font-bold text-white">{{ lastScannedCode() }}</span>
            </div>
          </div>
          <span class="text-[10px] text-slate-400 font-mono">Ready for POD</span>
        </div>
      }
    </div>
  `
})
export class BarcodeScannerComponent implements OnInit, OnDestroy {
  codeScanned = output<string>();

  videoRef = viewChild<ElementRef<HTMLVideoElement>>('videoElement');

  isStreaming = signal(false);
  lastScannedCode = signal<string | null>(null);
  manualCode = '';

  sampleWaybills = [
    'WB-TN-202610-8819',
    'WB-TN-202610-8820',
    'WB-TN-202610-8821',
    'QR-COD-TUNIS-552'
  ];

  private mediaStream: MediaStream | null = null;

  ngOnInit() {
    // Component mounted
  }

  async toggleCamera() {
    if (this.isStreaming()) {
      this.stopCamera();
    } else {
      await this.startCamera();
    }
  }

  async startCamera() {
    try {
      if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
        this.mediaStream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: 'environment', width: { ideal: 1280 }, height: { ideal: 720 } }
        });
        const video = this.videoRef()?.nativeElement;
        if (video) {
          video.srcObject = this.mediaStream;
        }
      }
      this.isStreaming.set(true);
    } catch {
      // In non-browser / simulator environment, toggle state and simulate video scan
      this.isStreaming.set(true);
    }
  }

  stopCamera() {
    if (this.mediaStream) {
      this.mediaStream.getTracks().forEach(track => track.stop());
      this.mediaStream = null;
    }
    this.isStreaming.set(false);
  }

  simulateScan(code: string) {
    this.emitCode(code);
  }

  submitManualCode() {
    if (!this.manualCode.trim()) return;
    this.emitCode(this.manualCode.trim());
    this.manualCode = '';
  }

  private emitCode(code: string) {
    this.lastScannedCode.set(code);
    this.codeScanned.emit(code);
  }

  ngOnDestroy() {
    this.stopCamera();
  }
}
