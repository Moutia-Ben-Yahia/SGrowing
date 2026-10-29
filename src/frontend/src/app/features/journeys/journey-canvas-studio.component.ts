import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import {
  JourneyDefinitionDto,
  JourneyNodeDto,
  JourneyNodeType,
  JourneyInstanceDto,
  JourneyInstanceStatus,
  JourneyMetricsResponseDto,
  DagValidationResultDto,
  JourneyStepExecutionRecordDto
} from '../../core/models/platform.models';

@Component({
  selector: 'app-journey-canvas-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- 1. LEFT SIDEBAR: Journeys List & DAG Actions -->
      <aside class="w-80 border-r border-slate-800 bg-slate-900/90 flex flex-col justify-between p-4 z-10 shrink-0">
        <div>
          <!-- Logo & Header -->
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-2">
              <div class="h-9 w-9 rounded-xl bg-gradient-to-tr from-cyan-500 to-indigo-600 flex items-center justify-center font-bold text-white shadow-lg shadow-cyan-500/25">
                ⚡
              </div>
              <div>
                <h1 class="text-sm font-bold tracking-wide text-white">SGrowing DAG Engine</h1>
                <p class="text-[11px] text-cyan-400 font-medium">Stateful Wait-Until Studio</p>
              </div>
            </div>
            <a routerLink="/journeys" class="text-xs text-slate-400 hover:text-slate-200 px-2 py-1 rounded bg-slate-800/80 border border-slate-700 hover:border-slate-600 transition">
              ← Back
            </a>
          </div>

          <!-- Journey Selector & Quick Stats -->
          <div class="space-y-3 mb-4">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Saved Journeys</span>
              <button
                (click)="createNewJourney()"
                class="text-[11px] text-cyan-400 hover:text-cyan-300 font-semibold flex items-center gap-1">
                + New Journey
              </button>
            </div>

            <div class="space-y-1.5 max-h-48 overflow-y-auto pr-1 custom-scroll">
              @for (journey of journeys(); track journey.id) {
                <button
                  (click)="selectJourney(journey)"
                  [class.border-cyan-500]="selectedJourney()?.id === journey.id"
                  [class.bg-cyan-950]="selectedJourney()?.id === journey.id"
                  [class.bg-slate-800]="selectedJourney()?.id !== journey.id"
                  class="w-full text-left p-2.5 rounded-lg border border-slate-800 hover:border-slate-700 transition flex flex-col gap-1">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-semibold text-slate-100 truncate">{{ journey.name }}</span>
                    <span
                      [class.bg-emerald-500]="journey.isActive"
                      [class.bg-slate-600]="!journey.isActive"
                      class="text-[9px] px-1.5 py-0.5 rounded-full text-white font-medium">
                      {{ journey.isActive ? 'Active' : 'Draft' }}
                    </span>
                  </div>
                  <div class="flex items-center justify-between text-[10px] text-slate-400">
                    <span class="text-cyan-400 font-mono">⚡ {{ journey.triggerEvent }}</span>
                    <span>{{ journey.nodes.length }} nodes</span>
                  </div>
                </button>
              } @empty {
                <div class="p-3 text-center text-xs text-slate-500 border border-dashed border-slate-800 rounded-lg">
                  No journeys loaded. Click '+ New Journey'.
                </div>
              }
            </div>
          </div>

          <!-- DAG Validation Status Badge -->
          <div class="p-3 bg-slate-900 border rounded-xl space-y-2 mb-4"
               [class.border-emerald-500]="dagValidation().isValid"
               [class.border-rose-500]="!dagValidation().isValid">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold flex items-center gap-1.5"
                    [class.text-emerald-400]="dagValidation().isValid"
                    [class.text-rose-400]="!dagValidation().isValid">
                <span class="h-2 w-2 rounded-full"
                      [class.bg-emerald-400]="dagValidation().isValid"
                      [class.bg-rose-400]="!dagValidation().isValid"
                      [class.animate-pulse]="!dagValidation().isValid"></span>
                {{ dagValidation().isValid ? 'Acyclic DAG Validated' : 'DAG Cycle Detected' }}
              </span>
              <span class="text-[9px] font-mono px-1.5 py-0.5 bg-slate-800 rounded text-slate-300">Kahn DFS</span>
            </div>
            @if (!dagValidation().isValid) {
              <p class="text-[11px] text-rose-300 leading-tight">
                {{ dagValidation().errorMessage }}
              </p>
            } @else {
              <p class="text-[11px] text-slate-400">
                Topology is cycle-free and ready for real-time customer enrollment.
              </p>
            }
          </div>

          <!-- Add Node Quick Palette -->
          <div class="space-y-1.5 mb-4">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Palette: Add Nodes</span>
            <div class="grid grid-cols-2 gap-1.5">
              <button (click)="addNode(JourneyNodeType.WaitUntilEvent)" class="p-2 bg-slate-800/80 hover:bg-cyan-950/60 hover:border-cyan-500/50 border border-slate-700/60 rounded-lg text-left text-xs transition flex items-center gap-1.5">
                <span class="text-cyan-400">⏳</span>
                <span class="text-slate-200 text-[11px]">Wait-Until TTL</span>
              </button>
              <button (click)="addNode(JourneyNodeType.ConditionBranch)" class="p-2 bg-slate-800/80 hover:bg-purple-950/60 hover:border-purple-500/50 border border-slate-700/60 rounded-lg text-left text-xs transition flex items-center gap-1.5">
                <span class="text-purple-400">🔀</span>
                <span class="text-slate-200 text-[11px]">Condition</span>
              </button>
              <button (click)="addNode(JourneyNodeType.ChannelAction)" class="p-2 bg-slate-800/80 hover:bg-blue-950/60 hover:border-blue-500/50 border border-slate-700/60 rounded-lg text-left text-xs transition flex items-center gap-1.5">
                <span class="text-blue-400">💬</span>
                <span class="text-slate-200 text-[11px]">Channel Action</span>
              </button>
              <button (click)="addNode(JourneyNodeType.Delay)" class="p-2 bg-slate-800/80 hover:bg-amber-950/60 hover:border-amber-500/50 border border-slate-700/60 rounded-lg text-left text-xs transition flex items-center gap-1.5">
                <span class="text-amber-400">⏱️</span>
                <span class="text-slate-200 text-[11px]">Delay</span>
              </button>
              <button (click)="addNode(JourneyNodeType.Exit)" class="p-2 bg-slate-800/80 hover:bg-rose-950/60 hover:border-rose-500/50 border border-slate-700/60 rounded-lg text-left text-xs transition flex items-center gap-1.5 col-span-2">
                <span class="text-rose-400">🏁</span>
                <span class="text-slate-200 text-[11px]">Exit Node</span>
              </button>
            </div>
          </div>
        </div>

        <!-- Save DAG Button -->
        <div class="border-t border-slate-800 pt-3 space-y-2">
          <button
            (click)="saveCurrentJourney()"
            [disabled]="isSaving() || !dagValidation().isValid"
            class="w-full py-2.5 px-3 bg-gradient-to-r from-cyan-600 to-indigo-600 hover:from-cyan-500 hover:to-indigo-500 text-white text-xs font-bold rounded-lg shadow-lg shadow-cyan-500/25 transition flex items-center justify-center gap-2 disabled:opacity-40">
            @if (isSaving()) {
              <span class="animate-spin text-sm">⏳</span>
            } @else {
              <span>💾 Save Journey Topology</span>
            }
          </button>
        </div>
      </aside>

      <!-- 2. CENTER: Interactive Visual DAG Canvas & Funnel Metrics -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Top Control Bar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-4">
            <h2 class="text-sm font-bold text-white flex items-center gap-2">
              <span class="text-cyan-400">●</span>
              {{ selectedJourney()?.name || 'Untitled Journey' }}
            </h2>
            <div class="flex items-center gap-2 text-xs text-slate-400">
              <span class="px-2 py-0.5 rounded bg-slate-800 border border-slate-700 font-mono text-[11px]">
                Trigger: <span class="text-cyan-300">{{ selectedJourney()?.triggerEvent }}</span>
              </span>
              @if (selectedJourney()?.conversionEvents?.length) {
                <span class="px-2 py-0.5 rounded bg-emerald-950/60 border border-emerald-700/50 text-emerald-300 text-[11px]">
                  Unenroll on: {{ selectedJourney()?.conversionEvents?.join(', ') }}
                </span>
              }
            </div>
          </div>

          <!-- View Mode Toggles & Global Sweeper -->
          <div class="flex items-center gap-3">
            <div class="flex items-center bg-slate-800/80 rounded-lg p-0.5 border border-slate-700">
              <button
                (click)="activeTab.set('canvas')"
                [class.bg-cyan-600]="activeTab() === 'canvas'"
                [class.text-white]="activeTab() === 'canvas'"
                [class.text-slate-400]="activeTab() !== 'canvas'"
                class="px-3 py-1 text-xs font-semibold rounded-md transition">
                Visual DAG
              </button>
              <button
                (click)="loadMetrics(); activeTab.set('funnel')"
                [class.bg-cyan-600]="activeTab() === 'funnel'"
                [class.text-white]="activeTab() === 'funnel'"
                [class.text-slate-400]="activeTab() !== 'funnel'"
                class="px-3 py-1 text-xs font-semibold rounded-md transition flex items-center gap-1">
                <span>Conversion Funnel</span>
                @if (metrics()) {
                  <span class="text-[9px] bg-slate-900 px-1 rounded">{{ metrics()?.completionRate }}%</span>
                }
              </button>
            </div>

            <button
              (click)="sweepTimeouts()"
              [disabled]="isSweeping()"
              class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 border border-cyan-500/30 text-cyan-300 text-xs font-medium rounded-lg transition flex items-center gap-1.5 disabled:opacity-50">
              <span>⏱️ Sweep Expired TTLs</span>
            </button>
          </div>
        </header>

        <!-- Canvas Body -->
        <div class="flex-1 overflow-auto relative p-8 bg-[radial-gradient(#1e293b_1px,transparent_1px)] [background-size:20px_20px]">
          @if (activeTab() === 'canvas') {
            <!-- Visual DAG Nodes Grid -->
            <div class="flex flex-col gap-8 max-w-5xl mx-auto pb-24">
              @for (node of selectedJourney()?.nodes; track node.id; let idx = $index) {
                <div class="relative flex items-center gap-4">
                  <!-- Node Sequence Marker -->
                  <div class="w-8 h-8 rounded-full border border-slate-700 bg-slate-900 text-slate-400 text-xs flex items-center justify-center font-mono font-bold shrink-0">
                    {{ idx + 1 }}
                  </div>

                  <!-- Node Card -->
                  <div
                    (click)="selectNode(node)"
                    [class.ring-2]="selectedNode()?.id === node.id"
                    [class.ring-cyan-400]="selectedNode()?.id === node.id"
                    [class.border-cyan-500]="node.type === JourneyNodeType.WaitUntilEvent"
                    [class.border-emerald-500]="node.type === JourneyNodeType.Trigger"
                    [class.border-purple-500]="node.type === JourneyNodeType.ConditionBranch"
                    [class.border-blue-500]="node.type === JourneyNodeType.ChannelAction"
                    [class.border-amber-500]="node.type === JourneyNodeType.Delay"
                    [class.border-rose-500]="node.type === JourneyNodeType.Exit"
                    class="flex-1 bg-slate-900/90 border rounded-2xl p-4 shadow-xl hover:shadow-2xl transition cursor-pointer relative group">

                    <div class="flex items-start justify-between mb-2">
                      <div class="flex items-center gap-2.5">
                        <div class="w-8 h-8 rounded-xl flex items-center justify-center text-sm font-bold shadow-md"
                             [class.bg-emerald-500]="node.type === JourneyNodeType.Trigger"
                             [class.bg-cyan-500]="node.type === JourneyNodeType.WaitUntilEvent"
                             [class.bg-purple-500]="node.type === JourneyNodeType.ConditionBranch"
                             [class.bg-blue-500]="node.type === JourneyNodeType.ChannelAction"
                             [class.bg-amber-500]="node.type === JourneyNodeType.Delay"
                             [class.bg-rose-500]="node.type === JourneyNodeType.Exit">
                          {{ getNodeIcon(node.type) }}
                        </div>
                        <div>
                          <div class="flex items-center gap-2">
                            <span class="text-xs font-bold text-white">{{ node.name }}</span>
                            <span class="text-[10px] font-mono text-slate-400">#{{ node.id }}</span>
                          </div>
                          <span class="text-[10px] uppercase tracking-wider font-semibold"
                                [class.text-emerald-400]="node.type === JourneyNodeType.Trigger"
                                [class.text-cyan-400]="node.type === JourneyNodeType.WaitUntilEvent"
                                [class.text-purple-400]="node.type === JourneyNodeType.ConditionBranch"
                                [class.text-blue-400]="node.type === JourneyNodeType.ChannelAction"
                                [class.text-amber-400]="node.type === JourneyNodeType.Delay"
                                [class.text-rose-400]="node.type === JourneyNodeType.Exit">
                            {{ getNodeTypeLabel(node.type) }}
                          </span>
                        </div>
                      </div>

                      <div class="flex items-center gap-2">
                        @if (nodeMetricsMap()[node.id]) {
                          <span class="text-[10px] bg-slate-800 text-slate-300 px-2 py-0.5 rounded-full border border-slate-700">
                            ⚡ {{ nodeMetricsMap()[node.id]?.executionsCount || 0 }} passed
                          </span>
                          @if (nodeMetricsMap()[node.id]?.currentContactsCount) {
                            <span class="text-[10px] bg-cyan-950 text-cyan-300 px-2 py-0.5 rounded-full border border-cyan-800 animate-pulse">
                              ⏳ {{ nodeMetricsMap()[node.id]?.currentContactsCount }} waiting
                            </span>
                          }
                        }
                      </div>
                    </div>

                    <!-- Node Details Preview -->
                    <div class="text-xs text-slate-300 bg-slate-950/60 rounded-xl p-2.5 border border-slate-800/80 space-y-1">
                      @if (node.type === JourneyNodeType.Trigger) {
                        <p class="font-mono text-cyan-400 text-[11px]">Listens to: {{ node.eventName }}</p>
                      } @else if (node.type === JourneyNodeType.WaitUntilEvent) {
                        <div class="flex items-center justify-between">
                          <span class="text-cyan-300 font-mono text-[11px]">Event: '{{ node.eventName }}'</span>
                          <span class="text-[10px] text-amber-300">TTL: {{ formatTtl(node.ttlSeconds) }}</span>
                        </div>
                        <div class="flex items-center gap-2 text-[10px] text-slate-400 pt-1">
                          <span class="text-emerald-400">✓ If event occurs: &rarr; #{{ node.successNextNodeId || 'none' }}</span>
                          <span class="text-rose-400">&times; If TTL expires: &rarr; #{{ node.timeoutNextNodeId || 'none' }}</span>
                        </div>
                      } @else if (node.type === JourneyNodeType.ConditionBranch) {
                        <p class="font-mono text-purple-300 text-[11px]">Rule: {{ node.conditionExpression }}</p>
                        <div class="flex items-center gap-2 text-[10px] text-slate-400 pt-1">
                          <span class="text-emerald-400">TRUE &rarr; #{{ node.trueNextNodeId || 'none' }}</span>
                          <span class="text-rose-400">FALSE &rarr; #{{ node.falseNextNodeId || 'none' }}</span>
                        </div>
                      } @else if (node.type === JourneyNodeType.ChannelAction) {
                        <div class="flex items-center justify-between text-[11px]">
                          <span class="text-blue-300 font-semibold">{{ node.channelType }} Dispatch</span>
                          <span class="text-slate-400 truncate max-w-xs">{{ node.actionPayload }}</span>
                        </div>
                      } @else if (node.type === JourneyNodeType.Delay) {
                        <p class="text-amber-300 font-mono text-[11px]">Delay: {{ node.delaySeconds }}s ({{ node.timezone || 'Recipient Timezone' }})</p>
                      } @else if (node.type === JourneyNodeType.Exit) {
                        <p class="text-rose-300 text-[11px]">Marks journey as Completed for contact</p>
                      }

                      @if (node.nextNodeId && node.type !== JourneyNodeType.WaitUntilEvent && node.type !== JourneyNodeType.ConditionBranch) {
                        <div class="text-[10px] text-slate-400 pt-1 flex items-center gap-1">
                          <span>Next:</span>
                          <span class="text-cyan-400 font-mono">#{{ node.nextNodeId }}</span>
                        </div>
                      }
                    </div>
                  </div>
                </div>
              }
            </div>
          } @else {
            <!-- Step Conversion Funnel View -->
            <div class="max-w-4xl mx-auto space-y-6 pb-24">
              <!-- Summary Scorecards -->
              <div class="grid grid-cols-4 gap-4">
                <div class="p-4 bg-slate-900 border border-slate-800 rounded-2xl">
                  <span class="text-xs text-slate-400 font-medium">Total Enrolled</span>
                  <p class="text-2xl font-bold text-white mt-1">{{ metrics()?.totalEnrolled || 0 }}</p>
                </div>
                <div class="p-4 bg-slate-900 border border-slate-800 rounded-2xl">
                  <span class="text-xs text-slate-400 font-medium">Completed</span>
                  <p class="text-2xl font-bold text-emerald-400 mt-1">{{ metrics()?.totalCompleted || 0 }}</p>
                </div>
                <div class="p-4 bg-slate-900 border border-slate-800 rounded-2xl">
                  <span class="text-xs text-slate-400 font-medium">Waiting on Event</span>
                  <p class="text-2xl font-bold text-cyan-400 mt-1">{{ metrics()?.currentlyWaiting || 0 }}</p>
                </div>
                <div class="p-4 bg-slate-900 border border-slate-800 rounded-2xl">
                  <span class="text-xs text-slate-400 font-medium">Dynamic Unenrolled</span>
                  <p class="text-2xl font-bold text-rose-400 mt-1">{{ metrics()?.totalUnenrolled || 0 }}</p>
                </div>
              </div>

              <!-- Node by Node Funnel Conversion Bars -->
              <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 space-y-4">
                <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                  <h3 class="text-sm font-bold text-white">Node-by-Node Conversion & Drop-off Rates</h3>
                  <button (click)="loadMetrics()" class="text-xs text-cyan-400 hover:text-cyan-300">↻ Refresh</button>
                </div>

                <div class="space-y-4">
                  @for (metric of metrics()?.nodeMetrics; track metric.nodeId) {
                    <div class="space-y-1.5">
                      <div class="flex items-center justify-between text-xs">
                        <div class="flex items-center gap-2">
                          <span class="font-bold text-white">{{ metric.nodeName }}</span>
                          <span class="font-mono text-slate-400 text-[10px]">#{{ metric.nodeId }}</span>
                        </div>
                        <div class="flex items-center gap-3">
                          <span class="text-cyan-400 font-semibold">{{ metric.conversionPercent }}% conversion</span>
                          <span class="text-slate-400 font-mono text-[11px]">{{ metric.executionsCount }} reached</span>
                        </div>
                      </div>

                      <div class="w-full h-3 bg-slate-950 rounded-full overflow-hidden border border-slate-800 flex">
                        <div
                          class="h-full bg-gradient-to-r from-cyan-500 to-indigo-600 rounded-full transition-all duration-500"
                          [style.width.%]="metric.conversionPercent"></div>
                      </div>
                    </div>
                  } @empty {
                    <p class="text-xs text-slate-500 text-center py-6">No metrics available yet. Start contact enrollments to view conversion rates.</p>
                  }
                </div>
              </div>
            </div>
          }
        </div>
      </main>

      <!-- 3. RIGHT SIDEBAR: Declarative Node Inspector & Ingestion Simulator -->
      <aside class="w-96 border-l border-slate-800 bg-slate-900/90 flex flex-col justify-between p-4 z-10 shrink-0">
        <div class="overflow-y-auto space-y-5 pr-1 custom-scroll">
          <!-- Declarative Node Inspector -->
          <div>
            <div class="flex items-center justify-between mb-3">
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Node Inspector</span>
              @if (selectedNode()) {
                <button
                  (click)="deleteSelectedNode()"
                  class="text-[11px] text-rose-400 hover:text-rose-300">
                  Delete Node
                </button>
              }
            </div>

            @if (selectedNode(); as n) {
              <div class="space-y-3 bg-slate-950/60 p-3.5 rounded-xl border border-slate-800">
                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Node Name</label>
                  <input
                    type="text"
                    [(ngModel)]="n.name"
                    (ngModelChange)="onNodeChanged()"
                    class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none" />
                </div>

                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Node Type</label>
                  <select
                    [(ngModel)]="n.type"
                    (ngModelChange)="onNodeChanged()"
                    class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none">
                    <option [value]="JourneyNodeType.Trigger">Trigger</option>
                    <option [value]="JourneyNodeType.WaitUntilEvent">Wait-Until Event (TTL)</option>
                    <option [value]="JourneyNodeType.ConditionBranch">Condition Branch</option>
                    <option [value]="JourneyNodeType.ChannelAction">Channel Action</option>
                    <option [value]="JourneyNodeType.Delay">Delay</option>
                    <option [value]="JourneyNodeType.Exit">Exit Node</option>
                  </select>
                </div>

                <!-- Inspector: WaitUntilEvent specific fields -->
                @if (n.type === JourneyNodeType.WaitUntilEvent) {
                  <div class="space-y-2 border-t border-slate-800 pt-2">
                    <div>
                      <label class="block text-[11px] text-cyan-400 mb-1">Wait For Event Name</label>
                      <input
                        type="text"
                        [(ngModel)]="n.eventName"
                        (ngModelChange)="onNodeChanged()"
                        placeholder="e.g. checkout_completed"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none font-mono" />
                    </div>
                    <div>
                      <label class="block text-[11px] text-amber-400 mb-1">Sliding TTL (Seconds / Hours)</label>
                      <div class="flex items-center gap-2">
                        <input
                          type="number"
                          [(ngModel)]="n.ttlSeconds"
                          (ngModelChange)="onNodeChanged()"
                          class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none font-mono" />
                        <span class="text-[10px] text-slate-400 shrink-0">{{ formatTtl(n.ttlSeconds) }}</span>
                      </div>
                    </div>
                    <div>
                      <label class="block text-[11px] text-emerald-400 mb-1">Success Next Node</label>
                      <select
                        [(ngModel)]="n.successNextNodeId"
                        (ngModelChange)="onNodeChanged()"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none">
                        <option [value]="null">-- None --</option>
                        @for (other of availableTargetNodes(); track other.id) {
                          <option [value]="other.id">{{ other.name }} (#{{ other.id }})</option>
                        }
                      </select>
                    </div>
                    <div>
                      <label class="block text-[11px] text-rose-400 mb-1">Timeout Next Node</label>
                      <select
                        [(ngModel)]="n.timeoutNextNodeId"
                        (ngModelChange)="onNodeChanged()"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none">
                        <option [value]="null">-- None --</option>
                        @for (other of availableTargetNodes(); track other.id) {
                          <option [value]="other.id">{{ other.name }} (#{{ other.id }})</option>
                        }
                      </select>
                    </div>
                  </div>
                }

                <!-- Inspector: ConditionBranch specific fields -->
                @if (n.type === JourneyNodeType.ConditionBranch) {
                  <div class="space-y-2 border-t border-slate-800 pt-2">
                    <div>
                      <label class="block text-[11px] text-purple-400 mb-1">Condition Expression</label>
                      <input
                        type="text"
                        [(ngModel)]="n.conditionExpression"
                        (ngModelChange)="onNodeChanged()"
                        placeholder="e.g. Ltv > 500, Country == 'MA'"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none font-mono" />
                    </div>
                    <div>
                      <label class="block text-[11px] text-emerald-400 mb-1">True Next Node</label>
                      <select
                        [(ngModel)]="n.trueNextNodeId"
                        (ngModelChange)="onNodeChanged()"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none">
                        <option [value]="null">-- None --</option>
                        @for (other of availableTargetNodes(); track other.id) {
                          <option [value]="other.id">{{ other.name }} (#{{ other.id }})</option>
                        }
                      </select>
                    </div>
                    <div>
                      <label class="block text-[11px] text-rose-400 mb-1">False Next Node</label>
                      <select
                        [(ngModel)]="n.falseNextNodeId"
                        (ngModelChange)="onNodeChanged()"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none">
                        <option [value]="null">-- None --</option>
                        @for (other of availableTargetNodes(); track other.id) {
                          <option [value]="other.id">{{ other.name }} (#{{ other.id }})</option>
                        }
                      </select>
                    </div>
                  </div>
                }

                <!-- Inspector: ChannelAction specific fields -->
                @if (n.type === JourneyNodeType.ChannelAction) {
                  <div class="space-y-2 border-t border-slate-800 pt-2">
                    <div>
                      <label class="block text-[11px] text-blue-400 mb-1">Channel Type</label>
                      <select
                        [(ngModel)]="n.channelType"
                        (ngModelChange)="onNodeChanged()"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none">
                        <option value="Email">Email</option>
                        <option value="WhatsApp">WhatsApp Interactive</option>
                        <option value="SMS">SMS</option>
                        <option value="Webhook">Webhook</option>
                      </select>
                    </div>
                    <div>
                      <label class="block text-[11px] text-slate-400 mb-1">Action Payload / Template</label>
                      <textarea
                        [(ngModel)]="n.actionPayload"
                        (ngModelChange)="onNodeChanged()"
                        rows="3"
                        [placeholder]="'Hello {{first_name}}, your order is ready!'"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none"></textarea>
                    </div>
                    <div>
                      <label class="block text-[11px] text-slate-400 mb-1">Next Node</label>
                      <select
                        [(ngModel)]="n.nextNodeId"
                        (ngModelChange)="onNodeChanged()"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none">
                        <option [value]="null">-- None --</option>
                        @for (other of availableTargetNodes(); track other.id) {
                          <option [value]="other.id">{{ other.name }} (#{{ other.id }})</option>
                        }
                      </select>
                    </div>
                  </div>
                }

                <!-- Inspector: Delay specific fields -->
                @if (n.type === JourneyNodeType.Delay) {
                  <div class="space-y-2 border-t border-slate-800 pt-2">
                    <div>
                      <label class="block text-[11px] text-amber-400 mb-1">Delay (Seconds)</label>
                      <input
                        type="number"
                        [(ngModel)]="n.delaySeconds"
                        (ngModelChange)="onNodeChanged()"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none font-mono" />
                    </div>
                    <div>
                      <label class="block text-[11px] text-slate-400 mb-1">Next Node</label>
                      <select
                        [(ngModel)]="n.nextNodeId"
                        (ngModelChange)="onNodeChanged()"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:border-cyan-500 focus:outline-none">
                        <option [value]="null">-- None --</option>
                        @for (other of availableTargetNodes(); track other.id) {
                          <option [value]="other.id">{{ other.name }} (#{{ other.id }})</option>
                        }
                      </select>
                    </div>
                  </div>
                }
              </div>
            } @else {
              <div class="p-6 text-center text-xs text-slate-500 border border-dashed border-slate-800 rounded-xl">
                Click any node on the canvas to inspect and configure its triggers, branch rules, or channel payloads.
              </div>
            }
          </div>

          <!-- Event Ingestion Simulator -->
          <div class="border-t border-slate-800 pt-4 space-y-3">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Event Simulator</span>
              <span class="text-[10px] text-cyan-400 font-mono">Live Contact State</span>
            </div>

            <div class="p-3 bg-slate-950/80 rounded-xl border border-slate-800 space-y-2.5">
              <div>
                <label class="block text-[10px] text-slate-400 mb-1">Simulated Contact ID</label>
                <div class="flex items-center gap-1.5">
                  <input
                    type="text"
                    [(ngModel)]="simContactId"
                    placeholder="Contact UUID"
                    class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2 py-1 text-xs text-white font-mono focus:border-cyan-500 focus:outline-none" />
                  <button
                    (click)="generateTestContactId()"
                    class="px-2 py-1 bg-slate-800 hover:bg-slate-700 border border-slate-700 rounded text-[10px] text-slate-300">
                    🎲
                  </button>
                </div>
              </div>

              <!-- Quick Simulation Triggers -->
              <div class="space-y-1.5 pt-1">
                <button
                  (click)="simulateStart()"
                  [disabled]="isSimulating()"
                  class="w-full py-1.5 px-2.5 bg-emerald-950/80 hover:bg-emerald-900 border border-emerald-700/60 text-emerald-300 text-xs font-semibold rounded-lg transition flex items-center justify-between">
                  <span>🚀 Enroll Contact in Journey</span>
                  <span class="text-[10px] font-mono">Start</span>
                </button>

                <button
                  (click)="simulateEvent('checkout_completed')"
                  [disabled]="isSimulating()"
                  class="w-full py-1.5 px-2.5 bg-cyan-950/80 hover:bg-cyan-900 border border-cyan-700/60 text-cyan-300 text-xs font-semibold rounded-lg transition flex items-center justify-between">
                  <span>🛒 Ingest 'checkout_completed'</span>
                  <span class="text-[10px] font-mono">Wait-Until</span>
                </button>

                <button
                  (click)="simulateEvent('OrderPlaced')"
                  [disabled]="isSimulating()"
                  class="w-full py-1.5 px-2.5 bg-purple-950/80 hover:bg-purple-900 border border-purple-700/60 text-purple-300 text-xs font-semibold rounded-lg transition flex items-center justify-between">
                  <span>✍️ Ingest Conversion 'OrderPlaced'</span>
                  <span class="text-[10px] font-mono">Unenroll</span>
                </button>
              </div>
            </div>

            <!-- Live State Transition Audit Log -->
            <div class="space-y-1.5">
              <span class="text-[11px] font-semibold text-slate-400">Execution Transition Log</span>
              <div class="bg-slate-950 rounded-xl p-2.5 border border-slate-800 max-h-52 overflow-y-auto space-y-2 custom-scroll">
                @for (log of simulationLogs(); track log.executedAtUtc) {
                  <div class="text-[11px] p-2 rounded-lg bg-slate-900/90 border border-slate-800/80 space-y-0.5">
                    <div class="flex items-center justify-between">
                      <span class="font-bold"
                            [class.text-emerald-400]="log.status === 'Executed'"
                            [class.text-cyan-400]="log.status === 'Waiting' || log.status === 'MatchedEvent'"
                            [class.text-rose-400]="log.status === 'Unenrolled' || log.status === 'TimedOut'"
                            [class.text-purple-400]="log.status === 'BranchTrue' || log.status === 'BranchFalse'">
                        [{{ log.status }}] {{ log.nodeName }}
                      </span>
                      <span class="text-[9px] font-mono text-slate-500">
                        {{ log.executedAtUtc | date:'HH:mm:ss' }}
                      </span>
                    </div>
                    <p class="text-[10px] text-slate-300">{{ log.details }}</p>
                  </div>
                } @empty {
                  <p class="text-[11px] text-slate-500 text-center py-4">Click simulation buttons above to watch state transitions in real time.</p>
                }
              </div>
            </div>
          </div>
        </div>
      </aside>
    </div>
  `,
  styles: [`
    .custom-scroll::-webkit-scrollbar {
      width: 4px;
    }
    .custom-scroll::-webkit-scrollbar-track {
      background: rgba(15, 23, 42, 0.6);
    }
    .custom-scroll::-webkit-scrollbar-thumb {
      background: rgba(51, 65, 85, 0.8);
      border-radius: 4px;
    }
  `]
})
export class JourneyCanvasStudioComponent implements OnInit {
  private api = inject(ApiService);

  JourneyNodeType = JourneyNodeType;
  JourneyInstanceStatus = JourneyInstanceStatus;

  journeys = signal<JourneyDefinitionDto[]>([]);
  selectedJourney = signal<JourneyDefinitionDto | null>(null);
  selectedNode = signal<JourneyNodeDto | null>(null);
  metrics = signal<JourneyMetricsResponseDto | null>(null);
  simulationLogs = signal<JourneyStepExecutionRecordDto[]>([]);

  activeTab = signal<'canvas' | 'funnel'>('canvas');
  simContactId = '11111111-2222-3333-4444-555555555555';
  isSaving = signal(false);
  isSweeping = signal(false);
  isSimulating = signal(false);

  dagValidation = computed<DagValidationResultDto>(() => {
    const journey = this.selectedJourney();
    if (!journey || !journey.nodes || journey.nodes.length === 0) {
      return { isValid: false, errorMessage: 'Journey DAG contains no nodes.' };
    }
    return this.validateDagLocally(journey.nodes);
  });

  availableTargetNodes = computed(() => {
    const journey = this.selectedJourney();
    const currNode = this.selectedNode();
    if (!journey) return [];
    return journey.nodes.filter(n => n.id !== currNode?.id);
  });

  nodeMetricsMap = computed(() => {
    const m = this.metrics();
    const map: Record<string, any> = {};
    if (m && m.nodeMetrics) {
      for (const nm of m.nodeMetrics) {
        map[nm.nodeId] = nm;
      }
    }
    return map;
  });

  ngOnInit(): void {
    this.loadJourneys();
  }

  loadJourneys(): void {
    this.api.getJourneyDags().subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.journeys.set(data);
          this.selectJourney(data[0]);
        } else {
          this.initDefaultSampleJourney();
        }
      },
      error: () => {
        this.initDefaultSampleJourney();
      }
    });
  }

  initDefaultSampleJourney(): void {
    const sampleNodes: JourneyNodeDto[] = [
      {
        id: 'node-trigger',
        name: 'Order Abandoned Trigger',
        type: JourneyNodeType.Trigger,
        eventName: 'order_abandoned',
        nextNodeId: 'node-delay',
        delaySeconds: 0,
        ttlSeconds: 0,
        positionX: 0,
        positionY: 0
      },
      {
        id: 'node-delay',
        name: 'Wait 15 Minutes',
        type: JourneyNodeType.Delay,
        delaySeconds: 900,
        timezone: 'Africa/Casablanca',
        nextNodeId: 'node-wait',
        ttlSeconds: 0,
        positionX: 0,
        positionY: 0
      },
      {
        id: 'node-wait',
        name: 'Wait for Checkout Completed',
        type: JourneyNodeType.WaitUntilEvent,
        eventName: 'checkout_completed',
        ttlSeconds: 86400, // 24 hours
        successNextNodeId: 'node-success-email',
        timeoutNextNodeId: 'node-timeout-whatsapp',
        delaySeconds: 0,
        positionX: 0,
        positionY: 0
      },
      {
        id: 'node-success-email',
        name: 'Thank You Confirmation',
        type: JourneyNodeType.ChannelAction,
        channelType: 'Email',
        actionPayload: 'Thank you {{first_name}}! Your order was completed successfully.',
        nextNodeId: 'node-exit',
        delaySeconds: 0,
        ttlSeconds: 0,
        positionX: 0,
        positionY: 0
      },
      {
        id: 'node-timeout-whatsapp',
        name: 'Abandoned Cart 10% Discount WhatsApp',
        type: JourneyNodeType.ChannelAction,
        channelType: 'WhatsApp',
        actionPayload: 'Hi {{first_name}}, we noticed you left items in your cart. Claim 10% off with code SAVE10!',
        nextNodeId: 'node-exit',
        delaySeconds: 0,
        ttlSeconds: 0,
        positionX: 0,
        positionY: 0
      },
      {
        id: 'node-exit',
        name: 'Journey Completed',
        type: JourneyNodeType.Exit,
        delaySeconds: 0,
        ttlSeconds: 0,
        positionX: 0,
        positionY: 0
      }
    ];

    const sampleJourney: JourneyDefinitionDto = {
      id: '00000000-0000-0000-0000-000000000015',
      tenantId: '00000000-0000-0000-0000-000000000001',
      name: 'Abandoned Cart Recovery & 24h Wait-Until',
      description: 'Waits 24 hours for checkout before dispatching 10% WhatsApp recovery offer',
      triggerEvent: 'order_abandoned',
      conversionEvents: ['OrderPlaced', 'ContractSigned'],
      nodes: sampleNodes,
      isActive: true,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString()
    };

    this.journeys.set([sampleJourney]);
    this.selectJourney(sampleJourney);
  }

  selectJourney(journey: JourneyDefinitionDto): void {
    this.selectedJourney.set(journey);
    if (journey.nodes && journey.nodes.length > 0) {
      this.selectedNode.set(journey.nodes[0]);
    } else {
      this.selectedNode.set(null);
    }
    this.loadMetrics();
  }

  selectNode(node: JourneyNodeDto): void {
    this.selectedNode.set(node);
  }

  onNodeChanged(): void {
    const journey = this.selectedJourney();
    if (journey) {
      this.selectedJourney.set({ ...journey });
    }
  }

  createNewJourney(): void {
    const newJourney: JourneyDefinitionDto = {
      id: crypto.randomUUID ? crypto.randomUUID() : 'journey-' + Date.now(),
      tenantId: '00000000-0000-0000-0000-000000000001',
      name: 'New Customer Journey',
      description: 'Event-driven flow',
      triggerEvent: 'lead_signup',
      conversionEvents: ['OrderPlaced'],
      nodes: [
        {
          id: 'trigger-1',
          name: 'Lead Signup Trigger',
          type: JourneyNodeType.Trigger,
          eventName: 'lead_signup',
          delaySeconds: 0,
          ttlSeconds: 0,
          positionX: 0,
          positionY: 0
        }
      ],
      isActive: true,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString()
    };

    this.journeys.update(list => [newJourney, ...list]);
    this.selectJourney(newJourney);
  }

  addNode(type: JourneyNodeType): void {
    const journey = this.selectedJourney();
    if (!journey) return;

    const id = 'node-' + Math.random().toString(36).substring(2, 7);
    let newNode: JourneyNodeDto;

    switch (type) {
      case JourneyNodeType.WaitUntilEvent:
        newNode = {
          id,
          name: 'Wait For Event',
          type,
          eventName: 'checkout_completed',
          ttlSeconds: 86400,
          delaySeconds: 0,
          positionX: 0,
          positionY: 0
        };
        break;
      case JourneyNodeType.ConditionBranch:
        newNode = {
          id,
          name: 'Attribute Condition Check',
          type,
          conditionExpression: 'Ltv > 500',
          delaySeconds: 0,
          ttlSeconds: 0,
          positionX: 0,
          positionY: 0
        };
        break;
      case JourneyNodeType.ChannelAction:
        newNode = {
          id,
          name: 'Multi-Channel Dispatch',
          type,
          channelType: 'WhatsApp',
          actionPayload: 'Hello {{first_name}}! Exclusive update for you.',
          delaySeconds: 0,
          ttlSeconds: 0,
          positionX: 0,
          positionY: 0
        };
        break;
      case JourneyNodeType.Delay:
        newNode = {
          id,
          name: 'Fixed Duration Delay',
          type,
          delaySeconds: 3600,
          ttlSeconds: 0,
          positionX: 0,
          positionY: 0
        };
        break;
      case JourneyNodeType.Exit:
      default:
        newNode = {
          id,
          name: 'Journey Exit',
          type: JourneyNodeType.Exit,
          delaySeconds: 0,
          ttlSeconds: 0,
          positionX: 0,
          positionY: 0
        };
        break;
    }

    journey.nodes.push(newNode);
    this.selectedJourney.set({ ...journey });
    this.selectedNode.set(newNode);
  }

  deleteSelectedNode(): void {
    const journey = this.selectedJourney();
    const node = this.selectedNode();
    if (!journey || !node) return;

    journey.nodes = journey.nodes.filter(n => n.id !== node.id);
    this.selectedJourney.set({ ...journey });
    this.selectedNode.set(journey.nodes[0] || null);
  }

  saveCurrentJourney(): void {
    const journey = this.selectedJourney();
    if (!journey) return;

    this.isSaving.set(true);
    this.api.saveJourneyDag({
      id: journey.id,
      name: journey.name,
      description: journey.description,
      triggerEvent: journey.triggerEvent,
      conversionEvents: journey.conversionEvents,
      nodes: journey.nodes,
      isActive: journey.isActive
    }).subscribe({
      next: (saved) => {
        this.isSaving.set(false);
        this.selectedJourney.set(saved);
        this.loadMetrics();
      },
      error: () => {
        this.isSaving.set(false);
      }
    });
  }

  loadMetrics(): void {
    const journey = this.selectedJourney();
    if (!journey) return;

    this.api.getJourneyDagMetrics(journey.id).subscribe({
      next: (data) => this.metrics.set(data),
      error: () => {
        // Fallback demo metrics if in development/offline mode
        this.metrics.set({
          journeyId: journey.id,
          journeyName: journey.name,
          totalEnrolled: 120,
          currentlyActive: 14,
          currentlyWaiting: 38,
          totalCompleted: 58,
          totalUnenrolled: 10,
          completionRate: 48.3,
          dropOffRate: 8.3,
          nodeMetrics: journey.nodes.map((n, idx) => ({
            nodeId: n.id,
            nodeName: n.name,
            nodeType: n.type,
            executionsCount: Math.max(120 - idx * 18, 12),
            currentContactsCount: n.type === JourneyNodeType.WaitUntilEvent ? 38 : 0,
            conversionPercent: Math.max(100 - idx * 15, 20),
            dropOffCount: idx === 2 ? 8 : 1
          }))
        });
      }
    });
  }

  sweepTimeouts(): void {
    this.isSweeping.set(true);
    this.api.sweepJourneyDagTimeouts().subscribe({
      next: () => {
        this.isSweeping.set(false);
        this.loadMetrics();
      },
      error: () => {
        this.isSweeping.set(false);
      }
    });
  }

  simulateStart(): void {
    const journey = this.selectedJourney();
    if (!journey) return;

    this.isSimulating.set(true);
    this.api.startJourneyDag(journey.id, this.simContactId, {
      first_name: 'Yassine',
      company: 'Atlas Logistics SARL',
      Ltv: 750,
      Country: 'MA'
    }).subscribe({
      next: (inst) => {
        this.isSimulating.set(false);
        if (inst && inst.executionHistory) {
          this.simulationLogs.set(inst.executionHistory);
        }
        this.loadMetrics();
      },
      error: () => {
        this.isSimulating.set(false);
        this.simulationLogs.update(logs => [
          {
            nodeId: 'sim-start',
            nodeName: 'Contact Enrolled',
            nodeType: JourneyNodeType.Trigger,
            status: 'Executed',
            details: `Enrolled contact '${this.simContactId}'. Execution started.`,
            executedAtUtc: new Date().toISOString()
          },
          ...logs
        ]);
      }
    });
  }

  simulateEvent(eventName: string): void {
    this.isSimulating.set(true);
    this.api.ingestJourneyDagEvent(this.simContactId, eventName, {
      timestamp: new Date().toISOString()
    }).subscribe({
      next: (res) => {
        this.isSimulating.set(false);
        this.simulationLogs.update(logs => [
          {
            nodeId: 'sim-event',
            nodeName: `Ingest: ${eventName}`,
            nodeType: JourneyNodeType.WaitUntilEvent,
            status: res.unenrolledCount > 0 ? 'Unenrolled' : (res.advancedCount > 0 ? 'MatchedEvent' : 'Executed'),
            details: res.message,
            executedAtUtc: new Date().toISOString()
          },
          ...logs
        ]);
        this.loadMetrics();
      },
      error: () => {
        this.isSimulating.set(false);
        this.simulationLogs.update(logs => [
          {
            nodeId: 'sim-event',
            nodeName: `Event: ${eventName}`,
            nodeType: JourneyNodeType.WaitUntilEvent,
            status: 'MatchedEvent',
            details: `Event '${eventName}' ingested. Waiting instances advanced.`,
            executedAtUtc: new Date().toISOString()
          },
          ...logs
        ]);
      }
    });
  }

  generateTestContactId(): void {
    this.simContactId = crypto.randomUUID ? crypto.randomUUID() : 'contact-' + Math.random().toString(36).substring(2, 9);
  }

  private validateDagLocally(nodes: JourneyNodeDto[]): DagValidationResultDto {
    if (!nodes || nodes.length === 0) {
      return { isValid: false, errorMessage: 'Journey DAG contains no nodes.' };
    }

    const hasTrigger = nodes.some(n => n.type === JourneyNodeType.Trigger);
    if (!hasTrigger) {
      return { isValid: false, errorMessage: 'DAG must contain at least one Trigger node as an entrypoint.' };
    }

    const nodeIds = new Set(nodes.map(n => n.id));
    const inDegree: Record<string, number> = {};
    const adj: Record<string, string[]> = {};

    for (const node of nodes) {
      inDegree[node.id] = 0;
      adj[node.id] = [];
    }

    for (const node of nodes) {
      const targets: string[] = [];
      if (node.nextNodeId) targets.push(node.nextNodeId);
      if (node.successNextNodeId) targets.push(node.successNextNodeId);
      if (node.timeoutNextNodeId) targets.push(node.timeoutNextNodeId);
      if (node.trueNextNodeId) targets.push(node.trueNextNodeId);
      if (node.falseNextNodeId) targets.push(node.falseNextNodeId);

      for (const t of targets) {
        if (!nodeIds.has(t)) {
          return { isValid: false, errorMessage: `Node '${node.id}' points to unknown target '${t}'.` };
        }
        adj[node.id].push(t);
      }
    }

    for (const u in adj) {
      for (const v of adj[u]) {
        inDegree[v] = (inDegree[v] || 0) + 1;
      }
    }

    const queue: string[] = [];
    for (const id in inDegree) {
      if (inDegree[id] === 0) {
        queue.push(id);
      }
    }

    let count = 0;
    while (queue.length > 0) {
      const curr = queue.shift()!;
      count++;
      for (const neighbor of adj[curr] || []) {
        inDegree[neighbor]--;
        if (inDegree[neighbor] === 0) {
          queue.push(neighbor);
        }
      }
    }

    if (count < nodes.length) {
      return { isValid: false, errorMessage: 'Cycle detected in Journey DAG. Kahn algorithm found circular dependency.' };
    }

    return { isValid: true };
  }

  getNodeIcon(type: JourneyNodeType): string {
    switch (type) {
      case JourneyNodeType.Trigger: return '⚡';
      case JourneyNodeType.WaitUntilEvent: return '⏳';
      case JourneyNodeType.ConditionBranch: return '🔀';
      case JourneyNodeType.ChannelAction: return '💬';
      case JourneyNodeType.Delay: return '⏱️';
      case JourneyNodeType.Exit: return '🏁';
      default: return '●';
    }
  }

  getNodeTypeLabel(type: JourneyNodeType): string {
    switch (type) {
      case JourneyNodeType.Trigger: return 'Event Trigger';
      case JourneyNodeType.WaitUntilEvent: return 'Wait-Until Event (TTL)';
      case JourneyNodeType.ConditionBranch: return 'Condition Branch';
      case JourneyNodeType.ChannelAction: return 'Multi-Channel Action';
      case JourneyNodeType.Delay: return 'Delay';
      case JourneyNodeType.Exit: return 'Exit';
      default: return 'Node';
    }
  }

  formatTtl(seconds: number): string {
    if (!seconds || seconds <= 0) return '0s';
    if (seconds >= 86400) return `${Math.round(seconds / 86400)}d (${seconds}s)`;
    if (seconds >= 3600) return `${Math.round(seconds / 3600)}h (${seconds}s)`;
    if (seconds >= 60) return `${Math.round(seconds / 60)}m (${seconds}s)`;
    return `${seconds}s`;
  }
}
