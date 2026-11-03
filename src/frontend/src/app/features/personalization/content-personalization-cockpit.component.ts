import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface FirmographicSegment {
  segmentId: string;
  name: string;
  targetIndustry: string;
  employeeRange: string;
  revenueTier: string;
  activeAccountsCount: number;
}

export interface PersonalizationRule {
  ruleId: string;
  ruleName: string;
  segmentId: string;
  targetUrlPattern: string;
  selectedHeroHeadline: string;
  callToActionText: string;
  conversionRatePct: number;
  sampleSize: number;
}

@Component({
  selector: 'app-content-personalization-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="content-personalization-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
            Dynamic Content Personalization Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Sub-50ms Edge Intent Content Injection & Multi-Armed Bandit Dynamic Experience Engine
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="evaluatePage()" class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white font-medium rounded-lg shadow-sm transition">
            Simulate Edge Evaluation
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Rules</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ activeRulesCount() }}</p>
          <span class="text-xs text-emerald-400">100% Edge Propagated</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Firmographic Segments</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">{{ segments().length }}</p>
          <span class="text-xs text-slate-400">Targeting 1,350+ Accounts</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Avg Conversion Lift</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">+{{ averageConversionLiftPct() }}%</p>
          <span class="text-xs text-slate-400">vs Unpersonalized Control</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">P99 Edge Latency</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">14ms</p>
          <span class="text-xs text-slate-400">Cloudflare Workers Mesh</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Segments Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Firmographic Intent Segments</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Segment Name</th>
                  <th class="pb-3 font-medium">Industry</th>
                  <th class="pb-3 font-medium">Employees</th>
                  <th class="pb-3 font-medium">Revenue</th>
                  <th class="pb-3 font-medium">Active Accounts</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let seg of segments()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-medium text-slate-200">{{ seg.name }}</td>
                  <td class="py-3 text-slate-400">{{ seg.targetIndustry }}</td>
                  <td class="py-3 text-slate-400">{{ seg.employeeRange }}</td>
                  <td class="py-3 text-indigo-300">{{ seg.revenueTier }}</td>
                  <td class="py-3 text-emerald-400 font-mono">{{ seg.activeAccountsCount }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Live Personalization Preview -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5 flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-2">Simulated Live Edge Experience</h2>
            <p class="text-xs text-slate-400 mb-4">Real-time tailored hero variant for inbound visitor:</p>
            <div class="bg-slate-950/70 border border-indigo-500/30 rounded-lg p-4 space-y-3">
              <span class="px-2 py-0.5 text-xs bg-indigo-500/20 text-indigo-300 rounded font-mono">Matched: Enterprise FinTech</span>
              <h3 class="text-base font-bold text-white">{{ selectedHeadline() }}</h3>
              <p class="text-xs text-slate-300">Automated firmographic match for Tier-1 Financial Institutions with SOC2 compliance emphasis.</p>
              <div class="pt-2 flex items-center justify-between">
                <span class="text-xs text-emerald-400 font-medium">Proof Logos: Stripe, JPMorgan, Adyen</span>
                <button class="px-3 py-1.5 bg-emerald-600 text-white text-xs font-semibold rounded hover:bg-emerald-500">
                  {{ selectedCta() }}
                </button>
              </div>
            </div>
          </div>
          <div class="mt-4 pt-3 border-t border-slate-700/60 flex items-center justify-between text-xs text-slate-400">
            <span>Bandit Weight: 0.985</span>
            <span class="text-emerald-400">Edge Cache: Hit (TTL 300s)</span>
          </div>
        </section>
      </div>
    </div>
  `
})
export class ContentPersonalizationCockpitComponent {
  segments = signal<FirmographicSegment[]>([
    { segmentId: 'SEG-ENT-01', name: 'Global Enterprise Fintech', targetIndustry: 'Financial Services', employeeRange: '1,000-10,000+', revenueTier: '$100M+', activeAccountsCount: 420 },
    { segmentId: 'SEG-HLT-02', name: 'HealthTech & Life Sciences', targetIndustry: 'Healthcare', employeeRange: '250-1,000', revenueTier: '$25M-$100M', activeAccountsCount: 280 },
    { segmentId: 'SEG-SAS-03', name: 'Hypergrowth B2B SaaS', targetIndustry: 'Technology', employeeRange: '50-250', revenueTier: '$10M-$50M', activeAccountsCount: 650 }
  ]);

  activeRulesCount = signal(3);
  averageConversionLiftPct = signal(16.5);
  selectedHeadline = signal('Sovereign Multi-Jurisdiction Growth OS for Global Financial Institutions');
  selectedCta = signal('Schedule Executive Architecture Review');

  evaluatePage() {
    this.selectedHeadline.set('Zero-Trust Patient Acquisition & Omnichannel Life Sciences Engine');
    this.selectedCta.set('Explore HIPAA Compliant Blueprints');
  }
}
