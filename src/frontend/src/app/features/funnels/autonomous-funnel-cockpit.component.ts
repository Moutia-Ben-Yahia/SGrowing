import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface StepDetail {
  number: number;
  name: string;
  status: 'Completed' | 'Running' | 'Pending';
  description: string;
  durationMs: number;
}

@Component({
  selector: 'app-autonomous-funnel-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Header -->
      <header class="border-b border-slate-800 bg-slate-900/60 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-50">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-slate-200">15-Step Idea-to-Site Autonomous Pipeline</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-violet-500/10 text-violet-400 border border-violet-500/20 font-medium">
              Ever-Works Workshop
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <app-language-switcher />
        </div>
      </header>

      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Hero Input Banner -->
        <div class="bg-gradient-to-r from-slate-900 via-slate-900/90 to-slate-950 border border-slate-800 rounded-2xl p-6 shadow-2xl space-y-4">
          <div class="max-w-2xl">
            <h1 class="text-xl font-black text-white">Generate High-Converting Commerce Funnels Autonomously</h1>
            <p class="text-xs text-slate-400 mt-1 leading-relaxed">
              From raw concept prompt to researched copy, Tailwind UI scaffolding, Lighthouse audit, and deployed CDN preview in 15 automated steps.
            </p>
          </div>

          <div class="flex flex-col md:flex-row gap-3 items-stretch md:items-center">
            <input [(ngModel)]="conceptPrompt" type="text" placeholder="e.g. Enterprise Multi-Warehouse B2B Wholesale Commerce with COD Remittance" class="flex-1 bg-slate-950 border border-slate-800 rounded-xl px-4 py-3 text-sm text-white focus:outline-none focus:border-violet-500 transition">
            
            <input [(ngModel)]="targetSku" type="text" placeholder="SKU-GROW-PRO" class="w-full md:w-44 bg-slate-950 border border-slate-800 rounded-xl px-4 py-3 text-sm text-white font-mono focus:outline-none focus:border-violet-500 transition">

            <button (click)="triggerPipeline()" [disabled]="isRunning()" class="px-6 py-3 bg-gradient-to-r from-violet-600 to-indigo-600 hover:from-violet-500 hover:to-indigo-500 disabled:opacity-50 text-white font-bold rounded-xl text-sm transition shadow-lg shadow-violet-950 flex items-center justify-center gap-2 whitespace-nowrap">
              @if (isRunning()) {
                <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                Executing Step {{ currentExecutingStep() }}/15...
              } @else {
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
                Trigger 15-Step Pipeline
              }
            </button>
          </div>
        </div>

        <!-- 2 Columns: 15-Step Progress Stepper (Left) & Live Preview + Audit (Right) -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- Left: 15-Step Stepper (5 cols) -->
          <div class="lg:col-span-5 space-y-4">
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 shadow-xl space-y-3">
              <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                <h2 class="text-sm font-bold text-white uppercase tracking-wider">15-Step Autonomous Pipeline</h2>
                <span class="text-xs font-mono font-bold text-violet-400">{{ overallProgress() }}% Complete</span>
              </div>

              <!-- Progress Bar -->
              <div class="w-full bg-slate-800 h-2 rounded-full overflow-hidden">
                <div class="bg-gradient-to-r from-violet-500 to-indigo-400 h-2 transition-all duration-300" [style.width.%]="overallProgress()"></div>
              </div>

              <!-- Steps List -->
              <div class="space-y-2 max-h-[580px] overflow-y-auto pr-1">
                @for (step of steps(); track step.number) {
                  <div class="p-3 rounded-lg border transition text-xs" [class.bg-slate-950]="step.status === 'Completed'" [class.border-slate-800]="step.status === 'Completed'" [class.bg-violet-950/20]="step.status === 'Running'" [class.border-violet-500/40]="step.status === 'Running'" [class.opacity-50]="step.status === 'Pending'">
                    <div class="flex items-center justify-between">
                      <div class="flex items-center gap-2">
                        <span class="w-5 h-5 rounded-full flex items-center justify-center font-bold text-[10px]" [class.bg-emerald-500]="step.status === 'Completed'" [class.text-slate-950]="step.status === 'Completed'" [class.bg-violet-500]="step.status === 'Running'" [class.text-white]="step.status === 'Running'" [class.bg-slate-800]="step.status === 'Pending'" [class.text-slate-400]="step.status === 'Pending'">
                          {{ step.number }}
                        </span>
                        <span class="font-semibold text-slate-200">{{ step.name }}</span>
                      </div>
                      <span class="font-mono text-[11px] text-slate-500">{{ step.durationMs }}ms</span>
                    </div>
                    <p class="text-[11px] text-slate-400 mt-1 pl-7 leading-normal">{{ step.description }}</p>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Right: Live Funnel Preview & Audit Scores (7 cols) -->
          <div class="lg:col-span-7 space-y-4">
            <!-- Lighthouse Audit Cards -->
            <div class="grid grid-cols-3 gap-3">
              <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 text-center">
                <div class="text-xs text-slate-400 uppercase font-semibold">Performance</div>
                <div class="text-2xl font-black text-emerald-400 mt-1">98%</div>
                <div class="text-[10px] text-slate-500">Zero layout shifts (CLS 0.0)</div>
              </div>

              <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 text-center">
                <div class="text-xs text-slate-400 uppercase font-semibold">SEO Readiness</div>
                <div class="text-2xl font-black text-emerald-400 mt-1">100%</div>
                <div class="text-[10px] text-slate-500">Schema.org JSON-LD</div>
              </div>

              <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 text-center">
                <div class="text-xs text-slate-400 uppercase font-semibold">Accessibility</div>
                <div class="text-2xl font-black text-emerald-400 mt-1">96%</div>
                <div class="text-[10px] text-slate-500">WCAG 2.2 AA Contrast</div>
              </div>
            </div>

            <!-- Preview Frame Container -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl overflow-hidden shadow-2xl">
              <div class="bg-slate-950 px-4 py-3 border-b border-slate-800 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="w-3 h-3 rounded-full bg-rose-500/80 inline-block"></span>
                  <span class="w-3 h-3 rounded-full bg-amber-500/80 inline-block"></span>
                  <span class="w-3 h-3 rounded-full bg-emerald-500/80 inline-block"></span>
                  <span class="text-xs text-slate-400 font-mono ml-2">{{ livePreviewUrl() }}</span>
                </div>
                <span class="text-xs font-semibold px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                  CDN Active
                </span>
              </div>

              <div class="p-6 bg-slate-950">
                <!-- Rendered Funnel Preview -->
                <div class="border border-slate-800/80 rounded-xl p-6 bg-slate-900 text-white space-y-6">
                  <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-xs font-semibold uppercase tracking-wider">
                    <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span> Live Wholesale Preview
                  </div>

                  <h2 class="text-2xl md:text-3xl font-black tracking-tight leading-tight">
                    Accelerate Your Wholesale Sales with Autonomous Commerce
                  </h2>

                  <p class="text-sm text-slate-300 leading-relaxed max-w-xl">
                    Automate your multi-warehouse stock allocations, COD logistics routing, and customer lifecycle engagement in one unified SaaS cockpit.
                  </p>

                  <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <div class="bg-slate-800/60 p-3.5 rounded-lg border border-slate-700/50">
                      <div class="text-emerald-400 font-bold text-lg">&lt; 15ms</div>
                      <div class="text-xs text-slate-400">Proximity Haversine Routing</div>
                    </div>
                    <div class="bg-slate-800/60 p-3.5 rounded-lg border border-slate-700/50">
                      <div class="text-cyan-400 font-bold text-lg">100% E2EE</div>
                      <div class="text-xs text-slate-400">Noise/Signal WhatsApp Gateway</div>
                    </div>
                    <div class="bg-slate-800/60 p-3.5 rounded-lg border border-slate-700/50">
                      <div class="text-violet-400 font-bold text-lg">True Margin</div>
                      <div class="text-xs text-slate-400">Dynamic Operational Costing</div>
                    </div>
                  </div>

                  <div class="flex items-center gap-3 pt-2">
                    <button class="px-5 py-2.5 bg-gradient-to-r from-emerald-500 to-teal-600 hover:from-emerald-400 hover:to-teal-500 text-slate-950 font-bold rounded-lg text-xs shadow-lg transition">
                      Claim Wholesale Access
                    </button>
                    <a [href]="livePreviewUrl()" target="_blank" class="px-4 py-2.5 bg-slate-800 hover:bg-slate-700 text-white font-medium rounded-lg text-xs border border-slate-700 transition">
                      Inspect Live URL &rarr;
                    </a>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class AutonomousFunnelCockpitComponent {
  private translationService = inject(TranslationService);

  conceptPrompt = 'Enterprise Multi-Warehouse B2B Wholesale Commerce with COD Remittance';
  targetSku = 'SKU-GROW-PRO';

  isRunning = signal<boolean>(false);
  currentExecutingStep = signal<number>(15);
  overallProgress = signal<number>(100);
  livePreviewUrl = signal<string>('https://preview.sgrowing.io/funnels/enterprise-wholesale-demo-a91b');

  steps = signal<StepDetail[]>([
    { number: 1, name: 'Concept Ingestion & Scoping', status: 'Completed', description: 'Parsed concept prompt into target commerce tier and customer demographics.', durationMs: 140 },
    { number: 2, name: 'Deep Market & Competitive Research', status: 'Completed', description: 'Crawled competitor landing pages and extracted key value drivers.', durationMs: 620 },
    { number: 3, name: 'Information Extraction & Grounding', status: 'Completed', description: 'Synthesized domain facts and verified B2B unit pricing models.', durationMs: 280 },
    { number: 4, name: 'Content Architecture & Taxonomy', status: 'Completed', description: 'Structured hero section, feature cards, and sticky checkout CTA.', durationMs: 190 },
    { number: 5, name: 'Editorial Copywriting & Value Prop', status: 'Completed', description: 'Generated high-conversion value propositions and social proof hooks.', durationMs: 350 },
    { number: 6, name: 'Codebase Scaffolding', status: 'Completed', description: 'Scaffolded Angular 22 component tree with Tailwind CSS v4 styling.', durationMs: 210 },
    { number: 7, name: 'Component Generation & Layout', status: 'Completed', description: 'Rendered responsive dark-mode layout with emerald accents.', durationMs: 430 },
    { number: 8, name: 'Type Checking & Static Analysis', status: 'Completed', description: 'Strict TypeScript compilation verified with zero type errors.', durationMs: 310 },
    { number: 9, name: 'Git Commit & Repo Synchronization', status: 'Completed', description: 'Atomic commit created with linked GitHub pull request.', durationMs: 180 },
    { number: 10, name: 'Automated Hosting Deployment', status: 'Completed', description: 'Edge CDN deployed with instant cache invalidation.', durationMs: 850 },
    { number: 11, name: 'Visual Screenshot Validation', status: 'Completed', description: 'Zero layout shift confirmed via headless browser viewport capture.', durationMs: 410 },
    { number: 12, name: 'Performance, SEO & A11y Audit', status: 'Completed', description: 'Lighthouse certified: Performance 98%, SEO 100%, A11y 96%.', durationMs: 520 },
    { number: 13, name: 'Custom Domain & SSL Provisioning', status: 'Completed', description: 'TLS 1.3 certificate signed and DNS record validated.', durationMs: 290 },
    { number: 14, name: 'Telemetry & Analytics Ingestion', status: 'Completed', description: 'PostHog analytics streams connected for click tracking.', durationMs: 160 },
    { number: 15, name: 'Recurring Cron Maintenance Scheduled', status: 'Completed', description: '6-hour drift detection daemon scheduled for automated copy updates.', durationMs: 110 }
  ]);

  triggerPipeline() {
    this.isRunning.set(true);
    this.overallProgress.set(10);
    this.currentExecutingStep.set(1);

    // Reset steps to pending
    this.steps.update(list => list.map(s => ({ ...s, status: 'Pending' })));

    let current = 1;
    const interval = setInterval(() => {
      this.currentExecutingStep.set(current);
      this.overallProgress.set(Math.min(100, Math.round((current / 15) * 100)));

      this.steps.update(list =>
        list.map(s => {
          if (s.number < current) return { ...s, status: 'Completed' };
          if (s.number === current) return { ...s, status: 'Running' };
          return { ...s, status: 'Pending' };
        })
      );

      current++;
      if (current > 15) {
        clearInterval(interval);
        this.steps.update(list => list.map(s => ({ ...s, status: 'Completed' })));
        this.isRunning.set(false);
        this.overallProgress.set(100);
      }
    }, 200);
  }
}
