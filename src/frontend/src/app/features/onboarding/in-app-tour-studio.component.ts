import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import {
  ProductTourDto,
  TourStepDto,
  AnnouncementModalDto,
  OnboardingMetricsSummaryDto,
  TourPlacement,
  AnnouncementModalType
} from '../../core/models/onboarding-tour.models';

@Component({
  selector: 'app-in-app-tour-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="h-full flex flex-col bg-slate-950 text-slate-100 overflow-y-auto font-sans" dir="ltr">
      <!-- Studio Header -->
      <header class="p-6 border-b border-slate-800 bg-slate-900/60 backdrop-blur sticky top-0 z-30 flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <a routerLink="/" class="text-xs text-slate-400 hover:text-white px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800 transition">
            &larr; Inbox
          </a>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-2xl">🧭</span>
              <h1 class="text-xl font-bold tracking-tight text-white">In-App Tour Studio & Behavioral Announcements</h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                Horizon 17 &bull; Activation & Retention
              </span>
            </div>
            <p class="text-xs text-slate-400 mt-1">
              Design interactive spotlight product walkthroughs, trigger contextual behavioral announcements, and inspect drop-off funnel analytics.
            </p>
          </div>
        </div>

        <!-- Navigation Tabs -->
        <div class="flex items-center gap-1 bg-slate-900 p-1 rounded-xl border border-slate-800 text-xs">
          <button
            (click)="activeTab.set('builder')"
            [class.bg-indigo-600]="activeTab() === 'builder'"
            [class.text-white]="activeTab() === 'builder'"
            class="px-3.5 py-1.5 rounded-lg text-slate-400 hover:text-white transition font-medium flex items-center gap-1.5">
            <span>🛠️</span> Tour Builder
          </button>
          <button
            (click)="activeTab.set('preview')"
            [class.bg-indigo-600]="activeTab() === 'preview'"
            [class.text-white]="activeTab() === 'preview'"
            class="px-3.5 py-1.5 rounded-lg text-slate-400 hover:text-white transition font-medium flex items-center gap-1.5">
            <span>🔦</span> Spotlight Preview
          </button>
          <button
            (click)="activeTab.set('announcements')"
            [class.bg-indigo-600]="activeTab() === 'announcements'"
            [class.text-white]="activeTab() === 'announcements'"
            class="px-3.5 py-1.5 rounded-lg text-slate-400 hover:text-white transition font-medium flex items-center gap-1.5">
            <span>📢</span> Announcement Modals
          </button>
          <button
            (click)="activeTab.set('analytics')"
            [class.bg-indigo-600]="activeTab() === 'analytics'"
            [class.text-white]="activeTab() === 'analytics'"
            class="px-3.5 py-1.5 rounded-lg text-slate-400 hover:text-white transition font-medium flex items-center gap-1.5">
            <span>📊</span> Funnel Analytics
          </button>
        </div>
      </header>

      <!-- Main Studio Content Area -->
      <main class="p-6 space-y-6 flex-1 max-w-7xl mx-auto w-full">
        <!-- TAB 1: INTERACTIVE TOUR BUILDER -->
        @if (activeTab() === 'builder') {
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            <!-- Left Panel: Tour List & New Tour Form -->
            <div class="lg:col-span-4 space-y-4">
              <div class="flex items-center justify-between">
                <h2 class="text-sm font-semibold uppercase tracking-wider text-slate-400">Available Tours</h2>
                <button
                  (click)="startNewTour()"
                  class="px-2.5 py-1 text-xs bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg transition font-medium flex items-center gap-1">
                  <span>+</span> New Tour
                </button>
              </div>

              <!-- Tour List Cards -->
              <div class="space-y-2">
                @for (tour of tours(); track tour.id) {
                  <div
                    (click)="selectTour(tour)"
                    [class.border-indigo-500]="selectedTour()?.id === tour.id"
                    [class.bg-slate-900]="selectedTour()?.id === tour.id"
                    [class.bg-slate-900/50]="selectedTour()?.id !== tour.id"
                    class="p-3.5 rounded-xl border border-slate-800 hover:border-slate-700 cursor-pointer transition">
                    <div class="flex items-start justify-between gap-2">
                      <div class="font-semibold text-sm text-white">{{ tour.title }}</div>
                      <span [class.bg-emerald-500_10]="tour.isActive" [class.text-emerald-400]="tour.isActive" class="text-[10px] px-2 py-0.5 rounded-full border border-emerald-500/20 bg-emerald-500/10">
                        {{ tour.isActive ? 'Active' : 'Draft' }}
                      </span>
                    </div>
                    <p class="text-xs text-slate-400 mt-1 line-clamp-2">{{ tour.description }}</p>
                    <div class="flex items-center gap-3 mt-3 text-[11px] text-slate-500 font-mono">
                      <span>🎯 {{ tour.targetRoute }}</span>
                      <span>👤 {{ tour.targetRole }}</span>
                      <span>🔢 {{ tour.steps.length }} steps</span>
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Right Panel: Step Sequence Editor -->
            <div class="lg:col-span-8 space-y-5">
              @if (selectedTour(); as tour) {
                <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-5">
                  <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-4">
                    <div>
                      <h3 class="text-base font-bold text-white flex items-center gap-2">
                        <span>🧭</span> {{ tour.title }}
                      </h3>
                      <p class="text-xs text-slate-400 mt-0.5">Route: <code class="text-indigo-400 font-mono">{{ tour.targetRoute }}</code> &bull; Role: <span class="text-purple-400">{{ tour.targetRole }}</span></p>
                    </div>
                    <div class="flex items-center gap-2">
                      <button
                        (click)="previewSelectedTour()"
                        class="px-3 py-1.5 text-xs bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg transition font-medium flex items-center gap-1.5 border border-slate-700">
                        <span>🔦</span> Test in Preview
                      </button>
                      <button
                        (click)="saveTourSteps()"
                        class="px-3.5 py-1.5 text-xs bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg transition font-medium flex items-center gap-1.5">
                        <span>💾</span> Save Steps
                      </button>
                    </div>
                  </div>

                  <!-- Tour Settings Details -->
                  <div class="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
                    <div>
                      <label class="block text-slate-400 mb-1">Target Route</label>
                      <input
                        type="text"
                        [(ngModel)]="tour.targetRoute"
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono"
                        placeholder="/channels/whatsapp-pairing" />
                    </div>
                    <div>
                      <label class="block text-slate-400 mb-1">Target Role</label>
                      <select
                        [(ngModel)]="tour.targetRole"
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500">
                        <option value="All">All Users</option>
                        <option value="Admin">Admin</option>
                        <option value="Merchant">Merchant</option>
                        <option value="CourierDispatcher">Courier Dispatcher</option>
                      </select>
                    </div>
                    <div>
                      <label class="block text-slate-400 mb-1">Status</label>
                      <div class="flex items-center gap-2 mt-2">
                        <input
                          type="checkbox"
                          [(ngModel)]="tour.isActive"
                          id="tourActiveToggle"
                          class="rounded bg-slate-950 border-slate-800 text-indigo-600 focus:ring-0" />
                        <label for="tourActiveToggle" class="text-slate-300">Live Auto-Trigger</label>
                      </div>
                    </div>
                  </div>

                  <!-- Steps Sequence List -->
                  <div class="space-y-3 pt-2">
                    <div class="flex items-center justify-between">
                      <h4 class="text-xs font-semibold uppercase tracking-wider text-slate-400">
                        Step Progression ({{ tour.steps.length }})
                      </h4>
                      <button
                        (click)="addStepToCurrentTour()"
                        class="px-2.5 py-1 text-xs bg-purple-600/20 hover:bg-purple-600/30 text-purple-300 border border-purple-500/30 rounded-lg transition font-medium flex items-center gap-1">
                        <span>+</span> Append Step
                      </button>
                    </div>

                    @for (step of tour.steps; track step.stepOrder; let idx = $index) {
                      <div class="p-4 bg-slate-950/70 border border-slate-800 rounded-xl space-y-3">
                        <div class="flex items-center justify-between">
                          <div class="flex items-center gap-2">
                            <span class="w-6 h-6 rounded-full bg-indigo-500/20 border border-indigo-500/40 text-indigo-400 text-xs font-bold flex items-center justify-center font-mono">
                              {{ step.stepOrder }}
                            </span>
                            <input
                              type="text"
                              [(ngModel)]="step.title"
                              placeholder="Step Title"
                              class="bg-transparent border-b border-transparent hover:border-slate-700 focus:border-indigo-500 text-sm font-semibold text-white px-1 py-0.5 focus:outline-none" />
                          </div>
                          <div class="flex items-center gap-1">
                            <button
                              (click)="moveStep(idx, -1)"
                              [disabled]="idx === 0"
                              class="p-1 text-slate-400 hover:text-white disabled:opacity-30 disabled:cursor-not-allowed">
                              &uarr;
                            </button>
                            <button
                              (click)="moveStep(idx, 1)"
                              [disabled]="idx === tour.steps.length - 1"
                              class="p-1 text-slate-400 hover:text-white disabled:opacity-30 disabled:cursor-not-allowed">
                              &darr;
                            </button>
                            <button
                              (click)="removeStep(idx)"
                              class="p-1 text-red-400 hover:text-red-300 ml-2">
                              &times;
                            </button>
                          </div>
                        </div>

                        <!-- Step Config Form -->
                        <div class="grid grid-cols-1 md:grid-cols-12 gap-3 text-xs">
                          <div class="md:col-span-5">
                            <label class="block text-slate-400 mb-1">Target Element Selector</label>
                            <input
                              type="text"
                              [(ngModel)]="step.targetSelector"
                              placeholder="e.g. [data-tour-step='qr-code'] or #inbox-list"
                              class="w-full bg-slate-900 border border-slate-800 rounded-lg px-2.5 py-1.5 text-slate-200 font-mono focus:border-indigo-500 focus:outline-none" />
                          </div>
                          <div class="md:col-span-3">
                            <label class="block text-slate-400 mb-1">Placement</label>
                            <select
                              [(ngModel)]="step.placement"
                              class="w-full bg-slate-900 border border-slate-800 rounded-lg px-2.5 py-1.5 text-slate-200 focus:border-indigo-500 focus:outline-none">
                              <option value="Bottom">Bottom</option>
                              <option value="Top">Top</option>
                              <option value="Left">Left</option>
                              <option value="Right">Right</option>
                            </select>
                          </div>
                          <div class="md:col-span-4 flex items-center gap-3 pt-4">
                            <label class="flex items-center gap-1.5 cursor-pointer text-slate-300">
                              <input type="checkbox" [(ngModel)]="step.backdropHighlight" class="rounded bg-slate-900 border-slate-800 text-indigo-600 focus:ring-0" />
                              <span>Backdrop Dim</span>
                            </label>
                            <label class="flex items-center gap-1.5 cursor-pointer text-slate-300">
                              <input type="checkbox" [(ngModel)]="step.requireInteraction" class="rounded bg-slate-900 border-slate-800 text-indigo-600 focus:ring-0" />
                              <span>Must Click</span>
                            </label>
                          </div>
                        </div>

                        <div>
                          <label class="block text-slate-400 text-xs mb-1">Tooltip Content & Instructions</label>
                          <textarea
                            [(ngModel)]="step.content"
                            rows="2"
                            placeholder="Detailed onboarding guidance..."
                            class="w-full bg-slate-900 border border-slate-800 rounded-lg p-2 text-xs text-slate-200 focus:border-indigo-500 focus:outline-none"></textarea>
                        </div>
                      </div>
                    }
                  </div>
                </div>
              } @else {
                <div class="p-12 border border-dashed border-slate-800 rounded-2xl text-center text-slate-500 text-sm">
                  Select a tour from the left or create a new one to begin designing guided steps.
                </div>
              }
            </div>
          </div>
        }

        <!-- TAB 2: LIVE TOUR SPOTLIGHT PREVIEW -->
        @if (activeTab() === 'preview') {
          <div class="space-y-4">
            <!-- Preview Controls Bar -->
            <div class="flex flex-wrap items-center justify-between gap-3 bg-slate-900 p-4 rounded-xl border border-slate-800">
              <div class="flex items-center gap-3">
                <span class="text-xs text-slate-400">Previewing:</span>
                <select
                  [ngModel]="selectedTour()?.id"
                  (ngModelChange)="onPreviewTourChange($event)"
                  class="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500">
                  @for (tour of tours(); track tour.id) {
                    <option [value]="tour.id">{{ tour.title }} ({{ tour.targetRoute }})</option>
                  }
                </select>
                <div class="text-xs text-indigo-400 font-mono bg-indigo-950/60 px-2.5 py-1 rounded-md border border-indigo-800/40">
                  Step {{ previewCurrentStepIndex() + 1 }} of {{ currentPreviewSteps().length }}
                </div>
              </div>
              <div class="flex items-center gap-2">
                <button
                  (click)="restartPreview()"
                  class="px-3 py-1 text-xs bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg transition border border-slate-700">
                  Restart Tour
                </button>
              </div>
            </div>

            <!-- Mock Application Viewport with Spotlight Cutout -->
            <div class="relative bg-slate-950 border border-slate-800 rounded-2xl overflow-hidden min-h-[540px] shadow-2xl flex flex-col">
              <!-- Mock Browser Address Bar -->
              <div class="bg-slate-900 border-b border-slate-800 px-4 py-2.5 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="w-3 h-3 rounded-full bg-rose-500/80 inline-block"></span>
                  <span class="w-3 h-3 rounded-full bg-amber-500/80 inline-block"></span>
                  <span class="w-3 h-3 rounded-full bg-emerald-500/80 inline-block"></span>
                  <span class="ml-3 text-xs font-mono text-slate-400">
                    https://app.sgrowing.io{{ selectedTour()?.targetRoute || '/dashboard' }}
                  </span>
                </div>
                <span class="text-[10px] text-slate-500 uppercase tracking-widest font-mono">Live Mock Sandbox</span>
              </div>

              <!-- Mock Application Content Body -->
              <div class="relative flex-1 p-6 grid grid-cols-1 md:grid-cols-3 gap-6">
                <!-- Target Area 1: Left Widget -->
                <div
                  id="target-mock-1"
                  [class.ring-2]="isTargetActive(1)"
                  [class.ring-indigo-400]="isTargetActive(1)"
                  [class.shadow-indigo-500_50]="isTargetActive(1)"
                  (click)="handleTargetClick(1)"
                  class="p-5 bg-slate-900/80 rounded-xl border border-slate-800 transition cursor-pointer relative z-10 flex flex-col justify-between">
                  <div>
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-mono text-indigo-400">[data-tour-step="qr-code"]</span>
                      <span class="text-xs">📱</span>
                    </div>
                    <h4 class="text-sm font-bold text-white mt-2">WhatsApp Baileys Pairing</h4>
                    <p class="text-xs text-slate-400 mt-1">Multi-device websocket pairing gateway.</p>
                    <div class="mt-4 p-4 bg-slate-950 rounded-lg flex items-center justify-center border border-slate-800">
                      <div class="w-24 h-24 bg-white/10 rounded-md border border-white/20 flex items-center justify-center text-2xl font-mono">
                        🏁 QR
                      </div>
                    </div>
                  </div>
                  <span class="text-[10px] text-slate-500 mt-3 block">Click to simulate scan action &rarr;</span>
                </div>

                <!-- Target Area 2: Middle Widget -->
                <div
                  id="target-mock-2"
                  [class.ring-2]="isTargetActive(2)"
                  [class.ring-indigo-400]="isTargetActive(2)"
                  (click)="handleTargetClick(2)"
                  class="p-5 bg-slate-900/80 rounded-xl border border-slate-800 transition cursor-pointer relative z-10 flex flex-col justify-between">
                  <div>
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-mono text-indigo-400">[data-tour-step="webhook-status"]</span>
                      <span class="text-xs">⚡</span>
                    </div>
                    <h4 class="text-sm font-bold text-white mt-2">Real-Time Inbound Webhooks</h4>
                    <p class="text-xs text-slate-400 mt-1">Status heartbeat & delivery monitor.</p>
                    <div class="mt-4 p-3 bg-emerald-950/40 border border-emerald-500/30 rounded-lg text-xs text-emerald-300 flex items-center gap-2">
                      <span class="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-ping"></span>
                      <span>Connected &bull; 100% Up</span>
                    </div>
                  </div>
                  <span class="text-[10px] text-slate-500 mt-3 block">Click to verify handshake &rarr;</span>
                </div>

                <!-- Target Area 3: Right Widget -->
                <div
                  id="target-mock-3"
                  [class.ring-2]="isTargetActive(3)"
                  [class.ring-indigo-400]="isTargetActive(3)"
                  (click)="handleTargetClick(3)"
                  class="p-5 bg-slate-900/80 rounded-xl border border-slate-800 transition cursor-pointer relative z-10 flex flex-col justify-between">
                  <div>
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-mono text-indigo-400">[data-tour-step="test-dispatch"]</span>
                      <span class="text-xs">🚀</span>
                    </div>
                    <h4 class="text-sm font-bold text-white mt-2">Test Live Template Ping</h4>
                    <p class="text-xs text-slate-400 mt-1">Dispatch test notification to verified mobile phone.</p>
                    <button class="mt-4 w-full py-2 bg-indigo-600/40 hover:bg-indigo-600 text-indigo-200 hover:text-white rounded-lg text-xs font-medium border border-indigo-500/40 transition">
                      Dispatch Test Ping
                    </button>
                  </div>
                  <span class="text-[10px] text-slate-500 mt-3 block">Click to fire template &rarr;</span>
                </div>
              </div>

              <!-- Interactive Spotlight Backdrop Layer -->
              @if (currentPreviewStep()?.backdropHighlight) {
                <div class="absolute inset-0 bg-slate-950/80 pointer-events-none z-20 backdrop-blur-[1px] transition-opacity duration-300"></div>
              }

              <!-- Interactive Tooltip Overlay Card -->
              @if (currentPreviewStep(); as activeStep) {
                <div
                  [class.translate-y-0]="true"
                  class="absolute z-30 transition-all duration-300 max-w-sm w-full p-4 rounded-xl bg-slate-900 border border-indigo-500/50 shadow-2xl shadow-indigo-950/60"
                  [ngStyle]="getTooltipPositionStyle()">
                  <!-- Tooltip Header -->
                  <div class="flex items-start justify-between gap-2 border-b border-slate-800 pb-2">
                    <div class="flex items-center gap-1.5">
                      <span class="text-xs px-1.5 py-0.5 rounded bg-indigo-500/20 text-indigo-400 font-mono font-bold">
                        {{ activeStep.stepOrder }}
                      </span>
                      <h5 class="text-sm font-bold text-white">{{ activeStep.title }}</h5>
                    </div>
                    <button (click)="skipPreviewTour()" class="text-slate-400 hover:text-white text-xs">&times;</button>
                  </div>

                  <!-- Tooltip Body -->
                  <p class="text-xs text-slate-300 mt-2.5 leading-relaxed">
                    {{ activeStep.content }}
                  </p>

                  @if (activeStep.requireInteraction) {
                    <div class="mt-2 text-[10px] text-amber-400 font-mono flex items-center gap-1 bg-amber-500/10 p-1.5 rounded border border-amber-500/20">
                      <span>⚠️</span> Interaction required: Click highlighted element to advance
                    </div>
                  }

                  <!-- Tooltip Footer Navigation Buttons -->
                  <div class="flex items-center justify-between mt-4 pt-2 border-t border-slate-800">
                    <span class="text-[11px] text-slate-400 font-mono">
                      {{ previewCurrentStepIndex() + 1 }} / {{ currentPreviewSteps().length }}
                    </span>
                    <div class="flex items-center gap-2">
                      @if (previewCurrentStepIndex() > 0) {
                        <button
                          (click)="prevPreviewStep()"
                          class="px-2.5 py-1 text-xs bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-md transition">
                          &larr; Back
                        </button>
                      }
                      @if (previewCurrentStepIndex() < currentPreviewSteps().length - 1) {
                        <button
                          (click)="nextPreviewStep()"
                          class="px-3 py-1 text-xs bg-indigo-600 hover:bg-indigo-500 text-white rounded-md transition font-medium">
                          Next &rarr;
                        </button>
                      } @else {
                        <button
                          (click)="completePreviewTour()"
                          class="px-3 py-1 text-xs bg-emerald-600 hover:bg-emerald-500 text-white rounded-md transition font-medium">
                          Finish Tour &check;
                        </button>
                      }
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- TAB 3: BEHAVIORAL ANNOUNCEMENT MODAL DESIGNER -->
        @if (activeTab() === 'announcements') {
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            <!-- Left Form: Create & Edit Announcement -->
            <div class="lg:col-span-5 bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
              <h3 class="text-sm font-semibold uppercase tracking-wider text-slate-400">Modal Specification</h3>

              <div>
                <label class="block text-xs text-slate-400 mb-1">Headline / Title</label>
                <input
                  type="text"
                  [(ngModel)]="newAnnouncement.title"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500"
                  placeholder="e.g. 🚀 WhatsApp Baileys Gateway v2 Released!" />
              </div>

              <div>
                <label class="block text-xs text-slate-400 mb-1">Body Text</label>
                <textarea
                  [(ngModel)]="newAnnouncement.bodyText"
                  rows="3"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500"
                  placeholder="Announce product upgrades, operational alerts, or new feature onboarding..."></textarea>
              </div>

              <div class="grid grid-cols-2 gap-3 text-xs">
                <div>
                  <label class="block text-slate-400 mb-1">CTA Button Text</label>
                  <input
                    type="text"
                    [(ngModel)]="newAnnouncement.ctaText"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-slate-200 focus:border-indigo-500 focus:outline-none"
                    placeholder="e.g. Try It Now" />
                </div>
                <div>
                  <label class="block text-slate-400 mb-1">CTA Destination URL</label>
                  <input
                    type="text"
                    [(ngModel)]="newAnnouncement.ctaUrl"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-slate-200 font-mono focus:border-indigo-500 focus:outline-none"
                    placeholder="/channels/whatsapp-pairing" />
                </div>
              </div>

              <!-- Format Switcher -->
              <div>
                <label class="block text-xs text-slate-400 mb-2">Display Format</label>
                <div class="grid grid-cols-3 gap-2">
                  <button
                    type="button"
                    (click)="newAnnouncement.modalType = 'CenteredModal'"
                    [class.border-indigo-500]="newAnnouncement.modalType === 'CenteredModal'"
                    [class.bg-indigo-950_40]="newAnnouncement.modalType === 'CenteredModal'"
                    class="p-2 rounded-lg border border-slate-800 text-center text-xs text-slate-300 hover:border-slate-700 transition">
                    <div class="text-base mb-1">🪟</div>
                    <div class="font-medium">Centered</div>
                  </button>
                  <button
                    type="button"
                    (click)="newAnnouncement.modalType = 'FloatingBanner'"
                    [class.border-indigo-500]="newAnnouncement.modalType === 'FloatingBanner'"
                    [class.bg-indigo-950_40]="newAnnouncement.modalType === 'FloatingBanner'"
                    class="p-2 rounded-lg border border-slate-800 text-center text-xs text-slate-300 hover:border-slate-700 transition">
                    <div class="text-base mb-1">🎗️</div>
                    <div class="font-medium">Top Ribbon</div>
                  </button>
                  <button
                    type="button"
                    (click)="newAnnouncement.modalType = 'SlideInToast'"
                    [class.border-indigo-500]="newAnnouncement.modalType === 'SlideInToast'"
                    [class.bg-indigo-950_40]="newAnnouncement.modalType === 'SlideInToast'"
                    class="p-2 rounded-lg border border-slate-800 text-center text-xs text-slate-300 hover:border-slate-700 transition">
                    <div class="text-base mb-1">🍞</div>
                    <div class="font-medium">Slide-in Toast</div>
                  </button>
                </div>
              </div>

              <!-- Behavioral Targeting Rules -->
              <div class="border-t border-slate-800 pt-3 space-y-3">
                <h4 class="text-xs font-semibold uppercase tracking-wider text-slate-400">Behavioral Targeting</h4>
                <div class="grid grid-cols-2 gap-3 text-xs">
                  <div>
                    <label class="block text-slate-400 mb-1">Target Role</label>
                    <select
                      [(ngModel)]="newAnnouncement.targetRole"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-slate-200 focus:outline-none focus:border-indigo-500">
                      <option value="All">All Roles</option>
                      <option value="Admin">Admin</option>
                      <option value="Merchant">Merchant</option>
                      <option value="CourierDispatcher">Courier Dispatcher</option>
                    </select>
                  </div>
                  <div>
                    <label class="block text-slate-400 mb-1">Min Session Count</label>
                    <input
                      type="number"
                      min="1"
                      [(ngModel)]="newAnnouncement.minSessionCount"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-slate-200 focus:outline-none focus:border-indigo-500" />
                  </div>
                </div>

                <div>
                  <label class="block text-xs text-slate-400 mb-1">Target Route Regex</label>
                  <input
                    type="text"
                    [(ngModel)]="newAnnouncement.targetRouteRegex"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-slate-200 font-mono focus:outline-none focus:border-indigo-500"
                    placeholder=".* or ^/funnels/.*" />
                </div>
              </div>

              <button
                (click)="submitAnnouncement()"
                class="w-full py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition flex items-center justify-center gap-1.5">
                <span>📢</span> Publish Announcement
              </button>
            </div>

            <!-- Right Panel: Real-Time Live Render Simulation -->
            <div class="lg:col-span-7 space-y-4">
              <h3 class="text-sm font-semibold uppercase tracking-wider text-slate-400">Live Appearance Preview</h3>

              <div class="relative bg-slate-950 border border-slate-800 rounded-2xl min-h-[460px] p-6 flex flex-col justify-between overflow-hidden shadow-inner">
                <!-- Top Ribbon Preview -->
                @if (newAnnouncement.modalType === 'FloatingBanner') {
                  <div class="w-full bg-gradient-to-r from-indigo-900 to-purple-900 border border-indigo-500/40 p-3 rounded-xl flex items-center justify-between text-xs text-white shadow-lg animate-fade-in">
                    <div class="flex items-center gap-2">
                      <span class="text-base">✨</span>
                      <div>
                        <strong class="font-bold mr-1">{{ newAnnouncement.title }}</strong>
                        <span class="text-slate-300">{{ newAnnouncement.bodyText }}</span>
                      </div>
                    </div>
                    <div class="flex items-center gap-2">
                      @if (newAnnouncement.ctaText) {
                        <button (click)="simulateAnnouncementCta()" class="px-2.5 py-1 bg-white text-slate-950 rounded font-semibold text-xs hover:bg-slate-200 transition">
                          {{ newAnnouncement.ctaText }}
                        </button>
                      }
                      <button (click)="simulateAnnouncementDismiss()" class="text-slate-400 hover:text-white px-1">&times;</button>
                    </div>
                  </div>
                }

                <!-- Mock Dashboard Underneath -->
                <div class="my-auto opacity-30 pointer-events-none p-6 text-center space-y-3">
                  <div class="h-6 w-1/3 bg-slate-800 rounded mx-auto"></div>
                  <div class="h-24 w-full bg-slate-900 rounded-xl border border-slate-800"></div>
                  <div class="grid grid-cols-3 gap-4">
                    <div class="h-16 bg-slate-900 rounded-lg"></div>
                    <div class="h-16 bg-slate-900 rounded-lg"></div>
                    <div class="h-16 bg-slate-900 rounded-lg"></div>
                  </div>
                </div>

                <!-- Centered Modal Preview -->
                @if (newAnnouncement.modalType === 'CenteredModal') {
                  <div class="absolute inset-0 bg-slate-950/70 backdrop-blur-sm flex items-center justify-center p-6 z-20">
                    <div class="max-w-md w-full bg-slate-900 border border-slate-700/60 rounded-2xl p-6 shadow-2xl space-y-4 animate-scale-up">
                      <div class="flex items-start justify-between">
                        <h4 class="text-base font-bold text-white">{{ newAnnouncement.title }}</h4>
                        <button (click)="simulateAnnouncementDismiss()" class="text-slate-400 hover:text-white text-base">&times;</button>
                      </div>
                      <p class="text-xs text-slate-300 leading-relaxed">{{ newAnnouncement.bodyText }}</p>
                      <div class="flex items-center justify-end gap-2 pt-2">
                        <button (click)="simulateAnnouncementDismiss()" class="px-3 py-1.5 text-xs text-slate-400 hover:text-white transition">
                          Dismiss
                        </button>
                        @if (newAnnouncement.ctaText) {
                          <button (click)="simulateAnnouncementCta()" class="px-4 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition">
                            {{ newAnnouncement.ctaText }}
                          </button>
                        }
                      </div>
                    </div>
                  </div>
                }

                <!-- Slide-In Toast Preview -->
                @if (newAnnouncement.modalType === 'SlideInToast') {
                  <div class="w-80 ml-auto bg-slate-900 border border-indigo-500/50 rounded-xl p-4 shadow-xl space-y-2 animate-slide-left z-20">
                    <div class="flex items-start justify-between gap-1">
                      <div class="font-bold text-xs text-white flex items-center gap-1.5">
                        <span>🔔</span> {{ newAnnouncement.title }}
                      </div>
                      <button (click)="simulateAnnouncementDismiss()" class="text-slate-400 hover:text-white text-xs">&times;</button>
                    </div>
                    <p class="text-[11px] text-slate-300">{{ newAnnouncement.bodyText }}</p>
                    @if (newAnnouncement.ctaText) {
                      <button (click)="simulateAnnouncementCta()" class="mt-2 w-full py-1 bg-indigo-600 hover:bg-indigo-500 text-white rounded text-xs font-semibold transition">
                        {{ newAnnouncement.ctaText }}
                      </button>
                    }
                  </div>
                }

                <!-- Interactive Toast Feedback Banner -->
                @if (simulationFeedback()) {
                  <div class="p-2.5 rounded-lg bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 text-xs text-center font-mono">
                    {{ simulationFeedback() }}
                  </div>
                }
              </div>
            </div>
          </div>
        }

        <!-- TAB 4: ONBOARDING FUNNEL ANALYTICS -->
        @if (activeTab() === 'analytics') {
          <div class="space-y-6">
            <!-- Top KPI Metric Cards -->
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
              <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
                <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Active Tours</span>
                <div class="text-2xl font-bold text-white mt-1">{{ metrics()?.activeToursCount || 0 }}</div>
              </div>
              <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
                <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Active Announcements</span>
                <div class="text-2xl font-bold text-white mt-1">{{ metrics()?.activeAnnouncementsCount || 0 }}</div>
              </div>
              <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
                <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Total Starts</span>
                <div class="text-2xl font-bold text-white mt-1">{{ metrics()?.totalTourStarts || 0 }}</div>
              </div>
              <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
                <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Total Completions</span>
                <div class="text-2xl font-bold text-emerald-400 mt-1">{{ metrics()?.totalTourCompletions || 0 }}</div>
              </div>
              <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
                <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Overall Completion Rate</span>
                <div class="text-2xl font-bold text-indigo-400 mt-1">{{ metrics()?.overallTourCompletionRate || 0 }}%</div>
              </div>
            </div>

            <!-- Tour Funnels Drop-Off Progression -->
            <div class="bg-slate-900 border border-slate-800 rounded-2xl p-6 space-y-6">
              <h3 class="text-base font-bold text-white flex items-center gap-2">
                <span>📉</span> Step Drop-Off Progression Funnels
              </h3>

              @for (funnel of metrics()?.tourFunnels || []; track funnel.tourId) {
                <div class="p-5 bg-slate-950/60 border border-slate-800 rounded-xl space-y-4">
                  <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-800/80 pb-3">
                    <div>
                      <h4 class="text-sm font-bold text-white">{{ funnel.tourTitle }}</h4>
                      <span class="text-xs text-slate-400">
                        {{ funnel.totalStarted }} Started &bull; {{ funnel.totalCompleted }} Finished &bull;
                        <strong class="text-emerald-400">{{ funnel.completionRatePercent }}% Completion</strong>
                      </span>
                    </div>
                  </div>

                  <!-- Step Bars -->
                  <div class="space-y-3">
                    @for (step of funnel.stepDropoffs; track step.stepOrder) {
                      <div class="space-y-1">
                        <div class="flex items-center justify-between text-xs">
                          <span class="text-slate-300 font-medium">Step {{ step.stepOrder }}: {{ step.stepTitle }}</span>
                          <span class="font-mono text-slate-400">
                            {{ step.usersReached }} users ({{ step.retentionRatePercent }}% retained) &bull;
                            <span class="text-rose-400">{{ step.dropoffRatePercent }}% drop-off</span>
                          </span>
                        </div>
                        <div class="h-3 w-full bg-slate-900 rounded-full overflow-hidden flex">
                          <div
                            class="bg-gradient-to-r from-indigo-500 to-purple-500 rounded-full transition-all duration-500"
                            [style.width.%]="step.retentionRatePercent"></div>
                        </div>
                      </div>
                    }
                  </div>
                </div>
              }
            </div>

            <!-- Announcement Modal Engagement Table -->
            <div class="bg-slate-900 border border-slate-800 rounded-2xl p-6 space-y-4">
              <h3 class="text-base font-bold text-white flex items-center gap-2">
                <span>📢</span> Behavioral Announcement Engagement & CTR
              </h3>

              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs">
                  <thead class="text-slate-400 border-b border-slate-800">
                    <tr>
                      <th class="py-2.5 px-3">Title</th>
                      <th class="py-2.5 px-3">Format</th>
                      <th class="py-2.5 px-3">Impressions</th>
                      <th class="py-2.5 px-3">Dismissals</th>
                      <th class="py-2.5 px-3">CTA Clicks</th>
                      <th class="py-2.5 px-3">Click-Through Rate (CTR)</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 text-slate-300">
                    @for (ann of metrics()?.announcementMetrics || []; track ann.announcementId) {
                      <tr class="hover:bg-slate-800/40 transition">
                        <td class="py-2.5 px-3 font-medium text-white">{{ ann.title }}</td>
                        <td class="py-2.5 px-3">
                          <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-800 text-slate-300 border border-slate-700">
                            {{ ann.modalType }}
                          </span>
                        </td>
                        <td class="py-2.5 px-3 font-mono">{{ ann.impressions }}</td>
                        <td class="py-2.5 px-3 font-mono text-slate-400">{{ ann.dismissals }}</td>
                        <td class="py-2.5 px-3 font-mono text-emerald-400 font-semibold">{{ ann.ctaClicks }}</td>
                        <td class="py-2.5 px-3 font-mono font-bold text-indigo-400">{{ ann.ctrPercent }}%</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      height: 100%;
    }
  `]
})
export class InAppTourStudioComponent implements OnInit {
  private api = inject(ApiService);

  public activeTab = signal<'builder' | 'preview' | 'announcements' | 'analytics'>('builder');
  public tours = signal<ProductTourDto[]>([]);
  public announcements = signal<AnnouncementModalDto[]>([]);
  public metrics = signal<OnboardingMetricsSummaryDto | null>(null);
  public selectedTour = signal<ProductTourDto | null>(null);

  // Preview spotlight state
  public previewCurrentStepIndex = signal<number>(0);
  public simulationFeedback = signal<string>('');

  // Announcement designer model
  public newAnnouncement = {
    title: '🚀 Launch WhatsApp Autopilot Today',
    bodyText: 'Enable conversational lead capture with zero wait time. Available on all plans.',
    ctaText: 'Pair Device Now',
    ctaUrl: '/channels/whatsapp-pairing',
    modalType: 'CenteredModal' as AnnouncementModalType,
    targetRole: 'All',
    targetRouteRegex: '.*',
    minSessionCount: 1,
    priority: 100,
    isActive: true
  };

  public currentPreviewSteps = computed(() => {
    return this.selectedTour()?.steps || [];
  });

  public currentPreviewStep = computed(() => {
    const steps = this.currentPreviewSteps();
    const idx = this.previewCurrentStepIndex();
    return steps.length > idx ? steps[idx] : null;
  });

  ngOnInit(): void {
    this.loadTours();
    this.loadAnnouncements();
    this.loadMetrics();
  }

  public loadTours(): void {
    this.api.getOnboardingTours().subscribe({
      next: (data) => {
        this.tours.set(data);
        if (data.length > 0 && !this.selectedTour()) {
          this.selectedTour.set(data[0]);
        }
      },
      error: (err) => console.error('Failed to load tours', err)
    });
  }

  public loadAnnouncements(): void {
    this.api.getOnboardingAnnouncements().subscribe({
      next: (data) => this.announcements.set(data),
      error: (err) => console.error('Failed to load announcements', err)
    });
  }

  public loadMetrics(): void {
    this.api.getOnboardingMetrics().subscribe({
      next: (data) => this.metrics.set(data),
      error: (err) => console.error('Failed to load onboarding metrics', err)
    });
  }

  public selectTour(tour: ProductTourDto): void {
    this.selectedTour.set(JSON.parse(JSON.stringify(tour)));
    this.previewCurrentStepIndex.set(0);
  }

  public startNewTour(): void {
    const fresh: ProductTourDto = {
      id: '',
      tenantId: '',
      title: 'New Guided Product Tour',
      description: 'Step-by-step walkthrough for users',
      targetRoute: '/funnels/canvas',
      targetRole: 'Merchant',
      isActive: true,
      createdAtUtc: new Date().toISOString(),
      steps: [
        {
          stepOrder: 1,
          targetSelector: '[data-tour-step="node-palette"]',
          title: 'Select Component Node',
          content: 'Drag your first node into the canvas.',
          placement: 'Right',
          backdropHighlight: true,
          requireInteraction: false
        }
      ]
    };
    this.selectedTour.set(fresh);
  }

  public addStepToCurrentTour(): void {
    const tour = this.selectedTour();
    if (!tour) return;

    const nextOrder = tour.steps.length + 1;
    tour.steps.push({
      stepOrder: nextOrder,
      targetSelector: `[data-tour-step="step-${nextOrder}"]`,
      title: `Step ${nextOrder}`,
      content: 'Explain feature interaction here...',
      placement: 'Bottom',
      backdropHighlight: true,
      requireInteraction: false
    });
  }

  public removeStep(index: number): void {
    const tour = this.selectedTour();
    if (!tour) return;
    tour.steps.splice(index, 1);
    tour.steps.forEach((s, idx) => (s.stepOrder = idx + 1));
  }

  public moveStep(index: number, delta: number): void {
    const tour = this.selectedTour();
    if (!tour) return;
    const targetIdx = index + delta;
    if (targetIdx < 0 || targetIdx >= tour.steps.length) return;

    const temp = tour.steps[index];
    tour.steps[index] = tour.steps[targetIdx];
    tour.steps[targetIdx] = temp;
    tour.steps.forEach((s, idx) => (s.stepOrder = idx + 1));
  }

  public saveTourSteps(): void {
    const tour = this.selectedTour();
    if (!tour) return;

    if (!tour.id) {
      // Create new tour
      this.api
        .createOnboardingTour({
          title: tour.title,
          description: tour.description,
          targetRoute: tour.targetRoute,
          targetRole: tour.targetRole,
          isActive: tour.isActive,
          steps: tour.steps
        })
        .subscribe({
          next: (created) => {
            this.selectedTour.set(created);
            this.loadTours();
            this.loadMetrics();
          }
        });
    } else {
      // Update steps
      this.api.setOnboardingTourSteps(tour.id, tour.steps).subscribe({
        next: () => {
          this.loadTours();
          this.loadMetrics();
        }
      });
    }
  }

  public previewSelectedTour(): void {
    this.previewCurrentStepIndex.set(0);
    this.activeTab.set('preview');
  }

  public onPreviewTourChange(tourId: string): void {
    const match = this.tours().find((t) => t.id === tourId);
    if (match) {
      this.selectedTour.set(match);
      this.previewCurrentStepIndex.set(0);
    }
  }

  public restartPreview(): void {
    this.previewCurrentStepIndex.set(0);
  }

  public nextPreviewStep(): void {
    if (this.previewCurrentStepIndex() < this.currentPreviewSteps().length - 1) {
      this.previewCurrentStepIndex.update((i) => i + 1);
    }
  }

  public prevPreviewStep(): void {
    if (this.previewCurrentStepIndex() > 0) {
      this.previewCurrentStepIndex.update((i) => i - 1);
    }
  }

  public completePreviewTour(): void {
    const tour = this.selectedTour();
    if (tour?.id) {
      this.api
        .recordTourTelemetry(tour.id, {
          userId: 'test_preview_user',
          action: 'completed',
          stepOrder: this.currentPreviewSteps().length
        })
        .subscribe(() => this.loadMetrics());
    }
    this.previewCurrentStepIndex.set(0);
    this.activeTab.set('analytics');
  }

  public skipPreviewTour(): void {
    this.previewCurrentStepIndex.set(0);
  }

  public isTargetActive(index: number): boolean {
    const currentOrder = this.currentPreviewStep()?.stepOrder;
    return currentOrder === index;
  }

  public handleTargetClick(index: number): void {
    const step = this.currentPreviewStep();
    if (step && step.stepOrder === index) {
      if (this.previewCurrentStepIndex() < this.currentPreviewSteps().length - 1) {
        this.nextPreviewStep();
      } else {
        this.completePreviewTour();
      }
    }
  }

  public getTooltipPositionStyle(): { [key: string]: string } {
    const step = this.currentPreviewStep();
    if (!step) return { top: '50%', left: '50%', transform: 'translate(-50%, -50%)' };

    switch (step.placement) {
      case 'Right':
        return { top: '35%', left: '36%' };
      case 'Bottom':
        return { top: '55%', left: '38%' };
      case 'Left':
        return { top: '35%', right: '36%' };
      case 'Top':
        return { top: '15%', left: '38%' };
      default:
        return { bottom: '20px', right: '20px' };
    }
  }

  public submitAnnouncement(): void {
    this.api.createOnboardingAnnouncement(this.newAnnouncement).subscribe({
      next: (ann) => {
        this.announcements.update((list) => [ann, ...list]);
        this.simulationFeedback.set(`Announcement "${ann.title}" published!`);
        this.loadMetrics();
        setTimeout(() => this.simulationFeedback.set(''), 3000);
      }
    });
  }

  public simulateAnnouncementDismiss(): void {
    this.simulationFeedback.set('Announcement dismissed. Dismissal tracked in user state.');
    setTimeout(() => this.simulationFeedback.set(''), 3000);
  }

  public simulateAnnouncementCta(): void {
    this.simulationFeedback.set(`CTA Clicked & Redirecting to: ${this.newAnnouncement.ctaUrl}`);
    setTimeout(() => this.simulationFeedback.set(''), 3000);
  }
}
