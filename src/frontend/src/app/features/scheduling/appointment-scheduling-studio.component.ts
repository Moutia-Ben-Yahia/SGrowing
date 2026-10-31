import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export interface WeeklyWorkingHours {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
}

export interface BusyInterval {
  startUtc: string;
  endUtc: string;
  reason: string;
}

export interface HostSchedule {
  hostId: string;
  hostName: string;
  hostEmail: string;
  timeZoneId: string;
  weight: number;
  isActive: boolean;
  bufferTime: string;
  minimumNotice: string;
  workingHours: WeeklyWorkingHours[];
  externalBusyIntervals: BusyInterval[];
  specialties: string[];
  weightedRoundRobinAssignments: number;
  lastAssignedAtUtc?: string;
}

export interface Appointment {
  id: string;
  hostId: string;
  hostName: string;
  hostEmail: string;
  title: string;
  description?: string;
  attendeeName: string;
  attendeeEmail: string;
  attendeePhone?: string;
  attendeeNotes?: string;
  startUtc: string;
  endUtc: string;
  durationMinutes: number;
  prospectTimeZoneId: string;
  status: 'Proposed' | 'Confirmed' | 'Rescheduled' | 'Completed' | 'Cancelled' | 'NoShow';
  meetingLink: string;
  rescheduleReason?: string;
  cancellationReason?: string;
  sequenceNumber: number;
  topologyUsed: 'WeightedRoundRobin' | 'LeastBusy' | 'Collective';
  collectiveHostIds?: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TimeSlot {
  startUtc: string;
  endUtc: string;
  startLocalFormatted: string;
  endLocalFormatted: string;
  timeZoneId: string;
  hostId?: string;
  hostName?: string;
  availableHostIds?: string[];
}

export interface HostRoutingMetric {
  hostId: string;
  hostName: string;
  hostEmail: string;
  timeZoneId: string;
  weight: number;
  isActive: boolean;
  confirmedBookingsCount: number;
  totalAssignedCount: number;
  lastAssignedAtUtc?: string;
  utilizationRatio: number;
}

export interface RoutingStatsResult {
  totalAppointments: number;
  activeAppointments: number;
  cancelledAppointments: number;
  topologyBookingsCount: { [key: string]: number };
  hostMetrics: HostRoutingMetric[];
}

@Component({
  selector: 'app-appointment-scheduling-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 p-6 md:p-8 font-sans">
      <!-- Top Enterprise Header -->
      <header class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 rounded-lg bg-indigo-600/20 text-indigo-400 border border-indigo-500/30">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
              </svg>
            </span>
            <div>
              <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
                Temporal Appointment Scheduling & Round-Robin Engine
                <span class="text-xs font-mono font-medium px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                  HORIZON 48
                </span>
              </h1>
              <p class="text-sm text-slate-400 mt-0.5">
                High-speed UTC slot intersections, weighted team distribution, collective availability & RFC 5545 calendar invitations.
              </p>
            </div>
          </div>
        </div>

        <!-- Global Action Tabs -->
        <div class="flex items-center gap-2 bg-slate-900/90 p-1 rounded-xl border border-slate-800">
          <button
            (click)="activeTab.set('booking')"
            [class.bg-indigo-600]="activeTab() === 'booking'"
            [class.text-white]="activeTab() === 'booking'"
            [class.text-slate-400]="activeTab() !== 'booking'"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-all hover:text-white flex items-center gap-2"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            Live Booking Studio
          </button>
          <button
            (click)="activeTab.set('hosts')"
            [class.bg-indigo-600]="activeTab() === 'hosts'"
            [class.text-white]="activeTab() === 'hosts'"
            [class.text-slate-400]="activeTab() !== 'hosts'"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-all hover:text-white flex items-center gap-2"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
            </svg>
            Team Hosts ({{ hosts().length }})
          </button>
          <button
            (click)="activeTab.set('ledger')"
            [class.bg-indigo-600]="activeTab() === 'ledger'"
            [class.text-white]="activeTab() === 'ledger'"
            [class.text-slate-400]="activeTab() !== 'ledger'"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-all hover:text-white flex items-center gap-2"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
            Appointments ({{ appointments().length }})
          </button>
          <button
            (click)="activeTab.set('stats')"
            [class.bg-indigo-600]="activeTab() === 'stats'"
            [class.text-white]="activeTab() === 'stats'"
            [class.text-slate-400]="activeTab() !== 'stats'"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-all hover:text-white flex items-center gap-2"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
            Routing Metrics
          </button>
        </div>
      </header>

      <!-- Feedback Alerts -->
      @if (statusMessage()) {
        <div class="mt-4 p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 flex items-center justify-between">
          <div class="flex items-center gap-2">
            <svg class="w-5 h-5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
            </svg>
            <span>{{ statusMessage() }}</span>
          </div>
          <button (click)="statusMessage.set(null)" class="text-emerald-400/80 hover:text-emerald-300">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
        </div>
      }

      @if (errorMessage()) {
        <div class="mt-4 p-4 rounded-xl bg-rose-500/10 border border-rose-500/20 text-rose-400 flex items-center justify-between">
          <div class="flex items-center gap-2">
            <svg class="w-5 h-5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span>{{ errorMessage() }}</span>
          </div>
          <button (click)="errorMessage.set(null)" class="text-rose-400/80 hover:text-rose-300">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
        </div>
      }

      <!-- TAB 1: LIVE BOOKING STUDIO -->
      @if (activeTab() === 'booking') {
        <div class="mt-6 grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- Left Column: Scheduling Controls (4 Cols) -->
          <div class="lg:col-span-4 space-y-6">
            <!-- Topology Selection Card -->
            <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800 shadow-xl space-y-4">
              <h3 class="text-sm font-semibold uppercase tracking-wider text-slate-400 flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-indigo-500"></span>
                1. Routing Topology
              </h3>

              <div class="grid grid-cols-1 gap-2">
                <button
                  type="button"
                  (click)="setTopology('WeightedRoundRobin')"
                  [class.border-indigo-500]="selectedTopology() === 'WeightedRoundRobin'"
                  [class.bg-indigo-950/40]="selectedTopology() === 'WeightedRoundRobin'"
                  class="p-3 rounded-xl border border-slate-800 text-left transition-all hover:border-slate-700"
                >
                  <div class="flex items-center justify-between">
                    <span class="font-semibold text-white text-sm">Weighted Round-Robin</span>
                    <span class="text-xs px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300">Recommended</span>
                  </div>
                  <p class="text-xs text-slate-400 mt-1">Dispatches to team hosts according to rep capability weights.</p>
                </button>

                <button
                  type="button"
                  (click)="setTopology('LeastBusy')"
                  [class.border-indigo-500]="selectedTopology() === 'LeastBusy'"
                  [class.bg-indigo-950/40]="selectedTopology() === 'LeastBusy'"
                  class="p-3 rounded-xl border border-slate-800 text-left transition-all hover:border-slate-700"
                >
                  <div class="flex items-center justify-between">
                    <span class="font-semibold text-white text-sm">Least-Busy Allocator</span>
                    <span class="text-xs px-2 py-0.5 rounded bg-cyan-500/20 text-cyan-300">Load-Balanced</span>
                  </div>
                  <p class="text-xs text-slate-400 mt-1">Routes appointment to the host with fewest active meetings.</p>
                </button>

                <button
                  type="button"
                  (click)="setTopology('Collective')"
                  [class.border-indigo-500]="selectedTopology() === 'Collective'"
                  [class.bg-indigo-950/40]="selectedTopology() === 'Collective'"
                  class="p-3 rounded-xl border border-slate-800 text-left transition-all hover:border-slate-700"
                >
                  <div class="flex items-center justify-between">
                    <span class="font-semibold text-white text-sm">Collective Multi-Host</span>
                    <span class="text-xs px-2 py-0.5 rounded bg-amber-500/20 text-amber-300">Intersection</span>
                  </div>
                  <p class="text-xs text-slate-400 mt-1">Only offers slots where all selected specialists are simultaneously free.</p>
                </button>
              </div>

              <!-- Host or Collective Selection -->
              @if (selectedTopology() !== 'Collective') {
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1.5">Direct Host (Optional override)</label>
                  <select
                    [ngModel]="selectedHostId()"
                    (ngModelChange)="onHostChange($event)"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-sm text-white focus:outline-none focus:border-indigo-500"
                  >
                    <option [ngValue]="null">Auto-Route via Topology (Any Available Rep)</option>
                    @for (host of hosts(); track host.hostId) {
                      <option [value]="host.hostId">
                        {{ host.hostName }} ({{ host.timeZoneId }} &bull; Weight {{ host.weight }})
                      </option>
                    }
                  </select>
                </div>
              } @else {
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1.5">Select Collective Team Members</label>
                  <div class="space-y-1.5 max-h-36 overflow-y-auto pr-1">
                    @for (host of hosts(); track host.hostId) {
                      <label class="flex items-center gap-2 p-2 rounded-lg bg-slate-950 border border-slate-800/80 text-xs cursor-pointer hover:border-slate-700">
                        <input
                          type="checkbox"
                          [checked]="isCollectiveHostSelected(host.hostId)"
                          (change)="toggleCollectiveHost(host.hostId)"
                          class="rounded bg-slate-900 border-slate-700 text-indigo-600 focus:ring-0"
                        />
                        <span class="font-medium text-slate-200">{{ host.hostName }}</span>
                        <span class="text-slate-500 ml-auto">{{ host.timeZoneId }}</span>
                      </label>
                    }
                  </div>
                </div>
              }
            </div>

            <!-- Parameters Card -->
            <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800 shadow-xl space-y-4">
              <h3 class="text-sm font-semibold uppercase tracking-wider text-slate-400 flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-indigo-500"></span>
                2. Temporal Constraints
              </h3>

              <!-- Duration Pills -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1.5">Meeting Duration</label>
                <div class="grid grid-cols-4 gap-2">
                  @for (dur of [15, 30, 45, 60]; track dur) {
                    <button
                      type="button"
                      (click)="setDuration(dur)"
                      [class.bg-indigo-600]="selectedDuration() === dur"
                      [class.text-white]="selectedDuration() === dur"
                      [class.bg-slate-950]="selectedDuration() !== dur"
                      [class.text-slate-300]="selectedDuration() !== dur"
                      class="py-2 rounded-xl border border-slate-800 text-xs font-semibold hover:border-slate-700 transition-all text-center"
                    >
                      {{ dur }}m
                    </button>
                  }
                </div>
              </div>

              <!-- Prospect Timezone Selector -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1.5">Prospect Timezone (IANA)</label>
                <select
                  [ngModel]="selectedTimeZone()"
                  (ngModelChange)="onTimeZoneChange($event)"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-sm text-white focus:outline-none focus:border-indigo-500"
                >
                  <option value="UTC">UTC (Universal Time Coordinated)</option>
                  <option value="America/New_York">America/New_York (US Eastern Time)</option>
                  <option value="Europe/Paris">Europe/Paris (Central European Time)</option>
                  <option value="Africa/Tunis">Africa/Tunis (Central Africa Time)</option>
                  <option value="Asia/Tokyo">Asia/Tokyo (Japan Standard Time)</option>
                  <option value="Europe/London">Europe/London (GMT / BST)</option>
                  <option value="America/Los_Angeles">America/Los_Angeles (US Pacific)</option>
                  <option value="Asia/Dubai">Asia/Dubai (Gulf Standard Time)</option>
                  <option value="Australia/Sydney">Australia/Sydney (AEST)</option>
                </select>
              </div>

              <!-- Date Picker -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1.5">Target Inspection Date</label>
                <input
                  type="date"
                  [ngModel]="selectedDate()"
                  (ngModelChange)="onDateChange($event)"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-sm text-white focus:outline-none focus:border-indigo-500"
                />
              </div>

              <button
                type="button"
                (click)="loadAvailableSlots()"
                class="w-full py-2.5 rounded-xl bg-slate-800 hover:bg-slate-700 text-white text-xs font-semibold tracking-wide transition-all flex items-center justify-center gap-2"
              >
                <svg class="w-4 h-4" [class.animate-spin]="isLoadingSlots()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                Recalculate Intersections
              </button>
            </div>
          </div>

          <!-- Center Column: Available Slots Grid (4 Cols) -->
          <div class="lg:col-span-4 space-y-4">
            <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800 shadow-xl h-full flex flex-col">
              <div class="flex items-center justify-between pb-3 border-b border-slate-800">
                <h3 class="text-sm font-semibold uppercase tracking-wider text-slate-400 flex items-center gap-2">
                  <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
                  3. Available Slots
                </h3>
                <span class="text-xs font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-300">
                  {{ slots().length }} Free Slots
                </span>
              </div>

              <p class="text-xs text-slate-500 mt-2">
                Displayed in timezone: <strong class="text-slate-300">{{ selectedTimeZone() }}</strong>
              </p>

              <!-- Slot Chip Grid -->
              <div class="mt-4 flex-1 overflow-y-auto max-h-[580px] space-y-2 pr-1">
                @if (isLoadingSlots()) {
                  <div class="py-16 text-center text-slate-500">
                    <svg class="w-8 h-8 animate-spin mx-auto text-indigo-500 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                    </svg>
                    <span class="text-xs">Computing temporal intersections...</span>
                  </div>
                } @else if (slots().length === 0) {
                  <div class="py-16 text-center text-slate-500">
                    <svg class="w-10 h-10 mx-auto text-slate-600 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    <p class="text-sm font-medium text-slate-400">No open slots on this date</p>
                    <p class="text-xs text-slate-500 mt-1">Try selecting another day or shortening the meeting duration.</p>
                  </div>
                } @else {
                  <div class="grid grid-cols-2 gap-2">
                    @for (slot of slots(); track slot.startUtc) {
                      <button
                        type="button"
                        (click)="selectSlot(slot)"
                        [class.border-indigo-500]="selectedSlot()?.startUtc === slot.startUtc"
                        [class.bg-indigo-600/30]="selectedSlot()?.startUtc === slot.startUtc"
                        [class.ring-2]="selectedSlot()?.startUtc === slot.startUtc"
                        [class.ring-indigo-500]="selectedSlot()?.startUtc === slot.startUtc"
                        class="p-2.5 rounded-xl border border-slate-800 bg-slate-950/80 hover:border-slate-700 text-left transition-all"
                      >
                        <div class="font-mono text-xs font-semibold text-white">
                          {{ formatSlotTime(slot.startLocalFormatted) }}
                        </div>
                        <div class="text-[10px] text-slate-400 mt-0.5 truncate">
                          {{ slot.hostName || 'Collective Team' }}
                        </div>
                      </button>
                    }
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Right Column: Attendee Form & Instant Confirmation (4 Cols) -->
          <div class="lg:col-span-4 space-y-4">
            <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800 shadow-xl space-y-4">
              <h3 class="text-sm font-semibold uppercase tracking-wider text-slate-400 flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-cyan-500"></span>
                4. Prospect Details & Booking
              </h3>

              @if (selectedSlot(); as slot) {
                <div class="p-3.5 rounded-xl bg-indigo-950/30 border border-indigo-500/30 space-y-1">
                  <div class="text-xs text-indigo-300 font-medium">Selected Temporal Slot:</div>
                  <div class="text-sm font-bold text-white font-mono">
                    {{ slot.startLocalFormatted }} &ndash; {{ formatSlotTime(slot.endLocalFormatted) }}
                  </div>
                  <div class="text-xs text-slate-400">
                    Host: <span class="text-slate-200">{{ slot.hostName || 'Team Balanced' }}</span>
                  </div>
                </div>
              } @else {
                <div class="p-3.5 rounded-xl bg-slate-950/80 border border-dashed border-slate-800 text-center text-xs text-slate-500">
                  Please pick an available slot from the middle column.
                </div>
              }

              <div class="space-y-3">
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Session Title</label>
                  <input
                    type="text"
                    [ngModel]="meetingTitle()"
                    (ngModelChange)="meetingTitle.set($event)"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-sm text-white focus:outline-none focus:border-indigo-500"
                    placeholder="e.g. Discovery & Architectural Alignment"
                  />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Attendee Full Name *</label>
                  <input
                    type="text"
                    [ngModel]="attendeeName()"
                    (ngModelChange)="attendeeName.set($event)"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-sm text-white focus:outline-none focus:border-indigo-500"
                    placeholder="e.g. Sophia Alami"
                  />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Attendee Email *</label>
                  <input
                    type="email"
                    [ngModel]="attendeeEmail()"
                    (ngModelChange)="attendeeEmail.set($event)"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-sm text-white focus:outline-none focus:border-indigo-500"
                    placeholder="sophia.alami@enterprise.com"
                  />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Phone Number (Optional)</label>
                  <input
                    type="tel"
                    [ngModel]="attendeePhone()"
                    (ngModelChange)="attendeePhone.set($event)"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-sm text-white focus:outline-none focus:border-indigo-500"
                    placeholder="+216 98 123 456"
                  />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Agenda / Meeting Notes</label>
                  <textarea
                    [ngModel]="meetingNotes()"
                    (ngModelChange)="meetingNotes.set($event)"
                    rows="2"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500"
                    placeholder="Key pain points, evaluation scope, requirements..."
                  ></textarea>
                </div>

                <button
                  type="button"
                  (click)="confirmBooking()"
                  [disabled]="!selectedSlot() || isBooking() || !attendeeName() || !attendeeEmail()"
                  class="w-full py-3 rounded-xl bg-gradient-to-r from-indigo-600 to-violet-600 hover:from-indigo-500 hover:to-violet-500 disabled:opacity-40 disabled:cursor-not-allowed text-white font-semibold text-sm shadow-lg shadow-indigo-600/30 transition-all flex items-center justify-center gap-2"
                >
                  @if (isBooking()) {
                    <svg class="w-4 h-4 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                    </svg>
                    Dispatching Reservation...
                  } @else {
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    Confirm & Synthesize .ICS
                  }
                </button>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- TAB 2: TEAM HOSTS BALANCER -->
      @if (activeTab() === 'hosts') {
        <div class="mt-6 space-y-6">
          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">
            @for (host of hosts(); track host.hostId) {
              <div class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl flex flex-col justify-between">
                <div>
                  <div class="flex items-start justify-between">
                    <div>
                      <h4 class="font-bold text-white text-base">{{ host.hostName }}</h4>
                      <p class="text-xs text-slate-400 font-mono">{{ host.hostEmail }}</p>
                    </div>
                    <span class="px-2 py-0.5 rounded text-[11px] font-semibold bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                      Weight {{ host.weight }}
                    </span>
                  </div>

                  <div class="mt-4 space-y-2 text-xs">
                    <div class="flex justify-between py-1 border-b border-slate-800/80 text-slate-400">
                      <span>Timezone</span>
                      <span class="font-mono text-slate-200">{{ host.timeZoneId }}</span>
                    </div>
                    <div class="flex justify-between py-1 border-b border-slate-800/80 text-slate-400">
                      <span>Meeting Buffer</span>
                      <span class="text-slate-200">15 mins</span>
                    </div>
                    <div class="flex justify-between py-1 border-b border-slate-800/80 text-slate-400">
                      <span>Notice Window</span>
                      <span class="text-slate-200">1-2 hrs</span>
                    </div>
                    <div class="flex justify-between py-1 border-b border-slate-800/80 text-slate-400">
                      <span>Round-Robin Assignments</span>
                      <span class="font-bold text-emerald-400">{{ host.weightedRoundRobinAssignments }}</span>
                    </div>
                  </div>

                  <!-- Specialties -->
                  <div class="mt-4">
                    <span class="text-[10px] uppercase font-semibold text-slate-500 tracking-wider">Expertise</span>
                    <div class="flex flex-wrap gap-1.5 mt-1.5">
                      @for (spec of host.specialties; track spec) {
                        <span class="px-2 py-0.5 rounded text-[10px] bg-slate-950 text-slate-300 border border-slate-800">
                          {{ spec }}
                        </span>
                      }
                    </div>
                  </div>
                </div>

                <div class="mt-5 pt-3 border-t border-slate-800 flex items-center justify-between text-xs">
                  <span class="text-emerald-400 flex items-center gap-1.5">
                    <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
                    Calendar Synced
                  </span>
                  <button
                    (click)="inspectHostSlots(host.hostId)"
                    class="text-indigo-400 hover:text-indigo-300 font-medium"
                  >
                    View Slots &rarr;
                  </button>
                </div>
              </div>
            }
          </div>
        </div>
      }

      <!-- TAB 3: APPOINTMENTS LEDGER TABLE -->
      @if (activeTab() === 'ledger') {
        <div class="mt-6 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl overflow-hidden">
          <div class="p-5 border-b border-slate-800 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h3 class="font-bold text-white text-base">Booked Appointments Ledger</h3>
              <p class="text-xs text-slate-400">Full audit trail of active, rescheduled, and cancelled meetings.</p>
            </div>
            <button
              (click)="loadAppointments()"
              class="px-3.5 py-1.5 rounded-xl bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-200 flex items-center gap-2"
            >
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Refresh
            </button>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-950 text-slate-400 font-medium uppercase tracking-wider border-b border-slate-800">
                <tr>
                  <th class="py-3 px-4">Attendee</th>
                  <th class="py-3 px-4">Session Title</th>
                  <th class="py-3 px-4">Host Rep</th>
                  <th class="py-3 px-4">Scheduled Slot (UTC)</th>
                  <th class="py-3 px-4">Status</th>
                  <th class="py-3 px-4">Meeting Room</th>
                  <th class="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/80">
                @for (apt of appointments(); track apt.id) {
                  <tr class="hover:bg-slate-800/40 transition-colors">
                    <td class="py-3 px-4">
                      <div class="font-semibold text-white">{{ apt.attendeeName }}</div>
                      <div class="text-[11px] text-slate-400 font-mono">{{ apt.attendeeEmail }}</div>
                    </td>
                    <td class="py-3 px-4 max-w-xs truncate text-slate-200">
                      {{ apt.title }}
                      @if (apt.rescheduleReason) {
                        <div class="text-[10px] text-amber-400 truncate">Note: {{ apt.rescheduleReason }}</div>
                      }
                      @if (apt.cancellationReason) {
                        <div class="text-[10px] text-rose-400 truncate">Reason: {{ apt.cancellationReason }}</div>
                      }
                    </td>
                    <td class="py-3 px-4 text-slate-300">
                      {{ apt.hostName }}
                    </td>
                    <td class="py-3 px-4 font-mono text-slate-300">
                      {{ formatUtcDateTime(apt.startUtc) }} ({{ apt.durationMinutes }}m)
                    </td>
                    <td class="py-3 px-4">
                      <span
                        [class.bg-emerald-500/10]="apt.status === 'Confirmed'"
                        [class.text-emerald-400]="apt.status === 'Confirmed'"
                        [class.border-emerald-500/30]="apt.status === 'Confirmed'"
                        [class.bg-amber-500/10]="apt.status === 'Rescheduled'"
                        [class.text-amber-400]="apt.status === 'Rescheduled'"
                        [class.border-amber-500/30]="apt.status === 'Rescheduled'"
                        [class.bg-rose-500/10]="apt.status === 'Cancelled'"
                        [class.text-rose-400]="apt.status === 'Cancelled'"
                        [class.border-rose-500/30]="apt.status === 'Cancelled'"
                        class="px-2 py-0.5 rounded text-[11px] font-semibold border"
                      >
                        {{ apt.status }}
                      </span>
                    </td>
                    <td class="py-3 px-4">
                      <a
                        [href]="apt.meetingLink"
                        target="_blank"
                        class="text-indigo-400 hover:underline flex items-center gap-1 font-mono text-[11px]"
                      >
                        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" />
                        </svg>
                        Join Room
                      </a>
                    </td>
                    <td class="py-3 px-4 text-right space-x-2">
                      <button
                        (click)="downloadIcs(apt.id)"
                        title="Download RFC 5545 .ics Calendar Invite"
                        class="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 transition-colors"
                      >
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                        </svg>
                      </button>

                      @if (apt.status !== 'Cancelled' && apt.status !== 'Completed') {
                        <button
                          (click)="openRescheduleModal(apt)"
                          title="Reschedule Appointment"
                          class="p-1.5 rounded-lg bg-amber-500/10 hover:bg-amber-500/20 text-amber-400 transition-colors"
                        >
                          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                          </svg>
                        </button>
                        <button
                          (click)="cancelAppointment(apt.id)"
                          title="Cancel Appointment"
                          class="p-1.5 rounded-lg bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 transition-colors"
                        >
                          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                          </svg>
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

      <!-- TAB 4: ROUTING METRICS -->
      @if (activeTab() === 'stats') {
        <div class="mt-6 space-y-6">
          @if (routingStats(); as stats) {
            <!-- High Level KPI Cards -->
            <div class="grid grid-cols-1 md:grid-cols-4 gap-6">
              <div class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl">
                <span class="text-xs uppercase text-slate-400 font-semibold">Total Bookings</span>
                <div class="text-3xl font-extrabold text-white mt-1">{{ stats.totalAppointments }}</div>
                <p class="text-xs text-slate-500 mt-1">Cumulative appointment reservations</p>
              </div>

              <div class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl">
                <span class="text-xs uppercase text-slate-400 font-semibold">Active Pipeline</span>
                <div class="text-3xl font-extrabold text-emerald-400 mt-1">{{ stats.activeAppointments }}</div>
                <p class="text-xs text-slate-500 mt-1">Confirmed & rescheduled meetings</p>
              </div>

              <div class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl">
                <span class="text-xs uppercase text-slate-400 font-semibold">Cancelled Ratio</span>
                <div class="text-3xl font-extrabold text-rose-400 mt-1">
                  {{ stats.totalAppointments > 0 ? (stats.cancelledAppointments / stats.totalAppointments * 100).toFixed(0) : 0 }}%
                </div>
                <p class="text-xs text-slate-500 mt-1">{{ stats.cancelledAppointments }} cancellations recorded</p>
              </div>

              <div class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl">
                <span class="text-xs uppercase text-slate-400 font-semibold">Weighted Round-Robin Rate</span>
                <div class="text-3xl font-extrabold text-indigo-400 mt-1">
                  {{ stats.topologyBookingsCount['WeightedRoundRobin'] || 0 }}
                </div>
                <p class="text-xs text-slate-500 mt-1">Algorithmic fair team allocations</p>
              </div>
            </div>

            <!-- Host Utilization Breakdown -->
            <div class="p-6 rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl space-y-4">
              <h3 class="font-bold text-white text-base">Host Capacity & Utilization Distribution</h3>
              <div class="space-y-4">
                @for (metric of stats.hostMetrics; track metric.hostId) {
                  <div>
                    <div class="flex items-center justify-between text-xs mb-1">
                      <span class="font-semibold text-slate-200">
                        {{ metric.hostName }} (Weight {{ metric.weight }})
                      </span>
                      <span class="text-slate-400">
                        {{ metric.confirmedBookingsCount }} Confirmed &bull;
                        {{ (metric.utilizationRatio * 100).toFixed(0) }}% Workload
                      </span>
                    </div>
                    <div class="w-full bg-slate-950 rounded-full h-2.5 overflow-hidden border border-slate-800">
                      <div
                        class="bg-indigo-500 h-2.5 rounded-full transition-all duration-500"
                        [style.width.%]="(metric.utilizationRatio * 100) || 5"
                      ></div>
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }

      <!-- Reschedule Modal -->
      @if (rescheduleTarget(); as target) {
        <div class="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4 z-50">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-md w-full shadow-2xl space-y-4">
            <h3 class="text-lg font-bold text-white">Reschedule Appointment</h3>
            <p class="text-xs text-slate-400">
              Select new start date & time for <strong>{{ target.attendeeName }}</strong> with <strong>{{ target.hostName }}</strong>.
            </p>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">New Start Time (UTC)</label>
                <input
                  type="datetime-local"
                  [ngModel]="rescheduleNewUtc()"
                  (ngModelChange)="rescheduleNewUtc.set($event)"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Reason for Rescheduling</label>
                <input
                  type="text"
                  [ngModel]="rescheduleReason()"
                  (ngModelChange)="rescheduleReason.set($event)"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500"
                  placeholder="e.g. Client requested postponement"
                />
              </div>
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button
                type="button"
                (click)="rescheduleTarget.set(null)"
                class="px-4 py-2 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-semibold"
              >
                Cancel
              </button>
              <button
                type="button"
                (click)="confirmReschedule()"
                class="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold"
              >
                Save Reschedule
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AppointmentSchedulingStudioComponent implements OnInit {
  private readonly http = inject(HttpClient);

  // Core Data Signals
  hosts = signal<HostSchedule[]>([]);
  appointments = signal<Appointment[]>([]);
  slots = signal<TimeSlot[]>([]);
  routingStats = signal<RoutingStatsResult | null>(null);

  // Tab & View Controls
  activeTab = signal<'booking' | 'hosts' | 'ledger' | 'stats'>('booking');

  // Booking Form State
  selectedTopology = signal<'WeightedRoundRobin' | 'LeastBusy' | 'Collective'>('WeightedRoundRobin');
  selectedHostId = signal<string | null>(null);
  selectedCollectiveHostIds = signal<string[]>([]);
  selectedDuration = signal<number>(30);
  selectedTimeZone = signal<string>('UTC');
  selectedDate = signal<string>(new Date().toISOString().substring(0, 10));
  selectedSlot = signal<TimeSlot | null>(null);

  attendeeName = signal<string>('Sophia Alami');
  attendeeEmail = signal<string>('sophia.alami@enterprise.com');
  attendeePhone = signal<string>('+216 98 123 456');
  meetingTitle = signal<string>('Strategic Architecture & Scaling Consultation');
  meetingNotes = signal<string>('Discussing multi-region clustering and COD automation.');

  // Loading & Feedback
  isLoadingSlots = signal<boolean>(false);
  isBooking = signal<boolean>(false);
  statusMessage = signal<string | null>(null);
  errorMessage = signal<string | null>(null);

  // Reschedule Modal State
  rescheduleTarget = signal<Appointment | null>(null);
  rescheduleNewUtc = signal<string>('');
  rescheduleReason = signal<string>('Attendee requested date adjustment');

  ngOnInit(): void {
    this.loadHosts();
    this.loadAppointments();
    this.loadRoutingStats();
    this.loadAvailableSlots();
  }

  loadHosts(): void {
    this.http.get<HostSchedule[]>('/api/v1/scheduling/appointments/hosts')
      .subscribe({
        next: (res) => {
          this.hosts.set(res);
          if (res.length >= 2 && this.selectedCollectiveHostIds().length === 0) {
            this.selectedCollectiveHostIds.set([res[0].hostId, res[1].hostId]);
          }
        },
        error: (err) => console.error('Failed to load hosts:', err)
      });
  }

  loadAppointments(): void {
    this.http.get<Appointment[]>('/api/v1/scheduling/appointments')
      .subscribe({
        next: (res) => this.appointments.set(res),
        error: (err) => console.error('Failed to load appointments:', err)
      });
  }

  loadRoutingStats(): void {
    this.http.get<RoutingStatsResult>('/api/v1/scheduling/appointments/routing-stats')
      .subscribe({
        next: (res) => this.routingStats.set(res),
        error: (err) => console.error('Failed to load routing stats:', err)
      });
  }

  loadAvailableSlots(): void {
    this.isLoadingSlots.set(true);
    this.selectedSlot.set(null);

    const dateStr = this.selectedDate();
    const startRange = new Date(`${dateStr}T00:00:00Z`).toISOString();
    const endRange = new Date(`${dateStr}T23:59:59Z`).toISOString();

    let url = `/api/v1/scheduling/appointments/slots?rangeStart=${encodeURIComponent(startRange)}&rangeEnd=${encodeURIComponent(endRange)}&duration=${this.selectedDuration()}&timeZoneId=${encodeURIComponent(this.selectedTimeZone())}&topology=${this.selectedTopology()}`;

    if (this.selectedTopology() === 'Collective') {
      const ids = this.selectedCollectiveHostIds();
      if (ids.length > 0) {
        url += `&collectiveHostIds=${encodeURIComponent(ids.join(','))}`;
      }
    } else if (this.selectedHostId()) {
      url += `&hostId=${this.selectedHostId()}`;
    }

    this.http.get<TimeSlot[]>(url)
      .subscribe({
        next: (res) => {
          this.slots.set(res);
          this.isLoadingSlots.set(false);
        },
        error: (err) => {
          console.error('Failed to load available slots:', err);
          this.isLoadingSlots.set(false);
          this.errorMessage.set(err.error?.error || 'Failed to calculate available slots.');
        }
      });
  }

  setTopology(topology: 'WeightedRoundRobin' | 'LeastBusy' | 'Collective'): void {
    this.selectedTopology.set(topology);
    this.loadAvailableSlots();
  }

  setDuration(duration: number): void {
    this.selectedDuration.set(duration);
    this.loadAvailableSlots();
  }

  onTimeZoneChange(tz: string): void {
    this.selectedTimeZone.set(tz);
    this.loadAvailableSlots();
  }

  onDateChange(d: string): void {
    this.selectedDate.set(d);
    this.loadAvailableSlots();
  }

  onHostChange(hId: string | null): void {
    this.selectedHostId.set(hId);
    this.loadAvailableSlots();
  }

  selectSlot(slot: TimeSlot): void {
    this.selectedSlot.set(slot);
  }

  isCollectiveHostSelected(hostId: string): boolean {
    return this.selectedCollectiveHostIds().includes(hostId);
  }

  toggleCollectiveHost(hostId: string): void {
    const current = [...this.selectedCollectiveHostIds()];
    const index = current.indexOf(hostId);
    if (index >= 0) {
      if (current.length > 1) {
        current.splice(index, 1);
      }
    } else {
      current.push(hostId);
    }
    this.selectedCollectiveHostIds.set(current);
    this.loadAvailableSlots();
  }

  confirmBooking(): void {
    const slot = this.selectedSlot();
    if (!slot) return;

    this.isBooking.set(true);
    this.statusMessage.set(null);
    this.errorMessage.set(null);

    const payload = {
      hostId: this.selectedTopology() === 'Collective' ? null : (this.selectedHostId() || slot.hostId),
      collectiveHostIds: this.selectedTopology() === 'Collective' ? this.selectedCollectiveHostIds() : null,
      topology: this.selectedTopology(),
      title: this.meetingTitle() || 'Consultation Session',
      description: this.meetingNotes(),
      attendeeName: this.attendeeName(),
      attendeeEmail: this.attendeeEmail(),
      attendeePhone: this.attendeePhone(),
      attendeeNotes: this.meetingNotes(),
      startUtc: slot.startUtc,
      durationMinutes: this.selectedDuration(),
      prospectTimeZoneId: this.selectedTimeZone()
    };

    this.http.post<Appointment>('/api/v1/scheduling/appointments/book', payload)
      .subscribe({
        next: (res) => {
          this.isBooking.set(false);
          this.statusMessage.set(`Appointment confirmed! Virtual room generated at ${res.meetingLink}.`);
          this.selectedSlot.set(null);
          this.loadAppointments();
          this.loadRoutingStats();
          this.loadAvailableSlots();
        },
        error: (err) => {
          this.isBooking.set(false);
          this.errorMessage.set(err.error?.error || 'Failed to book appointment.');
        }
      });
  }

  downloadIcs(appointmentId: string): void {
    window.open(`/api/v1/scheduling/appointments/${appointmentId}/ics`, '_blank');
  }

  openRescheduleModal(appointment: Appointment): void {
    this.rescheduleTarget.set(appointment);
    // Suggest tomorrow at same hour
    const date = new Date(appointment.startUtc);
    date.setDate(date.getDate() + 1);
    this.rescheduleNewUtc.set(date.toISOString().slice(0, 16));
  }

  confirmReschedule(): void {
    const target = this.rescheduleTarget();
    if (!target) return;

    const newStart = new Date(this.rescheduleNewUtc()).toISOString();
    const payload = {
      newStartUtc: newStart,
      newDurationMinutes: target.durationMinutes,
      reason: this.rescheduleReason()
    };

    this.http.post<Appointment>(`/api/v1/scheduling/appointments/reschedule/${target.id}`, payload)
      .subscribe({
        next: () => {
          this.rescheduleTarget.set(null);
          this.statusMessage.set('Appointment successfully rescheduled.');
          this.loadAppointments();
          this.loadRoutingStats();
          this.loadAvailableSlots();
        },
        error: (err) => {
          this.errorMessage.set(err.error?.error || 'Reschedule failed due to conflict.');
        }
      });
  }

  cancelAppointment(appointmentId: string): void {
    if (!confirm('Are you sure you want to cancel this appointment and free the slot?')) {
      return;
    }

    this.http.post<Appointment>(`/api/v1/scheduling/appointments/cancel/${appointmentId}`, {
      reason: 'Cancelled from Appointment Studio Ledger'
    }).subscribe({
      next: () => {
        this.statusMessage.set('Appointment cancelled.');
        this.loadAppointments();
        this.loadRoutingStats();
        this.loadAvailableSlots();
      },
      error: (err) => {
        this.errorMessage.set(err.error?.error || 'Failed to cancel appointment.');
      }
    });
  }

  inspectHostSlots(hostId: string): void {
    this.selectedTopology.set('WeightedRoundRobin');
    this.selectedHostId.set(hostId);
    this.activeTab.set('booking');
    this.loadAvailableSlots();
  }

  formatSlotTime(formattedDateStr: string): string {
    if (!formattedDateStr) return '';
    const parts = formattedDateStr.split(' ');
    return parts.length > 1 ? parts[1] : formattedDateStr;
  }

  formatUtcDateTime(utcStr: string): string {
    if (!utcStr) return '';
    const d = new Date(utcStr);
    return d.toISOString().replace('T', ' ').substring(0, 16);
  }
}
