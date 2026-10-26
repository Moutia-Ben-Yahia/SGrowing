import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface StageForecastRowUi {
  stageName: string;
  dealsCount: number;
  totalValue: number;
  probabilityPercent: number;
  weightedValue: number;
}

export interface CommissionStatementUi {
  id: string;
  salesRepName: string;
  dealsClosedCount: number;
  totalDealsValue: number;
  calculatedCommission: number;
  status: 'Draft' | 'Approved' | 'Paid';
  approvedAtUtc?: string;
  notes?: string;
}

@Component({
  selector: 'app-forecasting-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-emerald-500 to-teal-400 flex items-center justify-center font-bold text-white shadow-lg shadow-emerald-500/20">
              📈
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-emerald-400 font-medium">Sales Forecasting & Quotas</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Core Suites</span>
            <div class="space-y-1">
              <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📥 Omnichannel Inbox</span>
              </a>
              <a routerLink="/documents" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span>📜 Document Envelopes & Telemetry</span>
              </a>
              <a routerLink="/publications" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span>📰 Publication CMS & Newsletters</span>
              </a>
              <a routerLink="/forecasting" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-medium">
                <span>📈 Sales Forecasts & Quotas</span>
              </a>
              <a routerLink="/roadmap" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span>🗺️ Public Roadmap & Voting</span>
              </a>
            </div>
          </div>

          <!-- Feature Highlight Box -->
          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-emerald-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-emerald-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-emerald-400 animate-pulse"></span>
                PRD §4.13 Engine
              </span>
              <span class="text-[10px] bg-emerald-500/10 text-emerald-300 border border-emerald-500/20 px-1.5 py-0.5 rounded">Accelerated</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Stage probability-weighted pipeline projections, rep quota attainment tracking, and tiered commission approval workflows.
            </p>
          </div>
        </div>
      </aside>

      <!-- Main Stage -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <div>
            <h2 class="text-base font-semibold text-white">Sales Pipeline Forecasting & Commission Attainment</h2>
            <p class="text-xs text-slate-400">Track stage-weighted revenue realization, quarterly quotas, and automated rep payouts.</p>
          </div>
          <div class="flex items-center gap-3">
            <select class="bg-slate-900 border border-slate-800 text-xs text-white rounded-lg px-3 py-1.5 focus:outline-none focus:border-emerald-500">
              <option>Q1 2026 (Jan 1 - Mar 31)</option>
              <option>Q2 2026 (Apr 1 - Jun 30)</option>
              <option>Full Year 2026</option>
            </select>
          </div>
        </header>

        <!-- Body Dashboard -->
        <div class="flex-1 overflow-y-auto p-6 space-y-6">
          <!-- KPI Metrics Row -->
          <div class="grid grid-cols-4 gap-4">
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Target Quota</span>
              <div class="text-2xl font-bold text-white mt-1">$100,000</div>
              <span class="text-[11px] text-slate-400">Active period target</span>
            </div>
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Closed Won Revenue</span>
              <div class="text-2xl font-bold text-emerald-400 mt-1">$40,000</div>
              <div class="w-full bg-slate-950 rounded-full h-1.5 mt-2 overflow-hidden border border-slate-800">
                <div class="bg-emerald-400 h-full rounded-full" style="width: 40%"></div>
              </div>
              <span class="text-[11px] text-emerald-400/80 mt-1 block">40.0% quota attained</span>
            </div>
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Weighted Pipeline Forecast</span>
              <div class="text-2xl font-bold text-teal-400 mt-1">$76,000</div>
              <span class="text-[11px] text-teal-400/80">76.0% expected attainment</span>
            </div>
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Unweighted Pipeline Total</span>
              <div class="text-2xl font-bold text-cyan-400 mt-1">$110,000</div>
              <span class="text-[11px] text-cyan-400/80">4 active open stages</span>
            </div>
          </div>

          <!-- Stage Probability Breakdown Table -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden">
            <div class="p-4 border-b border-slate-800 flex items-center justify-between">
              <div>
                <h3 class="text-sm font-semibold text-white">Deal Stage Probability Realization</h3>
                <p class="text-xs text-slate-400">Deals are multiplied by stage probability weights to compute mathematically expected revenue</p>
              </div>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="bg-slate-950/60 text-slate-400 uppercase font-semibold text-[10px] border-b border-slate-800">
                  <tr>
                    <th class="p-3.5">Deal Stage</th>
                    <th class="p-3.5">Active Deals</th>
                    <th class="p-3.5">Unweighted Value</th>
                    <th class="p-3.5">Probability Weight</th>
                    <th class="p-3.5">Weighted Forecast</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/80">
                  @for (s of stageMetrics; track s.stageName) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <td class="p-3.5 font-medium text-white flex items-center gap-2">
                        <span class="h-2 w-2 rounded-full"
                          [class.bg-emerald-400]="s.stageName === 'Won'"
                          [class.bg-teal-400]="s.stageName === 'Proposal'"
                          [class.bg-blue-400]="s.stageName === 'Qualified'"
                          [class.bg-amber-400]="s.stageName === 'Prospecting'">
                        </span>
                        {{ s.stageName }}
                      </td>
                      <td class="p-3.5 font-mono">{{ s.dealsCount }}</td>
                      <td class="p-3.5 font-mono">\${{ s.totalValue.toLocaleString() }}</td>
                      <td class="p-3.5">
                        <span class="px-2 py-0.5 bg-slate-800 rounded text-slate-200 font-mono">{{ s.probabilityPercent }}%</span>
                      </td>
                      <td class="p-3.5 font-mono font-semibold text-emerald-400">
                        \${{ s.weightedValue.toLocaleString() }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <!-- Sales Commission Statements Approval Box -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden">
            <div class="p-4 border-b border-slate-800 flex items-center justify-between">
              <div>
                <h3 class="text-sm font-semibold text-white">Sales Commission Statements & Tiered Accelerators</h3>
                <p class="text-xs text-slate-400">Base Rate: 10% • Quota Threshold: 100% • Accelerated Surplus Rate: 20%</p>
              </div>
            </div>
            <div class="divide-y divide-slate-800/80">
              @for (stmt of statements(); track stmt.id) {
                <div class="p-4 flex items-center justify-between hover:bg-slate-800/30 transition">
                  <div class="space-y-1">
                    <div class="flex items-center gap-2">
                      <span class="text-sm font-medium text-white">{{ stmt.salesRepName }}</span>
                      <span
                        [class.bg-emerald-500-10]="stmt.status === 'Approved'"
                        [class.text-emerald-400]="stmt.status === 'Approved'"
                        [class.border-emerald-500-20]="stmt.status === 'Approved'"
                        [class.bg-amber-500-10]="stmt.status === 'Draft'"
                        [class.text-amber-400]="stmt.status === 'Draft'"
                        [class.border-amber-500-20]="stmt.status === 'Draft'"
                        class="text-[10px] px-2 py-0.5 rounded-full border">
                        {{ stmt.status }}
                      </span>
                      @if (stmt.totalDealsValue > 50000) {
                        <span class="text-[10px] bg-purple-500/10 text-purple-300 border border-purple-500/20 px-1.5 py-0.5 rounded">
                          🚀 Quota Exceeded (Accelerated 20%)
                        </span>
                      }
                    </div>
                    <div class="text-xs text-slate-400 flex items-center gap-3">
                      <span>{{ stmt.dealsClosedCount }} Deals Won</span>
                      <span>•</span>
                      <span>Total Realized Value: \${{ stmt.totalDealsValue.toLocaleString() }}</span>
                      <span>•</span>
                      <span>Commission: <strong class="text-emerald-400">\${{ stmt.calculatedCommission.toLocaleString() }}</strong></span>
                    </div>
                  </div>
                  <div>
                    @if (stmt.status === 'Draft') {
                      <button
                        (click)="approveStatement(stmt)"
                        class="px-3 py-1.5 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-emerald-500/20 transition">
                        ✓ Approve Statement
                      </button>
                    } @else {
                      <span class="text-xs text-emerald-400 font-medium">Approved by Sales Director</span>
                    }
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class ForecastingStudioComponent implements OnInit {
  stageMetrics: StageForecastRowUi[] = [
    { stageName: 'Won', dealsCount: 2, totalValue: 40000, probabilityPercent: 100, weightedValue: 40000 },
    { stageName: 'Proposal', dealsCount: 3, totalValue: 50000, probabilityPercent: 60, weightedValue: 30000 },
    { stageName: 'Qualified', dealsCount: 4, totalValue: 20000, probabilityPercent: 30, weightedValue: 6000 },
    { stageName: 'Prospecting', dealsCount: 5, totalValue: 15000, probabilityPercent: 10, weightedValue: 1500 }
  ];

  statements = signal<CommissionStatementUi[]>([
    {
      id: 'stmt-1',
      salesRepName: 'Alex Vance',
      dealsClosedCount: 2,
      totalDealsValue: 70000,
      calculatedCommission: 9000,
      status: 'Draft',
      notes: 'Exceeded $50k target quota by $20k'
    },
    {
      id: 'stmt-2',
      salesRepName: 'Jordan Belfort',
      dealsClosedCount: 1,
      totalDealsValue: 25000,
      calculatedCommission: 2500,
      status: 'Approved',
      approvedAtUtc: '2026-10-25T11:00:00Z',
      notes: 'Standard 10% base rate'
    }
  ]);

  ngOnInit() {}

  approveStatement(stmt: CommissionStatementUi) {
    stmt.status = 'Approved';
    stmt.approvedAtUtc = new Date().toISOString();
  }
}
