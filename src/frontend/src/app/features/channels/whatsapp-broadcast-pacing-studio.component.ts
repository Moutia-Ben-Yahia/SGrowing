import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface WarmupDayData {
  day: number;
  dailyLimit: number;
  recommendedBatchSize: number;
  safeRatePerMinute: number;
  phaseDescription: string;
}

export interface SuppressionEntry {
  id: string;
  phoneNumber: string;
  triggerKeyword: string;
  reason: string;
  suppressedAt: string;
  source: string;
  isActive: boolean;
}

export interface SimulatedBatchSlice {
  batchNumber: number;
  messageCount: number;
  durationSeconds: number;
  cooldownPauseSeconds: number;
}

@Component({
  selector: 'app-whatsapp-broadcast-pacing-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans selection:bg-emerald-500/30">
      <!-- Top Bar -->
      <header class="border-b border-slate-800/80 bg-slate-900/70 backdrop-blur-md px-6 py-4 sticky top-0 z-40 flex flex-wrap items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-tr from-emerald-600 via-teal-500 to-cyan-500 flex items-center justify-center shadow-lg shadow-emerald-500/20">
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
            </svg>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-lg font-bold text-slate-100 tracking-tight">WhatsApp Autonomous Pacing & Anti-Ban Shaper</h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                Horizon 44
              </span>
            </div>
            <p class="text-xs text-slate-400">Leaky-bucket stochastic jitter, 14-day warmup ramp & defensive opt-out ledger</p>
          </div>
        </div>

        <!-- Phone & Status Selector -->
        <div class="flex items-center gap-3">
          <div class="bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 flex items-center gap-2.5">
            <span class="w-2.5 h-2.5 rounded-full" [ngClass]="isEmergencyPaused() ? 'bg-rose-500 animate-pulse' : (cooldownMultiplier() > 1.0 ? 'bg-amber-400 animate-ping' : 'bg-emerald-400')"></span>
            <div class="text-xs">
              <span class="text-slate-400 font-mono">{{ activePhoneNumber() }}</span>
              <span class="text-slate-500 mx-1">|</span>
              <span class="font-medium" [ngClass]="isEmergencyPaused() ? 'text-rose-400 font-bold' : (cooldownMultiplier() > 1.0 ? 'text-amber-300' : 'text-emerald-400')">
                {{ isEmergencyPaused() ? 'EMERGENCY PAUSE' : (cooldownMultiplier() > 1.0 ? cooldownMultiplier().toFixed(1) + 'x Cooldown Throttle' : 'Normal Pacing') }}
              </span>
            </div>
          </div>

          <button (click)="toggleEmergencyPause()" class="px-3 py-1.5 rounded-lg text-xs font-semibold transition border flex items-center gap-1.5"
            [ngClass]="isEmergencyPaused() ? 'bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border-rose-500/40' : 'bg-slate-800 hover:bg-slate-700 text-slate-300 border-slate-700'">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
            </svg>
            {{ isEmergencyPaused() ? 'Lift Emergency Pause' : 'Trigger Protective Pause' }}
          </button>
        </div>
      </header>

      <!-- Main Body -->
      <main class="flex-1 max-w-7xl w-full mx-auto p-6 space-y-6">

        <!-- Metrics Overview Strip -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 relative overflow-hidden">
            <div class="text-xs uppercase font-medium text-slate-400 tracking-wider">Warmup Phase</div>
            <div class="mt-2 flex items-baseline justify-between">
              <div class="text-2xl font-black text-cyan-400">Day {{ currentWarmupDay() }} <span class="text-sm font-normal text-slate-400">/ 14</span></div>
              <span class="text-xs px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-300 border border-cyan-500/20 font-medium">
                {{ selectedAccountTier() }}
              </span>
            </div>
            <div class="mt-2 text-xs text-slate-400 flex justify-between">
              <span>Today Quota: {{ currentDailyLimit() | number }}</span>
              <span class="text-emerald-400 font-medium">{{ remainingToday() | number }} left</span>
            </div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
            <div class="text-xs uppercase font-medium text-slate-400 tracking-wider">Ban Risk Index</div>
            <div class="mt-2 flex items-baseline justify-between">
              <div class="text-2xl font-black" [ngClass]="getRiskColorClass(banRiskIndex())">
                {{ banRiskIndex().toFixed(1) }}%
              </div>
              <span class="text-xs px-2 py-0.5 rounded font-semibold uppercase" [ngClass]="getRiskBadgeClass(banRiskIndex())">
                {{ banRiskLevel() }}
              </span>
            </div>
            <div class="mt-2 text-xs text-slate-400">
              Complaint: <span class="text-slate-200">{{ complaintRate().toFixed(2) }}%</span> | Blocks: <span class="text-slate-200">{{ blockRate().toFixed(2) }}%</span>
            </div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
            <div class="text-xs uppercase font-medium text-slate-400 tracking-wider">Current Pacing Rate</div>
            <div class="mt-2 flex items-baseline justify-between">
              <div class="text-2xl font-black text-emerald-400">
                {{ effectiveRatePerMinute().toFixed(1) }} <span class="text-xs text-slate-400 font-normal">msg/min</span>
              </div>
              <span class="text-xs text-amber-400 font-mono">
                Δt: {{ averageIntervalSeconds().toFixed(2) }}s
              </span>
            </div>
            <div class="mt-2 text-xs text-slate-400 flex items-center justify-between">
              <span>Safety Jitter: <strong class="text-slate-200">±{{ jitterPercent() }}%</strong></span>
              <span class="text-slate-500 font-mono">[{{ minIntervalSeconds().toFixed(2) }}s - {{ maxIntervalSeconds().toFixed(2) }}s]</span>
            </div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
            <div class="text-xs uppercase font-medium text-slate-400 tracking-wider">Suppressed Opt-Outs</div>
            <div class="mt-2 flex items-baseline justify-between">
              <div class="text-2xl font-black text-rose-400">{{ suppressionList().length }}</div>
              <span class="text-xs px-2 py-0.5 rounded bg-rose-500/10 text-rose-300 border border-rose-500/20 font-medium">
                100% Protected
              </span>
            </div>
            <div class="mt-2 text-xs text-slate-400">Defensive keyword interceptor auto-blocked</div>
          </div>
        </div>

        <!-- Studio Grid: Left = Pacing Simulator & Warmup Curve, Right = Ban Risk & Suppression -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">

          <!-- Left Column (7 cols): Pacing Simulator & 14-Day Trajectory -->
          <div class="lg:col-span-7 space-y-6">

            <!-- Section 1: Leaky-Bucket Pacing Simulator -->
            <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-5">
              <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
                <div class="flex items-center gap-2">
                  <div class="w-7 h-7 rounded-lg bg-teal-500/10 text-teal-400 border border-teal-500/20 flex items-center justify-center">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
                    </svg>
                  </div>
                  <h2 class="text-sm font-semibold text-slate-100">Leaky-Bucket Stochastic Pacing Simulator</h2>
                </div>
                <span class="text-xs font-mono text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20">
                  Δt = (60 / rate) × (1.0 + jitter)
                </span>
              </div>

              <!-- Sliders -->
              <div class="space-y-4">
                <!-- Recipient Count -->
                <div>
                  <div class="flex justify-between text-xs mb-1.5">
                    <span class="text-slate-300 font-medium">Campaign Recipients</span>
                    <span class="text-cyan-400 font-bold font-mono">{{ campaignRecipients() | number }} contacts</span>
                  </div>
                  <input type="range" min="10" max="5000" step="10" [(ngModel)]="campaignRecipients"
                    class="w-full accent-cyan-500 bg-slate-800 h-1.5 rounded-lg appearance-none cursor-pointer">
                  <div class="flex justify-between text-[10px] text-slate-500 mt-1 font-mono">
                    <span>10 (Test)</span>
                    <span>500 (Standard)</span>
                    <span>1,000 (Tier 1 Max)</span>
                    <span>5,000 (Bulk)</span>
                  </div>
                </div>

                <!-- Rate Per Minute -->
                <div>
                  <div class="flex justify-between text-xs mb-1.5">
                    <span class="text-slate-300 font-medium">Base Dispatch Velocity</span>
                    <span class="text-emerald-400 font-bold font-mono">{{ targetRatePerMinute() }} msgs / min</span>
                  </div>
                  <input type="range" min="5" max="120" step="1" [(ngModel)]="targetRatePerMinute"
                    class="w-full accent-emerald-500 bg-slate-800 h-1.5 rounded-lg appearance-none cursor-pointer">
                  <div class="flex justify-between text-[10px] text-slate-500 mt-1 font-mono">
                    <span>5 (Ultra Safe)</span>
                    <span>30 (Warmup Safe)</span>
                    <span>60 (1 msg/s)</span>
                    <span>120 (Max Enterprise)</span>
                  </div>
                </div>

                <!-- Jitter Factor -->
                <div>
                  <div class="flex justify-between text-xs mb-1.5">
                    <span class="text-slate-300 font-medium">Humanized Stochastic Jitter</span>
                    <span class="text-amber-400 font-bold font-mono">±{{ jitterPercent() }}% randomized dispersion</span>
                  </div>
                  <input type="range" min="0" max="50" step="1" [(ngModel)]="jitterPercent"
                    class="w-full accent-amber-500 bg-slate-800 h-1.5 rounded-lg appearance-none cursor-pointer">
                  <div class="flex justify-between text-[10px] text-slate-500 mt-1 font-mono">
                    <span>0% (Robotic Fixed)</span>
                    <span>20% (Recommended)</span>
                    <span>35% (High Entropy)</span>
                    <span>50% (Max Dispersion)</span>
                  </div>
                </div>
              </div>

              <!-- Simulation Output KPI Cards -->
              <div class="grid grid-cols-3 gap-3 p-3.5 bg-slate-950/70 border border-slate-800/80 rounded-xl">
                <div>
                  <div class="text-[11px] text-slate-400">Estimated Duration</div>
                  <div class="text-base font-bold text-slate-100 font-mono mt-0.5">{{ simulationETA().formatted }}</div>
                  <div class="text-[10px] text-slate-500">{{ simulationETA().totalSeconds | number:'1.0-0' }} seconds total</div>
                </div>
                <div>
                  <div class="text-[11px] text-slate-400">Batch Slices</div>
                  <div class="text-base font-bold text-cyan-400 font-mono mt-0.5">{{ simulatedBatches().length }} Batches</div>
                  <div class="text-[10px] text-slate-500">{{ simulatedBatchSize() }} msg/batch + {{ interBatchPauseSeconds() }}s pause</div>
                </div>
                <div>
                  <div class="text-[11px] text-slate-400">Effective Interval</div>
                  <div class="text-base font-bold text-emerald-400 font-mono mt-0.5">{{ averageIntervalSeconds().toFixed(2) }}s</div>
                  <div class="text-[10px] text-slate-500 font-mono">Jitter: [{{ minIntervalSeconds().toFixed(2) }}s..{{ maxIntervalSeconds().toFixed(2) }}s]</div>
                </div>
              </div>

              <!-- Safety Recommendation Warning if Volume exceeds day limit -->
              @if (campaignRecipients() > currentDailyLimit()) {
                <div class="p-3 bg-amber-500/10 border border-amber-500/30 rounded-lg text-xs text-amber-200 flex items-start gap-2.5">
                  <svg class="w-4 h-4 text-amber-400 mt-0.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
                  </svg>
                  <div>
                    <span class="font-bold">Warmup Capacity Warning:</span> Campaign volume ({{ campaignRecipients() | number }}) exceeds Day {{ currentWarmupDay() }} capacity limit ({{ currentDailyLimit() | number }}). Recommendation: Split into <strong>{{ getRequiredDailyWindows() }} daily dispatch windows</strong>.
                  </div>
                </div>
              }

              <!-- Live Simulated Interval Stream Preview -->
              <div>
                <div class="flex items-center justify-between text-xs text-slate-400 mb-2">
                  <span>Simulated Message Interval Stream (First 8 Dispatches)</span>
                  <button (click)="regenerateSampleSequence()" class="text-cyan-400 hover:text-cyan-300 font-medium">Re-roll Randomizer</button>
                </div>
                <div class="grid grid-cols-4 md:grid-cols-8 gap-2">
                  @for (sample of sampleIntervals(); track sample.index) {
                    <div class="bg-slate-950 border border-slate-800 rounded-lg p-2 text-center">
                      <div class="text-[10px] text-slate-500 font-mono">#{{ sample.index }}</div>
                      <div class="text-xs font-bold text-slate-200 font-mono mt-0.5">{{ sample.intervalSeconds.toFixed(2) }}s</div>
                      <div class="text-[9px] font-mono mt-0.5" [ngClass]="sample.jitterRatio >= 0 ? 'text-emerald-400' : 'text-amber-400'">
                        {{ sample.jitterRatio >= 0 ? '+' : '' }}{{ (sample.jitterRatio * 100).toFixed(0) }}%
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>

            <!-- Section 2: 14-Day Phone Number Warmup Ramp Visualizer -->
            <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
              <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
                <div class="flex items-center gap-2">
                  <div class="w-7 h-7 rounded-lg bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 flex items-center justify-center">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z"/>
                    </svg>
                  </div>
                  <div>
                    <h2 class="text-sm font-semibold text-slate-100">14-Day Phone Warmup Ramp Curve</h2>
                    <span class="text-[10px] font-mono text-slate-400">DailyLimit(d) = Min(MaxTier, InitialCap × 1.4^(d-1))</span>
                  </div>
                </div>

                <!-- Tier Selector Buttons -->
                <div class="flex gap-1 bg-slate-950 p-1 rounded-lg border border-slate-800">
                  <button (click)="selectTier('Tier 1')" [ngClass]="selectedAccountTier() === 'Tier 1' ? 'bg-cyan-600 text-white' : 'text-slate-400 hover:text-slate-200'" class="px-2 py-1 rounded text-[11px] font-semibold transition">
                    Tier 1 (1K)
                  </button>
                  <button (click)="selectTier('Tier 2')" [ngClass]="selectedAccountTier() === 'Tier 2' ? 'bg-cyan-600 text-white' : 'text-slate-400 hover:text-slate-200'" class="px-2 py-1 rounded text-[11px] font-semibold transition">
                    Tier 2 (10K)
                  </button>
                  <button (click)="selectTier('Tier 3')" [ngClass]="selectedAccountTier() === 'Tier 3' ? 'bg-cyan-600 text-white' : 'text-slate-400 hover:text-slate-200'" class="px-2 py-1 rounded text-[11px] font-semibold transition">
                    Tier 3 (100K)
                  </button>
                  <button (click)="selectTier('Unlimited')" [ngClass]="selectedAccountTier() === 'Unlimited' ? 'bg-cyan-600 text-white' : 'text-slate-400 hover:text-slate-200'" class="px-2 py-1 rounded text-[11px] font-semibold transition">
                    Unlimited
                  </button>
                </div>
              </div>

              <!-- Bar Chart Visualizer -->
              <div class="space-y-1.5 pt-2">
                <div class="grid grid-cols-14 gap-1.5 items-end h-40 px-2 pb-2 bg-slate-950/80 rounded-xl border border-slate-800/80">
                  @for (dayData of warmupDays(); track dayData.day) {
                    <div (click)="setCurrentWarmupDay(dayData.day)" class="group flex flex-col items-center justify-end h-full cursor-pointer">
                      <!-- Bar Tooltip -->
                      <div class="opacity-0 group-hover:opacity-100 transition duration-150 text-[9px] bg-slate-900 border border-slate-700 text-slate-200 px-1.5 py-0.5 rounded -mb-1 z-20 pointer-events-none whitespace-nowrap shadow-lg">
                        {{ dayData.dailyLimit | number }} msgs
                      </div>

                      <!-- The Bar itself -->
                      <div class="w-full rounded-t-sm transition-all duration-300 relative"
                        [style.height.%]="getBarHeightPercent(dayData.dailyLimit)"
                        [ngClass]="dayData.day === currentWarmupDay()
                          ? 'bg-gradient-to-t from-emerald-600 to-teal-400 shadow-lg shadow-teal-500/30'
                          : (dayData.day < currentWarmupDay() ? 'bg-slate-700 hover:bg-slate-600' : 'bg-cyan-700/60 hover:bg-cyan-500/80')">
                        @if (dayData.day === currentWarmupDay()) {
                          <div class="absolute -top-1 left-1/2 -translate-x-1/2 w-1.5 h-1.5 bg-white rounded-full"></div>
                        }
                      </div>

                      <!-- Day label -->
                      <span class="text-[10px] mt-1 font-mono" [ngClass]="dayData.day === currentWarmupDay() ? 'text-teal-400 font-bold' : 'text-slate-500'">
                        D{{ dayData.day }}
                      </span>
                    </div>
                  }
                </div>
              </div>

              <!-- Active Day Details Card -->
              <div class="p-3.5 bg-slate-950/70 border border-slate-800 rounded-lg flex flex-wrap items-center justify-between gap-4">
                <div>
                  <div class="text-xs font-semibold text-slate-200 flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-teal-400"></span>
                    Day {{ activeWarmupDetail().day }} Warmup Configuration
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">{{ activeWarmupDetail().phaseDescription }}</div>
                </div>
                <div class="flex items-center gap-4 text-xs font-mono">
                  <div>
                    <span class="text-slate-500">Daily Cap:</span>
                    <strong class="text-emerald-400 ml-1">{{ activeWarmupDetail().dailyLimit | number }}</strong>
                  </div>
                  <div>
                    <span class="text-slate-500">Rec. Batch:</span>
                    <strong class="text-cyan-400 ml-1">{{ activeWarmupDetail().recommendedBatchSize }}</strong>
                  </div>
                  <div>
                    <span class="text-slate-500">Safe Velocity:</span>
                    <strong class="text-amber-400 ml-1">{{ activeWarmupDetail().safeRatePerMinute }} msg/min</strong>
                  </div>
                </div>
              </div>
            </div>

          </div>

          <!-- Right Column (5 cols): Ban Risk Radar & Opt-Out Suppression Ledger -->
          <div class="lg:col-span-5 space-y-6">

            <!-- Section 3: Ban Risk Index Radar Gauge -->
            <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
              <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
                <div class="flex items-center gap-2">
                  <div class="w-7 h-7 rounded-lg bg-rose-500/10 text-rose-400 border border-rose-500/20 flex items-center justify-center">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
                    </svg>
                  </div>
                  <h2 class="text-sm font-semibold text-slate-100">Ban Risk Radar & Cooldown Shaper</h2>
                </div>
                <span class="text-xs px-2 py-0.5 rounded font-bold uppercase" [ngClass]="getRiskBadgeClass(banRiskIndex())">
                  {{ banRiskLevel() }} Risk
                </span>
              </div>

              <!-- Risk Radial Score -->
              <div class="flex items-center gap-5 p-4 bg-slate-950/70 border border-slate-800 rounded-xl">
                <div class="relative w-24 h-24 flex items-center justify-center flex-shrink-0">
                  <svg class="w-24 h-24 transform -rotate-90" viewBox="0 0 36 36">
                    <path class="text-slate-800" stroke-width="3.5" stroke="currentColor" fill="none"
                      d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                    <path [ngClass]="getRiskStrokeClass(banRiskIndex())" stroke-width="3.5"
                      [attr.stroke-dasharray]="banRiskIndex().toFixed(0) + ', 100'"
                      stroke-linecap="round" stroke="currentColor" fill="none"
                      d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                  </svg>
                  <div class="absolute inset-0 flex flex-col items-center justify-center">
                    <span class="text-xl font-black" [ngClass]="getRiskColorClass(banRiskIndex())">{{ banRiskIndex().toFixed(0) }}%</span>
                    <span class="text-[9px] uppercase tracking-wider text-slate-500">Ban Risk</span>
                  </div>
                </div>

                <div class="flex-1 space-y-1.5 text-xs">
                  <div class="flex justify-between text-slate-400">
                    <span>Active Cooldown Multiplier:</span>
                    <span class="font-mono font-bold" [ngClass]="cooldownMultiplier() > 1.0 ? 'text-amber-400' : 'text-emerald-400'">
                      {{ cooldownMultiplier().toFixed(1) }}x
                    </span>
                  </div>
                  <div class="flex justify-between text-slate-400">
                    <span>Emergency Pause State:</span>
                    <span class="font-bold" [ngClass]="isEmergencyPaused() ? 'text-rose-400' : 'text-slate-300'">
                      {{ isEmergencyPaused() ? 'ACTIVE (All Sends Halted)' : 'Disabled (Operational)' }}
                    </span>
                  </div>
                  <div class="text-[11px] text-slate-500 pt-1 border-t border-slate-800">
                    Thresholds: >65% triggers 2.0x throttle, >85% triggers emergency protective lockdown.
                  </div>
                </div>
              </div>

              <!-- Interactive Risk Simulation Controls -->
              <div class="space-y-3 pt-1">
                <div class="text-xs font-semibold text-slate-300">Stress Test Anti-Ban Triggers</div>

                <!-- Complaint Rate Slider -->
                <div>
                  <div class="flex justify-between text-[11px] mb-1">
                    <span class="text-slate-400">User Complaint / Spam Report Rate</span>
                    <span class="font-mono font-bold" [ngClass]="complaintRate() > 1.0 ? 'text-rose-400' : 'text-slate-200'">{{ complaintRate().toFixed(2) }}%</span>
                  </div>
                  <input type="range" min="0" max="3" step="0.05" [(ngModel)]="complaintRate"
                    class="w-full accent-rose-500 bg-slate-800 h-1.5 rounded-lg appearance-none cursor-pointer">
                </div>

                <!-- Block Rate Slider -->
                <div>
                  <div class="flex justify-between text-[11px] mb-1">
                    <span class="text-slate-400">User Contact Block Rate</span>
                    <span class="font-mono font-bold" [ngClass]="blockRate() > 3.0 ? 'text-rose-400' : 'text-slate-200'">{{ blockRate().toFixed(2) }}%</span>
                  </div>
                  <input type="range" min="0" max="8" step="0.1" [(ngModel)]="blockRate"
                    class="w-full accent-amber-500 bg-slate-800 h-1.5 rounded-lg appearance-none cursor-pointer">
                </div>

                <!-- Template Failure Rate Slider -->
                <div>
                  <div class="flex justify-between text-[11px] mb-1">
                    <span class="text-slate-400">Template / Handshake Failure Rate</span>
                    <span class="font-mono font-bold text-slate-200">{{ templateFailureRate().toFixed(1) }}%</span>
                  </div>
                  <input type="range" min="0" max="20" step="0.5" [(ngModel)]="templateFailureRate"
                    class="w-full accent-cyan-500 bg-slate-800 h-1.5 rounded-lg appearance-none cursor-pointer">
                </div>
              </div>
            </div>

            <!-- Section 4: Opt-Out Suppression Ledger -->
            <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
              <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
                <div class="flex items-center gap-2">
                  <div class="w-7 h-7 rounded-lg bg-violet-500/10 text-violet-400 border border-violet-500/20 flex items-center justify-center">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"/>
                    </svg>
                  </div>
                  <h2 class="text-sm font-semibold text-slate-100">Opt-Out Suppression Ledger</h2>
                </div>
                <span class="text-xs font-mono text-slate-400">{{ filteredSuppressionList().length }} records</span>
              </div>

              <!-- Search Bar & Tag Filters -->
              <div class="space-y-2">
                <div class="relative">
                  <input type="text" [(ngModel)]="searchQuery" placeholder="Search phone number, keyword..."
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-cyan-500">
                </div>

                <!-- Keyword Filter Pills -->
                <div class="flex flex-wrap gap-1">
                  @for (tag of keywordFilterTags; track tag) {
                    <button (click)="selectKeywordFilter(tag)"
                      class="px-2 py-0.5 rounded text-[10px] font-mono transition"
                      [ngClass]="activeKeywordFilter() === tag ? 'bg-violet-600 text-white' : 'bg-slate-950 text-slate-400 hover:text-slate-200 border border-slate-800'">
                      {{ tag }}
                    </button>
                  }
                </div>
              </div>

              <!-- Interactive Inbound Keyword Interceptor Test Box -->
              <div class="p-3 bg-slate-950/80 border border-slate-800/90 rounded-lg space-y-2">
                <div class="text-[11px] font-medium text-slate-300 flex items-center justify-between">
                  <span>Inbound Message Defensive Interceptor Test</span>
                  <span class="text-[10px] text-teal-400">EN / FR / ES / AR Supported</span>
                </div>
                <div class="flex gap-2">
                  <input type="text" [(ngModel)]="testInboundText" placeholder="e.g. STOP, Arrêter, توقف, cancelar..."
                    class="flex-1 bg-slate-900 border border-slate-700/80 rounded px-2.5 py-1 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-teal-400">
                  <button (click)="testInterceptMessage()" class="px-3 py-1 bg-teal-600 hover:bg-teal-500 text-white rounded text-xs font-semibold transition">
                    Test Intercept
                  </button>
                </div>
                @if (interceptTestFeedback()) {
                  <div class="text-[11px] font-mono px-2 py-1 rounded" [ngClass]="interceptTestFeedback()!.isOptOut ? 'bg-rose-500/10 text-rose-300 border border-rose-500/20' : 'bg-slate-800 text-slate-400'">
                    {{ interceptTestFeedback()!.message }}
                  </div>
                }
              </div>

              <!-- Suppression Table -->
              <div class="max-h-56 overflow-y-auto rounded-lg border border-slate-800 bg-slate-950/60 divide-y divide-slate-800/60 text-xs">
                @for (entry of filteredSuppressionList(); track entry.id) {
                  <div class="p-2.5 flex items-center justify-between hover:bg-slate-900/40 transition">
                    <div class="space-y-0.5">
                      <div class="flex items-center gap-2">
                        <span class="font-mono text-slate-200 font-semibold">{{ entry.phoneNumber }}</span>
                        <span class="px-1.5 py-0.2 rounded text-[10px] font-mono font-bold bg-rose-500/10 text-rose-400 border border-rose-500/20">
                          {{ entry.triggerKeyword }}
                        </span>
                      </div>
                      <div class="text-[11px] text-slate-400">{{ entry.reason }}</div>
                      <div class="text-[10px] text-slate-500">{{ entry.suppressedAt }} via {{ entry.source }}</div>
                    </div>
                    <button (click)="removeSuppression(entry.id)" class="text-slate-500 hover:text-rose-400 p-1 transition" title="Remove suppression">
                      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
                      </svg>
                    </button>
                  </div>
                } @empty {
                  <div class="p-6 text-center text-xs text-slate-500">
                    No suppressed contacts found matching query.
                  </div>
                }
              </div>

            </div>

          </div>

        </div>

      </main>
    </div>
  `
})
export class WhatsAppBroadcastPacingStudioComponent {
  // State Signals
  activePhoneNumber = signal<string>('+216 98 123 456');
  selectedAccountTier = signal<string>('Tier 1');
  currentWarmupDay = signal<number>(3);
  initialCap = signal<number>(50);

  // Sliders for Pacing Simulator
  campaignRecipients = signal<number>(300);
  targetRatePerMinute = signal<number>(30);
  jitterPercent = signal<number>(20);

  // Stress test sliders for Ban Risk
  complaintRate = signal<number>(0.12);
  blockRate = signal<number>(0.65);
  templateFailureRate = signal<number>(1.5);
  manualEmergencyPauseOverride = signal<boolean | null>(null);

  // Inbound interceptor test state
  testInboundText = signal<string>('');
  interceptTestFeedback = signal<{ isOptOut: boolean; message: string } | null>(null);

  // Search & Filter for Suppression Ledger
  searchQuery = signal<string>('');
  activeKeywordFilter = signal<string>('ALL');
  keywordFilterTags = ['ALL', 'STOP', 'UNSUBSCRIBE', 'CANCEL', 'ARRETER', 'DESINSCRIBIR', 'توقف', 'إلغاء'];

  // Suppression list records
  suppressionList = signal<SuppressionEntry[]>([
    { id: '1', phoneNumber: '+1 555 123 4567', triggerKeyword: 'STOP', reason: 'Opt-Out Keyword [STOP] detected', suppressedAt: '2026-10-31 04:12', source: 'InboundInterceptor', isActive: true },
    { id: '2', phoneNumber: '+33 6 12 34 56 78', triggerKeyword: 'ARRETER', reason: 'Opt-Out Keyword [ARRETER] detected', suppressedAt: '2026-10-31 03:45', source: 'InboundInterceptor', isActive: true },
    { id: '3', phoneNumber: '+34 612 34 56 78', triggerKeyword: 'DESINSCRIBIR', reason: 'Opt-Out Keyword [DESINSCRIBIR] detected', suppressedAt: '2026-10-30 22:15', source: 'InboundInterceptor', isActive: true },
    { id: '4', phoneNumber: '+216 99 887 766', triggerKeyword: 'توقف', reason: 'Opt-Out Keyword [توقف] detected', suppressedAt: '2026-10-30 18:30', source: 'InboundInterceptor', isActive: true },
    { id: '5', phoneNumber: '+44 7700 900077', triggerKeyword: 'UNSUBSCRIBE', reason: 'Opt-Out Keyword [UNSUBSCRIBE] detected', suppressedAt: '2026-10-30 12:00', source: 'InboundInterceptor', isActive: true }
  ]);

  // Seed simulated message interval samples
  sampleIntervalRandomSeed = signal<number>(42);

  // 14-Day Warmup Schedule Computed Trajectory
  warmupDays = computed<WarmupDayData[]>(() => {
    const tier = this.selectedAccountTier();
    const cap = this.initialCap();
    const maxLimit = tier === 'Tier 1' ? 1000 : (tier === 'Tier 2' ? 10000 : (tier === 'Tier 3' ? 100000 : 10000000));

    const result: WarmupDayData[] = [];
    for (let day = 1; day <= 14; day++) {
      const calculated = Math.round(cap * Math.pow(1.4, day - 1));
      const limit = Math.min(maxLimit, calculated);
      const batchSize = Math.min(250, Math.max(25, Math.round(limit * 0.25)));
      const safeRate = Math.min(60, 10 + (day - 1) * 3.5);

      let phase = 'Maturity';
      if (day <= 3) phase = 'Incubation & 1:1 engagement priority';
      else if (day <= 7) phase = 'Progressive Ramp: Bi-directional flow';
      else if (day <= 11) phase = 'Accelerated Expansion: Scaled broadcast';
      else phase = 'High-Throughput: Full tier unlocked';

      result.push({
        day,
        dailyLimit: limit,
        recommendedBatchSize: batchSize,
        safeRatePerMinute: safeRate,
        phaseDescription: phase
      });
    }
    return result;
  });

  // Current Warmup Day Detail
  activeWarmupDetail = computed<WarmupDayData>(() => {
    const days = this.warmupDays();
    const current = this.currentWarmupDay();
    return days.find(d => d.day === current) || days[0];
  });

  currentDailyLimit = computed<number>(() => {
    return this.activeWarmupDetail().dailyLimit;
  });

  remainingToday = computed<number>(() => {
    return Math.max(0, this.currentDailyLimit() - 14);
  });

  // Ban Risk Calculation Engine
  banRiskIndex = computed<number>(() => {
    const compNorm = Math.min(1.0, this.complaintRate() / 1.5);
    const blockNorm = Math.min(1.0, this.blockRate() / 4.0);
    const failNorm = Math.min(1.0, this.templateFailureRate() / 12.0);

    const safeVel = this.activeWarmupDetail().safeRatePerMinute;
    const targetVel = this.targetRatePerMinute();
    const velOvershoot = Math.max(0, targetVel - safeVel) / safeVel;
    const velNorm = Math.min(1.0, velOvershoot);

    let rawScore = (0.35 * compNorm + 0.35 * blockNorm + 0.15 * failNorm + 0.15 * velNorm) * 100.0;
    if (compNorm > 0.6 && blockNorm > 0.6) {
      rawScore = Math.min(100.0, rawScore * 1.25);
    }
    return Math.min(100.0, Math.max(5.0, rawScore));
  });

  banRiskLevel = computed<'Low' | 'Moderate' | 'Elevated' | 'Critical'>(() => {
    const risk = this.banRiskIndex();
    if (risk > 85) return 'Critical';
    if (risk > 65) return 'Elevated';
    if (risk > 40) return 'Moderate';
    return 'Low';
  });

  // Automated Cooldown Multiplier
  cooldownMultiplier = computed<number>(() => {
    const risk = this.banRiskIndex();
    if (risk > 85) return 3.0;
    if (risk > 65) return 2.0;
    if (risk > 40) return 1.25;
    return 1.0;
  });

  isEmergencyPaused = computed<boolean>(() => {
    const manual = this.manualEmergencyPauseOverride();
    if (manual !== null) return manual;
    return this.banRiskIndex() > 85;
  });

  // Pacing intervals
  effectiveRatePerMinute = computed<number>(() => {
    return this.targetRatePerMinute() / this.cooldownMultiplier();
  });

  averageIntervalSeconds = computed<number>(() => {
    return 60.0 / this.effectiveRatePerMinute();
  });

  minIntervalSeconds = computed<number>(() => {
    const avg = this.averageIntervalSeconds();
    const jitter = this.jitterPercent() / 100.0;
    return avg * (1.0 - jitter);
  });

  maxIntervalSeconds = computed<number>(() => {
    const avg = this.averageIntervalSeconds();
    const jitter = this.jitterPercent() / 100.0;
    return avg * (1.0 + jitter);
  });

  simulatedBatchSize = computed<number>(() => {
    const day = this.currentWarmupDay();
    if (day <= 3) return 30;
    if (day <= 7) return 60;
    if (day <= 10) return 120;
    return 250;
  });

  interBatchPauseSeconds = computed<number>(() => {
    const day = this.currentWarmupDay();
    if (day <= 3) return 90;
    if (day <= 7) return 60;
    return 30;
  });

  simulatedBatches = computed<SimulatedBatchSlice[]>(() => {
    const total = this.campaignRecipients();
    const batchSize = this.simulatedBatchSize();
    const pause = this.interBatchPauseSeconds();
    const avgInterval = this.averageIntervalSeconds();

    const count = Math.ceil(total / batchSize);
    const slices: SimulatedBatchSlice[] = [];
    let remaining = total;

    for (let i = 0; i < count; i++) {
      const currentBatchCount = Math.min(batchSize, remaining);
      const duration = currentBatchCount * avgInterval;
      const cooldown = (i === count - 1) ? 0 : pause;
      slices.push({
        batchNumber: i + 1,
        messageCount: currentBatchCount,
        durationSeconds: duration,
        cooldownPauseSeconds: cooldown
      });
      remaining -= currentBatchCount;
    }
    return slices;
  });

  simulationETA = computed<{ totalSeconds: number; formatted: string }>(() => {
    const batches = this.simulatedBatches();
    let totalSec = 0;
    for (const b of batches) {
      totalSec += b.durationSeconds + b.cooldownPauseSeconds;
    }

    const minutes = Math.floor(totalSec / 60);
    const seconds = Math.round(totalSec % 60);
    const formatted = minutes > 60
      ? `${(minutes / 60).toFixed(1)} hrs`
      : `${minutes}m ${seconds}s`;

    return { totalSeconds: totalSec, formatted };
  });

  // Live interval sample sequence
  sampleIntervals = computed<{ index: number; intervalSeconds: number; jitterRatio: number }[]>(() => {
    this.sampleIntervalRandomSeed();
    const avg = this.averageIntervalSeconds();
    const jitterRatio = this.jitterPercent() / 100.0;
    const samples: { index: number; intervalSeconds: number; jitterRatio: number }[] = [];

    // Realistic pseudo-random sequence for display
    const pseudoRand = [0.12, -0.18, 0.05, -0.09, 0.22, -0.15, 0.08, -0.04];
    for (let i = 0; i < 8; i++) {
      const factor = (pseudoRand[i % pseudoRand.length]) * (jitterRatio / 0.20);
      const clampedFactor = Math.max(-jitterRatio, Math.min(jitterRatio, factor));
      const interval = Math.max(0.05, avg * (1.0 + clampedFactor));
      samples.push({
        index: i + 1,
        intervalSeconds: interval,
        jitterRatio: clampedFactor
      });
    }
    return samples;
  });

  // Filtered suppression ledger
  filteredSuppressionList = computed<SuppressionEntry[]>(() => {
    const list = this.suppressionList();
    const query = this.searchQuery().toLowerCase().trim();
    const tag = this.activeKeywordFilter();

    return list.filter(entry => {
      const matchesTag = tag === 'ALL' || entry.triggerKeyword.toUpperCase().includes(tag.toUpperCase());
      const matchesSearch = !query ||
        entry.phoneNumber.toLowerCase().includes(query) ||
        entry.reason.toLowerCase().includes(query) ||
        entry.triggerKeyword.toLowerCase().includes(query);
      return matchesTag && matchesSearch;
    });
  });

  // UI Actions
  selectTier(tier: string) {
    this.selectedAccountTier.set(tier);
  }

  setCurrentWarmupDay(day: number) {
    this.currentWarmupDay.set(day);
  }

  selectKeywordFilter(tag: string) {
    this.activeKeywordFilter.set(tag);
  }

  toggleEmergencyPause() {
    this.manualEmergencyPauseOverride.set(!this.isEmergencyPaused());
  }

  regenerateSampleSequence() {
    this.sampleIntervalRandomSeed.update(s => s + 1);
  }

  removeSuppression(id: string) {
    this.suppressionList.update(list => list.filter(entry => entry.id !== id));
  }

  testInterceptMessage() {
    const text = this.testInboundText().trim();
    if (!text) {
      this.interceptTestFeedback.set(null);
      return;
    }

    const keywords = ['STOP', 'UNSUBSCRIBE', 'CANCEL', 'ARRETER', 'ARRÊTER', 'DESINSCRIBIR', 'BAJA', 'PARAR', 'توقف', 'إلغاء', 'الغاء', 'إيقاف', 'إيقاف الاشتراك'];
    const upper = text.toUpperCase();

    const matched = keywords.find(k => upper.includes(k.toUpperCase()));

    if (matched) {
      const randomPhone = '+216 9' + Math.floor(1000000 + Math.random() * 9000000);
      const newEntry: SuppressionEntry = {
        id: Date.now().toString(),
        phoneNumber: randomPhone,
        triggerKeyword: matched,
        reason: `Opt-Out Keyword [${matched}] detected in: "${text}"`,
        suppressedAt: new Date().toISOString().replace('T', ' ').substring(0, 16),
        source: 'InboundInterceptor',
        isActive: true
      };

      this.suppressionList.update(l => [newEntry, ...l]);
      this.interceptTestFeedback.set({
        isOptOut: true,
        message: `DEFENSIVE INTERCEPT TRIGGERED: Keyword [${matched}] detected! Number ${randomPhone} added to suppression ledger.`
      });
    } else {
      this.interceptTestFeedback.set({
        isOptOut: false,
        message: `Message passed through: No opt-out keywords detected in "${text}".`
      });
    }
  }

  getBarHeightPercent(limit: number): number {
    const tier = this.selectedAccountTier();
    const max = tier === 'Tier 1' ? 1000 : (tier === 'Tier 2' ? 10000 : (tier === 'Tier 3' ? 100000 : 10000000));
    return Math.max(8, Math.min(100, Math.round((limit / max) * 100)));
  }

  getRequiredDailyWindows(): number {
    return Math.ceil(this.campaignRecipients() / this.currentDailyLimit());
  }

  getRiskColorClass(score: number): string {
    if (score > 85) return 'text-rose-400';
    if (score > 65) return 'text-orange-400';
    if (score > 40) return 'text-amber-400';
    return 'text-emerald-400';
  }

  getRiskBadgeClass(score: number): string {
    if (score > 85) return 'bg-rose-500/10 text-rose-400 border border-rose-500/20';
    if (score > 65) return 'bg-orange-500/10 text-orange-400 border border-orange-500/20';
    if (score > 40) return 'bg-amber-500/10 text-amber-400 border border-amber-500/20';
    return 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20';
  }

  getRiskStrokeClass(score: number): string {
    if (score > 85) return 'text-rose-500';
    if (score > 65) return 'text-orange-500';
    if (score > 40) return 'text-amber-500';
    return 'text-emerald-500';
  }
}
