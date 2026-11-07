import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type AutonomyReadinessLevel = 'Read' | 'Draft' | 'Approve' | 'Execute';
export type RegressionTrend = 'Improving' | 'Stable' | 'Regressing';

export interface AgentRadarScorecard {
  agentRole: string;
  autonomyLevel: AutonomyReadinessLevel;
  compositeScore: number;
  taskSuccessRate: number;
  toolPrecisionRate: number;
  approvalRate: number;
  hallucinationIndex: number;
  costPerTaskSar: number;
  averageLatencyMs: number;
  totalTasksEvaluated: number;
  trend: RegressionTrend;
  recommendation: string;
}

export interface RejectionReasonCluster {
  reason: string;
  count: number;
}

const INITIAL_SCORECARDS: AgentRadarScorecard[] = [
  {
    agentRole: 'ResearchAgent',
    autonomyLevel: 'Execute',
    compositeScore: 97.5,
    taskSuccessRate: 98.0,
    toolPrecisionRate: 97.0,
    approvalRate: 97.0,
    hallucinationIndex: 0.02,
    costPerTaskSar: 4.20,
    averageLatencyMs: 820.0,
    totalTasksEvaluated: 2400,
    trend: 'Improving',
    recommendation: 'Autonomous execution verified. Hallucination rate is at an ultra-low 2.0% with strong multi-source grounding.'
  },
  {
    agentRole: 'SeoAgent',
    autonomyLevel: 'Execute',
    compositeScore: 95.5,
    taskSuccessRate: 96.0,
    toolPrecisionRate: 95.0,
    approvalRate: 95.0,
    hallucinationIndex: 0.04,
    costPerTaskSar: 6.50,
    averageLatencyMs: 1150.0,
    totalTasksEvaluated: 1850,
    trend: 'Stable',
    recommendation: 'Maintains high programmatic SEO precision. Autonomous keyword clustering and canonical schema generation.'
  },
  {
    agentRole: 'AdvertisingAgent',
    autonomyLevel: 'Approve',
    compositeScore: 88.0,
    taskSuccessRate: 92.0,
    toolPrecisionRate: 84.0,
    approvalRate: 84.0,
    hallucinationIndex: 0.08,
    costPerTaskSar: 12.00,
    averageLatencyMs: 1420.0,
    totalTasksEvaluated: 920,
    trend: 'Improving',
    recommendation: 'Requires human approval before ad spend reallocation. Approval rate currently at 84.0% (target > 95.0% for Execute).'
  },
  {
    agentRole: 'ContentAgent',
    autonomyLevel: 'Approve',
    compositeScore: 89.8,
    taskSuccessRate: 94.0,
    toolPrecisionRate: 85.0,
    approvalRate: 86.0,
    hallucinationIndex: 0.05,
    costPerTaskSar: 8.40,
    averageLatencyMs: 980.0,
    totalTasksEvaluated: 1540,
    trend: 'Stable',
    recommendation: 'Supervised mode active. Creative briefs pass review; Arabic cultural dialect validation required prior to publishing.'
  },
  {
    agentRole: 'ApprovalGatekeeper',
    autonomyLevel: 'Execute',
    compositeScore: 100.0,
    taskSuccessRate: 100.0,
    toolPrecisionRate: 100.0,
    approvalRate: 100.0,
    hallucinationIndex: 0.00,
    costPerTaskSar: 1.10,
    averageLatencyMs: 190.0,
    totalTasksEvaluated: 4890,
    trend: 'Stable',
    recommendation: 'Deterministic governance gatekeeper running with 0% hallucination and zero tolerance on budget policy infractions.'
  }
];

const INITIAL_REJECTIONS: RejectionReasonCluster[] = [
  { reason: 'Unverified pricing claim against ERP price list', count: 42 },
  { reason: 'Tool schema validation failure on WhatsApp payload', count: 28 },
  { reason: 'Exceeded tone guidelines (over-promising ROI in Saudi market)', count: 19 },
  { reason: 'Hallucinated citation or non-existent discount policy', count: 14 },
  { reason: 'Safety filter flagged competitor mention', count: 8 }
];

@Component({
  selector: 'app-ai-agent-evaluation-radar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 p-6 lg:p-8 space-y-8" dir="ltr">
      <!-- Header -->
      <header class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-slate-800 pb-6">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2.5 bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 rounded-xl text-2xl shadow-inner">
              🎯
            </span>
            <div>
              <h1 class="text-2xl lg:text-3xl font-extrabold text-white tracking-tight">
                AI Agent Evaluation & Supervised Autonomy Radar
              </h1>
              <p class="text-sm text-slate-400 mt-0.5">
                Multi-axis benchmark telemetry, precision scorecards, hallucination tracking & promotion gating (PRD 6.6 & 14)
              </p>
            </div>
          </div>
        </div>

        <!-- Vitals Pills -->
        <div class="flex flex-wrap items-center gap-2">
          <span class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
            <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            Evaluation Mesh Active
          </span>
          <span class="inline-flex items-center px-3 py-1.5 rounded-full text-xs font-medium bg-indigo-500/10 text-indigo-300 border border-indigo-500/20">
            Thread-Safe Registry
          </span>
          <span class="inline-flex items-center px-3 py-1.5 rounded-full text-xs font-medium bg-slate-800 text-slate-300 border border-slate-700">
            5 Fleet Roles
          </span>
        </div>
      </header>

      <!-- Top KPI Cards -->
      <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <!-- Global Quality Score -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-lg relative overflow-hidden group hover:border-slate-700 transition">
          <div class="flex items-center justify-between text-slate-400 text-xs font-semibold uppercase tracking-wider">
            <span>Global Quality Score</span>
            <span class="text-emerald-400 text-lg">⚡</span>
          </div>
          <div class="mt-3 flex items-baseline gap-2">
            <span class="text-3xl font-black text-white tracking-tight">{{ globalScore() }}%</span>
            <span class="text-xs text-emerald-400 font-medium">Fleet Weighted</span>
          </div>
          <div class="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
            <div class="bg-gradient-to-r from-emerald-500 to-indigo-500 h-1.5 rounded-full transition-all duration-500"
                 [style.width.%]="globalScore()"></div>
          </div>
        </div>

        <!-- Fully Autonomous Agents -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-lg relative overflow-hidden group hover:border-slate-700 transition">
          <div class="flex items-center justify-between text-slate-400 text-xs font-semibold uppercase tracking-wider">
            <span>Fully Autonomous Agents</span>
            <span class="text-indigo-400 text-lg">🚀</span>
          </div>
          <div class="mt-3 flex items-baseline gap-2">
            <span class="text-3xl font-black text-indigo-400 tracking-tight">{{ fullyAutonomousCount() }}</span>
            <span class="text-xs text-slate-400 font-normal">Execute Level</span>
          </div>
          <p class="text-xs text-slate-500 mt-2">Zero human intervention required</p>
        </div>

        <!-- Supervised Agents -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-lg relative overflow-hidden group hover:border-slate-700 transition">
          <div class="flex items-center justify-between text-slate-400 text-xs font-semibold uppercase tracking-wider">
            <span>Supervised Agents</span>
            <span class="text-amber-400 text-lg">🛡️</span>
          </div>
          <div class="mt-3 flex items-baseline gap-2">
            <span class="text-3xl font-black text-amber-400 tracking-tight">{{ supervisedCount() }}</span>
            <span class="text-xs text-slate-400 font-normal">Approve / Draft</span>
          </div>
          <p class="text-xs text-slate-500 mt-2">Human sign-off gates enforced</p>
        </div>

        <!-- Average Cost / Task -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-lg relative overflow-hidden group hover:border-slate-700 transition">
          <div class="flex items-center justify-between text-slate-400 text-xs font-semibold uppercase tracking-wider">
            <span>Average Cost / Task</span>
            <span class="text-cyan-400 text-lg">💰</span>
          </div>
          <div class="mt-3 flex items-baseline gap-2">
            <span class="text-3xl font-black text-cyan-400 tracking-tight">{{ averageCostPerTaskSar() }} SAR</span>
            <span class="text-xs text-slate-400 font-normal">per execution</span>
          </div>
          <p class="text-xs text-slate-500 mt-2">Across {{ totalTasksEvaluated() | number }} evaluated tasks</p>
        </div>
      </section>

      <!-- Autonomy Progression Stage Meter -->
      <section class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
          <div>
            <h2 class="text-base font-bold text-white flex items-center gap-2">
              <span>📈</span> Autonomy Progression Stage Meter
            </h2>
            <p class="text-xs text-slate-400">
              Visual progression gating from observation (Read) to deterministic execution (Execute)
            </p>
          </div>
          <span class="text-xs text-indigo-400 bg-indigo-500/10 px-3 py-1 rounded-full border border-indigo-500/20">
            Safety Gating: Composite &gt; 90% + Approval &gt; 95%
          </span>
        </div>

        <!-- Stage meter pipeline -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-3 pt-2">
          @for (stage of stages; track stage.key) {
            @let count = stageCounts()[stage.key] || 0;
            @let isExecute = stage.key === 'Execute';
            @let isApprove = stage.key === 'Approve';
            <div class="rounded-xl border p-4 transition-all"
                 [class.border-emerald-500/40]="isExecute && count > 0"
                 [class.bg-emerald-950/20]="isExecute && count > 0"
                 [class.border-amber-500/40]="isApprove && count > 0"
                 [class.bg-amber-950/20]="isApprove && count > 0"
                 [class.border-slate-800]="count === 0"
                 [class.bg-slate-900/60]="count === 0">
              <div class="flex items-center justify-between mb-2">
                <span class="text-xs font-mono uppercase tracking-wider text-slate-400">{{ stage.label }}</span>
                <span class="w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold"
                      [class.bg-emerald-500/20]="isExecute && count > 0"
                      [class.text-emerald-400]="isExecute && count > 0"
                      [class.bg-amber-500/20]="isApprove && count > 0"
                      [class.text-amber-400]="isApprove && count > 0"
                      [class.bg-slate-800]="count === 0"
                      [class.text-slate-500]="count === 0">
                  {{ count }}
                </span>
              </div>
              <div class="text-sm font-semibold text-white">{{ stage.title }}</div>
              <p class="text-xs text-slate-400 mt-1 leading-snug">{{ stage.description }}</p>
            </div>
          }
        </div>
      </section>

      <!-- Main Layout: Leaderboards + Quick Simulation + Rejection Reasons -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-8">
        <!-- Left: Agent Leaderboard Cards (8 cols) -->
        <div class="lg:col-span-8 space-y-4">
          <div class="flex items-center justify-between pb-1">
            <h2 class="text-lg font-bold text-white flex items-center gap-2">
              <span>🏆</span> AI Fleet Quality Leaderboard
            </h2>
            <div class="flex items-center gap-2 text-xs">
              <button (click)="filterLevel.set('ALL')"
                      class="px-2.5 py-1 rounded-lg border transition"
                      [class.bg-indigo-600]="filterLevel() === 'ALL'"
                      [class.border-indigo-500]="filterLevel() === 'ALL'"
                      [class.bg-slate-800]="filterLevel() !== 'ALL'"
                      [class.border-slate-700]="filterLevel() !== 'ALL'">
                All ({{ scorecards().length }})
              </button>
              <button (click)="filterLevel.set('Execute')"
                      class="px-2.5 py-1 rounded-lg border transition"
                      [class.bg-emerald-600]="filterLevel() === 'Execute'"
                      [class.border-emerald-500]="filterLevel() === 'Execute'"
                      [class.bg-slate-800]="filterLevel() !== 'Execute'"
                      [class.border-slate-700]="filterLevel() !== 'Execute'">
                Autonomous ({{ fullyAutonomousCount() }})
              </button>
              <button (click)="filterLevel.set('Approve')"
                      class="px-2.5 py-1 rounded-lg border transition"
                      [class.bg-amber-600]="filterLevel() === 'Approve'"
                      [class.border-amber-500]="filterLevel() === 'Approve'"
                      [class.bg-slate-800]="filterLevel() !== 'Approve'"
                      [class.border-slate-700]="filterLevel() !== 'Approve'">
                Supervised ({{ supervisedCount() }})
              </button>
            </div>
          </div>

          <div class="space-y-4">
            @for (agent of filteredScorecards(); track agent.agentRole) {
              <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 hover:border-slate-700 transition shadow-lg space-y-4">
                <!-- Card Header -->
                <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-slate-800/80 pb-4">
                  <div class="flex items-center gap-3">
                    <div class="w-10 h-10 rounded-xl flex items-center justify-center font-bold text-base"
                         [class.bg-indigo-500/20]="agent.autonomyLevel === 'Execute'"
                         [class.text-indigo-400]="agent.autonomyLevel === 'Execute'"
                         [class.bg-amber-500/20]="agent.autonomyLevel === 'Approve'"
                         [class.text-amber-400]="agent.autonomyLevel === 'Approve'">
                      {{ agent.agentRole.slice(0, 2).toUpperCase() }}
                    </div>
                    <div>
                      <h3 class="text-base font-bold text-white">{{ agent.agentRole }}</h3>
                      <div class="flex items-center gap-2 mt-0.5">
                        <span class="text-xs text-slate-400">{{ agent.totalTasksEvaluated | number }} tasks evaluated</span>
                        <span class="text-slate-600">•</span>
                        <!-- Trend Pill -->
                        @switch (agent.trend) {
                          @case ('Improving') {
                            <span class="inline-flex items-center gap-1 text-[11px] font-semibold text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded-full border border-emerald-500/20">
                              ▲ Improving
                            </span>
                          }
                          @case ('Stable') {
                            <span class="inline-flex items-center gap-1 text-[11px] font-semibold text-blue-400 bg-blue-500/10 px-2 py-0.5 rounded-full border border-blue-500/20">
                              ● Stable
                            </span>
                          }
                          @case ('Regressing') {
                            <span class="inline-flex items-center gap-1 text-[11px] font-semibold text-rose-400 bg-rose-500/10 px-2 py-0.5 rounded-full border border-rose-500/20">
                              ▼ Regressing
                            </span>
                          }
                        }
                      </div>
                    </div>
                  </div>

                  <!-- Right: Autonomy Level & Promotion Action -->
                  <div class="flex items-center gap-2">
                    <span class="text-xs px-3 py-1 rounded-full font-bold uppercase tracking-wider"
                          [class.bg-emerald-500/20]="agent.autonomyLevel === 'Execute'"
                          [class.text-emerald-400]="agent.autonomyLevel === 'Execute'"
                          [class.border]="true"
                          [class.border-emerald-500/30]="agent.autonomyLevel === 'Execute'"
                          [class.bg-amber-500/20]="agent.autonomyLevel === 'Approve'"
                          [class.text-amber-400]="agent.autonomyLevel === 'Approve'"
                          [class.border-amber-500/30]="agent.autonomyLevel === 'Approve'"
                          [class.bg-sky-500/20]="agent.autonomyLevel === 'Draft'"
                          [class.text-sky-400]="agent.autonomyLevel === 'Draft'"
                          [class.border-sky-500/30]="agent.autonomyLevel === 'Draft'">
                      Level: {{ agent.autonomyLevel }}
                    </span>
                    <button (click)="runAutonomyCheck(agent.agentRole)"
                            class="text-xs bg-slate-800 hover:bg-slate-700 text-slate-200 px-3 py-1 rounded-lg border border-slate-700 transition flex items-center gap-1">
                      <span>⚖️</span> Autonomy Check
                    </button>
                  </div>
                </div>

                <!-- Progress Bar & Composite Score -->
                <div class="space-y-1.5">
                  <div class="flex items-center justify-between text-xs">
                    <span class="font-semibold text-slate-300">Composite Quality Score</span>
                    <span class="font-bold text-white">{{ agent.compositeScore }}%</span>
                  </div>
                  <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                    <div class="h-2 rounded-full transition-all duration-300"
                         [class.bg-gradient-to-r]="true"
                         [class.from-emerald-500]="agent.compositeScore >= 90"
                         [class.to-indigo-500]="agent.compositeScore >= 90"
                         [class.from-amber-500]="agent.compositeScore < 90 && agent.compositeScore >= 80"
                         [class.to-orange-500]="agent.compositeScore < 90 && agent.compositeScore >= 80"
                         [class.from-rose-500]="agent.compositeScore < 80"
                         [class.to-rose-600]="agent.compositeScore < 80"
                         [style.width.%]="agent.compositeScore">
                    </div>
                  </div>
                </div>

                <!-- Vitals Grid -->
                <div class="grid grid-cols-2 sm:grid-cols-5 gap-2 text-center text-xs">
                  <div class="bg-slate-950/60 p-2.5 rounded-xl border border-slate-800">
                    <span class="text-slate-400 block text-[10px] uppercase">Task Success</span>
                    <span class="font-bold text-white text-sm mt-0.5 block">{{ agent.taskSuccessRate }}%</span>
                  </div>
                  <div class="bg-slate-950/60 p-2.5 rounded-xl border border-slate-800">
                    <span class="text-slate-400 block text-[10px] uppercase">Tool Precision</span>
                    <span class="font-bold text-white text-sm mt-0.5 block">{{ agent.toolPrecisionRate }}%</span>
                  </div>
                  <div class="bg-slate-950/60 p-2.5 rounded-xl border border-slate-800">
                    <span class="text-slate-400 block text-[10px] uppercase">Human Approval</span>
                    <span class="font-bold text-white text-sm mt-0.5 block"
                          [class.text-emerald-400]="agent.approvalRate > 95"
                          [class.text-amber-400]="agent.approvalRate <= 95">
                      {{ agent.approvalRate }}%
                    </span>
                  </div>
                  <div class="bg-slate-950/60 p-2.5 rounded-xl border border-slate-800">
                    <span class="text-slate-400 block text-[10px] uppercase">Hallucination</span>
                    <span class="font-bold text-white text-sm mt-0.5 block"
                          [class.text-emerald-400]="agent.hallucinationIndex <= 0.02"
                          [class.text-amber-400]="agent.hallucinationIndex > 0.02">
                      {{ (agent.hallucinationIndex * 100).toFixed(1) }}%
                    </span>
                  </div>
                  <div class="bg-slate-950/60 p-2.5 rounded-xl border border-slate-800 col-span-2 sm:col-span-1">
                    <span class="text-slate-400 block text-[10px] uppercase">Cost / Latency</span>
                    <span class="font-bold text-cyan-400 text-xs mt-0.5 block">
                      {{ agent.costPerTaskSar }} SAR · {{ agent.averageLatencyMs | number:'1.0-0' }}ms
                    </span>
                  </div>
                </div>

                <!-- Recommendation Banner -->
                <div class="bg-slate-950/80 border border-slate-800/80 rounded-xl p-3 flex items-start gap-2 text-xs">
                  <span class="text-indigo-400 mt-0.5">💡</span>
                  <div class="text-slate-300 leading-relaxed">{{ agent.recommendation }}</div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Right: Simulation Action & Rejection Breakdown (4 cols) -->
        <div class="lg:col-span-4 space-y-6">
          <!-- Simulate Evaluation Run Quick Action -->
          <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <h2 class="text-base font-bold text-white flex items-center gap-2">
                <span>🧪</span> Simulate Evaluation Run
              </h2>
              <span class="text-[10px] font-mono text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20">
                Reactive Signal
              </span>
            </div>
            <p class="text-xs text-slate-400">
              Inject synthetic execution benchmarks to inspect live score updates and autonomy promotion gating.
            </p>

            <!-- Quick Presets -->
            <div class="space-y-2">
              <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider block">One-Click Presets</span>
              <div class="grid grid-cols-2 gap-2">
                <button (click)="simulatePreset('high-quality')"
                        class="px-2.5 py-1.5 text-xs bg-emerald-950/60 hover:bg-emerald-900/60 text-emerald-300 rounded-lg border border-emerald-800/80 transition text-left">
                  ⚡ 100% Quality Run
                </button>
                <button (click)="simulatePreset('hallucination')"
                        class="px-2.5 py-1.5 text-xs bg-rose-950/60 hover:bg-rose-900/60 text-rose-300 rounded-lg border border-rose-800/80 transition text-left">
                  ⚠️ Flag Hallucination
                </button>
                <button (click)="simulatePreset('approve-promotion')"
                        class="px-2.5 py-1.5 text-xs bg-indigo-950/60 hover:bg-indigo-900/60 text-indigo-300 rounded-lg border border-indigo-800/80 transition text-left">
                  🚀 Batch Approvals
                </button>
                <button (click)="simulatePreset('policy-reject')"
                        class="px-2.5 py-1.5 text-xs bg-amber-950/60 hover:bg-amber-900/60 text-amber-300 rounded-lg border border-amber-800/80 transition text-left">
                  🛑 Policy Rejection
                </button>
              </div>
            </div>

            <!-- Custom Simulation Form -->
            <div class="space-y-3 pt-2 border-t border-slate-800">
              <div>
                <label class="text-[11px] font-medium text-slate-400 block mb-1">Target Agent Role</label>
                <select [(ngModel)]="simulationForm.agentRole"
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500">
                  @for (agent of scorecards(); track agent.agentRole) {
                    <option [value]="agent.agentRole">{{ agent.agentRole }}</option>
                  }
                </select>
              </div>

              <div class="grid grid-cols-2 gap-2 text-xs">
                <label class="flex items-center gap-2 bg-slate-950 p-2 rounded-lg border border-slate-800 text-slate-300 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="simulationForm.wasSuccessful" class="rounded accent-indigo-600">
                  <span>Task Success</span>
                </label>
                <label class="flex items-center gap-2 bg-slate-950 p-2 rounded-lg border border-slate-800 text-slate-300 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="simulationForm.toolValid" class="rounded accent-indigo-600">
                  <span>Tool Valid</span>
                </label>
                <label class="flex items-center gap-2 bg-slate-950 p-2 rounded-lg border border-slate-800 text-slate-300 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="simulationForm.humanApproved" class="rounded accent-indigo-600">
                  <span>Approved</span>
                </label>
                <label class="flex items-center gap-2 bg-slate-950 p-2 rounded-lg border border-slate-800 text-slate-300 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="simulationForm.hadHallucination" class="rounded accent-rose-600">
                  <span class="text-rose-300">Hallucinated</span>
                </label>
              </div>

              <button (click)="submitCustomSimulation()"
                      class="w-full py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-bold transition shadow-lg shadow-indigo-600/20">
                Execute Simulation Run
              </button>
            </div>

            <!-- Last Simulation Feedback Banner -->
            @if (lastSimulationFeedback()) {
              <div class="p-2.5 rounded-lg text-xs bg-slate-950 border border-indigo-500/30 text-indigo-300 flex items-center justify-between">
                <span>{{ lastSimulationFeedback() }}</span>
                <button (click)="lastSimulationFeedback.set('')" class="text-slate-500 hover:text-white">✕</button>
              </div>
            }
          </div>

          <!-- Rejection Reasons Breakdown Card List -->
          <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <h2 class="text-base font-bold text-white flex items-center gap-2">
                <span>🛑</span> Top Rejection Reasons
              </h2>
              <span class="text-xs text-slate-400">{{ totalRejections() }} Incidents</span>
            </div>
            <p class="text-xs text-slate-400">
              Clustered governance rejects, hallucinated claims & policy breaches across all agents.
            </p>

            <div class="space-y-3 pt-1">
              @for (cluster of rejectionClusters(); track cluster.reason) {
                @let pct = totalRejections() > 0 ? (cluster.count / totalRejections()) * 100 : 0;
                <div class="space-y-1 text-xs">
                  <div class="flex items-center justify-between">
                    <span class="text-slate-300 font-medium truncate pr-2" [title]="cluster.reason">{{ cluster.reason }}</span>
                    <span class="text-slate-400 font-mono">{{ cluster.count }}</span>
                  </div>
                  <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                    <div class="bg-rose-500 h-1.5 rounded-full transition-all duration-300"
                         [style.width.%]="pct"></div>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class AiAgentEvaluationRadarComponent {
  // Reactive Signals
  readonly scorecards = signal<AgentRadarScorecard[]>(INITIAL_SCORECARDS);
  readonly rejectionClusters = signal<RejectionReasonCluster[]>(INITIAL_REJECTIONS);
  readonly filterLevel = signal<'ALL' | 'Execute' | 'Approve'>('ALL');
  readonly lastSimulationFeedback = signal<string>('');

  // Simulation Form State
  simulationForm = {
    agentRole: 'AdvertisingAgent',
    wasSuccessful: true,
    toolValid: true,
    humanApproved: true,
    hadHallucination: false,
    costSar: 8.50,
    latencyMs: 950.0,
    rejectionReason: ''
  };

  readonly stages = [
    { key: 'Read', label: 'Stage 1', title: 'Read-Only Mode', description: 'Agent inspects, monitors & audits data without write access.' },
    { key: 'Draft', label: 'Stage 2', title: 'Draft Mode', description: 'Agent proposes campaigns & brief copy for human editing.' },
    { key: 'Approve', label: 'Stage 3', title: 'Supervised Autonomy', description: 'High precision; actions queued for human gatekeeper sign-off.' },
    { key: 'Execute', label: 'Stage 4', title: 'Fully Autonomous', description: 'Deterministic execution with near-zero hallucination rate.' }
  ];

  // Computed KPIs
  readonly filteredScorecards = computed(() => {
    const level = this.filterLevel();
    const cards = this.scorecards();
    if (level === 'ALL') return cards;
    return cards.filter(c => c.autonomyLevel === level);
  });

  readonly fullyAutonomousCount = computed(() =>
    this.scorecards().filter(c => c.autonomyLevel === 'Execute').length
  );

  readonly supervisedCount = computed(() =>
    this.scorecards().filter(c => c.autonomyLevel !== 'Execute').length
  );

  readonly globalScore = computed(() => {
    const cards = this.scorecards();
    if (cards.length === 0) return 0;
    const avg = cards.reduce((sum, c) => sum + c.compositeScore, 0) / cards.length;
    return Math.round(avg * 10) / 10;
  });

  readonly averageCostPerTaskSar = computed(() => {
    const cards = this.scorecards();
    if (cards.length === 0) return 0;
    const avg = cards.reduce((sum, c) => sum + c.costPerTaskSar, 0) / cards.length;
    return Math.round(avg * 100) / 100;
  });

  readonly totalTasksEvaluated = computed(() =>
    this.scorecards().reduce((sum, c) => sum + c.totalTasksEvaluated, 0)
  );

  readonly totalRejections = computed(() =>
    this.rejectionClusters().reduce((sum, r) => sum + r.count, 0)
  );

  readonly stageCounts = computed(() => {
    const map: Record<string, number> = { Read: 0, Draft: 0, Approve: 0, Execute: 0 };
    for (const card of this.scorecards()) {
      map[card.autonomyLevel] = (map[card.autonomyLevel] || 0) + 1;
    }
    return map;
  });

  // Autonomy Check & Promotion / Demotion Logic
  runAutonomyCheck(agentRole: string): void {
    this.scorecards.update(cards => {
      return cards.map(agent => {
        if (agent.agentRole !== agentRole) return agent;

        let nextLevel = agent.autonomyLevel;
        let message = '';

        if (agent.autonomyLevel === 'Draft' && agent.compositeScore > 80.0) {
          nextLevel = 'Approve';
          message = `Promoted ${agent.agentRole} from Draft to Approve (composite > 80%).`;
        } else if (agent.autonomyLevel === 'Approve' && agent.compositeScore > 90.0 && agent.approvalRate > 95.0) {
          nextLevel = 'Execute';
          message = `Promoted ${agent.agentRole} from Approve to Execute (composite > 90% and approval > 95%).`;
        } else if (agent.autonomyLevel === 'Execute' && (agent.compositeScore <= 90.0 || agent.approvalRate <= 95.0)) {
          nextLevel = 'Approve';
          message = `Demoted ${agent.agentRole} from Execute to Approve for safety guardrails.`;
        } else if (agent.autonomyLevel === 'Approve' && agent.compositeScore <= 80.0) {
          nextLevel = 'Draft';
          message = `Demoted ${agent.agentRole} from Approve to Draft.`;
        } else {
          message = `${agent.agentRole} is within expected thresholds for ${agent.autonomyLevel} mode.`;
        }

        this.lastSimulationFeedback.set(message);

        return {
          ...agent,
          autonomyLevel: nextLevel,
          recommendation: nextLevel === 'Execute'
            ? `Full autonomous execution verified at ${agent.compositeScore}% composite score.`
            : `Supervised mode active. Approval rate at ${agent.approvalRate}% (target > 95.0%).`
        };
      });
    });
  }

  // Quick Action Presets
  simulatePreset(preset: 'high-quality' | 'hallucination' | 'approve-promotion' | 'policy-reject'): void {
    switch (preset) {
      case 'high-quality':
        this.applyEvaluationRun({
          agentRole: 'ResearchAgent',
          wasSuccessful: true,
          toolValid: true,
          humanApproved: true,
          hadHallucination: false,
          costSar: 4.10,
          latencyMs: 760
        });
        this.lastSimulationFeedback.set('Simulated 100% Quality Run on ResearchAgent.');
        break;

      case 'hallucination':
        this.applyEvaluationRun({
          agentRole: 'AdvertisingAgent',
          wasSuccessful: false,
          toolValid: true,
          humanApproved: false,
          hadHallucination: true,
          costSar: 12.50,
          latencyMs: 1850,
          rejectionReason: 'Hallucinated citation or non-existent discount policy'
        });
        this.lastSimulationFeedback.set('Simulated Hallucination Incident on AdvertisingAgent.');
        break;

      case 'approve-promotion':
        // Batch push AdvertisingAgent to push approvals and trigger promotion check
        for (let i = 0; i < 20; i++) {
          this.applyEvaluationRun({
            agentRole: 'AdvertisingAgent',
            wasSuccessful: true,
            toolValid: true,
            humanApproved: true,
            hadHallucination: false,
            costSar: 11.00,
            latencyMs: 1100
          });
        }
        this.runAutonomyCheck('AdvertisingAgent');
        break;

      case 'policy-reject':
        this.applyEvaluationRun({
          agentRole: 'ContentAgent',
          wasSuccessful: false,
          toolValid: false,
          humanApproved: false,
          hadHallucination: false,
          costSar: 8.00,
          latencyMs: 900,
          rejectionReason: 'Unverified pricing claim against ERP price list'
        });
        this.lastSimulationFeedback.set('Recorded Policy Rejection Incident on ContentAgent.');
        break;
    }
  }

  submitCustomSimulation(): void {
    this.applyEvaluationRun({
      agentRole: this.simulationForm.agentRole,
      wasSuccessful: this.simulationForm.wasSuccessful,
      toolValid: this.simulationForm.toolValid,
      humanApproved: this.simulationForm.humanApproved,
      hadHallucination: this.simulationForm.hadHallucination,
      costSar: this.simulationForm.costSar,
      latencyMs: this.simulationForm.latencyMs,
      rejectionReason: this.simulationForm.rejectionReason
    });
    this.lastSimulationFeedback.set(`Recorded simulated evaluation on ${this.simulationForm.agentRole}.`);
  }

  private applyEvaluationRun(params: {
    agentRole: string;
    wasSuccessful: boolean;
    toolValid: boolean;
    humanApproved: boolean;
    hadHallucination: boolean;
    costSar: number;
    latencyMs: number;
    rejectionReason?: string;
  }): void {
    // 1. Update Scorecards
    this.scorecards.update(cards => {
      return cards.map(agent => {
        if (agent.agentRole !== params.agentRole) return agent;

        const prevCount = agent.totalTasksEvaluated;
        const newCount = prevCount + 1;

        const successRate = Math.round((((agent.taskSuccessRate * prevCount) + (params.wasSuccessful ? 100 : 0)) / newCount) * 100) / 100;
        const precisionRate = Math.round((((agent.toolPrecisionRate * prevCount) + (params.toolValid ? 100 : 0)) / newCount) * 100) / 100;
        const approvalRate = Math.round((((agent.approvalRate * prevCount) + (params.humanApproved ? 100 : 0)) / newCount) * 100) / 100;
        const hallucinationIndex = Math.round((((agent.hallucinationIndex * prevCount) + (params.hadHallucination ? 1 : 0)) / newCount) * 10000) / 10000;
        const cost = Math.round((((agent.costPerTaskSar * prevCount) + params.costSar) / newCount) * 100) / 100;
        const latency = Math.round((((agent.averageLatencyMs * prevCount) + params.latencyMs) / newCount) * 10) / 10;

        // Composite formula
        const penalty = Math.min(Math.max(hallucinationIndex, 0), 1);
        const composite = Math.round(((successRate * 0.40) + (precisionRate * 0.30) + (approvalRate * 0.20) + ((1.0 - penalty) * 100.0 * 0.10)) * 10) / 10;

        const trend: RegressionTrend = composite > agent.compositeScore + 0.2
          ? 'Improving'
          : composite < agent.compositeScore - 0.2
            ? 'Regressing'
            : 'Stable';

        return {
          ...agent,
          compositeScore: composite,
          taskSuccessRate: successRate,
          toolPrecisionRate: precisionRate,
          approvalRate: approvalRate,
          hallucinationIndex: hallucinationIndex,
          costPerTaskSar: cost,
          averageLatencyMs: latency,
          totalTasksEvaluated: newCount,
          trend: trend
        };
      });
    });

    // 2. Cluster Rejections
    if (params.rejectionReason && params.rejectionReason.trim().length > 0) {
      const reason = params.rejectionReason.trim();
      this.rejectionClusters.update(clusters => {
        const found = clusters.find(c => c.reason.toLowerCase() === reason.toLowerCase());
        if (found) {
          return clusters.map(c => c.reason.toLowerCase() === reason.toLowerCase() ? { ...c, count: c.count + 1 } : c)
            .sort((a, b) => b.count - a.count);
        } else {
          return [...clusters, { reason, count: 1 }].sort((a, b) => b.count - a.count);
        }
      });
    }
  }
}
