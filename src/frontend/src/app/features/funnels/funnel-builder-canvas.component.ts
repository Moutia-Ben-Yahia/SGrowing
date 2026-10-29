import { Component, OnInit, OnDestroy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';

export interface FunnelPipelineStep {
  number: number;
  name: string;
  status: 'Completed' | 'Running' | 'Pending';
  description: string;
  durationMs: number;
}

export interface FunnelSectionItem {
  id: string;
  type: string;
  name: string;
  order: number;
  icon: string;
  isEnabled: boolean;
  settings: Record<string, any>;
  liquidSnippet: string;
}

export interface QuantityTier {
  id: number;
  title: string;
  badge: string;
  quantity: number;
  priceMultiplier: number;
  discountPct: number;
  shipping: string;
  isPopular: boolean;
  isBestValue: boolean;
}

@Component({
  selector: 'app-funnel-builder-canvas',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Top Navigation Header -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 py-3.5 flex items-center justify-between sticky top-0 z-50">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90 transition">
            SGrowing
          </a>
          <span class="text-slate-700">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-bold text-white">Autonomous 15-Step Idea-to-Funnel Studio</span>
            <span class="text-[11px] px-2.5 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-semibold flex items-center gap-1.5">
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
              Liquid Section Engine • Horizon 11
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          @if (isPublished()) {
            <span class="inline-flex items-center gap-1.5 text-xs text-emerald-400 font-mono bg-emerald-500/10 border border-emerald-500/30 px-3 py-1.5 rounded-lg">
              <span class="w-2 h-2 rounded-full bg-emerald-400 animate-ping"></span>
              LIVE: {{ publishedSlug() }}
            </span>
          }

          <button
            (click)="publishFunnel()"
            [disabled]="isGenerating()"
            class="px-4 py-2 bg-gradient-to-r from-emerald-500 to-teal-600 hover:from-emerald-400 hover:to-teal-500 disabled:opacity-50 text-slate-950 font-black rounded-xl text-xs shadow-lg shadow-emerald-950/30 transition flex items-center gap-2 active:scale-95">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>
            {{ isPublished() ? 'Republish Funnel' : 'Publish Funnel Live' }}
          </button>
        </div>
      </header>

      <!-- Main Workspace -->
      <div class="flex-1 p-6 max-w-[1600px] w-full mx-auto space-y-6">
        <!-- Funnel Concept Prompt & Generation Bar -->
        <div class="bg-gradient-to-r from-slate-900 via-slate-900/95 to-slate-950 border border-slate-800 rounded-2xl p-6 shadow-2xl space-y-4">
          <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-slate-800/80 pb-4">
            <div>
              <h1 class="text-lg font-black text-white flex items-center gap-2">
                <span>🚀 Autonomous 15-Step Idea-to-Funnel Pipeline</span>
                <span class="text-xs font-mono font-normal text-slate-400">(Autonnel &amp; Ever-Works Spec)</span>
              </h1>
              <p class="text-xs text-slate-400 mt-1">
                Enter your product concept, target audience, and price point. The pipeline deterministically executes all 15 stages to compile a production-ready COD funnel.
              </p>
            </div>

            <!-- Preset buttons -->
            <div class="flex items-center gap-2 flex-wrap">
              <span class="text-xs text-slate-500 font-semibold">Presets:</span>
              <button (click)="applyPreset('leather')" class="text-xs px-2.5 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition">
                👜 Leather Messenger Bag (49 TND)
              </button>
              <button (click)="applyPreset('charger')" class="text-xs px-2.5 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition">
                ⚡ Wireless Magnetic Charger (39 TND)
              </button>
              <button (click)="applyPreset('serum')" class="text-xs px-2.5 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition">
                🌿 Organic Argan Serum (59 TND)
              </button>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-12 gap-3 items-center">
            <div class="md:col-span-5">
              <label class="block text-[11px] font-semibold text-slate-400 mb-1">Concept Prompt / Product Title</label>
              <input
                [(ngModel)]="conceptPrompt"
                type="text"
                placeholder="e.g. Artisan Handcrafted Full-Grain Leather Messenger Bag"
                class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2.5 text-xs text-white focus:outline-none focus:border-emerald-500 transition">
            </div>

            <div class="md:col-span-2">
              <label class="block text-[11px] font-semibold text-slate-400 mb-1">Product SKU</label>
              <input
                [(ngModel)]="productSku"
                type="text"
                placeholder="SKU-LEATHER-01"
                class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2.5 text-xs text-white font-mono focus:outline-none focus:border-emerald-500 transition">
            </div>

            <div class="md:col-span-2">
              <label class="block text-[11px] font-semibold text-slate-400 mb-1">Base Price</label>
              <div class="flex">
                <input
                  [(ngModel)]="pricePoint"
                  type="number"
                  min="1"
                  class="w-full bg-slate-950 border border-slate-800 rounded-l-xl px-3 py-2.5 text-xs text-white font-mono focus:outline-none focus:border-emerald-500 transition">
                <select
                  [(ngModel)]="currency"
                  class="bg-slate-900 border border-l-0 border-slate-800 rounded-r-xl px-2.5 py-2.5 text-xs text-emerald-400 font-bold focus:outline-none">
                  <option value="TND">TND</option>
                  <option value="EGP">EGP</option>
                  <option value="USD">USD</option>
                  <option value="MAD">MAD</option>
                  <option value="SAR">SAR</option>
                </select>
              </div>
            </div>

            <div class="md:col-span-3 flex items-end">
              <button
                (click)="triggerPipeline()"
                [disabled]="isGenerating()"
                class="w-full py-2.5 bg-gradient-to-r from-emerald-500 via-teal-500 to-cyan-500 hover:from-emerald-400 hover:to-cyan-400 disabled:opacity-50 text-slate-950 font-black rounded-xl text-xs transition shadow-lg shadow-emerald-950/40 flex items-center justify-center gap-2">
                @if (isGenerating()) {
                  <svg class="animate-spin h-3.5 w-3.5 text-slate-950" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                  <span>Synthesizing Step {{ currentExecutingStep() }}/15...</span>
                } @else {
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
                  <span>Execute 15-Step Pipeline</span>
                }
              </button>
            </div>
          </div>
        </div>

        <!-- Split Studio: Left (Inspector & Stepper) & Right (Mobile Viewport Preview) -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          <!-- Left Column: Stepper & Declarative Section Tree Inspector (7 cols) -->
          <div class="lg:col-span-7 space-y-5">
            <!-- 15-Step Pipeline Progress Timeline -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-3">
              <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                <div class="flex items-center gap-2">
                  <span class="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-pulse"></span>
                  <h2 class="text-xs font-black uppercase tracking-wider text-white">15-Step Deterministic Funnel Pipeline</h2>
                </div>
                <div class="flex items-center gap-3">
                  <span class="text-xs font-mono font-bold text-emerald-400">{{ overallProgress() }}% Complete</span>
                  <span class="text-[11px] font-mono text-slate-500">Total: {{ totalExecutionTimeMs() }}ms</span>
                </div>
              </div>

              <!-- Animated Progress Bar -->
              <div class="w-full bg-slate-950 h-2 rounded-full overflow-hidden border border-slate-800">
                <div
                  class="bg-gradient-to-r from-emerald-500 via-teal-400 to-cyan-400 h-2 transition-all duration-300"
                  [style.width.%]="overallProgress()">
                </div>
              </div>

              <!-- Steps List (Scrollable) -->
              <div class="space-y-1.5 max-h-[320px] overflow-y-auto pr-1">
                @for (step of steps(); track step.number) {
                  <div
                    class="p-2.5 rounded-xl border transition text-xs flex items-center justify-between"
                    [class.bg-slate-950]="step.status === 'Completed'"
                    [class.border-slate-800]="step.status === 'Completed'"
                    [class.bg-emerald-950/20]="step.status === 'Running'"
                    [class.border-emerald-500/50]="step.status === 'Running'"
                    [class.opacity-40]="step.status === 'Pending'">
                    <div class="flex items-center gap-2.5">
                      <span
                        class="w-5 h-5 rounded-full flex items-center justify-center font-bold text-[10px]"
                        [class.bg-emerald-500]="step.status === 'Completed'"
                        [class.text-slate-950]="step.status === 'Completed'"
                        [class.bg-emerald-400]="step.status === 'Running'"
                        [class.animate-spin]="step.status === 'Running'"
                        [class.bg-slate-800]="step.status === 'Pending'"
                        [class.text-slate-400]="step.status === 'Pending'">
                        @if (step.status === 'Completed') {
                          ✓
                        } @else if (step.status === 'Running') {
                          ⟳
                        } @else {
                          {{ step.number }}
                        }
                      </span>
                      <div>
                        <div class="font-bold text-slate-200 text-[11px]">
                          {{ step.number }}. {{ step.name }}
                        </div>
                        <div class="text-[10px] text-slate-400 leading-tight">
                          {{ step.description }}
                        </div>
                      </div>
                    </div>
                    <span class="font-mono text-[10px] text-slate-500 whitespace-nowrap pl-2">
                      {{ step.durationMs }}ms
                    </span>
                  </div>
                }
              </div>
            </div>

            <!-- Declarative Section Tree Inspector -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
              <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                <div class="flex items-center gap-2">
                  <span class="text-sm">📑</span>
                  <h2 class="text-xs font-black uppercase tracking-wider text-white">Declarative Section Tree Inspector</h2>
                </div>
                <!-- Tabs -->
                <div class="flex items-center gap-1 bg-slate-950 p-1 rounded-xl border border-slate-800 text-xs">
                  <button
                    (click)="activeTab.set('tree')"
                    [class.bg-slate-800]="activeTab() === 'tree'"
                    [class.text-white]="activeTab() === 'tree'"
                    [class.text-slate-400]="activeTab() !== 'tree'"
                    class="px-2.5 py-1 rounded-lg font-medium transition">
                    Tree Inspector
                  </button>
                  <button
                    (click)="activeTab.set('schema')"
                    [class.bg-slate-800]="activeTab() === 'schema'"
                    [class.text-white]="activeTab() === 'schema'"
                    [class.text-slate-400]="activeTab() !== 'schema'"
                    class="px-2.5 py-1 rounded-lg font-medium transition">
                    Liquid Schema
                  </button>
                  <button
                    (click)="activeTab.set('tokens')"
                    [class.bg-slate-800]="activeTab() === 'tokens'"
                    [class.text-white]="activeTab() === 'tokens'"
                    [class.text-slate-400]="activeTab() !== 'tokens'"
                    class="px-2.5 py-1 rounded-lg font-medium transition">
                    Tailwind Tokens
                  </button>
                </div>
              </div>

              @if (activeTab() === 'tree') {
                <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                  @for (sec of sections(); track sec.id) {
                    <div
                      (click)="selectedSectionId.set(sec.id)"
                      [class.border-emerald-500]="selectedSectionId() === sec.id"
                      [class.bg-emerald-950/20]="selectedSectionId() === sec.id"
                      [class.border-slate-800]="selectedSectionId() !== sec.id"
                      [class.bg-slate-950]="selectedSectionId() !== sec.id"
                      class="p-3.5 rounded-xl border cursor-pointer hover:border-slate-700 transition flex items-center justify-between">
                      <div class="flex items-center gap-2.5">
                        <span class="text-lg">{{ sec.icon }}</span>
                        <div>
                          <div class="text-xs font-bold text-white">{{ sec.name }}</div>
                          <div class="text-[10px] text-slate-400 font-mono">{{ sec.type }} (Order: {{ sec.order }})</div>
                        </div>
                      </div>
                      <span class="text-[10px] px-2 py-0.5 rounded-full bg-slate-800 text-emerald-400 font-mono font-bold">
                        ACTIVE
                      </span>
                    </div>
                  }
                </div>

                <!-- Selected Section Details -->
                @if (selectedSection()) {
                  <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                    <div class="flex items-center justify-between text-xs font-bold text-emerald-400 border-b border-slate-800 pb-2">
                      <span>Section: {{ selectedSection().name }}</span>
                      <span class="font-mono text-slate-400">{{ selectedSection().id }}</span>
                    </div>
                    <pre class="text-[11px] font-mono text-slate-300 p-2.5 bg-slate-900 rounded-lg overflow-x-auto leading-relaxed">{{ selectedSection().liquidSnippet }}</pre>
                  </div>
                }
              } @else if (activeTab() === 'schema') {
                <div class="p-3 rounded-xl bg-slate-950 border border-slate-800">
                  <pre class="text-[11px] font-mono text-emerald-300/90 max-h-[360px] overflow-y-auto leading-relaxed">{{ compiledSchemaJson() }}</pre>
                </div>
              } @else if (activeTab() === 'tokens') {
                <div class="grid grid-cols-2 sm:grid-cols-3 gap-3 text-xs">
                  <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
                    <span class="text-[10px] text-slate-400 uppercase font-semibold">Primary Brand</span>
                    <div class="flex items-center gap-2 mt-1">
                      <span class="w-4 h-4 rounded-full bg-emerald-500 border border-white/20"></span>
                      <span class="font-mono text-white font-bold">#10b981 (Emerald)</span>
                    </div>
                  </div>
                  <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
                    <span class="text-[10px] text-slate-400 uppercase font-semibold">Conversion Accent</span>
                    <div class="flex items-center gap-2 mt-1">
                      <span class="w-4 h-4 rounded-full bg-amber-500 border border-white/20"></span>
                      <span class="font-mono text-white font-bold">#f59e0b (Amber Bump)</span>
                    </div>
                  </div>
                  <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
                    <span class="text-[10px] text-slate-400 uppercase font-semibold">Base Surface</span>
                    <div class="flex items-center gap-2 mt-1">
                      <span class="w-4 h-4 rounded-full bg-slate-900 border border-white/20"></span>
                      <span class="font-mono text-white font-bold">#0f172a (Slate 900)</span>
                    </div>
                  </div>
                  <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
                    <span class="text-[10px] text-slate-400 uppercase font-semibold">Mobile Breakpoint</span>
                    <div class="font-mono text-white font-bold mt-1">375px - 430px (iPhone)</div>
                  </div>
                  <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
                    <span class="text-[10px] text-slate-400 uppercase font-semibold">Typography Scale</span>
                    <div class="font-mono text-white font-bold mt-1">Inter (800 / 600 / 400)</div>
                  </div>
                  <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
                    <span class="text-[10px] text-slate-400 uppercase font-semibold">Container Max</span>
                    <div class="font-mono text-white font-bold mt-1">448px (max-w-md)</div>
                  </div>
                </div>
              }
            </div>

            <!-- Order Bump Expected Value (ROI) Card -->
            <div class="bg-gradient-to-br from-amber-950/20 via-slate-900 to-slate-950 border border-amber-500/30 rounded-2xl p-5 shadow-xl space-y-3">
              <div class="flex items-center justify-between border-b border-amber-500/20 pb-2.5">
                <div class="flex items-center gap-2">
                  <span class="text-base">💎</span>
                  <h3 class="text-xs font-black uppercase tracking-wider text-amber-400">Order Bump EV &amp; Net Profit Lift Calculator</h3>
                </div>
                <span class="text-[10px] px-2.5 py-0.5 rounded-full bg-amber-500/10 text-amber-300 border border-amber-500/30 font-bold">
                  EV = P_bump * Price * Margin - Friction
                </span>
              </div>

              <div class="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs">
                <div class="p-3 bg-slate-950/60 rounded-xl border border-slate-800">
                  <span class="text-[10px] text-slate-400">Bump Price (-50%)</span>
                  <div class="text-base font-black text-amber-400 mt-0.5">14.00 {{ currency() }}</div>
                </div>
                <div class="p-3 bg-slate-950/60 rounded-xl border border-slate-800">
                  <span class="text-[10px] text-slate-400">Est. Take Rate</span>
                  <div class="text-base font-black text-cyan-400 mt-0.5">28.0%</div>
                </div>
                <div class="p-3 bg-slate-950/60 rounded-xl border border-slate-800">
                  <span class="text-[10px] text-slate-400">Gross Margin</span>
                  <div class="text-base font-black text-emerald-400 mt-0.5">75.0%</div>
                </div>
                <div class="p-3 bg-slate-950/60 rounded-xl border border-slate-800">
                  <span class="text-[10px] text-slate-400">Expected Value EV</span>
                  <div class="text-base font-black text-emerald-400 mt-0.5">+{{ orderBumpRoi().expectedValueBump }} {{ currency() }}</div>
                </div>
              </div>

              <div class="flex items-center justify-between text-xs text-slate-300 pt-1 border-t border-slate-800/80">
                <span class="text-[11px] text-slate-400">
                  Gross Profit: <b class="text-white">{{ orderBumpRoi().grossContribution }} {{ currency() }}</b> | Friction Cost: <b class="text-red-400">-{{ orderBumpRoi().frictionCost }} {{ currency() }}</b>
                </span>
                <span class="text-[11px] font-bold text-emerald-400">
                  Net AOV Lift: +{{ orderBumpRoi().netAovLiftPct }}%
                </span>
              </div>
            </div>
          </div>

          <!-- Right Column: Interactive Smartphone Viewport Preview (5 cols) -->
          <div class="lg:col-span-5 flex flex-col items-center">
            <div class="flex items-center justify-between w-full max-w-[390px] mb-3 px-2">
              <span class="text-xs font-bold text-slate-300 flex items-center gap-1.5">
                <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                Mobile Viewport Live Simulation (390 x 844)
              </span>
              <span class="text-[10px] font-mono text-slate-500">iPhone 15 Pro Frame</span>
            </div>

            <!-- Realistic Smartphone Frame -->
            <div class="w-full max-w-[390px] bg-slate-950 rounded-[48px] p-3 shadow-2xl border-4 border-slate-700 ring-1 ring-slate-800 relative">
              <!-- Speaker Notch / Dynamic Island -->
              <div class="absolute top-5 left-1/2 -translate-x-1/2 w-28 h-5 bg-black rounded-full z-50 flex items-center justify-end px-2 gap-1.5 border border-slate-800/60">
                <span class="w-2 h-2 rounded-full bg-slate-900 border border-slate-700"></span>
                <span class="w-1.5 h-1.5 rounded-full bg-emerald-500/80"></span>
              </div>

              <!-- Smartphone Screen -->
              <div class="w-full h-[760px] bg-slate-900 rounded-[38px] overflow-y-auto overflow-x-hidden text-slate-100 relative scroll-smooth border border-slate-800/80 select-none pb-20">
                <!-- Status Bar -->
                <div class="h-9 px-6 flex items-center justify-between text-[11px] font-semibold text-slate-300 sticky top-0 bg-slate-900/90 backdrop-blur z-40 border-b border-slate-800/40">
                  <span>9:41</span>
                  <div class="flex items-center gap-1.5">
                    <span class="text-[10px]">5G</span>
                    <span class="text-[10px]">100%</span>
                  </div>
                </div>

                <!-- 1. Urgency Announcement Bar -->
                <div class="bg-emerald-600 text-white text-[10px] font-bold py-1.5 px-3 text-center flex items-center justify-center gap-1.5 tracking-wide">
                  <span class="w-1.5 h-1.5 rounded-full bg-white animate-ping"></span>
                  <span>FLASH SALE: Free 24h Express COD Delivery</span>
                </div>

                <!-- 2. Hero Product Gallery & Rating -->
                <div class="p-4 border-b border-slate-800/80 bg-slate-900/60">
                  <div class="aspect-square bg-slate-800 rounded-2xl overflow-hidden relative border border-slate-700/60 shadow-lg flex items-center justify-center">
                    <div class="text-center p-4">
                      <div class="w-20 h-20 mx-auto mb-2 rounded-2xl bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-4xl shadow-inner">
                        🛍️
                      </div>
                      <span class="text-xs font-bold text-white block">{{ conceptPrompt() }}</span>
                      <span class="text-[10px] font-mono text-emerald-400 mt-0.5 block">{{ productSku() }}</span>
                    </div>
                    <div class="absolute top-2.5 left-2.5 bg-red-600 text-white text-[9px] font-black uppercase px-2 py-0.5 rounded-full shadow">
                      Save Up To 25% Today
                    </div>
                    <!-- Live Ticking Countdown Badge -->
                    <div class="absolute bottom-2.5 right-2.5 bg-slate-950/90 border border-slate-700 text-amber-400 text-[10px] font-mono font-bold px-2.5 py-0.5 rounded-full shadow flex items-center gap-1">
                      <span>⏳</span>
                      <span>{{ countdownTimer() }}</span>
                    </div>
                  </div>

                  <div class="flex items-center justify-between mt-3 px-1 text-[11px]">
                    <div class="flex items-center gap-1 text-amber-400 font-bold">
                      ★★★★★ <span class="text-white font-mono ml-0.5">4.9/5</span>
                    </div>
                    <span class="text-slate-400 text-[10px]">(487 Verified Regional Orders)</span>
                  </div>
                </div>

                <!-- 3. Headline & Copy Narrative -->
                <div class="p-4 space-y-2.5 border-b border-slate-800/80">
                  <h2 class="text-sm font-black text-white leading-snug">
                    Stop Settling For Cheap Clones: Discover Handcrafted Luxury Delivered Direct
                  </h2>
                  <p class="text-[11px] text-slate-300 leading-relaxed">
                    Tired of flimsy imitations that wear out in weeks? Order with complete confidence. Inspect your package at the door and pay the courier only after verification.
                  </p>
                  <div class="space-y-1.5 pt-1">
                    <div class="flex items-center gap-1.5 text-[11px] text-slate-200">
                      <span class="text-emerald-400 font-bold text-xs">✓</span>
                      <span>100% Certified High-Grade Materials</span>
                    </div>
                    <div class="flex items-center gap-1.5 text-[11px] text-slate-200">
                      <span class="text-emerald-400 font-bold text-xs">✓</span>
                      <span>Zero-Risk Cash On Delivery (Doorstep Inspection)</span>
                    </div>
                    <div class="flex items-center gap-1.5 text-[11px] text-slate-200">
                      <span class="text-emerald-400 font-bold text-xs">✓</span>
                      <span>Free 24h Express Shipping on 2+ Packs</span>
                    </div>
                  </div>
                </div>

                <!-- 4. Interactive Quantity Break Tier Cards -->
                <div class="p-4 space-y-2.5 border-b border-slate-800/80 bg-slate-950/40">
                  <div class="flex items-center justify-between">
                    <span class="text-[11px] font-bold uppercase tracking-wider text-white">Select Quantity Bundle:</span>
                    <span class="text-[10px] text-emerald-400 font-semibold">✓ Free Delivery</span>
                  </div>

                  <div class="space-y-2">
                    @for (tier of quantityTiers(); track tier.id) {
                      <div
                        (click)="selectedPack.set(tier.id)"
                        [class.border-emerald-500]="selectedPack() === tier.id"
                        [class.bg-emerald-950/20]="selectedPack() === tier.id"
                        [class.ring-1]="selectedPack() === tier.id"
                        [class.ring-emerald-500/50]="selectedPack() === tier.id"
                        [class.border-slate-800]="selectedPack() !== tier.id"
                        [class.bg-slate-900/60]="selectedPack() !== tier.id"
                        class="p-3 rounded-xl border transition cursor-pointer hover:border-slate-700">
                        <div class="flex items-center justify-between">
                          <div class="flex items-center gap-2">
                            <input
                              type="radio"
                              name="preview_tier"
                              [checked]="selectedPack() === tier.id"
                              class="accent-emerald-500">
                            <div>
                              <div class="font-bold text-xs text-white">{{ tier.title }}</div>
                              <div class="text-[10px] text-slate-400">{{ tier.shipping }}</div>
                            </div>
                          </div>
                          <div class="text-right">
                            <div class="font-black text-xs text-emerald-400">
                              {{ calculateTierPrice(tier.id) }} {{ currency() }}
                            </div>
                            @if (tier.discountPct > 0) {
                              <div class="text-[9px] text-slate-500 line-through">
                                {{ calculateOriginalTierPrice(tier.id) }} {{ currency() }}
                              </div>
                            }
                          </div>
                        </div>
                      </div>
                    }
                  </div>
                </div>

                <!-- 5. Interactive Order Bump Checkbox Widget -->
                <div class="p-3.5 mx-3.5 my-3.5 rounded-xl border-2 border-dashed border-amber-500/60 bg-amber-500/10 space-y-1.5">
                  <div class="flex items-center gap-1.5">
                    <span class="bg-amber-500 text-slate-950 font-black text-[8px] uppercase px-2 py-0.5 rounded-full">
                      50% OFF CHECKOUT EXCLUSIVE
                    </span>
                  </div>
                  <label class="flex items-start gap-2.5 cursor-pointer pt-0.5">
                    <input
                      type="checkbox"
                      [checked]="isOrderBumpChecked()"
                      (change)="isOrderBumpChecked.set(!isOrderBumpChecked())"
                      class="mt-0.5 accent-amber-500 w-4 h-4 rounded">
                    <div class="text-[11px]">
                      <div class="font-bold text-white flex items-center justify-between">
                        <span>+ Add Protective Pouch &amp; Care Kit</span>
                        <span class="text-amber-400 font-black">+14.00 {{ currency() }}</span>
                      </div>
                      <p class="text-[10px] text-slate-300 mt-0.5 leading-tight">
                        Check this box to include our custom-fit protective sleeve and leather conditioning balm.
                      </p>
                    </div>
                  </label>
                </div>

                <!-- 6. 1-Step Cash on Delivery Form -->
                <div id="cod-checkout-form" class="p-4 space-y-3 border-b border-slate-800/80">
                  <div class="border-b border-slate-800 pb-2">
                    <h3 class="text-xs font-black text-white">1-Step Cash on Delivery Form</h3>
                    <p class="text-[10px] text-slate-400">Pay directly to the courier upon delivery.</p>
                  </div>
                  <div class="space-y-2 text-[11px]">
                    <div>
                      <label class="block font-semibold text-slate-300 mb-0.5 text-[10px]">Full Name *</label>
                      <input
                        type="text"
                        placeholder="e.g. Mohamed Ben Salem"
                        value="Mohamed Ben Salem"
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-white text-xs">
                    </div>
                    <div>
                      <label class="block font-semibold text-slate-300 mb-0.5 text-[10px]">Phone Number (Courier Call) *</label>
                      <div class="flex gap-1.5">
                        <span class="bg-slate-800 px-2 py-1.5 rounded-lg text-slate-400 font-mono text-[10px] flex items-center">+216</span>
                        <input
                          type="tel"
                          placeholder="98 123 456"
                          value="98 765 432"
                          class="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-white font-mono text-xs">
                      </div>
                    </div>
                    <div>
                      <label class="block font-semibold text-slate-300 mb-0.5 text-[10px]">Governorate / Wilaya *</label>
                      <select class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2 py-1.5 text-white text-xs">
                        <option>Tunis</option>
                        <option>Ariana</option>
                        <option>Sousse</option>
                        <option>Sfax</option>
                        <option>Bizerte</option>
                        <option>Nabeul</option>
                      </select>
                    </div>
                    <div>
                      <label class="block font-semibold text-slate-300 mb-0.5 text-[10px]">Street Address *</label>
                      <input
                        type="text"
                        placeholder="Avenue Habib Bourguiba"
                        value="12 Avenue Habib Bourguiba, Apt 4"
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-white text-xs">
                    </div>

                    <!-- Live Total Calculation Bar -->
                    <div class="p-2.5 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between text-xs">
                      <div>
                        <div class="text-[10px] text-slate-400">Total COD Amount:</div>
                        @if (isOrderBumpChecked()) {
                          <div class="text-[9px] text-amber-400 font-mono">+14 {{ currency() }} Bump Included</div>
                        }
                      </div>
                      <div class="text-right">
                        <span class="text-sm font-black text-emerald-400">{{ totalPreviewPrice() }} {{ currency() }}</span>
                      </div>
                    </div>

                    <button
                      type="button"
                      class="w-full py-2.5 bg-gradient-to-r from-emerald-500 to-teal-600 hover:from-emerald-400 hover:to-teal-500 text-slate-950 font-black rounded-xl text-xs shadow-lg transition active:scale-95">
                      Confirm COD Order - Pay at Door &rarr;
                    </button>
                  </div>
                </div>

                <!-- 7. Verified Reviews Carousel -->
                <div class="p-4 space-y-2 border-b border-slate-800/80 bg-slate-950/40">
                  <div class="text-center">
                    <span class="text-[10px] font-bold uppercase tracking-wider text-slate-400">Verified Customer Feedback</span>
                  </div>
                  <div class="p-2.5 rounded-xl bg-slate-900 border border-slate-800 text-[10px] space-y-1">
                    <div class="flex items-center justify-between">
                      <span class="font-bold text-white">Kais B. (Tunis)</span>
                      <span class="text-emerald-400 text-[9px] bg-emerald-500/10 px-1.5 py-0.5 rounded">Verified COD Buyer</span>
                    </div>
                    <div class="text-amber-400 text-[10px]">★★★★★</div>
                    <p class="text-slate-300">"Courier arrived in 24 hours. Waited while I checked the package. Very satisfied with the craftsmanship!"</p>
                  </div>
                </div>

                <!-- 8. Accordion FAQs -->
                <div class="p-4 space-y-2">
                  <span class="text-[10px] font-bold uppercase tracking-wider text-slate-400 block mb-1">Frequently Asked Questions</span>
                  <details class="p-2 rounded-lg bg-slate-900 border border-slate-800 text-[10px]">
                    <summary class="font-semibold text-slate-200 cursor-pointer">Can I inspect the parcel before paying?</summary>
                    <p class="text-slate-400 mt-1.5 pt-1.5 border-t border-slate-800">Yes! You inspect the item directly with the delivery agent before paying.</p>
                  </details>
                  <details class="p-2 rounded-lg bg-slate-900 border border-slate-800 text-[10px]">
                    <summary class="font-semibold text-slate-200 cursor-pointer">How long does shipping take?</summary>
                    <p class="text-slate-400 mt-1.5 pt-1.5 border-t border-slate-800">Delivery takes 24 to 48 hours to all governorates.</p>
                  </details>
                </div>

                <!-- 9. Sticky Mobile Add-to-Cart Action Bar -->
                <div class="absolute bottom-0 left-0 right-0 p-2.5 bg-slate-950/95 backdrop-blur border-t border-slate-800 z-50">
                  <div class="flex items-center justify-between gap-2">
                    <div>
                      <div class="text-[9px] text-slate-400 font-semibold">Total COD:</div>
                      <div class="text-xs font-black text-emerald-400">{{ totalPreviewPrice() }} {{ currency() }}</div>
                    </div>
                    <button
                      type="button"
                      class="flex-1 py-2 bg-gradient-to-r from-emerald-500 to-teal-500 text-slate-950 font-black rounded-lg text-[11px] text-center shadow transition animate-pulse">
                      Order COD Now &rarr;
                    </button>
                  </div>
                </div>
              </div>

              <!-- Home Indicator Bar -->
              <div class="w-32 h-1 bg-slate-700 rounded-full mx-auto mt-2"></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Published Success Modal -->
      @if (showPublishModal()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-emerald-500/40 rounded-2xl p-6 max-w-md w-full shadow-2xl space-y-4">
            <div class="w-12 h-12 rounded-full bg-emerald-500/20 border border-emerald-500/40 text-emerald-400 flex items-center justify-center text-2xl mx-auto">
              ✓
            </div>
            <div class="text-center space-y-1">
              <h3 class="text-lg font-black text-white">Funnel Successfully Published!</h3>
              <p class="text-xs text-slate-400">
                Your autonomous 15-step funnel is now live with CAPI pixel tracking and single-step COD checkout.
              </p>
            </div>

            <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
              <span class="text-[10px] text-slate-500 font-semibold uppercase">Public Live URL:</span>
              <div class="text-xs font-mono text-emerald-400 break-all select-all">
                {{ livePublishedUrl() }}
              </div>
            </div>

            <div class="grid grid-cols-2 gap-2 text-xs">
              <a
                [href]="livePublishedUrl()"
                target="_blank"
                class="py-2.5 bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold rounded-xl text-center transition">
                Open Live Funnel ↗
              </a>
              <button
                (click)="showPublishModal.set(false)"
                class="py-2.5 bg-slate-800 hover:bg-slate-700 text-white font-semibold rounded-xl transition">
                Done
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class FunnelBuilderCanvasComponent implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  private auth = inject(AuthService);

  // Form Inputs
  conceptPrompt = signal('Artisan Handcrafted Full-Grain Leather Messenger Bag');
  productSku = signal('SKU-LEATHER-01');
  pricePoint = signal(49);
  currency = signal('TND');

  // Execution State
  isGenerating = signal(false);
  currentExecutingStep = signal(15);
  overallProgress = signal(100);
  isPublished = signal(false);
  publishedSlug = signal('artisan-leather-bag-c4d8e2');
  showPublishModal = signal(false);
  activeTab = signal<'tree' | 'schema' | 'tokens'>('tree');
  selectedSectionId = signal('sec_order_bump');

  // Preview State
  selectedPack = signal(2); // 2 packs = most popular
  isOrderBumpChecked = signal(true);
  countdownSeconds = signal(15 * 60 - 12);
  private timerInterval?: any;

  // 15 Steps Data
  steps = signal<FunnelPipelineStep[]>([
    { number: 1, name: 'Concept Extraction & Product Entity Creation', status: 'Completed', description: 'Extracted product title, SKU, dimensions, and specifications.', durationMs: 42 },
    { number: 2, name: 'Target Audience & Pain Point Taxonomy', status: 'Completed', description: 'Mapped 3 friction points and COD trust triggers for regional buyers.', durationMs: 38 },
    { number: 3, name: 'Value Proposition & Competitive Angle Formulation', status: 'Completed', description: 'Formulated 4-angle competitive positioning and zero-risk COD hook.', durationMs: 35 },
    { number: 4, name: 'AIDA / PAS Copywriting Generation', status: 'Completed', description: 'Authored attention headline, PAS problem-solution, and desire bullets.', durationMs: 45 },
    { number: 5, name: 'Tiered Pricing & Quantity Break Structuring', status: 'Completed', description: 'Structured 1-pack, 2-pack (-15%), and 3-pack (-25%) bundles.', durationMs: 36 },
    { number: 6, name: 'High-Converting Order Bump Identification', status: 'Completed', description: 'Synthesized +14 TND care kit bump with +2.70 TND expected value.', durationMs: 40 },
    { number: 7, name: 'Social Proof & Verified Buyer Reviews Synthesis', status: 'Completed', description: 'Generated 4 verified buyer reviews (4.9/5 stars) across Tunis & Sousse.', durationMs: 30 },
    { number: 8, name: 'FAQ Accordion Item Generation', status: 'Completed', description: 'Created 5 objection-handling FAQs for doorstep inspection and warranty.', durationMs: 28 },
    { number: 9, name: 'Sticky Mobile Add-to-Cart Action Bar Spec', status: 'Completed', description: 'Configured fixed bottom mobile CTA bar with price synchronization.', durationMs: 22 },
    { number: 10, name: 'One-Step No-Cart COD Checkout Configuration', status: 'Completed', description: 'Configured 1-step form with 24 governorates and phone validation.', durationMs: 34 },
    { number: 11, name: 'Urgency & Scarcity Countdown Mechanics', status: 'Completed', description: 'Wired 15-minute rolling countdown timer and scarcity counter (7 left).', durationMs: 25 },
    { number: 12, name: 'Declarative Liquid Section Schema Compilation', status: 'Completed', description: 'Compiled 9 modular Liquid sections into JSON section schema.', durationMs: 48 },
    { number: 13, name: 'Meta / TikTok CAPI Pixel Event Wireup', status: 'Completed', description: 'Wired server-side and browser CAPI events (ViewContent, Purchase).', durationMs: 32 },
    { number: 14, name: 'Mobile Viewport CSS/Tailwind Token Generation', status: 'Completed', description: 'Synthesized 375px-430px Tailwind tokens, emerald accents, and dark theme.', durationMs: 28 },
    { number: 15, name: 'Instant Publishing State & Unique Slug Assignment', status: 'Completed', description: 'Assigned unique slug artisan-leather-bag-c4d8e2 with 1-click publish.', durationMs: 35 }
  ]);

  // Quantity Tiers
  quantityTiers = signal<QuantityTier[]>([
    { id: 1, title: '1 Pack (Trial Pack)', badge: 'Standard', quantity: 1, priceMultiplier: 1.0, discountPct: 0, shipping: 'Standard Shipping', isPopular: false, isBestValue: false },
    { id: 2, title: '2 Packs (Double Pack)', badge: '🔥 MOST POPULAR - SAVE 15%', quantity: 2, priceMultiplier: 1.70, discountPct: 15, shipping: 'FREE 24h Express Delivery', isPopular: true, isBestValue: false },
    { id: 3, title: '3 Packs (Family / Value)', badge: '⭐ BEST VALUE - SAVE 25%', quantity: 3, priceMultiplier: 2.25, discountPct: 25, shipping: 'FREE Express + Priority Handling', isPopular: false, isBestValue: true }
  ]);

  // Declarative Sections
  sections = signal<FunnelSectionItem[]>([
    { id: 'sec_announcement_bar', type: 'announcement-bar', name: 'Top Urgency Marquee', order: 1, icon: '⚡', isEnabled: true, settings: { text: 'Free 24h Express Delivery' }, liquidSnippet: '{% render "announcement-bar", text: section.settings.text %}' },
    { id: 'sec_hero_gallery', type: 'hero-gallery', name: 'Hero Product Gallery', order: 2, icon: '🖼️', isEnabled: true, settings: { rating: 4.9 }, liquidSnippet: '{% render "hero-gallery", product: product %}' },
    { id: 'sec_value_prop', type: 'value-proposition', name: 'PAS Copywriting & Benefits', order: 3, icon: '✍️', isEnabled: true, settings: {}, liquidSnippet: '{% render "value-proposition", bullets: copywriting.bullets %}' },
    { id: 'sec_quantity_breaks', type: 'quantity-breaks', name: 'Quantity Breaks Bundles', order: 4, icon: '📦', isEnabled: true, settings: { tiers: 3 }, liquidSnippet: '{% render "quantity-breaks", tiers: section.settings.tiers %}' },
    { id: 'sec_order_bump', type: 'order-bump', name: 'High-Converting Order Bump', order: 5, icon: '💎', isEnabled: true, settings: { price: 14, discount: 50 }, liquidSnippet: '{% render "order-bump", bump_price: 14.00, discount: "50%" %}' },
    { id: 'sec_checkout_form', type: 'one-step-checkout', name: '1-Step COD Checkout Form', order: 6, icon: '📝', isEnabled: true, settings: { wilayaPicker: true }, liquidSnippet: '{% render "one-step-checkout", method: "COD" %}' },
    { id: 'sec_social_proof', type: 'social-proof', name: 'Verified Social Proof', order: 7, icon: '⭐', isEnabled: true, settings: { reviewsCount: 4 }, liquidSnippet: '{% render "social-proof", reviews: social_proof.reviews %}' },
    { id: 'sec_faqs', type: 'faq-accordion', name: 'Objection-Busting FAQs', order: 8, icon: '❓', isEnabled: true, settings: { faqsCount: 5 }, liquidSnippet: '{% render "faq-accordion", items: faqs.items %}' },
    { id: 'sec_sticky_cta', type: 'sticky-cta', name: 'Floating Mobile Action Bar', order: 9, icon: '📱', isEnabled: true, settings: { thresholdPx: 350 }, liquidSnippet: '{% render "sticky-cta", target: "#cod-checkout-form" %}' }
  ]);

  selectedSection = computed(() => {
    return this.sections().find(s => s.id === this.selectedSectionId()) ?? this.sections()[0];
  });

  totalExecutionTimeMs = computed(() => {
    return this.steps().reduce((sum, s) => sum + s.durationMs, 0);
  });

  countdownTimer = computed(() => {
    const s = this.countdownSeconds();
    const m = Math.floor(s / 60);
    const remS = s % 60;
    return `${m.toString().padStart(2, '0')}:${remS.toString().padStart(2, '0')}`;
  });

  orderBumpRoi = computed(() => {
    const base = this.pricePoint();
    const bumpPrice = 14.00;
    const margin = 0.75;
    const takeRate = 0.28;
    const gross = Math.round(takeRate * bumpPrice * margin * 100) / 100;
    const friction = Math.round(base * 0.005 * 100) / 100;
    const ev = Math.round((gross - friction) * 100) / 100;
    const liftPct = base > 0 ? Math.round((ev / base) * 1000) / 10 : 0;
    return {
      grossContribution: gross.toFixed(2),
      frictionCost: friction.toFixed(2),
      expectedValueBump: ev.toFixed(2),
      netAovLiftPct: liftPct
    };
  });

  totalPreviewPrice = computed(() => {
    const tier = this.quantityTiers().find(t => t.id === this.selectedPack()) ?? this.quantityTiers()[1];
    const baseTotal = this.pricePoint() * tier.priceMultiplier;
    const bump = this.isOrderBumpChecked() ? 14.00 : 0.00;
    return (baseTotal + bump).toFixed(2);
  });

  livePublishedUrl = computed(() => {
    return `https://buy.sgrowing.io/f/${this.publishedSlug()}`;
  });

  compiledSchemaJson = computed(() => {
    return JSON.stringify({
      funnel: {
        name: this.conceptPrompt(),
        sku: this.productSku(),
        basePrice: this.pricePoint(),
        currency: this.currency(),
        sections: this.sections().map(s => ({
          id: s.id,
          type: s.type,
          order: s.order,
          settings: s.settings
        }))
      }
    }, null, 2);
  });

  ngOnInit(): void {
    this.timerInterval = setInterval(() => {
      this.countdownSeconds.update(s => s > 0 ? s - 1 : 15 * 60);
    }, 1000);
  }

  ngOnDestroy(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
    }
  }

  calculateTierPrice(tierId: number): string {
    const tier = this.quantityTiers().find(t => t.id === tierId) ?? this.quantityTiers()[0];
    return (this.pricePoint() * tier.priceMultiplier).toFixed(2);
  }

  calculateOriginalTierPrice(tierId: number): string {
    const tier = this.quantityTiers().find(t => t.id === tierId) ?? this.quantityTiers()[0];
    return (this.pricePoint() * tier.quantity).toFixed(2);
  }

  applyPreset(type: 'leather' | 'charger' | 'serum'): void {
    if (type === 'leather') {
      this.conceptPrompt.set('Artisan Handcrafted Full-Grain Leather Messenger Bag');
      this.productSku.set('SKU-LEATHER-01');
      this.pricePoint.set(49);
      this.currency.set('TND');
    } else if (type === 'charger') {
      this.conceptPrompt.set('MagFast Ultra-Thin 15W Magnetic Wireless Fast Charger');
      this.productSku.set('SKU-MAGFAST-02');
      this.pricePoint.set(39);
      this.currency.set('TND');
    } else {
      this.conceptPrompt.set('Pure Organic Argan Oil Anti-Aging Facial Serum 50ml');
      this.productSku.set('SKU-ARGAN-03');
      this.pricePoint.set(59);
      this.currency.set('TND');
    }
  }

  async triggerPipeline(): Promise<void> {
    this.isGenerating.set(true);
    this.isPublished.set(false);

    // Reset steps to pending
    const currentSteps = [...this.steps()];
    for (let i = 0; i < currentSteps.length; i++) {
      currentSteps[i] = { ...currentSteps[i], status: 'Pending' };
    }
    this.steps.set(currentSteps);

    // Animate execution through each step
    for (let i = 0; i < currentSteps.length; i++) {
      this.currentExecutingStep.set(i + 1);
      this.overallProgress.set(Math.round(((i + 1) / 15) * 100));

      const updated = [...this.steps()];
      updated[i] = { ...updated[i], status: 'Running' };
      this.steps.set(updated);

      await new Promise(r => setTimeout(r, 60));

      const completed = [...this.steps()];
      completed[i] = { ...completed[i], status: 'Completed' };
      this.steps.set(completed);
    }

    // Generate new unique slug
    const clean = this.conceptPrompt().toLowerCase().replace(/[^a-z0-9]+/g, '-').slice(0, 18);
    const suffix = Math.floor(Math.random() * 899999 + 100000).toString(16);
    this.publishedSlug.set(`${clean}-${suffix}`);

    this.isGenerating.set(false);
  }

  publishFunnel(): void {
    this.isPublished.set(true);
    this.showPublishModal.set(true);
  }
}
