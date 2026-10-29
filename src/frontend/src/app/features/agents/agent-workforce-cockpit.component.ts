import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface AgentRoleUi {
  roleCode: string;
  displayName: string;
  echelon: 'Business' | 'Engineering';
  category: string;
  description: string;
  competencies: string[];
  defaultModelTier: 'FastTier' | 'StandardTier' | 'ReasoningTier';
  icon: string;
  defaultStatus: string;
  systemObjective: string;
}

export interface MissionNodeUi {
  id: string;
  parentId?: string | null;
  title: string;
  description: string;
  layerLevel: number; // 1 = Strategic Pillar, 2 = Tactical Stream, 3 = Operational Task
  echelon: 'Business' | 'Engineering';
  assignedRoleCode: string;
  assignedRoleName: string;
  modelTier: 'FastTier' | 'StandardTier' | 'ReasoningTier';
  prerequisiteTaskIds: string[];
  status: 'Pending' | 'InProgress' | 'Completed' | 'Blocked' | 'Failed';
  progressPercentage: number;
  weight: number;
  executionOutput?: string | null;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
}

export interface GoalTreeUi {
  id: string;
  tenantId: string;
  executiveGoalText: string;
  targetTimeframe: string;
  status: string;
  overallAchievementPercentage: number;
  nodes: MissionNodeUi[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AgentActivityLogUi {
  id: string;
  tenantId: string;
  goalTreeId: string;
  nodeId: string;
  roleCode: string;
  roleName: string;
  actionSummary: string;
  modelTierUsed: 'FastTier' | 'StandardTier' | 'ReasoningTier';
  tokensConsumed: number;
  latencyMs: number;
  timestampUtc: string;
}

export interface RolesCatalogResponseUi {
  totalRolesCount: number;
  businessRolesCount: number;
  engineeringRolesCount: number;
  businessRoles: AgentRoleUi[];
  engineeringRoles: AgentRoleUi[];
}

@Component({
  selector: 'app-agent-workforce-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans selection:bg-indigo-500/30 selection:text-indigo-200">
      <!-- 1. Header & Cockpit Topbar -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 py-4 sticky top-0 z-30 flex flex-wrap items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <div class="h-10 w-10 rounded-xl bg-gradient-to-tr from-indigo-600 to-purple-600 text-white flex items-center justify-center font-bold text-xl shadow-lg shadow-indigo-500/20">
            ⚡
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-base font-bold text-white tracking-tight">Autonomous Agent Workforce Cockpit</h1>
              <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-indigo-900/60 text-indigo-300 border border-indigo-700/60">
                EvoNexus • 38-Role Dual-Layer
              </span>
              <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-emerald-900/60 text-emerald-300 border border-emerald-700/60 flex items-center gap-1">
                <span class="h-1.5 w-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                Heartbeat 100% Online
              </span>
            </div>
            <p class="text-xs text-slate-400">Multi-echelon agent workforce, OKR goal tree DAG decomposition & continuous heartbeat loop</p>
          </div>
        </div>

        <div class="flex items-center gap-2.5">
          <button
            (click)="triggerHeartbeatRun()"
            [disabled]="isHeartbeatRunning()"
            class="px-3.5 py-1.5 rounded-lg bg-emerald-600/90 hover:bg-emerald-500 text-white text-xs font-semibold shadow-md shadow-emerald-950 flex items-center gap-1.5 transition disabled:opacity-50">
            <span>{{ isHeartbeatRunning() ? '⏳ Sweeping...' : '⚡ Run Heartbeat Sweep' }}</span>
          </button>

          <a routerLink="/agents/console" class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium border border-slate-700/60 transition flex items-center gap-1">
            <span>← Agent Console</span>
          </a>
        </div>
      </header>

      <!-- 2. Main Content Area -->
      <main class="flex-1 p-6 max-w-7xl mx-auto w-full space-y-6">
        <!-- Summary Stats Banner -->
        <section class="grid grid-cols-2 md:grid-cols-5 gap-3.5">
          <!-- Total Workforce -->
          <div class="p-4 bg-slate-900/70 border border-slate-800 rounded-xl flex flex-col justify-between shadow-sm">
            <span class="text-[11px] font-mono text-slate-400 uppercase tracking-wider">Total Workforce</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-white font-mono">{{ totalRolesCount() }}</span>
              <span class="text-xs text-indigo-400 font-medium">Active Personas</span>
            </div>
            <span class="text-[10px] text-slate-500 mt-2">Zero-idle background daemons</span>
          </div>

          <!-- Business Echelon -->
          <div class="p-4 bg-slate-900/70 border border-slate-800 rounded-xl flex flex-col justify-between shadow-sm">
            <span class="text-[11px] font-mono text-slate-400 uppercase tracking-wider">Business Echelon</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-amber-300 font-mono">{{ businessRolesCount() }}</span>
              <span class="text-xs text-amber-400 font-medium">Ops & RevOps</span>
            </div>
            <span class="text-[10px] text-slate-500 mt-2">Margin, logistics, SDR & copy</span>
          </div>

          <!-- Technical Echelon -->
          <div class="p-4 bg-slate-900/70 border border-slate-800 rounded-xl flex flex-col justify-between shadow-sm">
            <span class="text-[11px] font-mono text-slate-400 uppercase tracking-wider">Engineering Echelon</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-cyan-300 font-mono">{{ engineeringRolesCount() }}</span>
              <span class="text-xs text-cyan-400 font-medium">Tech & Systems</span>
            </div>
            <span class="text-[10px] text-slate-500 mt-2">Architecture, TDD & security</span>
          </div>

          <!-- Heartbeat Status -->
          <div class="p-4 bg-slate-900/70 border border-slate-800 rounded-xl flex flex-col justify-between shadow-sm">
            <span class="text-[11px] font-mono text-slate-400 uppercase tracking-wider">Heartbeat Sweep</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-emerald-400 font-mono">100%</span>
              <span class="text-xs text-emerald-400 font-medium">Operational</span>
            </div>
            <span class="text-[10px] text-slate-500 mt-2">Simulated cron cadence active</span>
          </div>

          <!-- Active Goal Trees -->
          <div class="p-4 bg-slate-900/70 border border-slate-800 rounded-xl flex flex-col justify-between shadow-sm col-span-2 md:col-span-1">
            <span class="text-[11px] font-mono text-slate-400 uppercase tracking-wider">Active Goal Trees</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-purple-300 font-mono">{{ activeGoalTrees().length }}</span>
              <span class="text-xs text-purple-400 font-medium">DAG Trees</span>
            </div>
            <span class="text-[10px] text-slate-500 mt-2">Average: {{ averageGoalProgress() }}% completion</span>
          </div>
        </section>

        <!-- Navigation Tabs -->
        <div class="border-b border-slate-800 flex items-center justify-between gap-4">
          <div class="flex gap-2">
            <button
              (click)="activeTab.set('roles')"
              [class]="activeTab() === 'roles' ? 'border-b-2 border-indigo-500 text-white font-bold' : 'text-slate-400 hover:text-slate-200'"
              class="px-4 py-3 text-xs uppercase tracking-wider transition flex items-center gap-2">
              <span>👥 38-Role Persona Matrix</span>
              <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 font-mono">{{ totalRolesCount() }}</span>
            </button>

            <button
              (click)="activeTab.set('decomposer')"
              [class]="activeTab() === 'decomposer' ? 'border-b-2 border-indigo-500 text-white font-bold' : 'text-slate-400 hover:text-slate-200'"
              class="px-4 py-3 text-xs uppercase tracking-wider transition flex items-center gap-2">
              <span>🌳 Goal Tree Decomposer (DAG)</span>
              <span *ngIf="selectedGoalTree()" class="px-1.5 py-0.2 rounded-full text-[10px] bg-indigo-900 text-indigo-300 font-mono">
                {{ selectedGoalTree()?.overallAchievementPercentage }}%
              </span>
            </button>

            <button
              (click)="activeTab.set('audit')"
              [class]="activeTab() === 'audit' ? 'border-b-2 border-indigo-500 text-white font-bold' : 'text-slate-400 hover:text-slate-200'"
              class="px-4 py-3 text-xs uppercase tracking-wider transition flex items-center gap-2">
              <span>📟 Live Heartbeat Activity Terminal</span>
              <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-emerald-950 text-emerald-400 font-mono">
                {{ activityFeed().length }}
              </span>
            </button>
          </div>

          <div *ngIf="activeTab() === 'roles'" class="flex items-center gap-2 pb-2">
            <!-- Filter Pills -->
            <button
              (click)="roleFilter.set('all')"
              [class]="roleFilter() === 'all' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-400 hover:text-slate-200'"
              class="px-2.5 py-1 rounded-md text-[11px] font-medium transition">
              All (38)
            </button>
            <button
              (click)="roleFilter.set('business')"
              [class]="roleFilter() === 'business' ? 'bg-amber-600 text-white' : 'bg-slate-800 text-slate-400 hover:text-slate-200'"
              class="px-2.5 py-1 rounded-md text-[11px] font-medium transition">
              Business (17)
            </button>
            <button
              (click)="roleFilter.set('engineering')"
              [class]="roleFilter() === 'engineering' ? 'bg-cyan-600 text-white' : 'bg-slate-800 text-slate-400 hover:text-slate-200'"
              class="px-2.5 py-1 rounded-md text-[11px] font-medium transition">
              Technical & Engineering (21)
            </button>
          </div>
        </div>

        <!-- ================= TAB 1: 38-ROLE PERSONA MATRIX GRID ================= -->
        <section *ngIf="activeTab() === 'roles'" class="space-y-4">
          <!-- Search Bar -->
          <div class="relative">
            <input
              type="text"
              [(ngModel)]="searchQuery"
              placeholder="Search roles by title, code, competency or objective..."
              class="w-full bg-slate-900 border border-slate-800 rounded-xl px-4 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500" />
            <span *ngIf="searchQuery" (click)="searchQuery = ''" class="absolute right-3 top-2.5 text-slate-400 hover:text-white cursor-pointer text-xs">
              ✕
            </span>
          </div>

          <!-- Grid of Personas -->
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <div
              *ngFor="let role of filteredRoles()"
              (click)="inspectRole(role)"
              class="p-4 bg-slate-900/60 border border-slate-800 hover:border-slate-700 rounded-xl transition duration-150 flex flex-col justify-between space-y-3 cursor-pointer group shadow-sm hover:shadow-md hover:bg-slate-900/90">
              
              <div class="space-y-2">
                <!-- Top Row: Icon + Title + Status Pill -->
                <div class="flex items-start justify-between gap-2">
                  <div class="flex items-center gap-2.5">
                    <div class="h-9 w-9 rounded-lg flex items-center justify-center text-lg shrink-0 border"
                      [ngClass]="role.echelon === 'Business' ? 'bg-amber-950/40 border-amber-800/50 text-amber-300' : 'bg-cyan-950/40 border-cyan-800/50 text-cyan-300'">
                      {{ getRoleEmoji(role.roleCode) }}
                    </div>
                    <div>
                      <h3 class="text-xs font-bold text-white group-hover:text-indigo-300 transition">
                        {{ role.displayName }}
                      </h3>
                      <span class="text-[10px] font-mono text-slate-500">{{ role.roleCode }}</span>
                    </div>
                  </div>

                  <!-- Model Tier Pill -->
                  <span
                    class="px-2 py-0.5 rounded text-[9px] font-mono font-bold border shrink-0"
                    [ngClass]="getModelTierClass(role.defaultModelTier)">
                    {{ role.defaultModelTier }}
                  </span>
                </div>

                <!-- Description -->
                <p class="text-[11px] text-slate-400 line-clamp-2 leading-relaxed">
                  {{ role.description }}
                </p>
              </div>

              <!-- Competencies tags -->
              <div class="space-y-2 pt-1">
                <div class="flex flex-wrap gap-1">
                  <span
                    *ngFor="let comp of role.competencies.slice(0, 3)"
                    class="px-2 py-0.5 rounded text-[10px] bg-slate-800/80 text-slate-300 border border-slate-700/50">
                    {{ comp }}
                  </span>
                  <span *ngIf="role.competencies.length > 3" class="text-[10px] text-slate-500 self-center">
                    +{{ role.competencies.length - 3 }} more
                  </span>
                </div>

                <!-- Footer: Status & Echelon -->
                <div class="pt-2 border-t border-slate-800/80 flex items-center justify-between text-[10px]">
                  <span class="flex items-center gap-1.5 font-medium"
                    [ngClass]="role.defaultStatus === 'Standing By' ? 'text-slate-400' : 'text-emerald-400'">
                    <span class="h-1.5 w-1.5 rounded-full" [ngClass]="role.defaultStatus === 'Standing By' ? 'bg-slate-500' : 'bg-emerald-400 animate-pulse'"></span>
                    {{ role.defaultStatus }}
                  </span>
                  <span class="font-mono text-slate-500">{{ role.category }}</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <!-- ================= TAB 2: GOAL TREE DECOMPOSER TAB ================= -->
        <section *ngIf="activeTab() === 'decomposer'" class="space-y-6">
          <!-- Goal Input Box -->
          <div class="p-5 bg-slate-900 border border-slate-800 rounded-2xl space-y-4 shadow-xl">
            <div class="flex flex-col md:flex-row md:items-center justify-between gap-2">
              <div>
                <h2 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>🎯</span> Executive Goal & OKR Tree Decomposer
                </h2>
                <p class="text-xs text-slate-400">
                  Input a high-level strategic mandate. SGrowing decomposes it into a multi-echelon DAG with dependency trees and optimal agent assignments.
                </p>
              </div>

              <!-- Timeframe picker -->
              <div class="flex items-center gap-2">
                <label class="text-xs text-slate-400">Timeframe:</label>
                <select
                  [(ngModel)]="selectedTimeframe"
                  class="bg-slate-800 border border-slate-700 rounded-lg px-2.5 py-1 text-xs text-white focus:outline-none">
                  <option value="30 Days">30 Days (Sprint)</option>
                  <option value="60 Days">60 Days (Mid-Horizon)</option>
                  <option value="90 Days">90 Days (Quarterly OKR)</option>
                  <option value="6 Months">6 Months (Enterprise Pivot)</option>
                </select>
              </div>
            </div>

            <!-- Goal Textarea -->
            <div class="space-y-2">
              <textarea
                [(ngModel)]="goalInputText"
                rows="3"
                placeholder="e.g. Scale Monthly Cash Remittance by +35% with < 12% Return Rate across regional hubs..."
                class="w-full bg-slate-950 border border-slate-800 rounded-xl p-3.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"></textarea>

              <!-- Sample Preset Buttons -->
              <div class="flex flex-wrap items-center gap-2 pt-1">
                <span class="text-[10px] font-mono text-slate-400">Quick Presets:</span>
                <button
                  (click)="applyPreset('Scale Monthly Cash Remittance by +35% with < 12% Return Rate')"
                  class="px-2 py-0.5 rounded bg-slate-800/80 hover:bg-slate-700 text-slate-300 text-[11px] border border-slate-700/60 transition">
                  📦 COD Remittance & Return Rate (+35%)
                </button>
                <button
                  (click)="applyPreset('Modernize Monolith to Modular Domain Contexts with 99.99% Availability and Zero Downtime')"
                  class="px-2 py-0.5 rounded bg-slate-800/80 hover:bg-slate-700 text-slate-300 text-[11px] border border-slate-700/60 transition">
                  🏛️ System Architecture Modernization
                </button>
                <button
                  (click)="applyPreset('Autonomous Multi-Channel Outbound Sales Blitz & Lead Generation for Q4')"
                  class="px-2 py-0.5 rounded bg-slate-800/80 hover:bg-slate-700 text-slate-300 text-[11px] border border-slate-700/60 transition">
                  🚀 Outbound SDR Sales Blitz
                </button>
              </div>
            </div>

            <div class="flex items-center justify-between pt-2">
              <div class="text-[11px] text-slate-400 flex items-center gap-1.5">
                <span>🤖 Routing Model:</span>
                <span class="text-indigo-400 font-mono">Multi-Tier LLM (FastTier • StandardTier • ReasoningTier)</span>
              </div>

              <button
                (click)="decomposeGoal()"
                [disabled]="isDecomposing() || !goalInputText.trim()"
                class="px-5 py-2 rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 hover:to-purple-500 text-white text-xs font-bold transition shadow-lg shadow-indigo-600/20 disabled:opacity-50 flex items-center gap-2">
                <span>{{ isDecomposing() ? '⚙️ Decomposing DAG...' : '✨ Decompose with AI' }}</span>
              </button>
            </div>
          </div>

          <!-- Rendered DAG Goal Tree -->
          <div *ngIf="selectedGoalTree()" class="space-y-5">
            <!-- Goal Tree Summary Card -->
            <div class="p-5 bg-slate-900/80 border border-slate-800 rounded-2xl flex flex-col md:flex-row md:items-center justify-between gap-4 shadow-xl">
              <div>
                <div class="flex items-center gap-2">
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono uppercase bg-indigo-900/60 text-indigo-300 border border-indigo-700/60 font-bold">
                    Goal ID: {{ selectedGoalTree()?.id }}
                  </span>
                  <span class="text-xs text-slate-400">Timeframe: {{ selectedGoalTree()?.targetTimeframe }}</span>
                </div>
                <h3 class="text-base font-bold text-white mt-1.5">
                  {{ selectedGoalTree()?.executiveGoalText }}
                </h3>
              </div>

              <!-- Progress bar -->
              <div class="w-full md:w-64 space-y-1.5">
                <div class="flex justify-between text-xs font-mono">
                  <span class="text-slate-400">Total Achievement</span>
                  <span class="text-emerald-400 font-bold">{{ selectedGoalTree()?.overallAchievementPercentage }}%</span>
                </div>
                <div class="h-2 w-full bg-slate-800 rounded-full overflow-hidden">
                  <div
                    class="h-full bg-gradient-to-r from-indigo-500 via-purple-500 to-emerald-400 transition-all duration-500 rounded-full"
                    [style.width.%]="selectedGoalTree()?.overallAchievementPercentage"></div>
                </div>
                <span class="text-[10px] text-slate-500">Status: {{ selectedGoalTree()?.status }}</span>
              </div>
            </div>

            <!-- Multi-Tier Visual Nodes (Level 1, Level 2, Level 3) -->
            <div class="space-y-4">
              <h4 class="text-xs font-mono uppercase text-slate-400 tracking-wider flex items-center gap-1.5">
                <span>📊 Mission Dependency Graph (DAG)</span>
                <span class="text-slate-600">• {{ selectedGoalTree()?.nodes?.length }} Total Nodes</span>
              </h4>

              <!-- Group nodes by LayerLevel -->
              <div class="space-y-6">
                <!-- Level 1: Strategic Pillars -->
                <div class="space-y-2">
                  <div class="flex items-center gap-2 text-xs font-bold text-indigo-300 font-mono">
                    <span class="h-2 w-2 rounded-full bg-indigo-500"></span>
                    <span>LAYER 1: STRATEGIC PILLARS</span>
                  </div>

                  <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <div
                      *ngFor="let node of getNodesByLayer(1)"
                      class="p-4 bg-slate-900/90 border rounded-xl space-y-2.5 transition"
                      [ngClass]="getNodeBorderClass(node)">
                      <div class="flex items-start justify-between gap-2">
                        <div class="flex items-center gap-2">
                          <span class="text-lg">{{ getRoleEmoji(node.assignedRoleCode) }}</span>
                          <div>
                            <h5 class="text-xs font-bold text-white">{{ node.title }}</h5>
                            <span class="text-[10px] font-mono text-indigo-400">{{ node.assignedRoleName }}</span>
                          </div>
                        </div>
                        <span class="px-2 py-0.5 rounded text-[9px] font-mono font-bold" [ngClass]="getStatusBadgeClass(node.status)">
                          {{ node.status }}
                        </span>
                      </div>

                      <p class="text-[11px] text-slate-400 leading-relaxed">{{ node.description }}</p>

                      <!-- Progress -->
                      <div class="space-y-1 pt-1">
                        <div class="flex justify-between text-[10px] font-mono">
                          <span class="text-slate-500">Progress</span>
                          <span class="text-slate-300">{{ node.progressPercentage }}%</span>
                        </div>
                        <div class="h-1.5 w-full bg-slate-800 rounded-full overflow-hidden">
                          <div class="h-full bg-indigo-500 rounded-full" [style.width.%]="node.progressPercentage"></div>
                        </div>
                      </div>

                      <div class="pt-2 border-t border-slate-800 flex items-center justify-between text-[10px] font-mono text-slate-500">
                        <span>Tier: {{ node.modelTier }}</span>
                        <span>Node #{{ node.id }}</span>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- Level 2: Tactical Streams -->
                <div class="space-y-2">
                  <div class="flex items-center gap-2 text-xs font-bold text-amber-300 font-mono">
                    <span class="h-2 w-2 rounded-full bg-amber-500"></span>
                    <span>LAYER 2: TACTICAL EXECUTION STREAMS</span>
                  </div>

                  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                    <div
                      *ngFor="let node of getNodesByLayer(2)"
                      class="p-4 bg-slate-900/90 border rounded-xl space-y-2.5 transition"
                      [ngClass]="getNodeBorderClass(node)">
                      <div class="flex items-start justify-between gap-2">
                        <div class="flex items-center gap-2">
                          <span class="text-lg">{{ getRoleEmoji(node.assignedRoleCode) }}</span>
                          <div>
                            <h5 class="text-xs font-bold text-white">{{ node.title }}</h5>
                            <span class="text-[10px] font-mono text-amber-400">{{ node.assignedRoleName }}</span>
                          </div>
                        </div>
                        <span class="px-2 py-0.5 rounded text-[9px] font-mono font-bold" [ngClass]="getStatusBadgeClass(node.status)">
                          {{ node.status }}
                        </span>
                      </div>

                      <p class="text-[11px] text-slate-400 leading-relaxed">{{ node.description }}</p>

                      <div *ngIf="node.prerequisiteTaskIds.length" class="text-[10px] font-mono text-slate-500">
                        Depends on: <span class="text-slate-400">{{ node.prerequisiteTaskIds.join(', ') }}</span>
                      </div>

                      <!-- Progress -->
                      <div class="space-y-1 pt-1">
                        <div class="flex justify-between text-[10px] font-mono">
                          <span class="text-slate-500">Progress</span>
                          <span class="text-slate-300">{{ node.progressPercentage }}%</span>
                        </div>
                        <div class="h-1.5 w-full bg-slate-800 rounded-full overflow-hidden">
                          <div class="h-full bg-amber-500 rounded-full" [style.width.%]="node.progressPercentage"></div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- Level 3: Concrete Operational Tasks -->
                <div class="space-y-2">
                  <div class="flex items-center gap-2 text-xs font-bold text-cyan-300 font-mono">
                    <span class="h-2 w-2 rounded-full bg-cyan-500"></span>
                    <span>LAYER 3: CONCRETE AGENT TASKS & VERIFICATIONS</span>
                  </div>

                  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
                    <div
                      *ngFor="let node of getNodesByLayer(3)"
                      class="p-4 bg-slate-900/90 border rounded-xl space-y-2.5 transition"
                      [ngClass]="getNodeBorderClass(node)">
                      <div class="flex items-start justify-between gap-2">
                        <div class="flex items-center gap-2">
                          <span class="text-lg">{{ getRoleEmoji(node.assignedRoleCode) }}</span>
                          <div>
                            <h5 class="text-xs font-bold text-white line-clamp-1">{{ node.title }}</h5>
                            <span class="text-[10px] font-mono text-cyan-400">{{ node.assignedRoleName }}</span>
                          </div>
                        </div>
                        <span class="px-2 py-0.5 rounded text-[9px] font-mono font-bold" [ngClass]="getStatusBadgeClass(node.status)">
                          {{ node.status }}
                        </span>
                      </div>

                      <p class="text-[11px] text-slate-400 line-clamp-2 leading-relaxed">{{ node.description }}</p>

                      <div *ngIf="node.prerequisiteTaskIds.length" class="text-[10px] font-mono text-slate-500">
                        Depends on: <span class="text-slate-400">{{ node.prerequisiteTaskIds.join(', ') }}</span>
                      </div>

                      <!-- Progress -->
                      <div class="space-y-1 pt-1">
                        <div class="flex justify-between text-[10px] font-mono">
                          <span class="text-slate-500">Progress</span>
                          <span class="text-slate-300">{{ node.progressPercentage }}%</span>
                        </div>
                        <div class="h-1.5 w-full bg-slate-800 rounded-full overflow-hidden">
                          <div class="h-full bg-cyan-500 rounded-full" [style.width.%]="node.progressPercentage"></div>
                        </div>
                      </div>

                      <!-- Output preview if completed -->
                      <div *ngIf="node.executionOutput" class="p-2 rounded bg-slate-950/80 border border-slate-800 text-[10px] text-emerald-400 font-mono">
                        {{ node.executionOutput }}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <!-- ================= TAB 3: LIVE HEARTBEAT AUDIT STREAM ================= -->
        <section *ngIf="activeTab() === 'audit'" class="space-y-4">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div>
              <h2 class="text-sm font-bold text-white font-mono flex items-center gap-2">
                <span>📟</span> Live Autonomous Heartbeat Activity Feed
              </h2>
              <p class="text-xs text-slate-400">
                Timestamped execution log stream capturing autonomous agent actions, token usage, and latency.
              </p>
            </div>

            <div class="flex items-center gap-2">
              <button
                (click)="loadActivityFeed()"
                class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-mono border border-slate-700 transition">
                ↻ Refresh Stream
              </button>
              <button
                (click)="triggerHeartbeatRun()"
                [disabled]="isHeartbeatRunning()"
                class="px-3.5 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold shadow-md transition disabled:opacity-50">
                <span>{{ isHeartbeatRunning() ? 'Sweeping...' : '▶ Run Heartbeat Cycle' }}</span>
              </button>
            </div>
          </div>

          <!-- Terminal Output Box -->
          <div class="bg-slate-950 border border-slate-800 rounded-2xl p-4 font-mono text-xs shadow-2xl overflow-hidden">
            <!-- Terminal Header -->
            <div class="flex items-center justify-between pb-3 mb-3 border-b border-slate-800/80 text-[11px] text-slate-500">
              <div class="flex items-center gap-2">
                <span class="h-2.5 w-2.5 rounded-full bg-rose-500 inline-block"></span>
                <span class="h-2.5 w-2.5 rounded-full bg-amber-500 inline-block"></span>
                <span class="h-2.5 w-2.5 rounded-full bg-emerald-500 inline-block"></span>
                <span class="ml-2 text-slate-400">sgrowing-daemon:~/agent-fleet-heartbeat$</span>
              </div>
              <div>Stream: active (poll interval 10s)</div>
            </div>

            <!-- Terminal Items List -->
            <div class="space-y-2.5 max-h-[520px] overflow-y-auto pr-2">
              <div *ngIf="activityFeed().length === 0" class="text-slate-600 py-8 text-center">
                No autonomous actions recorded yet. Click 'Run Heartbeat Sweep' to trigger agent cycles.
              </div>

              <div
                *ngFor="let log of activityFeed()"
                class="p-2.5 rounded-lg bg-slate-900/60 border border-slate-800/70 hover:border-slate-700 transition flex flex-col md:flex-row md:items-center justify-between gap-2">
                <div class="flex items-start md:items-center gap-2.5">
                  <span class="text-slate-500 text-[10px] shrink-0">
                    {{ log.timestampUtc | date:'HH:mm:ss.SSS' }}
                  </span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-bold shrink-0"
                    [ngClass]="getModelTierClass(log.modelTierUsed)">
                    {{ log.roleCode }}
                  </span>
                  <span class="text-slate-200 text-xs">{{ log.actionSummary }}</span>
                </div>

                <div class="flex items-center gap-3 text-[10px] text-slate-400 shrink-0">
                  <span class="text-slate-500">{{ log.modelTierUsed }}</span>
                  <span class="text-indigo-400">{{ log.tokensConsumed }} tok</span>
                  <span class="text-emerald-400">{{ log.latencyMs }}ms</span>
                </div>
              </div>
            </div>
          </div>
        </section>
      </main>

      <!-- Role Details Modal -->
      <div *ngIf="inspectedRole()" class="fixed inset-0 z-50 bg-black/75 backdrop-blur-sm flex items-center justify-center p-4">
        <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-2xl">
          <div class="flex items-start justify-between">
            <div class="flex items-center gap-3">
              <div class="h-12 w-12 rounded-xl flex items-center justify-center text-2xl border bg-slate-800 border-slate-700">
                {{ getRoleEmoji(inspectedRole()!.roleCode) }}
              </div>
              <div>
                <h3 class="text-base font-bold text-white">{{ inspectedRole()?.displayName }}</h3>
                <span class="text-xs font-mono text-indigo-400">{{ inspectedRole()?.roleCode }} • {{ inspectedRole()?.category }}</span>
              </div>
            </div>
            <button (click)="inspectedRole.set(null)" class="text-slate-400 hover:text-white text-lg">✕</button>
          </div>

          <div class="space-y-3 text-xs">
            <div>
              <span class="text-slate-400 font-mono text-[10px] uppercase">Description</span>
              <p class="text-slate-200 mt-0.5 leading-relaxed">{{ inspectedRole()?.description }}</p>
            </div>

            <div>
              <span class="text-slate-400 font-mono text-[10px] uppercase">System Objective</span>
              <p class="text-emerald-300 font-mono mt-0.5 leading-relaxed bg-slate-950 p-2 rounded-lg border border-slate-800">
                {{ inspectedRole()?.systemObjective }}
              </p>
            </div>

            <div>
              <span class="text-slate-400 font-mono text-[10px] uppercase">Competencies & Skills</span>
              <div class="flex flex-wrap gap-1.5 mt-1">
                <span *ngFor="let c of inspectedRole()?.competencies" class="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300 border border-slate-700">
                  {{ c }}
                </span>
              </div>
            </div>

            <div class="pt-2 border-t border-slate-800 flex items-center justify-between">
              <span class="text-slate-400">Assigned Model Tier:</span>
              <span class="px-2 py-0.5 rounded font-mono font-bold text-[10px] border" [ngClass]="getModelTierClass(inspectedRole()!.defaultModelTier)">
                {{ inspectedRole()?.defaultModelTier }}
              </span>
            </div>
          </div>

          <div class="pt-2">
            <button
              (click)="inspectedRole.set(null)"
              class="w-full py-2 bg-slate-800 hover:bg-slate-700 text-white rounded-xl text-xs font-semibold transition">
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class AgentWorkforceCockpitComponent implements OnInit {
  private http = inject(HttpClient);

  // Active state signals
  activeTab = signal<'roles' | 'decomposer' | 'audit'>('roles');
  roleFilter = signal<'all' | 'business' | 'engineering'>('all');
  searchQuery = '';
  selectedTimeframe = '90 Days';
  goalInputText = 'Scale Monthly Cash Remittance by +35% with < 12% Return Rate';

  // Loading signals
  isDecomposing = signal(false);
  isHeartbeatRunning = signal(false);

  // Data signals
  allRoles = signal<AgentRoleUi[]>([]);
  activeGoalTrees = signal<GoalTreeUi[]>([]);
  selectedGoalTree = signal<GoalTreeUi | null>(null);
  activityFeed = signal<AgentActivityLogUi[]>([]);
  inspectedRole = signal<AgentRoleUi | null>(null);

  // Computed values
  totalRolesCount = computed(() => this.allRoles().length);
  businessRolesCount = computed(() => this.allRoles().filter(r => r.echelon === 'Business').length);
  engineeringRolesCount = computed(() => this.allRoles().filter(r => r.echelon === 'Engineering').length);

  averageGoalProgress = computed(() => {
    const trees = this.activeGoalTrees();
    if (trees.length === 0) return 0;
    const sum = trees.reduce((acc, t) => acc + (t.overallAchievementPercentage || 0), 0);
    return Math.round(sum / trees.length);
  });

  filteredRoles = computed(() => {
    let list = this.allRoles();
    const filter = this.roleFilter();
    if (filter === 'business') {
      list = list.filter(r => r.echelon === 'Business');
    } else if (filter === 'engineering') {
      list = list.filter(r => r.echelon === 'Engineering');
    }

    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(r =>
        r.displayName.toLowerCase().includes(q) ||
        r.roleCode.toLowerCase().includes(q) ||
        r.description.toLowerCase().includes(q) ||
        r.competencies.some(c => c.toLowerCase().includes(q))
      );
    }

    return list;
  });

  ngOnInit(): void {
    this.loadRolesCatalog();
    this.loadGoalTrees();
    this.loadActivityFeed();
  }

  loadRolesCatalog(): void {
    this.http.get<RolesCatalogResponseUi>('/api/v1/agents/fleet/roles').subscribe({
      next: (res) => {
        const combined = [...res.businessRoles, ...res.engineeringRoles];
        this.allRoles.set(combined);
      },
      error: () => {
        // Fallback seed catalog for offline/standalone mode
        this.allRoles.set(this.getSeedRoles());
      }
    });
  }

  loadGoalTrees(): void {
    this.http.get<GoalTreeUi[]>('/api/v1/agents/fleet/goals').subscribe({
      next: (res) => {
        this.activeGoalTrees.set(res || []);
        if (res && res.length > 0 && !this.selectedGoalTree()) {
          this.selectedGoalTree.set(res[0]);
        }
      },
      error: () => {
        // Fallback sample goal tree
        const seedTree = this.getSeedGoalTree();
        this.activeGoalTrees.set([seedTree]);
        this.selectedGoalTree.set(seedTree);
      }
    });
  }

  loadActivityFeed(): void {
    this.http.get<AgentActivityLogUi[]>('/api/v1/agents/fleet/activity-feed?limit=50').subscribe({
      next: (res) => {
        this.activityFeed.set(res || []);
      },
      error: () => {
        this.activityFeed.set(this.getSeedActivityLogs());
      }
    });
  }

  decomposeGoal(): void {
    if (!this.goalInputText.trim()) return;

    this.isDecomposing.set(true);
    const body = {
      executiveGoalText: this.goalInputText,
      targetTimeframe: this.selectedTimeframe
    };

    this.http.post<GoalTreeUi>('/api/v1/agents/fleet/goals/decompose', body).subscribe({
      next: (res) => {
        this.selectedGoalTree.set(res);
        this.activeGoalTrees.update(trees => [res, ...trees.filter(t => t.id !== res.id)]);
        this.isDecomposing.set(false);
        this.loadActivityFeed();
      },
      error: () => {
        // Fallback local simulation
        const tree = this.getSeedGoalTree();
        tree.executiveGoalText = this.goalInputText;
        tree.targetTimeframe = this.selectedTimeframe;
        this.selectedGoalTree.set(tree);
        this.activeGoalTrees.update(trees => [tree, ...trees]);
        this.isDecomposing.set(false);
      }
    });
  }

  triggerHeartbeatRun(): void {
    this.isHeartbeatRunning.set(true);

    this.http.post<any>('/api/v1/agents/fleet/heartbeat/run', {}).subscribe({
      next: () => {
        this.loadGoalTrees();
        this.loadActivityFeed();
        this.isHeartbeatRunning.set(false);
      },
      error: () => {
        // Simulate local heartbeat advance
        const tree = this.selectedGoalTree();
        if (tree) {
          tree.nodes.forEach(n => {
            if (n.status === 'Pending') n.status = 'InProgress';
            n.progressPercentage = Math.min(100, n.progressPercentage + 35);
            if (n.progressPercentage >= 100) n.status = 'Completed';
          });
          tree.overallAchievementPercentage = Math.min(100, tree.overallAchievementPercentage + 25);
          this.selectedGoalTree.set({ ...tree });
        }
        this.isHeartbeatRunning.set(false);
      }
    });
  }

  applyPreset(text: string): void {
    this.goalInputText = text;
  }

  inspectRole(role: AgentRoleUi): void {
    this.inspectedRole.set(role);
  }

  getNodesByLayer(layer: number): MissionNodeUi[] {
    const tree = this.selectedGoalTree();
    if (!tree || !tree.nodes) return [];
    return tree.nodes.filter(n => n.layerLevel === layer);
  }

  getModelTierClass(tier: string): string {
    switch (tier) {
      case 'ReasoningTier':
        return 'bg-purple-900/40 text-purple-300 border-purple-700/60';
      case 'StandardTier':
        return 'bg-sky-900/40 text-sky-300 border-sky-700/60';
      case 'FastTier':
        return 'bg-emerald-900/40 text-emerald-300 border-emerald-700/60';
      default:
        return 'bg-slate-800 text-slate-300 border-slate-700';
    }
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Completed':
        return 'bg-emerald-950 text-emerald-400 border border-emerald-700/60';
      case 'InProgress':
        return 'bg-indigo-950 text-indigo-300 border border-indigo-700/60 animate-pulse';
      case 'Pending':
        return 'bg-slate-800 text-slate-400 border border-slate-700/60';
      default:
        return 'bg-slate-800 text-slate-300';
    }
  }

  getNodeBorderClass(node: MissionNodeUi): string {
    if (node.status === 'Completed') return 'border-emerald-600/50 bg-emerald-950/10';
    if (node.status === 'InProgress') return 'border-indigo-500/60 bg-indigo-950/10 shadow-lg shadow-indigo-950/30';
    return 'border-slate-800';
  }

  getRoleEmoji(roleCode: string): string {
    const emojis: Record<string, string> = {
      RevOpsStrategist: '📈',
      LogisticsController: '🚚',
      ColdOutreachSdr: '✉️',
      CustomerEscalationHandler: '🚨',
      SocialGrowthHacker: '📱',
      FinancialMarginAuditor: '💰',
      CommunityModerator: '🛡️',
      OnboardingConcierge: '🧭',
      AffiliateManager: '🤝',
      CatalogMerchandiser: '🛍️',
      ComplianceOfficer: '⚖️',
      ExecutiveChiefOfStaff: '💼',
      BrandVoiceStylist: '✒️',
      RetentionSpecialist: '❤️',
      MarketplaceScout: '🔭',
      AdSpendOptimizer: '📊',
      LocalizationAnalyst: '🌐',
      SoftwareArchitect: '🏛️',
      BackendDeveloper: '⚙️',
      FrontendSpecialist: '💻',
      DatabaseSpecialist: '🗄️',
      TestEngineer: '🧪',
      SecurityAuditor: '🔒',
      DevOpsEngineer: '🚀',
      PerformanceAnalyst: '⚡',
      ApiDesigner: '🔌',
      BuildErrorResolver: '🛠️',
      RefactorCleaner: '🧹',
      CodeReviewer: '👀',
      SlaGuardian: '⏱️',
      TelemetryEngineer: '📡',
      ScraperEngineer: '🕷️',
      DockerSpecialist: '🐳',
      AspireOrchestrator: '🌐',
      MobileCapacitorEngineer: '📱',
      DnnAiSpecialist: '🧠',
      DisasterRecoverySpecialist: '🛟',
      PromptEngineer: '💬'
    };
    return emojis[roleCode] || '🤖';
  }

  private getSeedRoles(): AgentRoleUi[] {
    // 38 complete roles catalog fallback
    return [
      { roleCode: 'RevOpsStrategist', displayName: 'RevOps Strategist', echelon: 'Business', category: 'Business Operations', description: 'Analyzes revenue funnels and CAC-to-LTV margins.', competencies: ['Revenue Operations', 'True Gross Margin'], defaultModelTier: 'ReasoningTier', icon: 'TrendingUp', defaultStatus: 'Standing By', systemObjective: 'Maximize revenue yield' },
      { roleCode: 'LogisticsController', displayName: 'Logistics Controller', echelon: 'Business', category: 'Business Operations', description: 'Multi-warehouse stock allocation and courier routing.', competencies: ['Inventory Allocation', 'Courier SLAs'], defaultModelTier: 'StandardTier', icon: 'Truck', defaultStatus: 'Auditing Logistics', systemObjective: 'Optimize delivery SLAs' },
      { roleCode: 'ColdOutreachSdr', displayName: 'Cold Outreach SDR', echelon: 'Business', category: 'Business Operations', description: 'Autonomous outbound prospecting and cadence dispatch.', competencies: ['Account Profiling', 'Personalized Copy'], defaultModelTier: 'StandardTier', icon: 'Mail', defaultStatus: 'Drafting Sequences', systemObjective: 'Generate qualified meetings' },
      { roleCode: 'CustomerEscalationHandler', displayName: 'Customer Escalation Handler', echelon: 'Business', category: 'Business Operations', description: 'De-escalates customer disputes and prevents VIP churn.', competencies: ['De-escalation', 'Conflict Resolution'], defaultModelTier: 'StandardTier', icon: 'AlertCircle', defaultStatus: 'Standing By', systemObjective: 'Resolve friction rapidly' },
      { roleCode: 'SocialGrowthHacker', displayName: 'Social Growth Hacker', echelon: 'Business', category: 'Business Operations', description: 'Viral loops and social content distribution.', competencies: ['Viral Content Loops', 'Omnichannel Scheduling'], defaultModelTier: 'StandardTier', icon: 'Share2', defaultStatus: 'Generating Ad Copy', systemObjective: 'Expand organic reach' },
      { roleCode: 'FinancialMarginAuditor', displayName: 'Financial Margin Auditor', echelon: 'Business', category: 'Business Operations', description: 'Audits courier remittance and true unit economics.', competencies: ['True Margin Audit', 'Courier Fee Reconciliation'], defaultModelTier: 'ReasoningTier', icon: 'DollarSign', defaultStatus: 'Standing By', systemObjective: 'Eliminate phantom fees' },
      { roleCode: 'CommunityModerator', displayName: 'Community Moderator', echelon: 'Business', category: 'Business Operations', description: 'Moderates user channels and removes spam.', competencies: ['Sentiment Analysis', 'Spam Filtering'], defaultModelTier: 'FastTier', icon: 'ShieldCheck', defaultStatus: 'Standing By', systemObjective: 'Protect community spaces' },
      { roleCode: 'OnboardingConcierge', displayName: 'Onboarding Concierge', echelon: 'Business', category: 'Business Operations', description: 'Accelerates time-to-value for new client signups.', competencies: ['Customer Activation', 'Interactive Guides'], defaultModelTier: 'StandardTier', icon: 'Compass', defaultStatus: 'Standing By', systemObjective: 'Achieve fast adoption' },
      { roleCode: 'AffiliateManager', displayName: 'Affiliate Manager', echelon: 'Business', category: 'Business Operations', description: 'Manages creator commissions and tracking links.', competencies: ['Partner Recruitment', 'Commission Modeling'], defaultModelTier: 'StandardTier', icon: 'Users', defaultStatus: 'Standing By', systemObjective: 'Scale affiliate revenue' },
      { roleCode: 'CatalogMerchandiser', displayName: 'Catalog Merchandiser', echelon: 'Business', category: 'Business Operations', description: 'Optimizes dynamic bundles and wholesale tiered pricing.', competencies: ['PIM Synchronization', 'Dynamic Bundling'], defaultModelTier: 'StandardTier', icon: 'ShoppingBag', defaultStatus: 'Standing By', systemObjective: 'Maximize AOV' },
      { roleCode: 'ComplianceOfficer', displayName: 'Compliance Officer', echelon: 'Business', category: 'Business Operations', description: 'Audits GDPR consumer rights and PII data governance.', competencies: ['Regulatory Auditing', 'GDPR/CCPA'], defaultModelTier: 'ReasoningTier', icon: 'Scale', defaultStatus: 'Standing By', systemObjective: 'Zero non-compliance' },
      { roleCode: 'ExecutiveChiefOfStaff', displayName: 'Executive Chief of Staff', echelon: 'Business', category: 'Business Operations', description: 'Orchestrates cross-echelon OKRs and board milestones.', competencies: ['OKR Tracking', 'Cross-Functional Alignment'], defaultModelTier: 'ReasoningTier', icon: 'Briefcase', defaultStatus: 'Standing By', systemObjective: 'Synchronize objectives' },
      { roleCode: 'BrandVoiceStylist', displayName: 'Brand Voice Stylist', echelon: 'Business', category: 'Business Operations', description: 'Enforces brand style guidelines across all copy.', competencies: ['Copywriting Guidelines', 'Tone Consistency'], defaultModelTier: 'StandardTier', icon: 'Feather', defaultStatus: 'Standing By', systemObjective: 'Preserve brand voice' },
      { roleCode: 'RetentionSpecialist', displayName: 'Retention Specialist', echelon: 'Business', category: 'Business Operations', description: 'Predicts churn and deploys automated win-back cadences.', competencies: ['Churn Prediction', 'Win-back Sequences'], defaultModelTier: 'StandardTier', icon: 'HeartHandshake', defaultStatus: 'Standing By', systemObjective: 'Maximize NRR' },
      { roleCode: 'MarketplaceScout', displayName: 'Marketplace Scout', echelon: 'Business', category: 'Business Operations', description: 'Scrapes competitor prices and identifies market gaps.', competencies: ['Competitor Benchmarking', 'Price Scraping'], defaultModelTier: 'StandardTier', icon: 'Search', defaultStatus: 'Standing By', systemObjective: 'Monitor competitor pricing' },
      { roleCode: 'AdSpendOptimizer', displayName: 'Ad Spend Optimizer', echelon: 'Business', category: 'Business Operations', description: 'Tuning ROAS and rebalancing ad spend budgets.', competencies: ['ROAS Optimization', 'CAPI Sync'], defaultModelTier: 'ReasoningTier', icon: 'PieChart', defaultStatus: 'Standing By', systemObjective: 'Eliminate ad waste' },
      { roleCode: 'LocalizationAnalyst', displayName: 'Localization Analyst', echelon: 'Business', category: 'Business Operations', description: 'Translates and culturally adapts commercial copy.', competencies: ['Cultural Adaptation', 'Multilingual Translation'], defaultModelTier: 'StandardTier', icon: 'Globe', defaultStatus: 'Standing By', systemObjective: 'Seamless regional resonance' },
      // 21 Engineering
      { roleCode: 'SoftwareArchitect', displayName: 'Software Architect', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Enforces modular monolith boundaries and DDD.', competencies: ['System Design', 'Modular Monolith'], defaultModelTier: 'ReasoningTier', icon: 'Cpu', defaultStatus: 'Standing By', systemObjective: 'Maintain architectural purity' },
      { roleCode: 'BackendDeveloper', displayName: 'Backend Developer', echelon: 'Engineering', category: 'Technical & Engineering', description: 'C# .NET 10 Minimal APIs and asynchronous handlers.', competencies: ['C# .NET 10', 'Minimal APIs'], defaultModelTier: 'StandardTier', icon: 'Server', defaultStatus: 'Standing By', systemObjective: 'Deliver robust services' },
      { roleCode: 'FrontendSpecialist', displayName: 'Frontend Specialist', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Angular 22 standalone Signals and Tailwind CSS.', competencies: ['Angular 22', 'Signals & Computed State'], defaultModelTier: 'StandardTier', icon: 'Layout', defaultStatus: 'Standing By', systemObjective: 'Craft responsive UI' },
      { roleCode: 'DatabaseSpecialist', displayName: 'Database Specialist', echelon: 'Engineering', category: 'Technical & Engineering', description: 'PostgreSQL 17, PostGIS spatial queries, EF Core.', competencies: ['PostgreSQL 17', 'PostGIS Indexing'], defaultModelTier: 'ReasoningTier', icon: 'Database', defaultStatus: 'Standing By', systemObjective: 'Sub-10ms query latency' },
      { roleCode: 'TestEngineer', displayName: 'Test Engineer', echelon: 'Engineering', category: 'Technical & Engineering', description: 'xUnit v3 parallel suites and WebApplicationFactory.', competencies: ['xUnit v3', 'TDD Red-Green-Refactor'], defaultModelTier: 'StandardTier', icon: 'CheckCircle', defaultStatus: 'Standing By', systemObjective: 'Prevent regressions' },
      { roleCode: 'SecurityAuditor', displayName: 'Security Auditor', echelon: 'Engineering', category: 'Technical & Engineering', description: 'OWASP Top 10 audits and AES-256 vault checks.', competencies: ['OWASP Top 10', 'AES-256 Vaults'], defaultModelTier: 'ReasoningTier', icon: 'Lock', defaultStatus: 'Standing By', systemObjective: 'Zero vulnerabilities' },
      { roleCode: 'DevOpsEngineer', displayName: 'DevOps Engineer', echelon: 'Engineering', category: 'Technical & Engineering', description: 'CI/CD GitHub Actions and Docker Swarm deployment.', competencies: ['CI/CD', 'Docker Swarm'], defaultModelTier: 'StandardTier', icon: 'Terminal', defaultStatus: 'Standing By', systemObjective: 'Automated releases' },
      { roleCode: 'PerformanceAnalyst', displayName: 'Performance Analyst', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Profiles GC allocations and tunes HybridCache.', competencies: ['Allocation Profiling', 'HybridCache'], defaultModelTier: 'ReasoningTier', icon: 'Activity', defaultStatus: 'Standing By', systemObjective: 'Sub-50ms P99 latency' },
      { roleCode: 'ApiDesigner', displayName: 'API Designer', echelon: 'Engineering', category: 'Technical & Engineering', description: 'OpenAPI specifications and RFC 7807 problem details.', competencies: ['OpenAPI v3', 'RFC 7807'], defaultModelTier: 'StandardTier', icon: 'Code', defaultStatus: 'Standing By', systemObjective: 'Clean REST contracts' },
      { roleCode: 'BuildErrorResolver', displayName: 'Build Error Resolver', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Auto-fixes C# compiler errors and Roslyn warnings.', competencies: ['Compiler Diagnostics', 'NuGet Resolution'], defaultModelTier: 'FastTier', icon: 'Wrench', defaultStatus: 'Standing By', systemObjective: 'Zero-warning builds' },
      { roleCode: 'RefactorCleaner', displayName: 'Refactor Cleaner', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Prunes dead code and de-sloppifies methods.', competencies: ['Dead Code Elimination', 'DRY'], defaultModelTier: 'StandardTier', icon: 'Trash2', defaultStatus: 'Standing By', systemObjective: 'Preserve code cleanliness' },
      { roleCode: 'CodeReviewer', displayName: 'Code Reviewer', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Blast-radius PR reviews and convention checks.', competencies: ['Blast Radius Analysis', 'Code Conventions'], defaultModelTier: 'ReasoningTier', icon: 'FileText', defaultStatus: 'Standing By', systemObjective: 'Maintain code quality' },
      { roleCode: 'SlaGuardian', displayName: 'SLA Guardian', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Monitors service uptime and SLA budget thresholds.', competencies: ['Uptime Monitoring', 'Latency Budgets'], defaultModelTier: 'FastTier', icon: 'ShieldAlert', defaultStatus: 'Standing By', systemObjective: '99.99% availability' },
      { roleCode: 'TelemetryEngineer', displayName: 'Telemetry Engineer', echelon: 'Engineering', category: 'Technical & Engineering', description: 'OpenTelemetry traces and Prometheus metric counters.', competencies: ['OpenTelemetry', 'Prometheus'], defaultModelTier: 'StandardTier', icon: 'BarChart', defaultStatus: 'Standing By', systemObjective: '100% observability' },
      { roleCode: 'ScraperEngineer', displayName: 'Scraper Engineer', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Headless browser automation and JSON extractors.', competencies: ['Headless Browsers', 'Proxy Rotation'], defaultModelTier: 'StandardTier', icon: 'DownloadCloud', defaultStatus: 'Standing By', systemObjective: 'Reliable data ingestion' },
      { roleCode: 'DockerSpecialist', displayName: 'Docker Specialist', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Chiseled container images and Dockerfiles.', competencies: ['Multi-Stage Dockerfiles', 'Alpine Hardening'], defaultModelTier: 'StandardTier', icon: 'Box', defaultStatus: 'Standing By', systemObjective: 'Minimal container images' },
      { roleCode: 'AspireOrchestrator', displayName: 'Aspire Orchestrator', echelon: 'Engineering', category: 'Technical & Engineering', description: '.NET Aspire AppHost service discovery.', competencies: ['.NET Aspire', 'Service Discovery'], defaultModelTier: 'StandardTier', icon: 'Layers', defaultStatus: 'Standing By', systemObjective: 'Distributed orchestration' },
      { roleCode: 'MobileCapacitorEngineer', displayName: 'Mobile Capacitor Engineer', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Ionic Capacitor plugins and Android WebViews.', competencies: ['Capacitor Plugins', 'Push Notifications'], defaultModelTier: 'StandardTier', icon: 'Smartphone', defaultStatus: 'Standing By', systemObjective: 'Seamless mobile bridges' },
      { roleCode: 'DnnAiSpecialist', displayName: 'DNN AI Specialist', echelon: 'Engineering', category: 'Technical & Engineering', description: 'ONNX runtimes, pgvector embeddings and semantic search.', competencies: ['Neural Networks', 'pgvector'], defaultModelTier: 'ReasoningTier', icon: 'Brain', defaultStatus: 'Standing By', systemObjective: 'Accurate neural search' },
      { roleCode: 'DisasterRecoverySpecialist', displayName: 'Disaster Recovery Specialist', echelon: 'Engineering', category: 'Technical & Engineering', description: 'Point-in-time recovery and database snapshots.', competencies: ['Backup Verification', 'Failover Runbooks'], defaultModelTier: 'ReasoningTier', icon: 'LifeBuoy', defaultStatus: 'Standing By', systemObjective: 'Zero data loss' },
      { roleCode: 'PromptEngineer', displayName: 'Prompt Engineer', echelon: 'Engineering', category: 'Technical & Engineering', description: 'In-context prompting and MCP tool schema enforcement.', competencies: ['Chain-of-Thought', 'Schema Enforcement'], defaultModelTier: 'StandardTier', icon: 'MessageSquare', defaultStatus: 'Standing By', systemObjective: 'Zero hallucination' }
    ];
  }

  private getSeedGoalTree(): GoalTreeUi {
    return {
      id: 'gt-88a29e41',
      tenantId: '00000000-0000-0000-0000-000000000001',
      executiveGoalText: 'Scale Monthly Cash Remittance by +35% with < 12% Return Rate',
      targetTimeframe: '90 Days',
      status: 'Active',
      overallAchievementPercentage: 42,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
      nodes: [
        {
          id: 'pillar-margin',
          parentId: null,
          title: 'Pillar 1: True Gross Margin & Remittance Reconciler',
          description: 'Audit courier COD remittance float and eliminate return fee leakage.',
          layerLevel: 1,
          echelon: 'Business',
          assignedRoleCode: 'FinancialMarginAuditor',
          assignedRoleName: 'Financial Margin Auditor',
          modelTier: 'ReasoningTier',
          prerequisiteTaskIds: [],
          status: 'InProgress',
          progressPercentage: 65,
          weight: 3.0
        },
        {
          id: 'pillar-logistics',
          parentId: null,
          title: 'Pillar 2: Carrier SLA & Multi-Hub Optimization',
          description: 'Deploy dynamic courier routing to enforce sub-12% delivery failure rate.',
          layerLevel: 1,
          echelon: 'Business',
          assignedRoleCode: 'LogisticsController',
          assignedRoleName: 'Logistics Controller',
          modelTier: 'StandardTier',
          prerequisiteTaskIds: [],
          status: 'InProgress',
          progressPercentage: 50,
          weight: 2.5
        },
        {
          id: 'pillar-architecture',
          parentId: null,
          title: 'Pillar 3: High-Concurrency Remittance Ledger Engine',
          description: 'Design idempotent webhook ingestion and spatial courier routing.',
          layerLevel: 1,
          echelon: 'Engineering',
          assignedRoleCode: 'SoftwareArchitect',
          assignedRoleName: 'Software Architect',
          modelTier: 'ReasoningTier',
          prerequisiteTaskIds: [],
          status: 'Completed',
          progressPercentage: 100,
          weight: 2.5,
          executionOutput: 'Architecture blueprint approved: Event-driven outbox pattern configured.'
        },
        {
          id: 'task-remittance-audit',
          parentId: 'pillar-margin',
          title: 'Daily Courier COD Remittance Variance Audit',
          description: 'Compare manifests against bank remittance deposits.',
          layerLevel: 2,
          echelon: 'Business',
          assignedRoleCode: 'FinancialMarginAuditor',
          assignedRoleName: 'Financial Margin Auditor',
          modelTier: 'ReasoningTier',
          prerequisiteTaskIds: ['pillar-margin'],
          status: 'InProgress',
          progressPercentage: 45,
          weight: 1.5
        },
        {
          id: 'task-adspend-rebalance',
          parentId: 'pillar-margin',
          title: 'Ad Spend ROAS Rebalancing & Negative Margin Pruning',
          description: 'Auto-trim Meta/Google ad spend on high-return SKUs.',
          layerLevel: 2,
          echelon: 'Business',
          assignedRoleCode: 'AdSpendOptimizer',
          assignedRoleName: 'Ad Spend Optimizer',
          modelTier: 'ReasoningTier',
          prerequisiteTaskIds: ['pillar-margin'],
          status: 'Completed',
          progressPercentage: 100,
          weight: 1.5,
          executionOutput: 'Reallocated $4,200 ad spend from negative ROAS campaigns.'
        },
        {
          id: 'task-outreach-confirmation',
          parentId: 'pillar-logistics',
          title: 'Automated WhatsApp Pre-Shipment COD Confirmation',
          description: 'Dispatch WhatsApp interactive buttons to confirm customer address.',
          layerLevel: 2,
          echelon: 'Business',
          assignedRoleCode: 'ColdOutreachSdr',
          assignedRoleName: 'Cold Outreach SDR',
          modelTier: 'StandardTier',
          prerequisiteTaskIds: ['pillar-logistics'],
          status: 'InProgress',
          progressPercentage: 55,
          weight: 1.2
        },
        {
          id: 'task-carrier-auto-reroute',
          parentId: 'pillar-logistics',
          title: 'Carrier Delivery SLA Breach Auto-Reroute',
          description: 'Monitor delivery attempts in real-time and switch delayed parcels.',
          layerLevel: 3,
          echelon: 'Engineering',
          assignedRoleCode: 'SlaGuardian',
          assignedRoleName: 'SLA Guardian',
          modelTier: 'FastTier',
          prerequisiteTaskIds: ['task-remittance-audit', 'task-outreach-confirmation'],
          status: 'Pending',
          progressPercentage: 0,
          weight: 1.0
        },
        {
          id: 'task-ledger-db-indexing',
          parentId: 'pillar-architecture',
          title: 'PostgreSQL Remittance Ledger Indexing & GiST Bounds',
          description: 'Implement BRIN and GiST spatial indexes for zero-contention settlement.',
          layerLevel: 2,
          echelon: 'Engineering',
          assignedRoleCode: 'DatabaseSpecialist',
          assignedRoleName: 'Database Specialist',
          modelTier: 'ReasoningTier',
          prerequisiteTaskIds: ['pillar-architecture'],
          status: 'Completed',
          progressPercentage: 100,
          weight: 1.5,
          executionOutput: 'GiST spatial indexes created; latency dropped by 74%.'
        },
        {
          id: 'task-webhook-worker',
          parentId: 'pillar-architecture',
          title: 'High-Throughput Carrier Webhook Ingestion Worker',
          description: 'Build lock-free Redis-backed background worker.',
          layerLevel: 3,
          echelon: 'Engineering',
          assignedRoleCode: 'BackendDeveloper',
          assignedRoleName: 'Backend Developer',
          modelTier: 'StandardTier',
          prerequisiteTaskIds: ['task-ledger-db-indexing'],
          status: 'InProgress',
          progressPercentage: 40,
          weight: 1.5
        },
        {
          id: 'task-regression-verification',
          parentId: 'pillar-architecture',
          title: 'End-to-End Remittance & Settlement xUnit Verification',
          description: 'Automated parallel xUnit tests validating zero-balance invariants.',
          layerLevel: 3,
          echelon: 'Engineering',
          assignedRoleCode: 'TestEngineer',
          assignedRoleName: 'Test Engineer',
          modelTier: 'StandardTier',
          prerequisiteTaskIds: ['task-webhook-worker', 'task-carrier-auto-reroute'],
          status: 'Pending',
          progressPercentage: 0,
          weight: 1.0
        }
      ]
    };
  }

  private getSeedActivityLogs(): AgentActivityLogUi[] {
    return [
      { id: 'log-1', tenantId: '00000000-0000-0000-0000-000000000001', goalTreeId: 'gt-88a29e41', nodeId: 'pillar-margin', roleCode: 'FinancialMarginAuditor', roleName: 'Financial Margin Auditor', actionSummary: '[FinancialMarginAuditor] Audited courier COD remittance float: Found 3 reconciling items, margin verified at 32.4%', modelTierUsed: 'ReasoningTier', tokensConsumed: 1120, latencyMs: 680, timestampUtc: new Date().toISOString() },
      { id: 'log-2', tenantId: '00000000-0000-0000-0000-000000000001', goalTreeId: 'gt-88a29e41', nodeId: 'task-outreach-confirmation', roleCode: 'ColdOutreachSdr', roleName: 'Cold Outreach SDR', actionSummary: '[ColdOutreachSdr] Dispatched 140 pre-shipment WhatsApp verification messages. 91% confirmed.', modelTierUsed: 'StandardTier', tokensConsumed: 640, latencyMs: 310, timestampUtc: new Date(Date.now() - 30000).toISOString() },
      { id: 'log-3', tenantId: '00000000-0000-0000-0000-000000000001', goalTreeId: 'gt-88a29e41', nodeId: 'pillar-architecture', roleCode: 'SoftwareArchitect', roleName: 'Software Architect', actionSummary: '[SoftwareArchitect] Completed architecture audit: Transaction isolation level verified with zero deadlocks.', modelTierUsed: 'ReasoningTier', tokensConsumed: 1450, latencyMs: 820, timestampUtc: new Date(Date.now() - 90000).toISOString() },
      { id: 'log-4', tenantId: '00000000-0000-0000-0000-000000000001', goalTreeId: 'gt-88a29e41', nodeId: 'task-carrier-auto-reroute', roleCode: 'SlaGuardian', roleName: 'SLA Guardian', actionSummary: '[SlaGuardian] Sweep complete: All carrier webhook response latencies within 45ms P99 budget.', modelTierUsed: 'FastTier', tokensConsumed: 220, latencyMs: 85, timestampUtc: new Date(Date.now() - 150000).toISOString() }
    ];
  }
}
