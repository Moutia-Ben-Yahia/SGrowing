import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export type LifecycleNodeType = 'Trigger' | 'Delay' | 'Condition' | 'Action';
export type ActionChannel = 'WhatsApp' | 'Email' | 'Push' | 'Sms' | 'Webhook' | 'CrmTask';
export type DelayUnit = 'Minutes' | 'Hours' | 'Days' | 'Weeks';
export type JourneyLifecycleStatus = 'Draft' | 'Active' | 'Paused' | 'Archived';
export type ContactJourneyStatus = 'InProgress' | 'Paused' | 'Completed' | 'Exited';

export interface TriggerNodeConfig {
  type: 'Event' | 'Segment';
  eventName: string;
  segmentName: string;
}

export interface DelayNodeConfig {
  value: number;
  unit: DelayUnit;
}

export interface ConditionNodeConfig {
  property: string;
  operator: string;
  value: string;
  trueNodeId?: string;
  falseNodeId?: string;
}

export interface ActionNodeConfig {
  channel: ActionChannel;
  fallbackChannel?: ActionChannel | null;
  templateId?: string;
  subject: string;
  messageBody: string;
  crmTaskTitle?: string;
  crmAssignee?: string;
  simulateChannelFailure: boolean;
}

export interface LifecycleJourneyNode {
  id: string;
  name: string;
  type: LifecycleNodeType;
  description?: string;
  positionX: number;
  positionY: number;
  nextNodeIds: string[];
  triggerConfig?: TriggerNodeConfig;
  delayConfig?: DelayNodeConfig;
  conditionConfig?: ConditionNodeConfig;
  actionConfig?: ActionNodeConfig;
}

export interface LifecycleJourneyEdge {
  id: string;
  sourceNodeId: string;
  targetNodeId: string;
  label?: string;
}

export interface CustomerLifecycleJourney {
  id: string;
  tenantId: string;
  name: string;
  description: string;
  status: JourneyLifecycleStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  nodes: LifecycleJourneyNode[];
  edges: LifecycleJourneyEdge[];
  totalEnrolled: number;
  totalCompleted: number;
  totalExited: number;
  activeCount: number;
  fallbackRecoveryCount: number;
  conversionRate: number;
}

export interface JourneyBreadcrumb {
  nodeId: string;
  nodeName: string;
  nodeType: LifecycleNodeType;
  enteredAtUtc: string;
  exitedAtUtc?: string;
  outcome: string;
}

export interface JourneyDeliveryLog {
  id: string;
  nodeId: string;
  channel: ActionChannel;
  success: boolean;
  errorMessage?: string;
  wasFallback: boolean;
  originalChannel?: ActionChannel;
  timestampUtc: string;
  details: string;
}

export interface JourneyStatePointer {
  id: string;
  journeyId: string;
  contactId: string;
  currentNodeId: string;
  status: ContactJourneyStatus;
  scheduledResumeAtUtc?: string;
  enrolledAtUtc: string;
  lastUpdatedAtUtc: string;
  completedAtUtc?: string;
  contactProperties: Record<string, any>;
  breadcrumbs: JourneyBreadcrumb[];
  deliveryLogs: JourneyDeliveryLog[];
}

export interface JourneyMetricsSummary {
  totalJourneys: number;
  activeJourneys: number;
  totalEnrolledContacts: number;
  activeInFlightContacts: number;
  totalCompletedContacts: number;
  totalFallbackRecoveries: number;
  overallConversionRate: number;
  contactsPerStepDistribution: Record<string, number>;
}

@Component({
  selector: 'app-customer-lifecycle-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans">
      <!-- Top Navigation Header -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 py-4 flex flex-wrap items-center justify-between gap-4 sticky top-0 z-30 shadow-md">
        <div class="flex items-center gap-3">
          <div class="h-10 w-10 rounded-xl bg-gradient-to-tr from-indigo-600 via-purple-600 to-pink-500 flex items-center justify-center shadow-lg shadow-purple-500/20">
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-xl font-bold bg-gradient-to-r from-white via-slate-200 to-slate-400 bg-clip-text text-transparent">
                Customer Lifecycle Studio
              </h1>
              <span class="px-2 py-0.5 text-xs font-semibold rounded-full bg-purple-900/40 border border-purple-700/50 text-purple-300">
                Horizon 52 • DAG Delayed Automation
              </span>
            </div>
            <p class="text-xs text-slate-400">
              Durable multi-channel journey orchestration, temporal delay sweep engine & cross-channel failover routing.
            </p>
          </div>
        </div>

        <div class="flex items-center gap-3 flex-wrap">
          <!-- Journey Selector Dropdown -->
          <div class="relative">
            <select
              [ngModel]="selectedJourneyId()"
              (ngModelChange)="selectJourney($event)"
              class="bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5 text-sm text-slate-200 focus:outline-none focus:ring-2 focus:ring-purple-500"
            >
              @for (j of journeys(); track j.id) {
                <option [value]="j.id">{{ j.name }} ({{ j.status }})</option>
              }
            </select>
          </div>

          <!-- Quick Action Buttons -->
          <button
            (click)="openTriggerModal()"
            class="px-3.5 py-1.5 rounded-lg text-sm font-medium bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white shadow-md shadow-purple-600/20 flex items-center gap-1.5 transition active:scale-95"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Trigger Contact
          </button>

          <button
            (click)="runAdvanceDueSweep()"
            [disabled]="isSweeping()"
            class="px-3.5 py-1.5 rounded-lg text-sm font-medium bg-slate-800 border border-amber-500/40 hover:bg-amber-950/30 text-amber-300 flex items-center gap-1.5 transition active:scale-95 disabled:opacity-50"
            title="Evaluate contacts with expired delays and advance them to downstream nodes"
          >
            <svg class="w-4 h-4" [class.animate-spin]="isSweeping()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Advance Due Delays
          </button>

          @if (activeJourney(); as journey) {
            <button
              (click)="togglePauseJourney()"
              class="px-3.5 py-1.5 rounded-lg text-sm font-medium border flex items-center gap-1.5 transition active:scale-95"
              [ngClass]="journey.status === 'Paused'
                ? 'bg-emerald-950/40 border-emerald-600/50 text-emerald-300 hover:bg-emerald-900/40'
                : 'bg-slate-800 border-slate-700 text-slate-300 hover:bg-slate-700'"
            >
              @if (journey.status === 'Paused') {
                <svg class="w-4 h-4 text-emerald-400" fill="currentColor" viewBox="0 0 20 20">
                  <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM9.555 7.168A1 1 0 008 8v4a1 1 0 001.555.832l3-2a1 1 0 000-1.664l-3-2z" clip-rule="evenodd" />
                </svg>
                Resume Journey
              } @else {
                <svg class="w-4 h-4 text-amber-400" fill="currentColor" viewBox="0 0 20 20">
                  <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zM7 8a1 1 0 012 0v4a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v4a1 1 0 102 0V8a1 1 0 00-1-1z" clip-rule="evenodd" />
                </svg>
                Pause Journey
              }
            </button>
          }

          <button
            (click)="validateCurrentDag()"
            class="px-3 py-1.5 rounded-lg text-sm font-medium bg-slate-800 border border-slate-700 hover:bg-slate-700 text-slate-300 flex items-center gap-1.5 transition"
            title="Check DAG for cycles and topological order"
          >
            <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            Validate DAG
          </button>
        </div>
      </header>

      <!-- Subheader Metrics Ribbon -->
      @if (activeJourney(); as journey) {
        <section class="bg-slate-900 border-b border-slate-800 px-6 py-3.5 grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          <div class="bg-slate-950/60 border border-slate-800/80 rounded-xl p-3 flex flex-col">
            <span class="text-xs text-slate-400 font-medium">Total Enrolled</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-white">{{ journey.totalEnrolled }}</span>
              <span class="text-xs text-purple-400">Contacts</span>
            </div>
          </div>

          <div class="bg-slate-950/60 border border-slate-800/80 rounded-xl p-3 flex flex-col">
            <span class="text-xs text-slate-400 font-medium">Active In-Flight</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-indigo-400">{{ journey.activeCount }}</span>
              <span class="text-xs text-indigo-300">Pointers</span>
            </div>
          </div>

          <div class="bg-slate-950/60 border border-slate-800/80 rounded-xl p-3 flex flex-col">
            <span class="text-xs text-slate-400 font-medium">Delays Scheduled</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-amber-400">{{ activeDelaysCount() }}</span>
              <span class="text-xs text-amber-300">Paused</span>
            </div>
          </div>

          <div class="bg-slate-950/60 border border-slate-800/80 rounded-xl p-3 flex flex-col">
            <span class="text-xs text-slate-400 font-medium">Completed Full DAG</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-emerald-400">{{ journey.totalCompleted }}</span>
              <span class="text-xs text-emerald-300">Finished</span>
            </div>
          </div>

          <div class="bg-slate-950/60 border border-slate-800/80 rounded-xl p-3 flex flex-col">
            <span class="text-xs text-slate-400 font-medium">Conversion Rate</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-cyan-400">{{ journey.conversionRate }}%</span>
              <span class="text-xs text-cyan-300">Throughput</span>
            </div>
          </div>

          <div class="bg-slate-950/60 border border-slate-800/80 rounded-xl p-3 flex flex-col">
            <span class="text-xs text-slate-400 font-medium">Fallback Recoveries</span>
            <div class="flex items-baseline gap-2 mt-1">
              <span class="text-2xl font-bold text-rose-400">{{ journey.fallbackRecoveryCount }}</span>
              <span class="text-xs text-rose-300">Failovers</span>
            </div>
          </div>
        </section>
      }

      <!-- Main Layout: Canvas on Left/Center, Drawer on Right -->
      <div class="flex-1 flex overflow-hidden relative">
        <main class="flex-1 flex flex-col overflow-y-auto">
          <!-- DAG Canvas Container -->
          @if (activeJourney(); as journey) {
            <div class="p-6 flex-1 flex flex-col">
              <div class="flex items-center justify-between mb-3">
                <div class="flex items-center gap-2">
                  <h2 class="text-base font-semibold text-slate-200">Interactive DAG Flow Canvas</h2>
                  <span class="text-xs text-slate-500">({{ journey.nodes.length }} Nodes, {{ journey.edges.length }} Edges)</span>
                </div>
                <div class="flex items-center gap-4 text-xs text-slate-400">
                  <span class="flex items-center gap-1.5"><span class="w-3 h-3 rounded-full bg-purple-500 inline-block"></span> Trigger</span>
                  <span class="flex items-center gap-1.5"><span class="w-3 h-3 rounded-full bg-amber-500 inline-block"></span> Delay</span>
                  <span class="flex items-center gap-1.5"><span class="w-3 h-3 rounded-full bg-cyan-500 inline-block"></span> Condition</span>
                  <span class="flex items-center gap-1.5"><span class="w-3 h-3 rounded-full bg-emerald-500 inline-block"></span> Action</span>
                </div>
              </div>

              <!-- Canvas Card -->
              <div class="relative bg-slate-900/90 border border-slate-800 rounded-2xl p-6 min-h-[460px] shadow-inner overflow-x-auto">
                <!-- Background Grid Pattern -->
                <div class="absolute inset-0 opacity-10 bg-[radial-gradient(#9333ea_1px,transparent_1px)] [background-size:16px_16px] pointer-events-none"></div>

                <!-- Nodes Display -->
                <div class="relative z-10 flex flex-wrap items-center gap-6 min-w-[700px]">
                  @for (node of journey.nodes; track node.id) {
                    <div
                      (click)="selectNode(node)"
                      class="group cursor-pointer relative bg-slate-950/90 border-2 rounded-2xl p-4 w-72 shadow-xl transition-all duration-200 hover:-translate-y-1"
                      [ngClass]="{
                        'border-purple-500 shadow-purple-500/20 ring-2 ring-purple-500/40': selectedNode()?.id === node.id,
                        'border-purple-800/60 hover:border-purple-500': node.type === 'Trigger' && selectedNode()?.id !== node.id,
                        'border-amber-800/60 hover:border-amber-500': node.type === 'Delay' && selectedNode()?.id !== node.id,
                        'border-cyan-800/60 hover:border-cyan-500': node.type === 'Condition' && selectedNode()?.id !== node.id,
                        'border-emerald-800/60 hover:border-emerald-500': node.type === 'Action' && selectedNode()?.id !== node.id
                      }"
                    >
                      <!-- Header with Icon & Type -->
                      <div class="flex items-center justify-between mb-2">
                        <div class="flex items-center gap-2">
                          @switch (node.type) {
                            @case ('Trigger') {
                              <div class="w-7 h-7 rounded-lg bg-purple-950 border border-purple-700/60 flex items-center justify-center text-purple-400">
                                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" /></svg>
                              </div>
                              <span class="text-xs font-semibold uppercase tracking-wider text-purple-400">Trigger</span>
                            }
                            @case ('Delay') {
                              <div class="w-7 h-7 rounded-lg bg-amber-950 border border-amber-700/60 flex items-center justify-center text-amber-400">
                                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                              </div>
                              <span class="text-xs font-semibold uppercase tracking-wider text-amber-400">Delay</span>
                            }
                            @case ('Condition') {
                              <div class="w-7 h-7 rounded-lg bg-cyan-950 border border-cyan-700/60 flex items-center justify-center text-cyan-400">
                                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 9l4-4 4 4m0 6l-4 4-4-4" /></svg>
                              </div>
                              <span class="text-xs font-semibold uppercase tracking-wider text-cyan-400">Condition</span>
                            }
                            @case ('Action') {
                              <div class="w-7 h-7 rounded-lg bg-emerald-950 border border-emerald-700/60 flex items-center justify-center text-emerald-400">
                                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" /></svg>
                              </div>
                              <span class="text-xs font-semibold uppercase tracking-wider text-emerald-400">Action</span>
                            }
                          }
                        </div>

                        <!-- Contact Count Pill on this node -->
                        <div class="px-2 py-0.5 rounded-full text-xs font-medium bg-slate-800 text-slate-300 border border-slate-700">
                          {{ getActiveContactsCountAtNode(node.id) }} in-step
                        </div>
                      </div>

                      <!-- Node Title & Details -->
                      <h3 class="text-sm font-bold text-white mb-1 truncate">{{ node.name }}</h3>
                      <p class="text-xs text-slate-400 line-clamp-2 mb-3">{{ node.description || 'No description configured' }}</p>

                      <!-- Specific Node Configuration Badges -->
                      <div class="text-xs pt-2 border-t border-slate-800/80 flex flex-wrap items-center justify-between gap-1">
                        @if (node.type === 'Delay' && node.delayConfig) {
                          <span class="text-amber-300 font-mono flex items-center gap-1">
                            ⏱️ {{ node.delayConfig.value }} {{ node.delayConfig.unit }}
                          </span>
                        }
                        @if (node.type === 'Condition' && node.conditionConfig) {
                          <span class="text-cyan-300 font-mono truncate max-w-[190px]">
                            {{ node.conditionConfig.property }} {{ node.conditionConfig.operator }} {{ node.conditionConfig.value }}
                          </span>
                        }
                        @if (node.type === 'Action' && node.actionConfig) {
                          <span class="px-1.5 py-0.5 rounded bg-emerald-950/60 border border-emerald-800 text-emerald-300">
                            {{ node.actionConfig.channel }}
                          </span>
                          @if (node.actionConfig.fallbackChannel) {
                            <span class="text-[10px] text-slate-400">
                              ↳ Fallback: <span class="text-rose-300">{{ node.actionConfig.fallbackChannel }}</span>
                            </span>
                          }
                        }
                        @if (node.type === 'Trigger' && node.triggerConfig) {
                          <span class="text-purple-300 font-mono text-[11px] truncate max-w-[190px]">
                            {{ node.triggerConfig.type === 'Event' ? '⚡ ' + node.triggerConfig.eventName : '👥 ' + node.triggerConfig.segmentName }}
                          </span>
                        }

                        <span class="text-[10px] text-slate-500 group-hover:text-purple-400 transition">Edit Node &rarr;</span>
                      </div>
                    </div>
                  }
                </div>
              </div>

              <!-- Enrolled Contacts Section -->
              <div class="mt-8">
                <div class="flex items-center justify-between mb-3">
                  <div class="flex items-center gap-2">
                    <h2 class="text-base font-semibold text-slate-200">Enrolled Contacts & Active Delays</h2>
                    <span class="text-xs text-slate-500">({{ enrolledContacts().length }} registered state pointers)</span>
                  </div>
                  <div class="flex items-center gap-2">
                    <input
                      type="text"
                      [(ngModel)]="contactSearch"
                      placeholder="Search contact ID or property..."
                      class="bg-slate-900 border border-slate-800 rounded-lg px-3 py-1 text-xs text-slate-300 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-purple-500"
                    />
                    <button
                      (click)="refreshActiveContacts()"
                      class="px-2.5 py-1 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-300 transition"
                    >
                      Refresh
                    </button>
                  </div>
                </div>

                <div class="bg-slate-900/90 border border-slate-800 rounded-2xl overflow-hidden shadow-lg">
                  <table class="w-full text-left text-xs text-slate-300">
                    <thead class="bg-slate-950/80 border-b border-slate-800 text-slate-400 font-semibold uppercase tracking-wider">
                      <tr>
                        <th class="px-4 py-3">Contact</th>
                        <th class="px-4 py-3">Current Step</th>
                        <th class="px-4 py-3">Status</th>
                        <th class="px-4 py-3">Temporal Delay / Resume</th>
                        <th class="px-4 py-3">Last Active</th>
                        <th class="px-4 py-3 text-right">Actions</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800/60">
                      @for (contact of filteredContacts(); track contact.id) {
                        <tr class="hover:bg-slate-800/40 transition">
                          <td class="px-4 py-3 font-medium text-white">
                            <div class="flex items-center gap-2">
                              <div class="w-6 h-6 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center text-[10px] text-purple-300">
                                {{ contact.contactId.slice(0, 2).toUpperCase() }}
                              </div>
                              <div>
                                <div>{{ contact.contactId }}</div>
                                <div class="text-[10px] text-slate-500">Enrolled: {{ formatDate(contact.enrolledAtUtc) }}</div>
                              </div>
                            </div>
                          </td>
                          <td class="px-4 py-3">
                            <span class="px-2 py-0.5 rounded font-mono text-[11px] bg-slate-800 text-slate-200 border border-slate-700">
                              {{ getNodeNameById(contact.currentNodeId) }}
                            </span>
                          </td>
                          <td class="px-4 py-3">
                            <span
                              class="px-2 py-0.5 rounded-full text-[10px] font-semibold border"
                              [ngClass]="{
                                'bg-indigo-950/70 border-indigo-700 text-indigo-300': contact.status === 'InProgress',
                                'bg-emerald-950/70 border-emerald-700 text-emerald-300': contact.status === 'Completed',
                                'bg-amber-950/70 border-amber-700 text-amber-300': contact.status === 'Paused',
                                'bg-slate-800 border-slate-700 text-slate-400': contact.status === 'Exited'
                              }"
                            >
                              {{ contact.status }}
                            </span>
                          </td>
                          <td class="px-4 py-3">
                            @if (contact.scheduledResumeAtUtc) {
                              <div class="flex items-center gap-1.5 text-amber-300">
                                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                                <span>{{ formatDelayRemaining(contact.scheduledResumeAtUtc) }}</span>
                              </div>
                              <div class="text-[10px] text-slate-500">Due: {{ formatDate(contact.scheduledResumeAtUtc) }}</div>
                            } @else if (contact.status === 'Completed') {
                              <span class="text-emerald-400 text-xs">Finished</span>
                            } @else {
                              <span class="text-slate-500 text-xs">Immediate Next</span>
                            }
                          </td>
                          <td class="px-4 py-3 text-slate-400">
                            {{ formatDate(contact.lastUpdatedAtUtc) }}
                          </td>
                          <td class="px-4 py-3 text-right">
                            <div class="flex items-center justify-end gap-2">
                              @if (contact.scheduledResumeAtUtc && contact.status === 'InProgress') {
                                <button
                                  (click)="forceAdvanceContact(contact)"
                                  class="px-2 py-1 rounded bg-amber-950/50 hover:bg-amber-900/60 border border-amber-800 text-[11px] text-amber-300 transition"
                                  title="Force delay expiration and advance immediately"
                                >
                                  Fast-Forward ⚡
                                </button>
                              }
                              <button
                                (click)="inspectContactBreadcrumbs(contact)"
                                class="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 border border-slate-700 text-[11px] text-slate-300 transition"
                              >
                                Breadcrumbs ({{ contact.breadcrumbs.length }})
                              </button>
                            </div>
                          </td>
                        </tr>
                      } @empty {
                        <tr>
                          <td colspan="6" class="px-4 py-8 text-center text-slate-500">
                            No enrolled contacts match your query. Click "Trigger Contact" to enroll a test customer.
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          }
        </main>

        <!-- Right Side: Node Configuration Drawer -->
        @if (selectedNode()) {
          <aside class="w-96 border-l border-slate-800 bg-slate-900/95 backdrop-blur flex flex-col z-20 shadow-2xl">
            <div class="p-4 border-b border-slate-800 flex items-center justify-between">
              <div class="flex items-center gap-2">
                <span class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider"
                  [ngClass]="{
                    'bg-purple-950 text-purple-300 border border-purple-700': selectedNode()?.type === 'Trigger',
                    'bg-amber-950 text-amber-300 border border-amber-700': selectedNode()?.type === 'Delay',
                    'bg-cyan-950 text-cyan-300 border border-cyan-700': selectedNode()?.type === 'Condition',
                    'bg-emerald-950 text-emerald-300 border border-emerald-700': selectedNode()?.type === 'Action'
                  }"
                >
                  {{ selectedNode()?.type }} Node
                </span>
                <h3 class="text-sm font-bold text-white truncate max-w-[180px]">{{ selectedNode()?.name }}</h3>
              </div>
              <button
                (click)="selectedNode.set(null)"
                class="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800"
              >
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
              </button>
            </div>

            <div class="p-5 flex-1 overflow-y-auto space-y-4 text-xs">
              <!-- General Info -->
              <div>
                <label class="block text-slate-400 mb-1 font-medium">Node Name</label>
                <input
                  type="text"
                  [(ngModel)]="selectedNode()!.name"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:ring-1 focus:ring-purple-500"
                />
              </div>

              <div>
                <label class="block text-slate-400 mb-1 font-medium">Description</label>
                <textarea
                  [(ngModel)]="selectedNode()!.description"
                  rows="2"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:ring-1 focus:ring-purple-500"
                ></textarea>
              </div>

              <!-- Type-Specific Controls -->

              <!-- 1. Delay Configuration -->
              @if (selectedNode()?.type === 'Delay' && selectedNode()?.delayConfig) {
                <div class="p-4 bg-slate-950 border border-amber-900/40 rounded-xl space-y-3">
                  <h4 class="font-bold text-amber-400 flex items-center gap-1.5">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                    Temporal Pause Interval
                  </h4>
                  <div class="grid grid-cols-2 gap-2">
                    <div>
                      <label class="block text-slate-400 mb-1">Duration Value</label>
                      <input
                        type="number"
                        [(ngModel)]="selectedNode()!.delayConfig!.value"
                        min="1"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                      />
                    </div>
                    <div>
                      <label class="block text-slate-400 mb-1">Unit</label>
                      <select
                        [(ngModel)]="selectedNode()!.delayConfig!.unit"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                      >
                        <option value="Minutes">Minutes</option>
                        <option value="Hours">Hours</option>
                        <option value="Days">Days</option>
                        <option value="Weeks">Weeks</option>
                      </select>
                    </div>
                  </div>
                  <p class="text-[11px] text-slate-400">
                    Contacts entering this node pause execution until the delay expires or is triggered via advance sweep.
                  </p>
                </div>
              }

              <!-- 2. Condition Configuration -->
              @if (selectedNode()?.type === 'Condition' && selectedNode()?.conditionConfig) {
                <div class="p-4 bg-slate-950 border border-cyan-900/40 rounded-xl space-y-3">
                  <h4 class="font-bold text-cyan-400 flex items-center gap-1.5">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 9l4-4 4 4m0 6l-4 4-4-4" /></svg>
                    Contact Branching Logic
                  </h4>

                  <div>
                    <label class="block text-slate-400 mb-1">Contact Property</label>
                    <input
                      type="text"
                      [(ngModel)]="selectedNode()!.conditionConfig!.property"
                      placeholder="e.g. has_activated, ltv, plan"
                      class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                    />
                  </div>

                  <div class="grid grid-cols-2 gap-2">
                    <div>
                      <label class="block text-slate-400 mb-1">Operator</label>
                      <select
                        [(ngModel)]="selectedNode()!.conditionConfig!.operator"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                      >
                        <option value="IsTrue">Is True</option>
                        <option value="IsFalse">Is False</option>
                        <option value="Equals">Equals (==)</option>
                        <option value="NotEquals">Not Equals (!=)</option>
                        <option value="GreaterThan">Greater Than (&gt;)</option>
                        <option value="LessThan">Less Than (&lt;)</option>
                        <option value="Contains">Contains</option>
                      </select>
                    </div>
                    <div>
                      <label class="block text-slate-400 mb-1">Target Value</label>
                      <input
                        type="text"
                        [(ngModel)]="selectedNode()!.conditionConfig!.value"
                        placeholder="e.g. true, 500"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                      />
                    </div>
                  </div>

                  <div class="pt-2 border-t border-slate-800 space-y-2">
                    <div>
                      <label class="block text-emerald-400 font-semibold mb-1">If TRUE &rarr; Route to Node</label>
                      <select
                        [(ngModel)]="selectedNode()!.conditionConfig!.trueNodeId"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                      >
                        <option [value]="undefined">-- Select Target Node --</option>
                        @for (other of getAvailableTargetNodes(); track other.id) {
                          <option [value]="other.id">{{ other.name }} ({{ other.type }})</option>
                        }
                      </select>
                    </div>
                    <div>
                      <label class="block text-rose-400 font-semibold mb-1">If FALSE &rarr; Route to Node</label>
                      <select
                        [(ngModel)]="selectedNode()!.conditionConfig!.falseNodeId"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                      >
                        <option [value]="undefined">-- Select Target Node --</option>
                        @for (other of getAvailableTargetNodes(); track other.id) {
                          <option [value]="other.id">{{ other.name }} ({{ other.type }})</option>
                        }
                      </select>
                    </div>
                  </div>
                </div>
              }

              <!-- 3. Action Configuration -->
              @if (selectedNode()?.type === 'Action' && selectedNode()?.actionConfig) {
                <div class="p-4 bg-slate-950 border border-emerald-900/40 rounded-xl space-y-3">
                  <h4 class="font-bold text-emerald-400 flex items-center gap-1.5">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" /></svg>
                    Outbound Dispatch & Failover
                  </h4>

                  <div class="grid grid-cols-2 gap-2">
                    <div>
                      <label class="block text-slate-400 mb-1">Primary Channel</label>
                      <select
                        [(ngModel)]="selectedNode()!.actionConfig!.channel"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                      >
                        <option value="WhatsApp">WhatsApp</option>
                        <option value="Email">Email</option>
                        <option value="Push">Push Notification</option>
                        <option value="Sms">SMS Text</option>
                        <option value="CrmTask">CRM Task</option>
                        <option value="Webhook">Webhook HTTP</option>
                      </select>
                    </div>
                    <div>
                      <label class="block text-rose-400 mb-1">Fallback Channel</label>
                      <select
                        [(ngModel)]="selectedNode()!.actionConfig!.fallbackChannel"
                        class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                      >
                        <option [ngValue]="null">None</option>
                        <option value="Email">Email</option>
                        <option value="Sms">SMS</option>
                        <option value="Push">Push</option>
                      </select>
                    </div>
                  </div>

                  <div>
                    <label class="block text-slate-400 mb-1">Subject / Header</label>
                    <input
                      type="text"
                      [(ngModel)]="selectedNode()!.actionConfig!.subject"
                      class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200"
                    />
                  </div>

                  <div>
                    <label class="block text-slate-400 mb-1">Message Body / Payload</label>
                    <textarea
                      [(ngModel)]="selectedNode()!.actionConfig!.messageBody"
                      rows="3"
                      class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-slate-200 font-mono text-[11px]"
                    ></textarea>
                  </div>

                  <div class="pt-2 border-t border-slate-800 flex items-center justify-between">
                    <div>
                      <span class="font-medium text-slate-300">Simulate Channel Failure</span>
                      <p class="text-[10px] text-slate-500">Tests automatic failover to fallback</p>
                    </div>
                    <input
                      type="checkbox"
                      [(ngModel)]="selectedNode()!.actionConfig!.simulateChannelFailure"
                      class="w-4 h-4 rounded text-purple-600 focus:ring-purple-500 bg-slate-900 border-slate-700"
                    />
                  </div>
                </div>
              }
            </div>

            <!-- Drawer Footer -->
            <div class="p-4 border-t border-slate-800 bg-slate-950 flex items-center justify-end gap-2">
              <button
                (click)="selectedNode.set(null)"
                class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white"
              >
                Close
              </button>
              <button
                (click)="saveNodeChanges()"
                class="px-4 py-1.5 rounded-lg font-medium bg-purple-600 hover:bg-purple-500 text-white shadow-md shadow-purple-600/20 transition active:scale-95"
              >
                Apply Changes
              </button>
            </div>
          </aside>
        }
      </div>

      <!-- Trigger Contact Simulation Modal -->
      @if (showTriggerModal()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg shadow-2xl p-6 space-y-4">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="text-base font-bold text-white flex items-center gap-2">
                <span class="w-7 h-7 rounded-lg bg-purple-900/60 border border-purple-700 flex items-center justify-center text-purple-300">⚡</span>
                Simulate Contact Journey Trigger
              </h3>
              <button (click)="showTriggerModal.set(false)" class="text-slate-400 hover:text-white">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
              </button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-400 mb-1 font-medium">Contact ID / Email</label>
                <input
                  type="text"
                  [(ngModel)]="triggerForm.contactId"
                  placeholder="e.g. contact_sarah_091"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200"
                />
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-slate-400 mb-1 font-medium">Trigger Event Name</label>
                  <input
                    type="text"
                    [(ngModel)]="triggerForm.eventName"
                    placeholder="e.g. user_signed_up"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200"
                  />
                </div>
                <div>
                  <label class="block text-slate-400 mb-1 font-medium">Segment Name</label>
                  <input
                    type="text"
                    [(ngModel)]="triggerForm.segmentName"
                    placeholder="e.g. vip_customers"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200"
                  />
                </div>
              </div>

              <div>
                <label class="block text-slate-400 mb-1 font-medium">Contact Properties (JSON)</label>
                <textarea
                  [(ngModel)]="triggerForm.propertiesJson"
                  rows="4"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 font-mono text-[11px]"
                ></textarea>
                <p class="text-[10px] text-slate-500 mt-1">
                  Tip: include <code class="text-purple-300">"has_activated": true</code> or <code class="text-rose-300">"fail_whatsapp": true</code> to test condition branching & failover.
                </p>
              </div>

              <div class="flex items-center gap-2 pt-2">
                <input
                  type="checkbox"
                  id="forceReEnroll"
                  [(ngModel)]="triggerForm.forceReEnroll"
                  class="w-4 h-4 rounded text-purple-600 bg-slate-950 border-slate-700"
                />
                <label for="forceReEnroll" class="text-slate-300">Force Re-Enrollment if already active</label>
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 border-t border-slate-800 pt-3">
              <button
                (click)="showTriggerModal.set(false)"
                class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white"
              >
                Cancel
              </button>
              <button
                (click)="executeTriggerContact()"
                class="px-4 py-1.5 rounded-lg font-medium bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white shadow-md transition"
              >
                Enroll Contact Now
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Contact Execution History & Breadcrumbs Modal -->
      @if (inspectingContact()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-2xl shadow-2xl p-6 space-y-4 max-h-[85vh] flex flex-col">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-base font-bold text-white flex items-center gap-2">
                  <span>📜</span>
                  Audit Breadcrumbs: {{ inspectingContact()!.contactId }}
                </h3>
                <span class="text-xs text-slate-400">Chronological DAG traversal and outbound failover logs</span>
              </div>
              <button (click)="inspectingContact.set(null)" class="text-slate-400 hover:text-white">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
              </button>
            </div>

            <div class="flex-1 overflow-y-auto space-y-3 pr-2 text-xs">
              <!-- Breadcrumbs Timeline -->
              <h4 class="font-semibold text-slate-300 uppercase tracking-wider text-[11px]">Execution Trail</h4>
              <div class="relative pl-6 border-l-2 border-purple-800/60 space-y-4">
                @for (crumb of inspectingContact()!.breadcrumbs; track crumb.nodeId + crumb.enteredAtUtc) {
                  <div class="relative group">
                    <!-- Dot -->
                    <div class="absolute -left-[31px] top-1 w-3.5 h-3.5 rounded-full border-2 border-slate-900"
                      [ngClass]="{
                        'bg-purple-500': crumb.nodeType === 'Trigger',
                        'bg-amber-500': crumb.nodeType === 'Delay',
                        'bg-cyan-500': crumb.nodeType === 'Condition',
                        'bg-emerald-500': crumb.nodeType === 'Action'
                      }"
                    ></div>

                    <div class="bg-slate-950 border border-slate-800 rounded-xl p-3">
                      <div class="flex items-center justify-between mb-1">
                        <span class="font-bold text-white">{{ crumb.nodeName }}</span>
                        <span class="text-[10px] text-slate-500">{{ formatDate(crumb.enteredAtUtc) }}</span>
                      </div>
                      <p class="text-slate-300">{{ crumb.outcome }}</p>
                    </div>
                  </div>
                }
              </div>

              <!-- Delivery Logs -->
              @if (inspectingContact()!.deliveryLogs.length > 0) {
                <div class="pt-4 border-t border-slate-800">
                  <h4 class="font-semibold text-slate-300 uppercase tracking-wider text-[11px] mb-2">Outbound Delivery & Failover Logs</h4>
                  <div class="space-y-2">
                    @for (log of inspectingContact()!.deliveryLogs; track log.id) {
                      <div class="p-3 rounded-xl border flex items-start justify-between gap-3"
                        [ngClass]="log.success
                          ? (log.wasFallback ? 'bg-amber-950/30 border-amber-800/50 text-amber-200' : 'bg-emerald-950/30 border-emerald-800/50 text-emerald-200')
                          : 'bg-rose-950/30 border-rose-800/50 text-rose-200'"
                      >
                        <div>
                          <div class="font-bold flex items-center gap-1.5">
                            @if (log.wasFallback) {
                              <span>⚠️ Failover: {{ log.originalChannel }} &rarr; {{ log.channel }}</span>
                            } @else {
                              <span>✓ {{ log.channel }}</span>
                            }
                          </div>
                          <div class="text-[11px] opacity-80 mt-0.5">{{ log.details || log.errorMessage }}</div>
                        </div>
                        <span class="text-[10px] opacity-60 whitespace-nowrap">{{ formatDate(log.timestampUtc) }}</span>
                      </div>
                    }
                  </div>
                </div>
              }
            </div>

            <div class="flex justify-end border-t border-slate-800 pt-3">
              <button
                (click)="inspectingContact.set(null)"
                class="px-4 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-white font-medium"
              >
                Close Audit
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class CustomerLifecycleStudioComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/lifecycle/journeys';

  // Signals
  journeys = signal<CustomerLifecycleJourney[]>([]);
  selectedJourneyId = signal<string>('');
  selectedNode = signal<LifecycleJourneyNode | null>(null);
  enrolledContacts = signal<JourneyStatePointer[]>([]);
  inspectingContact = signal<JourneyStatePointer | null>(null);
  isSweeping = signal<boolean>(false);
  showTriggerModal = signal<boolean>(false);

  // Search & Filter State
  contactSearch = '';

  // Trigger Contact Form
  triggerForm = {
    contactId: 'contact_user_' + Math.floor(1000 + Math.random() * 9000),
    eventName: 'user_signed_up',
    segmentName: '',
    propertiesJson: '{\n  "has_activated": true,\n  "ltv": 650,\n  "fail_whatsapp": false\n}',
    forceReEnroll: true
  };

  // Computed state
  activeJourney = computed(() => {
    return this.journeys().find(j => j.id === this.selectedJourneyId()) || this.journeys()[0] || null;
  });

  activeDelaysCount = computed(() => {
    return this.enrolledContacts().filter(c => c.status === 'InProgress' && !!c.scheduledResumeAtUtc).length;
  });

  filteredContacts = computed(() => {
    const term = this.contactSearch.trim().toLowerCase();
    const list = this.enrolledContacts();
    if (!term) return list;
    return list.filter(c =>
      c.contactId.toLowerCase().includes(term) ||
      c.currentNodeId.toLowerCase().includes(term) ||
      c.status.toLowerCase().includes(term)
    );
  });

  ngOnInit(): void {
    this.loadJourneys();
  }

  loadJourneys(): void {
    this.http.get<CustomerLifecycleJourney[]>(this.baseUrl).subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.journeys.set(data);
          if (!this.selectedJourneyId() || !data.some(j => j.id === this.selectedJourneyId())) {
            this.selectedJourneyId.set(data[0].id);
          }
          this.refreshActiveContacts();
        } else {
          this.loadFallbackPreSeeded();
        }
      },
      error: () => {
        this.loadFallbackPreSeeded();
      }
    });
  }

  selectJourney(id: string): void {
    this.selectedJourneyId.set(id);
    this.selectedNode.set(null);
    this.refreshActiveContacts();
  }

  selectNode(node: LifecycleJourneyNode): void {
    // Clone to prevent mutating until saved
    this.selectedNode.set(JSON.parse(JSON.stringify(node)));
  }

  getAvailableTargetNodes(): LifecycleJourneyNode[] {
    const cur = this.selectedNode();
    const j = this.activeJourney();
    if (!j || !cur) return [];
    return j.nodes.filter(n => n.id !== cur.id);
  }

  getActiveContactsCountAtNode(nodeId: string): number {
    return this.enrolledContacts().filter(c => c.currentNodeId === nodeId && c.status === 'InProgress').length;
  }

  getNodeNameById(nodeId: string): string {
    const node = this.activeJourney()?.nodes.find(n => n.id === nodeId);
    return node ? node.name : nodeId;
  }

  refreshActiveContacts(): void {
    const jId = this.selectedJourneyId();
    if (!jId) return;

    this.http.get<JourneyStatePointer[]>(`${this.baseUrl}/${jId}/active-contacts`).subscribe({
      next: (contacts) => {
        this.enrolledContacts.set(contacts || []);
      },
      error: () => {
        // Fallback demo state pointers
        this.enrolledContacts.set(this.getDemoContacts(jId));
      }
    });
  }

  openTriggerModal(): void {
    this.triggerForm.contactId = 'contact_lead_' + Math.floor(1000 + Math.random() * 9000);
    this.showTriggerModal.set(true);
  }

  executeTriggerContact(): void {
    const jId = this.selectedJourneyId();
    if (!jId) return;

    let props: Record<string, any> = {};
    try {
      props = JSON.parse(this.triggerForm.propertiesJson);
    } catch {
      props = { raw: this.triggerForm.propertiesJson };
    }

    const payload = {
      contactId: this.triggerForm.contactId.trim(),
      eventName: this.triggerForm.eventName.trim() || null,
      segmentName: this.triggerForm.segmentName.trim() || null,
      contactProperties: props,
      forceReEnroll: this.triggerForm.forceReEnroll
    };

    this.http.post<any>(`${this.baseUrl}/${jId}/trigger`, payload).subscribe({
      next: (res) => {
        this.showTriggerModal.set(false);
        this.refreshActiveContacts();
        this.loadJourneys();
        if (res.statePointer) {
          this.inspectingContact.set(res.statePointer);
        }
      },
      error: (err) => {
        alert('Trigger failed: ' + (err.error?.message || err.message));
      }
    });
  }

  runAdvanceDueSweep(): void {
    const jId = this.selectedJourneyId();
    if (!jId) return;

    this.isSweeping.set(true);
    this.http.post<any>(`${this.baseUrl}/${jId}/advance-due`, {}).subscribe({
      next: (res) => {
        this.isSweeping.set(false);
        this.refreshActiveContacts();
        this.loadJourneys();
        alert(`Temporal Sweep Complete: ${res.advancedCount} contacts advanced, ${res.completedCount} completed.`);
      },
      error: (err) => {
        this.isSweeping.set(false);
        alert('Sweep error: ' + (err.error?.message || err.message));
      }
    });
  }

  forceAdvanceContact(contact: JourneyStatePointer): void {
    // Fast forward by setting scheduled resume to past and calling advance
    contact.scheduledResumeAtUtc = new Date(Date.now() - 60000).toISOString();
    this.runAdvanceDueSweep();
  }

  togglePauseJourney(): void {
    const j = this.activeJourney();
    if (!j) return;

    const action = j.status === 'Paused' ? 'resume' : 'pause';
    this.http.post<any>(`${this.baseUrl}/${j.id}/${action}`, {}).subscribe({
      next: () => {
        this.loadJourneys();
      },
      error: (err) => alert('Action failed: ' + err.message)
    });
  }

  validateCurrentDag(): void {
    const jId = this.selectedJourneyId();
    if (!jId) return;

    this.http.post<any>(`${this.baseUrl}/${jId}/validate`, {}).subscribe({
      next: (res) => {
        alert(`✓ DAG is Valid!\nTopological execution order:\n${res.topologicalOrder.join(' → ')}`);
      },
      error: (err) => {
        alert(`❌ DAG Validation Failed:\n${err.error?.errors?.join('\n') || err.message}`);
      }
    });
  }

  saveNodeChanges(): void {
    const edited = this.selectedNode();
    const j = this.activeJourney();
    if (!edited || !j) return;

    const idx = j.nodes.findIndex(n => n.id === edited.id);
    if (idx >= 0) {
      j.nodes[idx] = edited;
      this.selectedNode.set(null);
    }
  }

  inspectContactBreadcrumbs(contact: JourneyStatePointer): void {
    this.inspectingContact.set(contact);
  }

  formatDate(dateStr?: string): string {
    if (!dateStr) return 'N/A';
    const d = new Date(dateStr);
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) + ' ' + d.toLocaleDateString();
  }

  formatDelayRemaining(resumeAtStr?: string): string {
    if (!resumeAtStr) return 'No Delay';
    const diffMs = new Date(resumeAtStr).getTime() - Date.now();
    if (diffMs <= 0) return '⚡ Due Now (Ready for sweep)';
    const mins = Math.floor(diffMs / 60000);
    const hours = Math.floor(mins / 60);
    const days = Math.floor(hours / 24);

    if (days > 0) return `Due in ${days}d ${hours % 24}h`;
    if (hours > 0) return `Due in ${hours}h ${mins % 60}m`;
    return `Due in ${mins}m`;
  }

  private loadFallbackPreSeeded(): void {
    const defaultJourneys: CustomerLifecycleJourney[] = [
      {
        id: '11111111-2222-3333-4444-555555555551',
        tenantId: '00000000-0000-0000-0000-000000000001',
        name: 'SaaS Onboarding & Activation',
        description: 'Automated onboarding flow with activation condition checks, temporal delays, and cross-channel failover.',
        status: 'Active',
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
        totalEnrolled: 142,
        totalCompleted: 98,
        totalExited: 4,
        activeCount: 40,
        fallbackRecoveryCount: 16,
        conversionRate: 69.0,
        nodes: [
          {
            id: 'saas_trg_1',
            name: 'User Sign-up Event',
            type: 'Trigger',
            positionX: 100,
            positionY: 150,
            nextNodeIds: ['saas_act_welcome'],
            triggerConfig: { type: 'Event', eventName: 'user_signed_up', segmentName: '' }
          },
          {
            id: 'saas_act_welcome',
            name: 'Welcome Email & Checklist',
            type: 'Action',
            positionX: 350,
            positionY: 150,
            nextNodeIds: ['saas_dly_wait1d'],
            actionConfig: {
              channel: 'Email',
              fallbackChannel: 'Sms',
              subject: 'Welcome to SGrowing!',
              messageBody: 'Start your onboarding journey.',
              simulateChannelFailure: false
            }
          },
          {
            id: 'saas_dly_wait1d',
            name: 'Wait 1 Day for Activation',
            type: 'Delay',
            positionX: 600,
            positionY: 150,
            nextNodeIds: ['saas_cnd_activated'],
            delayConfig: { value: 1, unit: 'Days' }
          },
          {
            id: 'saas_cnd_activated',
            name: 'Has Activated Workspace?',
            type: 'Condition',
            positionX: 850,
            positionY: 150,
            nextNodeIds: [],
            conditionConfig: {
              property: 'has_activated',
              operator: 'IsTrue',
              value: 'true',
              trueNodeId: 'saas_act_celebrate',
              falseNodeId: 'saas_act_nudge'
            }
          },
          {
            id: 'saas_act_celebrate',
            name: 'Milestone Celebration (VIP Webinar)',
            type: 'Action',
            positionX: 1100,
            positionY: 80,
            nextNodeIds: [],
            actionConfig: {
              channel: 'WhatsApp',
              fallbackChannel: 'Email',
              subject: 'VIP Masterclass Access',
              messageBody: 'You are rocking it!',
              simulateChannelFailure: false
            }
          },
          {
            id: 'saas_act_nudge',
            name: 'WhatsApp Activation Nudge',
            type: 'Action',
            positionX: 1100,
            positionY: 240,
            nextNodeIds: [],
            actionConfig: {
              channel: 'WhatsApp',
              fallbackChannel: 'Email',
              subject: 'Need help setting up?',
              messageBody: 'We are here to support you.',
              simulateChannelFailure: false
            }
          }
        ],
        edges: []
      }
    ];

    this.journeys.set(defaultJourneys);
    this.selectedJourneyId.set(defaultJourneys[0].id);
    this.enrolledContacts.set(this.getDemoContacts(defaultJourneys[0].id));
  }

  private getDemoContacts(journeyId: string): JourneyStatePointer[] {
    return [
      {
        id: 'ptr_1',
        journeyId,
        contactId: 'contact_sarah_miller',
        currentNodeId: 'saas_dly_wait1d',
        status: 'InProgress',
        scheduledResumeAtUtc: new Date(Date.now() + 18 * 3600000).toISOString(),
        enrolledAtUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
        lastUpdatedAtUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
        contactProperties: { has_activated: true, ltv: 850 },
        breadcrumbs: [
          {
            nodeId: 'saas_trg_1',
            nodeName: 'User Sign-up Event',
            nodeType: 'Trigger',
            enteredAtUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
            exitedAtUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
            outcome: 'Enrolled via user_signed_up'
          },
          {
            nodeId: 'saas_act_welcome',
            nodeName: 'Welcome Email & Checklist',
            nodeType: 'Action',
            enteredAtUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
            exitedAtUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
            outcome: 'Dispatched via Primary [Email]'
          },
          {
            nodeId: 'saas_dly_wait1d',
            nodeName: 'Wait 1 Day for Activation',
            nodeType: 'Delay',
            enteredAtUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
            outcome: 'Delaying for 1 Days'
          }
        ],
        deliveryLogs: [
          {
            id: 'log_1',
            nodeId: 'saas_act_welcome',
            channel: 'Email',
            success: true,
            wasFallback: false,
            timestampUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
            details: 'Welcome email delivered to sarah@acme.io'
          }
        ]
      },
      {
        id: 'ptr_2',
        journeyId,
        contactId: 'contact_marcus_vance',
        currentNodeId: 'saas_act_celebrate',
        status: 'Completed',
        enrolledAtUtc: new Date(Date.now() - 48 * 3600000).toISOString(),
        lastUpdatedAtUtc: new Date(Date.now() - 2 * 3600000).toISOString(),
        completedAtUtc: new Date(Date.now() - 2 * 3600000).toISOString(),
        contactProperties: { has_activated: true, fail_whatsapp: true },
        breadcrumbs: [
          {
            nodeId: 'saas_trg_1',
            nodeName: 'User Sign-up Event',
            nodeType: 'Trigger',
            enteredAtUtc: new Date(Date.now() - 48 * 3600000).toISOString(),
            outcome: 'Enrolled via user_signed_up'
          },
          {
            nodeId: 'saas_cnd_activated',
            nodeName: 'Has Activated Workspace?',
            nodeType: 'Condition',
            enteredAtUtc: new Date(Date.now() - 2 * 3600000).toISOString(),
            outcome: 'Condition evaluated to True'
          },
          {
            nodeId: 'saas_act_celebrate',
            nodeName: 'Milestone Celebration',
            nodeType: 'Action',
            enteredAtUtc: new Date(Date.now() - 2 * 3600000).toISOString(),
            outcome: 'Dispatched via Fallback [Email] (Original: WhatsApp)'
          }
        ],
        deliveryLogs: [
          {
            id: 'log_2',
            nodeId: 'saas_act_celebrate',
            channel: 'Email',
            originalChannel: 'WhatsApp',
            success: true,
            wasFallback: true,
            timestampUtc: new Date(Date.now() - 2 * 3600000).toISOString(),
            details: 'Primary channel WhatsApp delivery bounced. Cross-channel failover routed to Email successfully.'
          }
        ]
      }
    ];
  }
}
