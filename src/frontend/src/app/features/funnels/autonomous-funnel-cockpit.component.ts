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

                <!-- Git-Native Autonomous PR Card -->
                <div class="mt-4 p-4 bg-slate-900 border border-slate-800 rounded-xl flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
                  <div class="flex items-center gap-3">
                    <span class="text-lg text-violet-400">🔀</span>
                    <div>
                      <div class="text-xs font-semibold text-white flex items-center gap-2">
                        <span>Git-Native Pull Request Auto-Created</span>
                        <span class="text-[10px] px-2 py-0.2 rounded bg-violet-500/20 text-violet-300 border border-violet-500/30">Ever-Works CI</span>
                      </div>
                      <div class="text-[11px] text-slate-400 font-mono mt-0.5">Branch: {{ gitHubBranchName() }}</div>
                    </div>
                  </div>
                  <a [href]="gitHubPrUrl()" target="_blank" class="px-3.5 py-1.5 bg-violet-600/20 hover:bg-violet-600/30 text-violet-300 border border-violet-500/30 rounded-lg text-xs font-semibold transition flex items-center gap-1.5">
                    <span>Review Pull Request</span>
                    <span>&rarr;</span>
                  </a>
                </div>

                <!-- Autonomous Funnel A/B Split-Testing Cockpit Card -->
                <div class="mt-4 p-5 bg-gradient-to-br from-slate-900 via-slate-900/90 to-slate-950 border border-violet-500/30 rounded-xl space-y-4 shadow-xl">
                  <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                    <div class="flex items-center gap-2.5">
                      <span class="p-1.5 rounded-lg bg-violet-500/20 text-violet-400 text-sm">⚖️</span>
                      <div>
                        <h3 class="text-xs font-bold text-white uppercase tracking-wider">Autonomous A/B Split-Testing Engine</h3>
                        <p class="text-[11px] text-slate-400">Bayesian Conversion Probability & Real-Time Traffic Routing</p>
                      </div>
                    </div>

                    <div class="flex items-center gap-2">
                      <span class="px-2 py-0.5 rounded-full text-[10px] font-mono font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                        {{ confidenceScore() }}% Statistical Confidence
                      </span>
                    </div>
                  </div>

                  <!-- Traffic Allocation Slider -->
                  <div class="space-y-1.5">
                    <div class="flex items-center justify-between text-xs">
                      <span class="text-slate-400">Traffic Distribution:</span>
                      <span class="font-mono font-bold text-violet-300">{{ 100 - trafficSplitRatio() }}% {{ variantAName() }} / {{ trafficSplitRatio() }}% {{ variantBName() }}</span>
                    </div>
                    <input
                      type="range"
                      min="10"
                      max="90"
                      step="5"
                      [ngModel]="trafficSplitRatio()"
                      (ngModelChange)="trafficSplitRatio.set($event)"
                      class="w-full accent-violet-500 bg-slate-800 h-1.5 rounded-lg cursor-pointer" />
                  </div>

                  <!-- Variant Comparison Grid -->
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                    <!-- Variant A Card -->
                    <div class="p-3.5 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-bold text-slate-300">Variant A (Control)</span>
                        <span class="text-[10px] text-slate-500 font-mono">{{ 100 - trafficSplitRatio() }}% traffic</span>
                      </div>
                      <div class="text-[11px] text-slate-400 truncate">Headline: Direct wholesale benefit statement</div>
                      <div class="grid grid-cols-3 gap-2 pt-1 border-t border-slate-800/80 text-center">
                        <div>
                          <div class="text-[10px] text-slate-500">Views</div>
                          <div class="text-sm font-mono font-bold text-slate-200">{{ viewsA() }}</div>
                        </div>
                        <div>
                          <div class="text-[10px] text-slate-500">Orders</div>
                          <div class="text-sm font-mono font-bold text-slate-200">{{ conversionsA() }}</div>
                        </div>
                        <div>
                          <div class="text-[10px] text-slate-500">Conv. %</div>
                          <div class="text-sm font-mono font-bold text-slate-300">{{ conversionRateA() | number:'1.1-1' }}%</div>
                        </div>
                      </div>
                    </div>

                    <!-- Variant B Card -->
                    <div class="p-3.5 rounded-xl bg-slate-950 border border-violet-500/40 relative space-y-2">
                      <span class="absolute -top-2 right-3 px-2 py-0.2 rounded-full text-[9px] font-bold uppercase tracking-wider bg-emerald-500 text-slate-950 shadow-sm">
                        Winner (+{{ conversionLift() | number:'1.0-0' }}% Lift)
                      </span>
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-bold text-violet-300">Variant B (Challenger)</span>
                        <span class="text-[10px] text-violet-400 font-mono">{{ trafficSplitRatio() }}% traffic</span>
                      </div>
                      <div class="text-[11px] text-slate-400 truncate">Headline: Urgency & Doorstep COD Guarantee</div>
                      <div class="grid grid-cols-3 gap-2 pt-1 border-t border-slate-800/80 text-center">
                        <div>
                          <div class="text-[10px] text-slate-500">Views</div>
                          <div class="text-sm font-mono font-bold text-violet-200">{{ viewsB() }}</div>
                        </div>
                        <div>
                          <div class="text-[10px] text-slate-500">Orders</div>
                          <div class="text-sm font-mono font-bold text-emerald-400">{{ conversionsB() }}</div>
                        </div>
                        <div>
                          <div class="text-[10px] text-slate-500">Conv. %</div>
                          <div class="text-sm font-mono font-bold text-emerald-400">{{ conversionRateB() | number:'1.1-1' }}%</div>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!-- Actions -->
                  <div class="flex flex-wrap items-center justify-between gap-3 pt-1">
                    <button
                      (click)="simulateVisitors()"
                      class="px-3.5 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium border border-slate-700 transition flex items-center gap-1.5">
                      <span>⚡ Simulate 50 Inbound Visitors</span>
                    </button>

                    <button
                      (click)="promoteWinner()"
                      [disabled]="winnerPromoted()"
                      class="px-4 py-1.5 rounded-lg bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-400 hover:to-teal-400 text-slate-950 text-xs font-bold shadow-md transition disabled:opacity-50">
                      {{ winnerPromoted() ? '✓ Variant B Promoted to 100%' : '🏆 Promote Variant B to 100%' }}
                    </button>
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
  gitHubPrUrl = signal<string>('https://github.com/sgrowing/storefront/pull/412');
  gitHubBranchName = signal<string>('funnel/enterprise-wholesale-demo');

  // A/B Split-Testing Signals & Metrics
  variantAName = signal<string>('Variant A (Direct Value)');
  variantBName = signal<string>('Variant B (Urgency & COD Proof)');
  trafficSplitRatio = signal<number>(50);
  viewsA = signal<number>(140);
  conversionsA = signal<number>(16);
  viewsB = signal<number>(145);
  conversionsB = signal<number>(28);
  confidenceScore = signal<number>(96.8);
  winnerPromoted = signal<boolean>(false);

  conversionRateA = computed(() => (this.viewsA() > 0 ? (this.conversionsA() / this.viewsA()) * 100 : 0));
  conversionRateB = computed(() => (this.viewsB() > 0 ? (this.conversionsB() / this.viewsB()) * 100 : 0));
  conversionLift = computed(() =>
    this.conversionRateA() > 0 ? ((this.conversionRateB() - this.conversionRateA()) / this.conversionRateA()) * 100 : 0
  );

  simulateVisitors() {
    const totalNew = 50;
    const split = this.trafficSplitRatio();
    const countB = Math.round((totalNew * split) / 100);
    const countA = totalNew - countB;

    this.viewsA.update(v => v + countA);
    this.viewsB.update(v => v + countB);

    // Simulate realistic conversion distributions
    const convA = Math.round(countA * 0.11);
    const convB = Math.round(countB * 0.19);

    this.conversionsA.update(c => c + convA);
    this.conversionsB.update(c => c + convB);
    this.confidenceScore.set(98.4);
  }

  promoteWinner() {
    this.trafficSplitRatio.set(100);
    this.winnerPromoted.set(true);
  }

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
