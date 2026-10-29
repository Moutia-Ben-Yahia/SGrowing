import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export type CadenceChannelUi = 'WhatsApp' | 'Phone' | 'Email' | 'Sms' | 'LinkedIn';
export type EnrollmentStatusUi = 'Active' | 'Paused' | 'Replied' | 'Graduated' | 'Bounced' | 'Completed' | 'DealWon';
export type ContactTimeBandUi = 'Peak' | 'High' | 'Moderate' | 'Low' | 'OffHours';
export type DialerPriorityTierUi = 'PeakPriority' | 'StandardPriority' | 'CoolingOff';

export interface HeatmapSlotUi {
  dayOfWeek: number; // 0=Sunday..6=Saturday
  dayName: string;
  hourOfDay: number; // 0..23
  hourLabel: string;
  overallProbabilityScore: number;
  band: ContactTimeBandUi;
  historicalSamples: number;
  channelScores: Record<CadenceChannelUi, number>;
  isQuietHours: boolean;
}

export interface OptimalSlotRecommendationUi {
  utcSlotTime: string;
  localSlotTime: string;
  dayOfWeek: string;
  hourOfDay: number;
  confidenceScore: number;
  channel: CadenceChannelUi;
  reasoning: string;
  band: ContactTimeBandUi;
}

export interface CadenceStepUi {
  id: string;
  stepNumber: number;
  name: string;
  channel: CadenceChannelUi;
  delayText: string;
  useOptimalTiming: boolean;
  conditionRule: string;
  subject?: string;
  templateBody: string;
}

export interface PredictiveCadenceUi {
  id: string;
  name: string;
  description: string;
  category: string;
  isActive: boolean;
  steps: CadenceStepUi[];
  createdAt: string;
}

export interface LeadEnrollmentUi {
  id: string;
  cadenceId: string;
  cadenceName: string;
  leadId: string;
  leadName: string;
  leadEmail: string;
  leadPhone: string;
  leadCompany: string;
  leadTimeZone: string;
  status: EnrollmentStatusUi;
  currentStepNumber: number;
  enrolledAtUtc: string;
  nextStepChannel?: CadenceChannelUi;
  nextStepScheduledAtUtc?: string;
  pauseReason?: string;
  instantaneousScore: number;
  attemptsCount: number;
}

export interface PredictiveDialerItemUi {
  enrollmentId: string;
  leadId: string;
  leadName: string;
  leadPhone: string;
  company: string;
  timeZone: string;
  leadLocalTime: string;
  instantaneousConfidenceScore: number;
  priorityTier: DialerPriorityTierUi;
  recommendedAction: string;
  currentStepNumber: number;
  attemptsCount: number;
}

@Component({
  selector: 'app-predictive-cadence-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <!-- Left Navigation Sidebar -->
      <aside class="w-72 border-r border-slate-800/90 bg-slate-900/95 flex flex-col justify-between p-4 shrink-0 shadow-2xl backdrop-blur-md">
        <div>
          <!-- Brand Header -->
          <div class="flex items-center gap-3 mb-6">
            <div class="h-10 w-10 rounded-xl bg-gradient-to-tr from-emerald-500 via-teal-500 to-cyan-500 flex items-center justify-center font-black text-white shadow-lg shadow-emerald-500/25">
              ⚡
            </div>
            <div>
              <h1 class="text-sm font-bold tracking-tight text-white flex items-center gap-1.5">
                SGrowing Cadence
                <span class="text-[10px] uppercase tracking-wider font-extrabold px-1.5 py-0.5 rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">AI</span>
              </h1>
              <p class="text-[11px] text-slate-400 font-medium">Predictive Timing & Dialer</p>
            </div>
          </div>

          <!-- Section Switcher -->
          <div class="space-y-1 mb-6">
            <span class="text-[10px] font-bold text-slate-500 uppercase tracking-wider px-2">Navigation</span>
            <div class="space-y-1 mt-1">
              <a routerLink="/" class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs text-slate-400 hover:text-white hover:bg-slate-800/60 transition">
                <span>← CRM Unified Desk</span>
              </a>
              <button 
                (click)="activeTab.set('heatmap')"
                [class]="activeTab() === 'heatmap' ? 'w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-semibold shadow-inner' : 'w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs text-slate-400 hover:text-white hover:bg-slate-800/60 transition'">
                <span class="flex items-center gap-2">🗓️ 7x24 Contact Heatmap</span>
                <span class="text-[10px] px-1.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 font-mono">168h</span>
              </button>
              <button 
                (click)="activeTab.set('cadences')"
                [class]="activeTab() === 'cadences' ? 'w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-semibold shadow-inner' : 'w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs text-slate-400 hover:text-white hover:bg-slate-800/60 transition'">
                <span class="flex items-center gap-2">🔀 Cadence Sequences</span>
                <span class="text-[10px] px-1.5 py-0.5 rounded-full bg-slate-800 text-slate-300 font-mono">{{ cadences().length }}</span>
              </button>
              <button 
                (click)="activeTab.set('enrollees')"
                [class]="activeTab() === 'enrollees' ? 'w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-semibold shadow-inner' : 'w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs text-slate-400 hover:text-white hover:bg-slate-800/60 transition'">
                <span class="flex items-center gap-2">👥 Active Enrollees</span>
                <span class="text-[10px] px-1.5 py-0.5 rounded-full bg-blue-500/20 text-blue-300 font-mono">{{ activeEnrolleesCount() }}</span>
              </button>
              <button 
                (click)="activeTab.set('dialer')"
                [class]="activeTab() === 'dialer' ? 'w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-semibold shadow-inner' : 'w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs text-slate-400 hover:text-white hover:bg-slate-800/60 transition'">
                <span class="flex items-center gap-2">📞 Predictive Dialer Queue</span>
                <span class="h-2 w-2 rounded-full bg-emerald-400 animate-ping"></span>
              </button>
            </div>
          </div>

          <!-- Timezone & Engine Status Widget -->
          <div class="p-3.5 rounded-xl bg-slate-950/70 border border-slate-800/90 text-xs space-y-2.5 mb-4">
            <div class="flex items-center justify-between text-[11px] font-semibold text-slate-400">
              <span class="flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-emerald-400 animate-pulse"></span>
                Heatmap Engine
              </span>
              <span class="text-emerald-400 font-mono text-[10px]">Active Bayesian</span>
            </div>
            
            <div class="space-y-1.5">
              <label class="text-[10px] text-slate-500 uppercase tracking-wider font-semibold">Active Timezone</label>
              <select 
                [ngModel]="selectedTimezone()" 
                (ngModelChange)="onTimezoneChange($event)"
                class="w-full bg-slate-900 border border-slate-800 text-xs rounded-lg px-2.5 py-1.5 text-slate-200 focus:outline-none focus:border-emerald-500 transition">
                <option value="UTC">UTC (Tenant Baseline)</option>
                <option value="UTC+1">UTC+1 (London / Paris / Lagos)</option>
                <option value="UTC-5">UTC-5 (New York / Eastern)</option>
                <option value="UTC-8">UTC-8 (San Francisco / Pacific)</option>
                <option value="UTC+4">UTC+4 (Dubai / Gulf)</option>
                <option value="UTC+9">UTC+9 (Tokyo / Japan)</option>
              </select>
            </div>

            <div class="pt-2 border-t border-slate-900 flex justify-between text-[11px] text-slate-400">
              <span>Auto-Pause on Reply:</span>
              <span class="text-emerald-400 font-semibold font-mono">ENFORCED</span>
            </div>
            <div class="flex justify-between text-[11px] text-slate-400">
              <span>Telemetry Events:</span>
              <span class="text-cyan-400 font-mono font-semibold">{{ telemetryCount() }} Logged</span>
            </div>
          </div>
        </div>

        <!-- Sidebar Footer -->
        <div class="border-t border-slate-800/80 pt-3 text-[11px] text-slate-500 flex items-center justify-between">
          <span class="flex items-center gap-1.5">
            <span class="text-emerald-400">●</span> Horizon 23 Ready
          </span>
          <span class="font-mono text-[10px] text-slate-600">v23.4.1</span>
        </div>
      </aside>

      <!-- Main Studio Body -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Application Bar -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/70 px-6 flex items-center justify-between shrink-0 backdrop-blur-md sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <div class="flex items-center gap-2">
              <span class="text-base font-bold text-white">AI Predictive Sales Cadence Studio</span>
              <span class="text-xs px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-medium">
                7x24 Optimal Contact Time
              </span>
            </div>
          </div>

          <!-- Quick Metrics Bar -->
          <div class="flex items-center gap-4 text-xs">
            <div class="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-950/80 border border-slate-800">
              <span class="text-slate-400">Reply Rate:</span>
              <span class="font-mono font-bold text-emerald-400">58.4%</span>
            </div>
            <div class="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-950/80 border border-slate-800">
              <span class="text-slate-400">Peak Window:</span>
              <span class="font-mono font-bold text-amber-300">Tue & Thu 10:00 AM</span>
            </div>
            <button 
              (click)="simulateIncomingReply()"
              class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-xs shadow-lg shadow-emerald-600/20 transition cursor-pointer">
              <span>⚡ Simulate Inbound Reply</span>
            </button>
          </div>
        </header>

        <!-- Studio Content Workspace -->
        <div class="p-6 space-y-6 flex-1">

          <!-- TAB 1: 7x24 Interactive Contact Time Heatmap -->
          @if (activeTab() === 'heatmap') {
            <div class="space-y-6">
              <!-- Heatmap Header & Controls -->
              <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl">
                <div>
                  <h2 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>168-Hour Optimal Contact Time Heatmap</span>
                    <span class="text-xs font-normal text-slate-400">({{ selectedTimezone() }})</span>
                  </h2>
                  <p class="text-xs text-slate-400 mt-1">
                    Normalized connection probabilities across Day of Week & Hour of Day. Incorporates historical opens, clicks, replies, and softphone answers.
                  </p>
                </div>

                <!-- Filter Controls -->
                <div class="flex items-center gap-3">
                  <div class="flex items-center gap-1.5 bg-slate-950 px-2.5 py-1.5 rounded-lg border border-slate-800 text-xs">
                    <span class="text-slate-400">Channel:</span>
                    <select 
                      [ngModel]="selectedChannelFilter()"
                      (ngModelChange)="onChannelFilterChange($event)"
                      class="bg-transparent text-slate-200 font-medium focus:outline-none">
                      <option value="All">All Channels (Weighted)</option>
                      <option value="WhatsApp">WhatsApp</option>
                      <option value="Phone">Phone / Dialer</option>
                      <option value="Email">Email</option>
                      <option value="Sms">SMS</option>
                      <option value="LinkedIn">LinkedIn</option>
                    </select>
                  </div>

                  <!-- Legend -->
                  <div class="flex items-center gap-2 text-[11px] bg-slate-950/60 px-3 py-1.5 rounded-lg border border-slate-800">
                    <div class="flex items-center gap-1">
                      <span class="h-2.5 w-2.5 rounded bg-emerald-500"></span>
                      <span class="text-slate-300">Peak (&ge;80%)</span>
                    </div>
                    <div class="flex items-center gap-1">
                      <span class="h-2.5 w-2.5 rounded bg-amber-500"></span>
                      <span class="text-slate-300">High (65-79%)</span>
                    </div>
                    <div class="flex items-center gap-1">
                      <span class="h-2.5 w-2.5 rounded bg-indigo-600/70"></span>
                      <span class="text-slate-300">Moderate (45-64%)</span>
                    </div>
                    <div class="flex items-center gap-1">
                      <span class="h-2.5 w-2.5 rounded bg-slate-800"></span>
                      <span class="text-slate-400">Low / Off-Hours</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- 7x24 Matrix Grid -->
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 overflow-x-auto shadow-xl">
                <!-- Hours Column Headers (0..23) -->
                <div class="grid grid-cols-[80px_repeat(24,_minmax(32px,_1fr))] gap-1 mb-2 text-center">
                  <div class="text-[10px] font-bold text-slate-500 uppercase tracking-wider text-left pl-1">Day</div>
                  @for (h of hoursArray; track h) {
                    <div class="text-[10px] font-mono text-slate-400 font-medium">
                      {{ h < 10 ? '0' + h : h }}
                    </div>
                  }
                </div>

                <!-- Days Rows (Sunday..Saturday) -->
                <div class="space-y-1.5">
                  @for (day of daysOfWeek; track day.index) {
                    <div class="grid grid-cols-[80px_repeat(24,_minmax(32px,_1fr))] gap-1 items-center">
                      <!-- Day Label -->
                      <div class="text-xs font-semibold text-slate-300 pl-1">
                        {{ day.name }}
                      </div>

                      <!-- 24 Hour Slots -->
                      @for (h of hoursArray; track h) {
                        @let slot = getSlot(day.index, h);
                        <button 
                          (click)="selectSlot(slot)"
                          [title]="day.name + ' ' + h + ':00 - ' + (slot?.overallProbabilityScore || 0) + '% connect score'"
                          [class]="getSlotColorClass(slot) + ' h-8 rounded text-[11px] font-mono font-bold flex items-center justify-center transition transform hover:scale-110 hover:z-10 relative cursor-pointer group'">
                          <span>{{ slot ? (slot.overallProbabilityScore | number:'1.0-0') : '0' }}</span>
                          
                          <!-- Hover Tooltip -->
                          <div class="absolute bottom-full left-1/2 -translate-x-1/2 mb-1.5 hidden group-hover:flex flex-col items-center pointer-events-none z-30">
                            <div class="bg-slate-900 border border-slate-700 text-white text-[10px] rounded-lg px-2.5 py-1 whitespace-nowrap shadow-xl">
                              <span class="font-bold text-emerald-400">{{ slot?.overallProbabilityScore }}%</span> • {{ day.name }} {{ h }}:00
                            </div>
                          </div>
                        </button>
                      }
                    </div>
                  }
                </div>
              </div>

              <!-- Selected Slot Detail & Channel Breakdown Inspector -->
              @if (inspectedSlot()) {
                <div class="p-5 rounded-2xl bg-gradient-to-r from-slate-900 via-slate-900/90 to-slate-950 border border-emerald-500/30 shadow-2xl flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
                  <div class="space-y-2">
                    <div class="flex items-center gap-2.5">
                      <span class="text-xs px-2.5 py-1 rounded-md bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-bold font-mono">
                        {{ inspectedSlot()!.dayName }} {{ inspectedSlot()!.hourLabel }}
                      </span>
                      <span [class]="getBandBadgeClass(inspectedSlot()!.band)">
                        {{ inspectedSlot()!.band }} Probability Window
                      </span>
                      @if (inspectedSlot()!.isQuietHours) {
                        <span class="text-xs px-2 py-0.5 rounded bg-red-500/20 text-red-300 border border-red-500/30 font-semibold">
                          🌙 Quiet Hours
                        </span>
                      }
                    </div>
                    <div class="flex items-baseline gap-2">
                      <span class="text-3xl font-black font-mono text-white">{{ inspectedSlot()!.overallProbabilityScore }}%</span>
                      <span class="text-xs text-slate-400">Connection Velocity Index ({{ inspectedSlot()!.historicalSamples }} Telemetry Events Recorded)</span>
                    </div>
                  </div>

                  <!-- Multi-Channel Probability Meter -->
                  <div class="grid grid-cols-2 sm:grid-cols-5 gap-3 w-full md:w-auto">
                    <div class="p-2.5 rounded-xl bg-slate-950/80 border border-slate-800 text-center space-y-1">
                      <span class="text-[10px] font-semibold text-emerald-400 flex items-center justify-center gap-1">💬 WhatsApp</span>
                      <p class="text-sm font-bold font-mono text-white">{{ inspectedSlot()!.channelScores.WhatsApp }}%</p>
                    </div>
                    <div class="p-2.5 rounded-xl bg-slate-950/80 border border-slate-800 text-center space-y-1">
                      <span class="text-[10px] font-semibold text-blue-400 flex items-center justify-center gap-1">📞 Softphone</span>
                      <p class="text-sm font-bold font-mono text-white">{{ inspectedSlot()!.channelScores.Phone }}%</p>
                    </div>
                    <div class="p-2.5 rounded-xl bg-slate-950/80 border border-slate-800 text-center space-y-1">
                      <span class="text-[10px] font-semibold text-purple-400 flex items-center justify-center gap-1">✉️ Email</span>
                      <p class="text-sm font-bold font-mono text-white">{{ inspectedSlot()!.channelScores.Email }}%</p>
                    </div>
                    <div class="p-2.5 rounded-xl bg-slate-950/80 border border-slate-800 text-center space-y-1">
                      <span class="text-[10px] font-semibold text-amber-400 flex items-center justify-center gap-1">📱 SMS</span>
                      <p class="text-sm font-bold font-mono text-white">{{ inspectedSlot()!.channelScores.Sms }}%</p>
                    </div>
                    <div class="p-2.5 rounded-xl bg-slate-950/80 border border-slate-800 text-center space-y-1">
                      <span class="text-[10px] font-semibold text-cyan-400 flex items-center justify-center gap-1">💼 LinkedIn</span>
                      <p class="text-sm font-bold font-mono text-white">{{ inspectedSlot()!.channelScores.LinkedIn }}%</p>
                    </div>
                  </div>
                </div>
              }

              <!-- Optimal Contact Time Advisor -->
              <div class="p-6 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl space-y-4">
                <div class="flex items-center justify-between border-b border-slate-800 pb-4">
                  <div>
                    <h3 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>🎯 Prospect Optimal Contact Time Advisor</span>
                      <span class="text-xs px-2 py-0.5 rounded-full bg-cyan-500/20 text-cyan-300 font-semibold">AI Recommendation</span>
                    </h3>
                    <p class="text-xs text-slate-400 mt-1">Select a prospect lead and channel to calculate their top 3 optimal engagement windows.</p>
                  </div>

                  <div class="flex items-center gap-3">
                    <select 
                      [ngModel]="advisorLeadId()"
                      (ngModelChange)="onAdvisorLeadChange($event)"
                      class="bg-slate-950 border border-slate-700 text-xs rounded-lg px-3 py-1.5 text-slate-200 focus:outline-none">
                      @for (enrollee of enrollments(); track enrollee.id) {
                        <option [value]="enrollee.leadId">{{ enrollee.leadName }} ({{ enrollee.leadCompany }})</option>
                      }
                    </select>

                    <select 
                      [ngModel]="advisorChannel()"
                      (ngModelChange)="onAdvisorChannelChange($event)"
                      class="bg-slate-950 border border-slate-700 text-xs rounded-lg px-3 py-1.5 text-slate-200 focus:outline-none">
                      <option value="Phone">Phone / Softphone</option>
                      <option value="WhatsApp">WhatsApp</option>
                      <option value="Email">Email</option>
                      <option value="Sms">SMS</option>
                      <option value="LinkedIn">LinkedIn</option>
                    </select>
                  </div>
                </div>

                <!-- 3 Recommended Slots -->
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4 pt-2">
                  @for (slot of recommendedSlots(); track slot.utcSlotTime; let i = $index) {
                    <div class="p-4 rounded-xl bg-slate-950 border border-slate-800/90 relative overflow-hidden flex flex-col justify-between space-y-3 shadow-lg hover:border-emerald-500/40 transition">
                      <div class="flex items-start justify-between">
                        <div class="flex items-center gap-2">
                          <span class="h-6 w-6 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center font-bold text-xs font-mono">
                            #{{ i + 1 }}
                          </span>
                          <div>
                            <p class="text-xs font-bold text-white">{{ slot.dayOfWeek }} {{ slot.hourOfDay }}:00</p>
                            <p class="text-[10px] text-slate-400">Prospect Local Time</p>
                          </div>
                        </div>
                        <span class="text-xs font-mono font-extrabold text-emerald-400 bg-emerald-950/60 border border-emerald-500/30 px-2 py-0.5 rounded-full">
                          {{ slot.confidenceScore }}%
                        </span>
                      </div>

                      <p class="text-xs text-slate-300 leading-relaxed bg-slate-900/60 p-2.5 rounded-lg border border-slate-800">
                        {{ slot.reasoning }}
                      </p>

                      <div class="flex items-center justify-between text-[11px] text-slate-400 pt-2 border-t border-slate-900">
                        <span>Channel: <strong class="text-slate-200">{{ slot.channel }}</strong></span>
                        <button 
                          (click)="scheduleRecommendedSlot(slot)"
                          class="px-2.5 py-1 rounded bg-slate-800 hover:bg-emerald-600 hover:text-white text-emerald-400 font-semibold text-[11px] transition cursor-pointer">
                          Apply Window
                        </button>
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- TAB 2: Predictive Cadence Sequences Visualizer -->
          @if (activeTab() === 'cadences') {
            <div class="space-y-6">
              <!-- Cadence Header -->
              <div class="flex items-center justify-between p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl">
                <div>
                  <h2 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>Multi-Channel Predictive Cadence Sequences</span>
                    <span class="text-xs font-normal text-slate-400">({{ cadences().length }} Configured)</span>
                  </h2>
                  <p class="text-xs text-slate-400 mt-1">Multi-step sequences with automated condition branching and optimal timing delays.</p>
                </div>

                <div class="flex items-center gap-2">
                  @for (cad of cadences(); track cad.id) {
                    <button 
                      (click)="selectedCadenceId.set(cad.id)"
                      [class]="selectedCadenceId() === cad.id ? 'px-3 py-1.5 rounded-lg text-xs font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' : 'px-3 py-1.5 rounded-lg text-xs text-slate-400 hover:text-white hover:bg-slate-800 transition'">
                      {{ cad.name }}
                    </button>
                  }
                </div>
              </div>

              <!-- Selected Cadence Visual Sequence Pipeline -->
              @if (currentCadence()) {
                <div class="p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-6 shadow-xl">
                  <div class="flex items-center justify-between">
                    <div>
                      <h3 class="text-base font-bold text-white">{{ currentCadence()!.name }}</h3>
                      <p class="text-xs text-slate-400 mt-0.5">{{ currentCadence()!.description }}</p>
                    </div>
                    <span class="text-xs px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-mono font-semibold">
                      {{ currentCadence()!.category }}
                    </span>
                  </div>

                  <!-- Step Cards Sequence -->
                  <div class="grid grid-cols-1 md:grid-cols-5 gap-4 relative">
                    @for (step of currentCadence()!.steps; track step.id; let i = $index) {
                      <div 
                        (click)="selectedStep.set(step)"
                        [class]="selectedStep()?.id === step.id ? 'p-4 rounded-xl bg-slate-950 border-2 border-emerald-500 shadow-xl shadow-emerald-500/10 cursor-pointer space-y-3' : 'p-4 rounded-xl bg-slate-950/80 border border-slate-800 hover:border-slate-700 cursor-pointer space-y-3 transition'">
                        
                        <div class="flex items-center justify-between">
                          <span class="h-6 w-6 rounded-full bg-slate-800 text-slate-300 flex items-center justify-center font-bold text-xs font-mono">
                            #{{ step.stepNumber }}
                          </span>
                          <span [class]="getChannelBadgeClass(step.channel)">
                            {{ step.channel }}
                          </span>
                        </div>

                        <div>
                          <h4 class="text-xs font-bold text-white leading-tight">{{ step.name }}</h4>
                          <p class="text-[11px] text-slate-400 mt-1 flex items-center gap-1 font-mono">
                            ⏱️ {{ step.delayText }}
                          </p>
                        </div>

                        <div class="space-y-1.5 pt-2 border-t border-slate-900 text-[10px]">
                          @if (step.useOptimalTiming) {
                            <div class="flex items-center gap-1 text-emerald-400 font-semibold">
                              <span>✨ AI Optimal Time</span>
                            </div>
                          } @else {
                            <div class="text-slate-500">Fixed Delay Trigger</div>
                          }
                          <div class="text-slate-400 font-mono">Rule: {{ step.conditionRule }}</div>
                        </div>
                      </div>
                    }
                  </div>

                  <!-- Step Content Template Inspector -->
                  @if (selectedStep()) {
                    <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-3">
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-bold text-white flex items-center gap-2">
                          <span>Template Preview: {{ selectedStep()!.name }}</span>
                          <span [class]="getChannelBadgeClass(selectedStep()!.channel)">{{ selectedStep()!.channel }}</span>
                        </span>
                        @if (selectedStep()!.subject) {
                          <span class="text-xs font-mono text-slate-400">Subject: <strong class="text-slate-200">{{ selectedStep()!.subject }}</strong></span>
                        }
                      </div>
                      <div class="p-3 rounded-lg bg-slate-900 text-xs font-mono text-slate-300 leading-relaxed border border-slate-800 whitespace-pre-wrap">
{{ selectedStep()!.templateBody }}
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          }

          <!-- TAB 3: Active Lead Enrollee Table -->
          @if (activeTab() === 'enrollees') {
            <div class="space-y-6">
              <!-- Enrollee Table Header & Controls -->
              <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl">
                <div>
                  <h2 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>Enrolled Prospects State Machine</span>
                    <span class="text-xs font-normal text-slate-400">({{ enrollments().length }} Total Leads)</span>
                  </h2>
                  <p class="text-xs text-slate-400 mt-1">Lifecycle tracking with instant pause-on-reply and predictive step advancement.</p>
                </div>

                <div class="flex items-center gap-3">
                  <select 
                    [ngModel]="statusFilter()"
                    (ngModelChange)="statusFilter.set($event)"
                    class="bg-slate-950 border border-slate-800 text-xs rounded-lg px-3 py-1.5 text-slate-200 focus:outline-none">
                    <option value="All">All Statuses</option>
                    <option value="Active">Active</option>
                    <option value="Paused">Paused</option>
                    <option value="Replied">Replied (Auto-Paused)</option>
                    <option value="Completed">Completed</option>
                    <option value="DealWon">Deal Won</option>
                  </select>
                </div>
              </div>

              <!-- Enrollee Table -->
              <div class="rounded-2xl bg-slate-900/60 border border-slate-800 overflow-hidden shadow-xl">
                <table class="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr class="border-b border-slate-800 bg-slate-950/60 text-slate-400 uppercase text-[10px] tracking-wider">
                      <th class="p-3.5 font-bold">Prospect</th>
                      <th class="p-3.5 font-bold">Cadence & Step</th>
                      <th class="p-3.5 font-bold">Timezone</th>
                      <th class="p-3.5 font-bold">Instant Connect Score</th>
                      <th class="p-3.5 font-bold">Status</th>
                      <th class="p-3.5 font-bold text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (enrollee of filteredEnrollments(); track enrollee.id) {
                      <tr class="hover:bg-slate-800/40 transition">
                        <!-- Prospect Info -->
                        <td class="p-3.5">
                          <div class="font-bold text-white">{{ enrollee.leadName }}</div>
                          <div class="text-[11px] text-slate-400">{{ enrollee.leadCompany }} • {{ enrollee.leadEmail }}</div>
                        </td>

                        <!-- Cadence & Step -->
                        <td class="p-3.5">
                          <div class="font-semibold text-slate-200">{{ enrollee.cadenceName }}</div>
                          <div class="text-[11px] text-slate-400 mt-0.5 flex items-center gap-1.5">
                            <span class="font-mono text-cyan-400">Step #{{ enrollee.currentStepNumber }}</span>
                            @if (enrollee.nextStepChannel) {
                              <span [class]="getChannelBadgeClass(enrollee.nextStepChannel)">{{ enrollee.nextStepChannel }}</span>
                            }
                          </div>
                        </td>

                        <!-- Timezone -->
                        <td class="p-3.5 font-mono text-slate-300">
                          {{ enrollee.leadTimeZone }}
                        </td>

                        <!-- Connect Score Meter -->
                        <td class="p-3.5">
                          <div class="flex items-center gap-2">
                            <div class="w-20 bg-slate-800 rounded-full h-2 overflow-hidden">
                              <div 
                                [class]="enrollee.instantaneousScore >= 75 ? 'bg-emerald-500' : enrollee.instantaneousScore >= 45 ? 'bg-amber-500' : 'bg-slate-600'" 
                                [style.width.%]="enrollee.instantaneousScore" 
                                class="h-full rounded-full transition-all"></div>
                            </div>
                            <span class="font-mono font-bold text-slate-200">{{ enrollee.instantaneousScore }}%</span>
                          </div>
                        </td>

                        <!-- Status Badge -->
                        <td class="p-3.5">
                          <span [class]="getStatusBadgeClass(enrollee.status)">
                            {{ enrollee.status }}
                          </span>
                          @if (enrollee.pauseReason) {
                            <p class="text-[10px] text-slate-400 mt-1 max-w-[200px] truncate" [title]="enrollee.pauseReason">
                              {{ enrollee.pauseReason }}
                            </p>
                          }
                        </td>

                        <!-- Action Buttons -->
                        <td class="p-3.5 text-right space-x-2">
                          @if (enrollee.status === 'Active') {
                            <button 
                              (click)="pauseEnrollment(enrollee)"
                              class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-amber-300 font-semibold text-[11px] transition cursor-pointer">
                              Pause
                            </button>
                            <button 
                              (click)="advanceEnrollment(enrollee)"
                              class="px-2.5 py-1 rounded bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-[11px] transition cursor-pointer">
                              Advance Touch
                            </button>
                          }
                          @if (enrollee.status === 'Paused') {
                            <button 
                              (click)="resumeEnrollment(enrollee)"
                              class="px-2.5 py-1 rounded bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-[11px] transition cursor-pointer">
                              Resume
                            </button>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- TAB 4: Live Predictive Dialer Priority Queue -->
          @if (activeTab() === 'dialer') {
            <div class="space-y-6">
              <!-- Dialer Queue Header -->
              <div class="flex items-center justify-between p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl">
                <div>
                  <h2 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>⚡ Live Predictive Dialer Queue</span>
                    <span class="h-2.5 w-2.5 rounded-full bg-emerald-400 animate-ping"></span>
                  </h2>
                  <p class="text-xs text-slate-400 mt-1">Real-time priority ranking based on instantaneous connect likelihood at the prospect's local time.</p>
                </div>

                <div class="flex items-center gap-3">
                  <div class="text-xs text-slate-400">
                    Queue Size: <strong class="text-white font-mono">{{ dialerQueue().length }} Contacts</strong>
                  </div>
                </div>
              </div>

              <!-- Queue Cards Grid -->
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                @for (item of dialerQueue(); track item.enrollmentId; let idx = $index) {
                  <div class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl hover:border-emerald-500/40 transition space-y-4 flex flex-col justify-between">
                    <div class="flex items-start justify-between">
                      <div class="flex items-center gap-3">
                        <div class="h-10 w-10 rounded-xl bg-gradient-to-tr from-blue-600 to-indigo-600 flex items-center justify-center font-black text-white font-mono text-sm shadow-lg shadow-blue-500/20">
                          #{{ idx + 1 }}
                        </div>
                        <div>
                          <h4 class="text-sm font-bold text-white">{{ item.leadName }}</h4>
                          <p class="text-xs text-slate-400">{{ item.company }} • {{ item.leadPhone }}</p>
                        </div>
                      </div>

                      <div class="text-right">
                        <span class="text-lg font-black font-mono text-emerald-400">{{ item.instantaneousConfidenceScore }}%</span>
                        <p class="text-[10px] text-slate-500 font-medium uppercase tracking-wider">Connect Prob</p>
                      </div>
                    </div>

                    <!-- Recommendation & Local Time -->
                    <div class="p-3 rounded-xl bg-slate-950/80 border border-slate-800 text-xs space-y-1.5">
                      <div class="flex items-center justify-between">
                        <span class="text-slate-400 font-mono text-[11px]">Local: {{ item.leadLocalTime }} ({{ item.timeZone }})</span>
                        <span [class]="getPriorityTierBadgeClass(item.priorityTier)">
                          {{ item.priorityTier }}
                        </span>
                      </div>
                      <p class="text-slate-200 font-medium">{{ item.recommendedAction }}</p>
                    </div>

                    <!-- Instant Dialer Call Trigger Simulation -->
                    <div class="flex items-center gap-2 pt-2 border-t border-slate-800/80">
                      <button 
                        (click)="simulateCall(item, 'Answered')"
                        class="flex-1 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs shadow-lg shadow-emerald-600/20 transition cursor-pointer flex items-center justify-center gap-1.5">
                        <span>📞 Connect (Answered)</span>
                      </button>
                      <button 
                        (click)="simulateCall(item, 'Voicemail')"
                        class="px-3 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-amber-300 font-semibold text-xs transition cursor-pointer">
                        Voicemail
                      </button>
                      <button 
                        (click)="simulateCall(item, 'NoAnswer')"
                        class="px-3 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 font-semibold text-xs transition cursor-pointer">
                        No Answer
                      </button>
                    </div>
                  </div>
                }
              </div>
            </div>
          }

        </div>
      </main>
    </div>
  `
})
export class PredictiveCadenceStudioComponent implements OnInit {
  private readonly http = inject(HttpClient);

  // Active navigation tab
  readonly activeTab = signal<'heatmap' | 'cadences' | 'enrollees' | 'dialer'>('heatmap');

  // Filters & Selected State
  readonly selectedTimezone = signal<string>('UTC');
  readonly selectedChannelFilter = signal<string>('All');
  readonly inspectedSlot = signal<HeatmapSlotUi | null>(null);

  readonly advisorLeadId = signal<string>('');
  readonly advisorChannel = signal<CadenceChannelUi>('Phone');
  readonly statusFilter = signal<string>('All');
  readonly selectedCadenceId = signal<string>('');
  readonly selectedStep = signal<CadenceStepUi | null>(null);

  readonly telemetryCount = signal<number>(42);

  // Reactive Data Signals
  readonly heatmapSlots = signal<HeatmapSlotUi[]>([]);
  readonly cadences = signal<PredictiveCadenceUi[]>([]);
  readonly enrollments = signal<LeadEnrollmentUi[]>([]);
  readonly recommendedSlots = signal<OptimalSlotRecommendationUi[]>([]);

  // Constant Arrays for Grid rendering
  readonly hoursArray = Array.from({ length: 24 }, (_, i) => i);
  readonly daysOfWeek = [
    { index: 0, name: 'Sunday' },
    { index: 1, name: 'Monday' },
    { index: 2, name: 'Tuesday' },
    { index: 3, name: 'Wednesday' },
    { index: 4, name: 'Thursday' },
    { index: 5, name: 'Friday' },
    { index: 6, name: 'Saturday' }
  ];

  // Computed States
  readonly activeEnrolleesCount = computed(() =>
    this.enrollments().filter(e => e.status === 'Active').length
  );

  readonly currentCadence = computed(() => {
    const list = this.cadences();
    const id = this.selectedCadenceId();
    return list.find(c => c.id === id) || list[0] || null;
  });

  readonly filteredEnrollments = computed(() => {
    const filter = this.statusFilter();
    const all = this.enrollments();
    if (filter === 'All') return all;
    return all.filter(e => e.status === filter);
  });

  readonly dialerQueue = computed(() => {
    const activePhoneLeads = this.enrollments()
      .filter(e => e.status === 'Active' && e.nextStepChannel === 'Phone');

    return activePhoneLeads
      .map(e => {
        const tier: DialerPriorityTierUi = e.instantaneousScore >= 75 ? 'PeakPriority'
          : e.instantaneousScore >= 45 ? 'StandardPriority' : 'CoolingOff';

        const action = e.instantaneousScore >= 80
          ? `Connect Now: Peak window in ${e.leadCompany}`
          : e.instantaneousScore >= 60
            ? `High connect velocity window`
            : `Awaiting optimal confidence window`;

        return {
          enrollmentId: e.id,
          leadId: e.leadId,
          leadName: e.leadName,
          leadPhone: e.leadPhone,
          company: e.leadCompany,
          timeZone: e.leadTimeZone,
          leadLocalTime: this.formatLocalTime(e.leadTimeZone),
          instantaneousConfidenceScore: e.instantaneousScore,
          priorityTier: tier,
          recommendedAction: action,
          currentStepNumber: e.currentStepNumber,
          attemptsCount: e.attemptsCount || 0
        };
      })
      .sort((a, b) => b.instantaneousConfidenceScore - a.instantaneousConfidenceScore);
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.initMockData();
    this.fetchFromBackendIfAvailable();
  }

  private fetchFromBackendIfAvailable(): void {
    const url = `/api/v1/sales/predictive-cadence/heatmap?timeZone=${encodeURIComponent(this.selectedTimezone())}`;
    this.http.get<{ slots: any[] }>(url).subscribe({
      next: (res) => {
        if (res && res.slots && res.slots.length === 168) {
          const mapped: HeatmapSlotUi[] = res.slots.map(s => ({
            dayOfWeek: s.dayOfWeek,
            dayName: this.getDayName(s.dayOfWeek),
            hourOfDay: s.hourOfDay,
            hourLabel: `${s.hourOfDay}:00`,
            overallProbabilityScore: s.overallProbabilityScore,
            band: s.band,
            historicalSamples: s.historicalSamples,
            channelScores: s.channelScores,
            isQuietHours: s.isQuietHours
          }));
          this.heatmapSlots.set(mapped);
          if (!this.inspectedSlot() && mapped.length > 0) {
            this.inspectedSlot.set(mapped[24 * 2 + 10]); // Tuesday 10 AM default
          }
        }
      },
      error: () => {
        // Fallback gracefully to built-in reactive mock state
      }
    });
  }

  onTimezoneChange(tz: string): void {
    this.selectedTimezone.set(tz);
    this.regenerateHeatmapForTimezone(tz);
    this.refreshAdvisorRecommendations();
  }

  onChannelFilterChange(ch: string): void {
    this.selectedChannelFilter.set(ch);
  }

  onAdvisorLeadChange(leadId: string): void {
    this.advisorLeadId.set(leadId);
    this.refreshAdvisorRecommendations();
  }

  onAdvisorChannelChange(ch: CadenceChannelUi): void {
    this.advisorChannel.set(ch);
    this.refreshAdvisorRecommendations();
  }

  selectSlot(slot: HeatmapSlotUi | undefined): void {
    if (slot) {
      this.inspectedSlot.set(slot);
    }
  }

  getSlot(day: number, hour: number): HeatmapSlotUi | undefined {
    return this.heatmapSlots().find(s => s.dayOfWeek === day && s.hourOfDay === hour);
  }

  getSlotColorClass(slot: HeatmapSlotUi | undefined): string {
    if (!slot) return 'bg-slate-900 text-slate-600';
    const score = slot.overallProbabilityScore;
    if (score >= 80) return 'bg-emerald-500 text-slate-950 font-black shadow-sm shadow-emerald-500/20';
    if (score >= 65) return 'bg-emerald-600/80 text-emerald-100';
    if (score >= 50) return 'bg-amber-500/80 text-slate-950';
    if (score >= 35) return 'bg-indigo-600/50 text-indigo-200';
    if (score >= 20) return 'bg-slate-800 text-slate-400';
    return 'bg-slate-900/80 text-slate-600';
  }

  getBandBadgeClass(band: ContactTimeBandUi): string {
    switch (band) {
      case 'Peak':
        return 'text-xs px-2.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-bold';
      case 'High':
        return 'text-xs px-2.5 py-0.5 rounded-full bg-emerald-600/20 text-emerald-300 border border-emerald-600/30 font-medium';
      case 'Moderate':
        return 'text-xs px-2.5 py-0.5 rounded-full bg-amber-500/20 text-amber-300 border border-amber-500/30 font-medium';
      case 'Low':
        return 'text-xs px-2.5 py-0.5 rounded-full bg-slate-800 text-slate-400 font-medium';
      default:
        return 'text-xs px-2.5 py-0.5 rounded-full bg-slate-900 text-slate-500 font-medium';
    }
  }

  getChannelBadgeClass(ch: CadenceChannelUi): string {
    switch (ch) {
      case 'WhatsApp': return 'px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 text-[10px] font-bold';
      case 'Phone': return 'px-2 py-0.5 rounded bg-blue-500/20 text-blue-400 border border-blue-500/30 text-[10px] font-bold';
      case 'Email': return 'px-2 py-0.5 rounded bg-purple-500/20 text-purple-400 border border-purple-500/30 text-[10px] font-bold';
      case 'Sms': return 'px-2 py-0.5 rounded bg-amber-500/20 text-amber-400 border border-amber-500/30 text-[10px] font-bold';
      case 'LinkedIn': return 'px-2 py-0.5 rounded bg-cyan-500/20 text-cyan-400 border border-cyan-500/30 text-[10px] font-bold';
    }
  }

  getStatusBadgeClass(status: EnrollmentStatusUi): string {
    switch (status) {
      case 'Active': return 'px-2.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-semibold';
      case 'Replied': return 'px-2.5 py-0.5 rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/30 font-semibold';
      case 'Paused': return 'px-2.5 py-0.5 rounded-full bg-amber-500/20 text-amber-400 border border-amber-500/30 font-semibold';
      case 'DealWon': return 'px-2.5 py-0.5 rounded-full bg-purple-500/20 text-purple-400 border border-purple-500/30 font-semibold';
      case 'Completed': return 'px-2.5 py-0.5 rounded-full bg-slate-800 text-slate-300 font-semibold';
      case 'Bounced': return 'px-2.5 py-0.5 rounded-full bg-red-500/20 text-red-400 border border-red-500/30 font-semibold';
      default: return 'px-2.5 py-0.5 rounded-full bg-slate-800 text-slate-400';
    }
  }

  getPriorityTierBadgeClass(tier: DialerPriorityTierUi): string {
    switch (tier) {
      case 'PeakPriority': return 'text-[10px] px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-bold';
      case 'StandardPriority': return 'text-[10px] px-2 py-0.5 rounded-full bg-blue-500/20 text-blue-400 border border-blue-500/30 font-medium';
      case 'CoolingOff': return 'text-[10px] px-2 py-0.5 rounded-full bg-slate-800 text-slate-400 font-medium';
    }
  }

  pauseEnrollment(enrollee: LeadEnrollmentUi): void {
    this.enrollments.update(items =>
      items.map(e => e.id === enrollee.id ? { ...e, status: 'Paused', pauseReason: 'Manually paused by SDR.' } : e)
    );
  }

  resumeEnrollment(enrollee: LeadEnrollmentUi): void {
    this.enrollments.update(items =>
      items.map(e => e.id === enrollee.id ? { ...e, status: 'Active', pauseReason: undefined } : e)
    );
  }

  advanceEnrollment(enrollee: LeadEnrollmentUi): void {
    this.enrollments.update(items =>
      items.map(e => {
        if (e.id !== enrollee.id) return e;
        const nextStep = e.currentStepNumber + 1;
        if (nextStep > 5) {
          return { ...e, currentStepNumber: nextStep, status: 'Completed', nextStepChannel: undefined };
        }
        const nextCh: CadenceChannelUi = nextStep === 2 ? 'Phone' : nextStep === 3 ? 'Email' : nextStep === 4 ? 'Sms' : 'LinkedIn';
        return { ...e, currentStepNumber: nextStep, nextStepChannel: nextCh };
      })
    );
  }

  simulateIncomingReply(): void {
    // Find first active enrollee and simulate reply
    const active = this.enrollments().find(e => e.status === 'Active');
    if (active) {
      this.enrollments.update(items =>
        items.map(e => e.id === active.id
          ? {
              ...e,
              status: 'Replied',
              pauseReason: `Auto-paused: Prospect replied via WhatsApp. Next scheduled touches halted.`,
              nextStepScheduledAtUtc: undefined
            }
          : e
        )
      );
      this.telemetryCount.update(c => c + 1);
    }
  }

  simulateCall(item: PredictiveDialerItemUi, outcome: 'Answered' | 'Voicemail' | 'NoAnswer'): void {
    if (outcome === 'Answered') {
      this.enrollments.update(items =>
        items.map(e => e.id === item.enrollmentId
          ? {
              ...e,
              status: 'Replied',
              pauseReason: `Auto-paused: Prospect answered softphone call and engaged with representative.`,
              nextStepScheduledAtUtc: undefined
            }
          : e
        )
      );
    } else {
      this.enrollments.update(items =>
        items.map(e => e.id === item.enrollmentId
          ? {
              ...e,
              attemptsCount: (e.attemptsCount || 0) + 1,
              currentStepNumber: e.currentStepNumber + 1,
              nextStepChannel: 'Email'
            }
          : e
        )
      );
    }
    this.telemetryCount.update(c => c + 1);
  }

  scheduleRecommendedSlot(slot: OptimalSlotRecommendationUi): void {
    const leadId = this.advisorLeadId();
    this.enrollments.update(items =>
      items.map(e => e.leadId === leadId
        ? {
            ...e,
            nextStepScheduledAtUtc: slot.utcSlotTime,
            nextStepChannel: slot.channel,
            instantaneousScore: slot.confidenceScore
          }
        : e
      )
    );
  }

  private refreshAdvisorRecommendations(): void {
    const channel = this.advisorChannel();
    this.recommendedSlots.set([
      {
        utcSlotTime: new Date(Date.now() + 86400000).toISOString(),
        localSlotTime: new Date(Date.now() + 86400000).toISOString(),
        dayOfWeek: 'Tuesday',
        hourOfDay: 10,
        confidenceScore: channel === 'Phone' ? 91.5 : 88.0,
        channel: channel,
        reasoning: `Peak Golden Window: Prospect previously opened message at 10:00 AM on Tuesdays (+18% telemetry boost).`,
        band: 'Peak'
      },
      {
        utcSlotTime: new Date(Date.now() + 172800000).toISOString(),
        localSlotTime: new Date(Date.now() + 172800000).toISOString(),
        dayOfWeek: 'Thursday',
        hourOfDay: 14,
        confidenceScore: channel === 'Phone' ? 84.2 : 82.5,
        channel: channel,
        reasoning: `High Confidence Window: Mid-afternoon decision-maker connect rate is elevated for ${channel}.`,
        band: 'High'
      },
      {
        utcSlotTime: new Date(Date.now() + 259200000).toISOString(),
        localSlotTime: new Date(Date.now() + 259200000).toISOString(),
        dayOfWeek: 'Wednesday',
        hourOfDay: 11,
        confidenceScore: 78.4,
        channel: channel,
        reasoning: `Favorable business-hours touchpoint with minimal meeting collision risk.`,
        band: 'High'
      }
    ]);
  }

  private formatLocalTime(tz: string): string {
    const now = new Date();
    return `${now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
  }

  private getDayName(d: number): string {
    return ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'][d] || 'Day';
  }

  private regenerateHeatmapForTimezone(tz: string): void {
    const slots: HeatmapSlotUi[] = [];
    const offsetHours = tz === 'UTC-5' ? -5 : tz === 'UTC+1' ? 1 : tz === 'UTC+4' ? 4 : tz === 'UTC+9' ? 9 : 0;

    for (let d = 0; d < 7; d++) {
      for (let h = 0; h < 24; h++) {
        const isWeekend = d === 0 || d === 6;
        const isQuiet = h < 8 || h >= 20;

        let baseScore = isQuiet ? 8.0 : isWeekend ? 25.0 : 55.0;
        if (!isQuiet && !isWeekend) {
          if ((d === 2 || d === 4) && (h === 10 || h === 14 || h === 15)) {
            baseScore = 88.5; // Peak golden window
          } else if (h >= 9 && h <= 17) {
            baseScore = 72.0;
          }
        }

        const score = Math.round(baseScore);
        const band: ContactTimeBandUi = score >= 80 ? 'Peak' : score >= 65 ? 'High' : score >= 45 ? 'Moderate' : score >= 20 ? 'Low' : 'OffHours';

        slots.push({
          dayOfWeek: d,
          dayName: this.getDayName(d),
          hourOfDay: h,
          hourLabel: `${h}:00`,
          overallProbabilityScore: score,
          band: band,
          historicalSamples: score >= 70 ? 14 : 3,
          channelScores: {
            WhatsApp: Math.min(95, score + 6),
            Phone: Math.min(94, isWeekend ? 10 : score + (h === 10 || h === 14 ? 12 : -5)),
            Email: Math.min(90, score + 2),
            Sms: Math.min(85, score - 4),
            LinkedIn: Math.min(88, isWeekend ? 8 : score)
          },
          isQuietHours: isQuiet
        });
      }
    }

    this.heatmapSlots.set(slots);
    this.inspectedSlot.set(slots[24 * 2 + 10]); // Tuesday 10 AM default
  }

  private initMockData(): void {
    this.regenerateHeatmapForTimezone(this.selectedTimezone());

    // 3 Mock Cadences
    const c1Id = 'c1111111-0000-0000-0000-000000000001';
    const c2Id = 'c2222222-0000-0000-0000-000000000002';
    const c3Id = 'c3333333-0000-0000-0000-000000000003';

    this.cadences.set([
      {
        id: c1Id,
        name: 'Enterprise SaaS Inbound Fast-Response',
        description: '5-touch omni-channel sequence triggered within 5 minutes of demo request.',
        category: 'InboundFastResponse',
        isActive: true,
        createdAt: '2026-10-15',
        steps: [
          {
            id: 's1',
            stepNumber: 1,
            name: 'Instant WhatsApp Welcome & Calendar Link',
            channel: 'WhatsApp',
            delayText: '0m (Instant)',
            useOptimalTiming: false,
            conditionRule: 'Always',
            templateBody: 'Hi {{LeadName}}, saw your demo request for {{CompanyName}}. Here is our executive calendar link: https://sgrowing.io/book'
          },
          {
            id: 's2',
            stepNumber: 2,
            name: 'Optimal Contact Window Discovery Softphone Call',
            channel: 'Phone',
            delayText: '+2h Delay',
            useOptimalTiming: true,
            conditionRule: 'IfNoReply',
            templateBody: 'Account Executive rapid qualification call using automated dialer queue.'
          },
          {
            id: 's3',
            stepNumber: 3,
            name: 'ROI Proof & Customer Case Study Email',
            channel: 'Email',
            delayText: '+1d Delay',
            useOptimalTiming: true,
            conditionRule: 'IfNoReply',
            subject: 'How similar enterprises achieved +34% margin growth',
            templateBody: 'Hello {{LeadName}},\n\nFollowing up with a brief case study on how teams like {{CompanyName}} deployed predictive orchestration.'
          },
          {
            id: 's4',
            stepNumber: 4,
            name: 'Urgent SMS Follow-up & Executive Summary',
            channel: 'Sms',
            delayText: '+2d Delay',
            useOptimalTiming: true,
            conditionRule: 'IfNoReply',
            templateBody: 'Hi {{LeadName}}, sent over the enterprise architecture PDF. Did you have 5 mins to chat today?'
          },
          {
            id: 's5',
            stepNumber: 5,
            name: 'Executive LinkedIn InMail Connection',
            channel: 'LinkedIn',
            delayText: '+3d Delay',
            useOptimalTiming: true,
            conditionRule: 'IfNoReply',
            subject: 'Partnership with {{CompanyName}}',
            templateBody: 'Hi {{LeadName}}, would love to connect and share notes on revenue orchestration trends in your sector.'
          }
        ]
      },
      {
        id: c2Id,
        name: 'High-Ticket COD Confirmation Sequence',
        description: 'Anti-fraud and logistics dispatch verification to maximize delivery success rate.',
        category: 'CodConfirmation',
        isActive: true,
        createdAt: '2026-10-10',
        steps: [
          {
            id: 's21',
            stepNumber: 1,
            name: 'WhatsApp Interactive Order Verification',
            channel: 'WhatsApp',
            delayText: '0m',
            useOptimalTiming: false,
            conditionRule: 'Always',
            templateBody: 'Hello {{LeadName}}, please confirm your shipping address for order #{{OrderId}} by tapping Confirm.'
          },
          {
            id: 's22',
            stepNumber: 2,
            name: 'Predictive Voice Call Confirmation',
            channel: 'Phone',
            delayText: '+1h',
            useOptimalTiming: true,
            conditionRule: 'IfNoReply',
            templateBody: 'Softphone agent automated confirmation call.'
          },
          {
            id: 's23',
            stepNumber: 3,
            name: 'SMS Tracking & Courier Lock Alert',
            channel: 'Sms',
            delayText: '+3h',
            useOptimalTiming: false,
            conditionRule: 'IfNoReply',
            templateBody: '{{LeadName}}, your parcel is prepped for courier handover. Reply YES to confirm delivery.'
          }
        ]
      },
      {
        id: c3Id,
        name: 'Re-engagement Dormant Leads',
        description: 'Multi-touch sequence revitalizing stalled pipeline and 60-day dormant accounts.',
        category: 'Reengagement',
        isActive: true,
        createdAt: '2026-09-28',
        steps: [
          {
            id: 's31',
            stepNumber: 1,
            name: 'New AI Feature Announcement & Direct Teaser',
            channel: 'Email',
            delayText: '0d',
            useOptimalTiming: true,
            conditionRule: 'Always',
            subject: 'Major updates to SGrowing sales acceleration',
            templateBody: 'Hi {{LeadName}}, we just introduced predictive dialer & 7x24 contact heatmap intelligence.'
          },
          {
            id: 's32',
            stepNumber: 2,
            name: 'LinkedIn Industry Touchpoint',
            channel: 'LinkedIn',
            delayText: '+2d',
            useOptimalTiming: true,
            conditionRule: 'IfNoReply',
            subject: 'Quick note on your tech stack',
            templateBody: 'Noticed recent expansions at {{CompanyName}}.'
          },
          {
            id: 's33',
            stepNumber: 3,
            name: 'VIP Relationship Check-in Call',
            channel: 'Phone',
            delayText: '+4d',
            useOptimalTiming: true,
            conditionRule: 'IfNoReply',
            templateBody: 'Account director phone check-in.'
          }
        ]
      }
    ]);

    this.selectedCadenceId.set(c1Id);
    this.selectedStep.set(this.cadences()[0].steps[0]);

    // Enrolled Leads
    this.enrollments.set([
      {
        id: 'e1',
        cadenceId: c1Id,
        cadenceName: 'Enterprise SaaS Inbound Fast-Response',
        leadId: 'l1',
        leadName: 'Sarah Jenkins',
        leadEmail: 'sarah.j@vertexcloud.com',
        leadPhone: '+1 415-555-0192',
        leadCompany: 'Vertex Cloud Corp',
        leadTimeZone: 'UTC-5',
        status: 'Active',
        currentStepNumber: 2,
        enrolledAtUtc: '2026-10-28T14:00:00Z',
        nextStepChannel: 'Phone',
        nextStepScheduledAtUtc: '2026-10-30T15:00:00Z',
        instantaneousScore: 91.5,
        attemptsCount: 0
      },
      {
        id: 'e2',
        cadenceId: c1Id,
        cadenceName: 'Enterprise SaaS Inbound Fast-Response',
        leadId: 'l2',
        leadName: 'Marcus Vance',
        leadEmail: 'm.vance@solarisdynamics.io',
        leadPhone: '+44 20 7946 0912',
        leadCompany: 'Solaris Dynamics',
        leadTimeZone: 'UTC+1',
        status: 'Active',
        currentStepNumber: 2,
        enrolledAtUtc: '2026-10-27T09:30:00Z',
        nextStepChannel: 'Phone',
        nextStepScheduledAtUtc: '2026-10-30T10:00:00Z',
        instantaneousScore: 84.8,
        attemptsCount: 1
      },
      {
        id: 'e3',
        cadenceId: c1Id,
        cadenceName: 'Enterprise SaaS Inbound Fast-Response',
        leadId: 'l3',
        leadName: 'Elena Rostova',
        leadEmail: 'elena@nordicscale.se',
        leadPhone: '+46 8 123 4567',
        leadCompany: 'Nordic Scale Tech',
        leadTimeZone: 'UTC+1',
        status: 'Active',
        currentStepNumber: 3,
        enrolledAtUtc: '2026-10-25T11:00:00Z',
        nextStepChannel: 'Email',
        nextStepScheduledAtUtc: '2026-10-31T09:00:00Z',
        instantaneousScore: 76.2,
        attemptsCount: 2
      },
      {
        id: 'e4',
        cadenceId: c2Id,
        cadenceName: 'High-Ticket COD Confirmation Sequence',
        leadId: 'l4',
        leadName: 'Tariq Al-Mansoor',
        leadEmail: 'tariq@gulflogistics.ae',
        leadPhone: '+971 4 321 0000',
        leadCompany: 'Gulf Logistics Hub',
        leadTimeZone: 'UTC+4',
        status: 'Active',
        currentStepNumber: 2,
        enrolledAtUtc: '2026-10-29T08:00:00Z',
        nextStepChannel: 'Phone',
        nextStepScheduledAtUtc: '2026-10-30T11:00:00Z',
        instantaneousScore: 89.0,
        attemptsCount: 0
      },
      {
        id: 'e5',
        cadenceId: c1Id,
        cadenceName: 'Enterprise SaaS Inbound Fast-Response',
        leadId: 'l5',
        leadName: 'Chloe Laurent',
        leadEmail: 'chloe@hexaprime.fr',
        leadPhone: '+33 1 42 68 55 00',
        leadCompany: 'Hexa Prime Group',
        leadTimeZone: 'UTC+1',
        status: 'Replied',
        currentStepNumber: 1,
        enrolledAtUtc: '2026-10-26T14:00:00Z',
        pauseReason: 'Auto-paused: Prospect replied via WhatsApp. All subsequent touches halted.',
        instantaneousScore: 95.0,
        attemptsCount: 1
      },
      {
        id: 'e6',
        cadenceId: c3Id,
        cadenceName: 'Re-engagement Dormant Leads',
        leadId: 'l6',
        leadName: 'Bradley Cooper',
        leadEmail: 'bcooper@apollocapital.com',
        leadPhone: '+1 212-555-0841',
        leadCompany: 'Apollo Capital Partners',
        leadTimeZone: 'UTC-5',
        status: 'Paused',
        currentStepNumber: 2,
        enrolledAtUtc: '2026-10-20T10:00:00Z',
        pauseReason: 'Manually paused by SDR pending contract review.',
        instantaneousScore: 68.0,
        attemptsCount: 1
      }
    ]);

    this.advisorLeadId.set('l1');
    this.refreshAdvisorRecommendations();
  }
}
