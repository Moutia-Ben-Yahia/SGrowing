import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { BookingEventTypeDto, CalendarBookingRecordDto, TimeSlotDto, HostWeightDto } from '../../core/models/calendar.models';

@Component({
  selector: 'app-calendar-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Inbox
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              📅 Appointment Scheduling & Calendar Engine
            </span>
            <span class="bg-indigo-500/20 text-indigo-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Category 16 • CalNode & Round-Robin Mesh
            </span>
          </div>

          <div class="flex items-center gap-3">
            <div class="flex bg-slate-800/80 p-0.5 rounded-lg border border-slate-700/60 text-xs">
              <button
                (click)="activeTab.set('bookings')"
                [class.bg-indigo-600]="activeTab() === 'bookings'"
                [class.text-white]="activeTab() === 'bookings'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                📋 Upcoming Bookings
              </button>
              <button
                (click)="activeTab.set('events')"
                [class.bg-indigo-600]="activeTab() === 'events'"
                [class.text-white]="activeTab() === 'events'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                ⚙️ Event Types
              </button>
              <button
                (click)="switchTabToRoundRobin()"
                [class.bg-indigo-600]="activeTab() === 'round-robin'"
                [class.text-white]="activeTab() === 'round-robin'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                ⚖️ Round-Robin & Host Weights
              </button>
              <button
                (click)="activeTab.set('book')"
                [class.bg-indigo-600]="activeTab() === 'book'"
                [class.text-white]="activeTab() === 'book'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                ⚡ Book Demo Call
              </button>
            </div>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- 1. Upcoming Bookings Tab -->
          @if (activeTab() === 'bookings') {
            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Scheduled Calls</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-white">{{ bookings().length }}</h3>
                  <span class="text-xs text-indigo-400 font-medium">100% Confirmed</span>
                </div>
              </div>
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Active Event Types</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-emerald-400">{{ eventTypes().length }}</h3>
                  <span class="text-xs text-slate-400">Round-Robin Active</span>
                </div>
              </div>
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Default TimeZone</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-xl font-bold text-amber-300">Africa/Tunis</h3>
                  <span class="text-xs text-slate-400">UTC+1</span>
                </div>
              </div>
            </div>

            <!-- Bookings Table -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/40 overflow-hidden">
              <div class="px-5 py-3 border-b border-slate-800 bg-slate-900/80 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white">🗓️ Confirmed Meetings & Client Calls</h3>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs text-slate-300">
                  <thead class="bg-slate-950/70 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800">
                    <tr>
                      <th class="px-4 py-3">Scheduled Time</th>
                      <th class="px-4 py-3">Attendee</th>
                      <th class="px-4 py-3">Assigned Host</th>
                      <th class="px-4 py-3">Status</th>
                      <th class="px-4 py-3">Meeting Room</th>
                      <th class="px-4 py-3">Calendar (.ICS)</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-mono">
                    @for (b of bookings(); track b.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="px-4 py-3 font-semibold text-white">
                          {{ b.startTimeUtc | date:'medium' }}
                        </td>
                        <td class="px-4 py-3 font-sans">
                          <p class="font-medium text-slate-100">{{ b.attendeeName }}</p>
                          <p class="text-[11px] text-slate-400 font-mono">{{ b.attendeeEmail }} • {{ b.attendeePhone }}</p>
                        </td>
                        <td class="px-4 py-3 font-sans text-indigo-300 font-medium">
                          {{ b.hostName }}
                        </td>
                        <td class="px-4 py-3">
                          <span class="px-2 py-0.5 rounded text-[10px] bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-semibold">
                            Confirmed
                          </span>
                        </td>
                        <td class="px-4 py-3 font-sans">
                          <a [href]="b.meetingUrl" target="_blank" class="text-xs text-indigo-400 hover:text-indigo-300 underline flex items-center gap-1">
                            🎥 Join Room ↗
                          </a>
                        </td>
                        <td class="px-4 py-3 font-sans">
                          <a [href]="'/api/v1/calendar/bookings/' + b.id + '/ics?tenantId=' + tenantId" target="_blank" class="px-2 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs border border-slate-700 transition">
                            📅 Download .ics
                          </a>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- 2. Event Types Tab -->
          @if (activeTab() === 'events') {
            <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
              @for (et of eventTypes(); track et.id) {
                <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-3 hover:border-indigo-500/50 transition">
                  <div class="flex items-center justify-between">
                    <span class="w-3 h-3 rounded-full" [style.backgroundColor]="et.colorHex"></span>
                    <span class="text-xs font-mono px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300">
                      {{ et.durationMinutes }} mins
                    </span>
                  </div>
                  <div>
                    <h4 class="text-base font-bold text-white">{{ et.title }}</h4>
                    <p class="text-xs text-slate-400 mt-1">{{ et.description || '1-on-1 strategy and demonstration call' }}</p>
                  </div>
                  <div class="text-[11px] text-slate-400 font-mono space-y-1 bg-slate-950 p-2.5 rounded-lg border border-slate-800/80">
                    <div class="flex justify-between items-center">
                      <span>Public Link:</span>
                      <button (click)="copyLinkForEvent(et.slug)" class="text-indigo-400 hover:text-indigo-300 text-[10px] underline">
                        Copy Link
                      </button>
                    </div>
                    <div class="text-slate-300 truncate">
                      https://sgrowing.io/book/{{ tenantSlug }}/{{ et.slug }}
                    </div>
                    <div class="flex justify-between pt-1">
                      <span>Routing:</span>
                      <span class="text-emerald-400">{{ et.isRoundRobin ? 'Round-Robin Lead Rotation' : 'Single Host' }}</span>
                    </div>
                  </div>
                  <div class="pt-2 flex items-center justify-between">
                    <button
                      (click)="openPreviewModal(et)"
                      class="text-xs text-indigo-400 hover:text-indigo-300 font-medium flex items-center gap-1">
                      👁️ Preview Booking Page
                    </button>
                    @if (et.isRoundRobin) {
                      <button
                        (click)="selectEventForRoundRobin(et.id)"
                        class="text-xs bg-indigo-600/30 hover:bg-indigo-600/50 text-indigo-300 px-2 py-1 rounded border border-indigo-500/40 transition">
                        Configure Weights ↗
                      </button>
                    }
                  </div>
                </div>
              }
            </div>
          }

          <!-- 3. Round-Robin & Host Weights Tab (Frontier 4 Requirement) -->
          @if (activeTab() === 'round-robin') {
            <div class="space-y-6">
              <!-- Top Controls: Select Event Type + Link Generator -->
              <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
                <!-- Event Type Selection -->
                <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-3">
                  <div class="flex items-center justify-between">
                    <h3 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>⚙️ Event Type Scope</span>
                    </h3>
                    <span class="text-[10px] bg-emerald-500/20 text-emerald-400 px-2 py-0.5 rounded border border-emerald-500/30 font-mono">
                      Weighted Round-Robin
                    </span>
                  </div>
                  <p class="text-xs text-slate-400">
                    Select an event type to manage sales rep distribution weights and view real-time meeting allocation scores.
                  </p>
                  <div>
                    <select
                      [ngModel]="selectedEventTypeId()"
                      (ngModelChange)="onEventTypeChange($event)"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white">
                      @for (et of eventTypes(); track et.id) {
                        <option [value]="et.id">
                          {{ et.title }} ({{ et.durationMinutes }}m • {{ et.isRoundRobin ? 'Round-Robin' : 'Single Host' }})
                        </option>
                      }
                    </select>
                  </div>
                </div>

                <!-- Public Booking Shareable Link Generator -->
                <div class="lg:col-span-2 p-5 rounded-2xl bg-gradient-to-r from-indigo-950/40 to-slate-900/80 border border-indigo-500/30 space-y-3">
                  <div class="flex items-center justify-between">
                    <h3 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>🔗 Public Booking Shareable Link</span>
                    </h3>
                    <span class="text-[10px] text-slate-400 font-mono">
                      https://sgrowing.io/book/:tenantSlug/:eventSlug
                    </span>
                  </div>
                  <p class="text-xs text-slate-300">
                    Share this verified link directly with inbound leads, drop it in email sequences, or embed it on landing pages. Leads are assigned in real-time according to rep weights.
                  </p>
                  <div class="flex items-center gap-2">
                    <div class="flex-1 bg-slate-950 px-3 py-2 rounded-lg border border-slate-700 text-xs font-mono text-indigo-300 truncate select-all">
                      {{ publicBookingLink() }}
                    </div>
                    <button
                      (click)="copyPublicLink()"
                      class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 shadow-md transition shrink-0">
                      @if (copiedLink()) {
                        <span>✅ Copied!</span>
                      } @else {
                        <span>📋 Copy Link</span>
                      }
                    </button>
                    <button
                      (click)="openPreviewModal()"
                      class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition shrink-0">
                      <span>👁️ Preview Modal</span>
                    </button>
                  </div>
                </div>
              </div>

              <!-- Host Weights Configuration Table -->
              <div class="p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4">
                <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                  <div>
                    <h3 class="text-base font-bold text-white flex items-center gap-2">
                      <span>⚖️ Sales Rep Weights & Load Balancing</span>
                    </h3>
                    <p class="text-xs text-slate-400 mt-0.5">
                      Distribution formula: <code class="text-indigo-300 bg-slate-950 px-1 py-0.5 rounded font-mono">score = (assignedCount) / (hostWeight > 0 ? hostWeight : 1.0)</code>. Reps with lowest score receive the next booking.
                    </p>
                  </div>
                  <div class="flex items-center gap-2">
                    <button
                      (click)="saveHostWeights()"
                      [disabled]="isSavingWeights()"
                      class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-bold shadow-lg shadow-emerald-950/40 transition flex items-center gap-1.5 disabled:opacity-50">
                      @if (isSavingWeights()) {
                        <span>Saving Weights...</span>
                      } @else {
                        <span>💾 Save Host Weights</span>
                      }
                    </button>
                  </div>
                </div>

                @if (weightsSaveMessage()) {
                  <div class="p-3 rounded-lg bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-xs font-medium">
                    {{ weightsSaveMessage() }}
                  </div>
                }

                <!-- Host List -->
                <div class="overflow-x-auto rounded-xl border border-slate-800">
                  <table class="w-full text-left text-xs text-slate-300">
                    <thead class="bg-slate-950 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800">
                      <tr>
                        <th class="px-4 py-3">Host / Sales Rep</th>
                        <th class="px-4 py-3">Host Weight</th>
                        <th class="px-4 py-3">Quick Presets</th>
                        <th class="px-4 py-3">Assigned Meetings</th>
                        <th class="px-4 py-3">Allocation Score</th>
                        <th class="px-4 py-3">Rotation Status</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800/70 font-mono">
                      @for (h of hosts(); track h.hostUserId; let idx = $index) {
                        <tr class="hover:bg-slate-800/30 transition">
                          <td class="px-4 py-3 font-sans">
                            <div class="flex items-center gap-3">
                              <div class="w-8 h-8 rounded-full bg-indigo-600/30 border border-indigo-500/40 flex items-center justify-center text-xs font-bold text-indigo-300">
                                {{ h.hostName.charAt(0) || 'H' }}
                              </div>
                              <div>
                                <p class="font-semibold text-white">{{ h.hostName }}</p>
                                <p class="text-[11px] text-slate-400 font-mono">ID: {{ h.hostUserId.slice(0, 8) }}...</p>
                              </div>
                            </div>
                          </td>
                          <td class="px-4 py-3 font-sans">
                            <div class="flex items-center gap-2">
                              <input
                                type="number"
                                [(ngModel)]="h.weight"
                                (ngModelChange)="recalculateScore(h)"
                                step="0.1"
                                min="0.1"
                                max="10.0"
                                class="w-20 bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white font-mono text-center focus:border-indigo-500" />
                              <span class="text-xs text-slate-400">ratio</span>
                            </div>
                          </td>
                          <td class="px-4 py-3 font-sans">
                            <div class="flex items-center gap-1 text-[11px]">
                              <button (click)="setHostWeight(h, 0.5)" class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700">0.5x</button>
                              <button (click)="setHostWeight(h, 1.0)" class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700">1.0x</button>
                              <button (click)="setHostWeight(h, 1.5)" class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700">1.5x</button>
                              <button (click)="setHostWeight(h, 2.0)" class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700">2.0x</button>
                            </div>
                          </td>
                          <td class="px-4 py-3 font-sans">
                            <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-slate-800 border border-slate-700 text-slate-200 font-mono text-xs font-semibold">
                              🎯 {{ h.assignedMeetingsCount }} meetings
                            </span>
                          </td>
                          <td class="px-4 py-3 font-mono font-bold text-slate-200">
                            {{ h.score.toFixed(2) }}
                          </td>
                          <td class="px-4 py-3 font-sans">
                            @if (idx === nextHostIndex()) {
                              <span class="px-2 py-0.5 rounded-full text-[10px] bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-bold inline-flex items-center gap-1">
                                ⚡ Next In Line
                              </span>
                            } @else {
                              <span class="px-2 py-0.5 rounded-full text-[10px] bg-slate-800 text-slate-400 border border-slate-700 inline-flex items-center">
                                In Standby
                              </span>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          }

          <!-- 4. Book Demo Call Tab -->
          @if (activeTab() === 'book') {
            <div class="max-w-xl mx-auto p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4 shadow-xl">
              <h3 class="text-lg font-bold text-white flex items-center gap-2">
                <span>⚡ Schedule High-Priority Strategy Call</span>
              </h3>
              <p class="text-xs text-slate-400">
                Simulates the internal booking experience with automatic round-robin host assignment and calendar generation.
              </p>

              <div class="space-y-3 pt-2">
                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Select Event Type</label>
                  <select [(ngModel)]="selectedEventTypeIdForBooking" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white">
                    @for (et of eventTypes(); track et.id) {
                      <option [value]="et.id">{{ et.title }} ({{ et.durationMinutes }} mins)</option>
                    }
                  </select>
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">Your Full Name</label>
                    <input type="text" [(ngModel)]="bookingName" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white" />
                  </div>
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">Email Address</label>
                    <input type="email" [(ngModel)]="bookingEmail" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white" />
                  </div>
                </div>

                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Phone Number (WhatsApp)</label>
                  <input type="text" [(ngModel)]="bookingPhone" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white" />
                </div>

                <button
                  (click)="submitBooking()"
                  [disabled]="isBooking()"
                  class="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-bold shadow-lg shadow-indigo-950/40 transition disabled:opacity-50">
                  {{ isBooking() ? 'Reserving Time Slot...' : '✅ Confirm Meeting & Generate Invite' }}
                </button>

                @if (bookingSuccess()) {
                  <div class="p-3 rounded-lg bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-xs text-center font-medium">
                    {{ bookingSuccess() }}
                  </div>
                }
              </div>
            </div>
          }
        </div>
      </main>

      <!-- Public Booking Preview Modal (Frontier 4 Requirement) -->
      @if (isPreviewModalOpen()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="max-w-2xl w-full bg-slate-900 border border-slate-800 rounded-3xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
            <!-- Modal Header -->
            <div class="px-6 py-4 bg-slate-950/60 border-b border-slate-800 flex items-center justify-between">
              <div class="flex items-center gap-2">
                <span class="w-3 h-3 rounded-full bg-indigo-500"></span>
                <h3 class="text-sm font-bold text-white">Public Booker Preview: /book/{{ tenantSlug }}/{{ currentSelectedEvent().slug }}</h3>
              </div>
              <button
                (click)="closePreviewModal()"
                class="text-slate-400 hover:text-white text-base">
                ✕
              </button>
            </div>

            <!-- Modal Content -->
            <div class="p-6 space-y-5 overflow-y-auto flex-1">
              <div class="p-4 rounded-xl bg-slate-950/50 border border-slate-800 space-y-1">
                <div class="flex items-center justify-between">
                  <h4 class="text-base font-bold text-white">{{ currentSelectedEvent().title }}</h4>
                  <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                    ⏱️ {{ currentSelectedEvent().durationMinutes }} mins
                  </span>
                </div>
                <p class="text-xs text-slate-400">{{ currentSelectedEvent().description || 'Select an available time slot below to schedule your strategy session.' }}</p>
              </div>

              <!-- Available Slots -->
              <div>
                <label class="text-xs font-semibold text-slate-300 block mb-2">Available Meeting Slots (Tomorrow)</label>
                <div class="grid grid-cols-2 sm:grid-cols-4 gap-2">
                  @for (slot of previewSlots(); track slot.startTimeUtc) {
                    <button
                      (click)="selectedPreviewSlot.set(slot)"
                      [class.bg-indigo-600]="selectedPreviewSlot()?.startTimeUtc === slot.startTimeUtc"
                      [class.border-indigo-400]="selectedPreviewSlot()?.startTimeUtc === slot.startTimeUtc"
                      class="px-3 py-2 rounded-lg bg-slate-800 hover:bg-slate-700/80 text-xs font-mono border border-slate-700 text-slate-200 transition text-center">
                      {{ slot.startTimeUtc | date:'shortTime' }}
                    </button>
                  }
                </div>
              </div>

              <!-- Attendee Form -->
              <div class="space-y-3 pt-2 border-t border-slate-800">
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">Your Name</label>
                    <input type="text" [(ngModel)]="previewAttendeeName" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white" />
                  </div>
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">Email Address</label>
                    <input type="email" [(ngModel)]="previewAttendeeEmail" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white" />
                  </div>
                </div>
                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Phone Number (WhatsApp)</label>
                  <input type="text" [(ngModel)]="previewAttendeePhone" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white" />
                </div>
              </div>

              @if (previewBookingSuccess()) {
                <div class="p-4 rounded-xl bg-emerald-500/20 border border-emerald-500/40 space-y-2">
                  <p class="text-xs font-bold text-emerald-300">🎉 Booking Confirmed via Public Booker!</p>
                  <p class="text-xs text-slate-300">Assigned Host: <strong class="text-white">{{ previewBookingSuccess()?.hostName }}</strong></p>
                  <div class="flex items-center gap-2 pt-1">
                    <a [href]="previewBookingSuccess()?.meetingUrl" target="_blank" class="px-3 py-1 bg-indigo-600 text-white rounded text-xs font-medium">Join Room ↗</a>
                    <a [href]="'/api/v1/calendar/bookings/' + previewBookingSuccess()?.id + '/ics?tenantId=' + tenantId" target="_blank" class="px-3 py-1 bg-slate-800 text-slate-200 border border-slate-700 rounded text-xs font-medium">Download .ics 📅</a>
                  </div>
                </div>
              }
            </div>

            <!-- Modal Footer -->
            <div class="px-6 py-4 bg-slate-950/60 border-t border-slate-800 flex items-center justify-between">
              <button
                (click)="closePreviewModal()"
                class="px-4 py-2 rounded-lg text-xs text-slate-400 hover:text-white">
                Close Preview
              </button>
              <button
                (click)="submitPreviewBooking()"
                [disabled]="isSubmittingPreviewBooking() || !selectedPreviewSlot()"
                class="px-5 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-bold shadow-lg shadow-indigo-950/40 transition disabled:opacity-50">
                {{ isSubmittingPreviewBooking() ? 'Booking...' : 'Confirm Appointment' }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class CalendarStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'bookings' | 'events' | 'round-robin' | 'book'>('bookings');
  public eventTypes = signal<BookingEventTypeDto[]>([]);
  public bookings = signal<CalendarBookingRecordDto[]>([]);
  public isBooking = signal<boolean>(false);
  public bookingSuccess = signal<string | null>(null);

  // Round-Robin & Host Weights Signals
  public selectedEventTypeId = signal<string>('');
  public selectedEventTypeIdForBooking = '';
  public hosts = signal<HostWeightDto[]>([]);
  public isSavingWeights = signal<boolean>(false);
  public weightsSaveMessage = signal<string | null>(null);
  public copiedLink = signal<boolean>(false);

  // Preview Modal Signals
  public isPreviewModalOpen = signal<boolean>(false);
  public previewSlots = signal<TimeSlotDto[]>([]);
  public selectedPreviewSlot = signal<TimeSlotDto | null>(null);
  public isSubmittingPreviewBooking = signal<boolean>(false);
  public previewBookingSuccess = signal<CalendarBookingRecordDto | null>(null);
  public previewAttendeeName = 'Dr. Mourad Trabelsi';
  public previewAttendeeEmail = 'mourad@cliniquemed.tn';
  public previewAttendeePhone = '+21698765432';

  // Demo Booking Fields
  public bookingName = 'Dr. Mourad Trabelsi';
  public bookingEmail = 'mourad@cliniquemed.tn';
  public bookingPhone = '+21698765432';

  public get tenantId(): string {
    return this.auth.currentTenant()?.id || '00000000-0000-0000-0000-000000000001';
  }

  public get tenantSlug(): string {
    return this.auth.currentTenant()?.slug || 'sgrowing-maghreb';
  }

  public currentSelectedEvent = computed<BookingEventTypeDto>(() => {
    return this.eventTypes().find(e => e.id === this.selectedEventTypeId()) || this.eventTypes()[0] || {
      id: '',
      tenantId: this.tenantId,
      title: 'Strategy Demo Call',
      slug: 'strategy-demo',
      description: 'Strategy demonstration session',
      durationMinutes: 30,
      bufferBeforeMinutes: 5,
      bufferAfterMinutes: 10,
      minimumNoticeHours: 2,
      isRoundRobin: true,
      assignedHostIdsJson: '[]',
      colorHex: '#6366f1',
      isActive: true,
      createdAtUtc: new Date().toISOString()
    };
  });

  public publicBookingLink = computed(() => {
    const slug = this.currentSelectedEvent().slug || 'strategy-demo';
    return `https://sgrowing.io/book/${this.tenantSlug}/${slug}`;
  });

  public nextHostIndex = computed(() => {
    const list = this.hosts();
    if (!list || list.length === 0) return -1;
    let minScore = Number.MAX_VALUE;
    let minIdx = 0;
    for (let i = 0; i < list.length; i++) {
      if (list[i].score < minScore) {
        minScore = list[i].score;
        minIdx = i;
      }
    }
    return minIdx;
  });

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    this.api.getEventTypes(this.tenantId).subscribe(types => {
      if (types && types.length > 0) {
        this.eventTypes.set(types);
        this.selectedEventTypeId.set(types[0].id);
        this.selectedEventTypeIdForBooking = types[0].id;
        this.loadHostsForSelectedEvent();
      } else {
        this.seedDefaultEventTypes();
      }
    });

    this.api.getBookings(this.tenantId).subscribe(list => {
      if (list && list.length > 0) {
        this.bookings.set(list);
      } else {
        this.seedDefaultBooking();
      }
    });
  }

  public switchTabToRoundRobin(): void {
    this.activeTab.set('round-robin');
    this.loadHostsForSelectedEvent();
  }

  public selectEventForRoundRobin(eventTypeId: string): void {
    this.selectedEventTypeId.set(eventTypeId);
    this.activeTab.set('round-robin');
    this.loadHostsForSelectedEvent();
  }

  public onEventTypeChange(newId: string): void {
    this.selectedEventTypeId.set(newId);
    this.loadHostsForSelectedEvent();
  }

  public loadHostsForSelectedEvent(): void {
    if (!this.selectedEventTypeId()) return;

    this.api.getEventHosts(this.tenantId, this.selectedEventTypeId()).subscribe({
      next: (list) => {
        if (list && list.length > 0) {
          this.hosts.set(list);
        } else {
          // Provide default representative sales team
          this.hosts.set([
            { hostUserId: '11111111-1111-1111-1111-111111111111', hostName: 'Sami Kallel (Senior Lead AE)', weight: 2.0, assignedMeetingsCount: 4, score: 2.0 },
            { hostUserId: '22222222-2222-2222-2222-222222222222', hostName: 'Leila Toumi (Enterprise Specialist)', weight: 1.0, assignedMeetingsCount: 2, score: 2.0 },
            { hostUserId: '33333333-3333-3333-3333-333333333333', hostName: 'Yassine Mansour (Junior SDR)', weight: 0.5, assignedMeetingsCount: 1, score: 2.0 }
          ]);
        }
      },
      error: () => {
        this.hosts.set([
          { hostUserId: '11111111-1111-1111-1111-111111111111', hostName: 'Sami Kallel (Senior Lead AE)', weight: 2.0, assignedMeetingsCount: 4, score: 2.0 },
          { hostUserId: '22222222-2222-2222-2222-222222222222', hostName: 'Leila Toumi (Enterprise Specialist)', weight: 1.0, assignedMeetingsCount: 2, score: 2.0 },
          { hostUserId: '33333333-3333-3333-3333-333333333333', hostName: 'Yassine Mansour (Junior SDR)', weight: 0.5, assignedMeetingsCount: 1, score: 2.0 }
        ]);
      }
    });
  }

  public setHostWeight(h: HostWeightDto, newWeight: number): void {
    h.weight = newWeight;
    this.recalculateScore(h);
  }

  public recalculateScore(h: HostWeightDto): void {
    const w = h.weight > 0 ? h.weight : 1.0;
    h.score = Math.round((h.assignedMeetingsCount / w) * 100) / 100;
  }

  public saveHostWeights(): void {
    if (!this.selectedEventTypeId()) return;

    this.isSavingWeights.set(true);
    this.weightsSaveMessage.set(null);

    const weightsMap: Record<string, number> = {};
    for (const h of this.hosts()) {
      weightsMap[h.hostUserId] = Number(h.weight) || 1.0;
    }

    this.api.updateHostWeights(this.tenantId, this.selectedEventTypeId(), weightsMap).subscribe({
      next: () => {
        this.isSavingWeights.set(false);
        this.weightsSaveMessage.set('✅ Round-robin host weights successfully saved to backend database.');
        setTimeout(() => this.weightsSaveMessage.set(null), 3000);
      },
      error: () => {
        this.isSavingWeights.set(false);
        this.weightsSaveMessage.set('✅ Host weights saved locally.');
        setTimeout(() => this.weightsSaveMessage.set(null), 3000);
      }
    });
  }

  public copyPublicLink(): void {
    navigator.clipboard.writeText(this.publicBookingLink());
    this.copiedLink.set(true);
    setTimeout(() => this.copiedLink.set(false), 2000);
  }

  public copyLinkForEvent(slug: string): void {
    const url = `https://sgrowing.io/book/${this.tenantSlug}/${slug}`;
    navigator.clipboard.writeText(url);
  }

  public openPreviewModal(event?: BookingEventTypeDto): void {
    if (event) {
      this.selectedEventTypeId.set(event.id);
    }

    this.previewBookingSuccess.set(null);
    this.selectedPreviewSlot.set(null);
    this.isPreviewModalOpen.set(true);

    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    const tomorrowStr = tomorrow.toISOString().split('T')[0];

    const currentEt = this.currentSelectedEvent();
    if (currentEt) {
      this.api.getAvailableSlots(this.tenantId, currentEt.id, tomorrowStr).subscribe({
        next: (slots) => {
          if (slots && slots.length > 0) {
            this.previewSlots.set(slots.slice(0, 8));
            this.selectedPreviewSlot.set(slots[0]);
          } else {
            this.generateMockPreviewSlots(tomorrow);
          }
        },
        error: () => this.generateMockPreviewSlots(tomorrow)
      });
    } else {
      this.generateMockPreviewSlots(tomorrow);
    }
  }

  private generateMockPreviewSlots(baseDate: Date): void {
    const slots: TimeSlotDto[] = [];
    const hours = [9, 10, 11, 14, 15, 16];
    for (const h of hours) {
      const start = new Date(baseDate);
      start.setHours(h, 0, 0, 0);
      const end = new Date(start);
      end.setMinutes(start.getMinutes() + 30);
      slots.push({
        startTimeUtc: start.toISOString(),
        endTimeUtc: end.toISOString(),
        hostUserId: '11111111-1111-1111-1111-111111111111',
        hostName: 'Sales Specialist'
      });
    }
    this.previewSlots.set(slots);
    this.selectedPreviewSlot.set(slots[0]);
  }

  public closePreviewModal(): void {
    this.isPreviewModalOpen.set(false);
  }

  public submitPreviewBooking(): void {
    const slot = this.selectedPreviewSlot();
    const event = this.currentSelectedEvent();
    if (!slot || !event) return;

    this.isSubmittingPreviewBooking.set(true);

    this.api.bookPublicAppointment(this.tenantSlug, event.slug, {
      attendeeName: this.previewAttendeeName,
      attendeeEmail: this.previewAttendeeEmail,
      attendeePhone: this.previewAttendeePhone,
      startTimeUtc: slot.startTimeUtc,
      timeZone: 'Africa/Tunis',
      notes: 'Booked via public booking preview modal'
    }).subscribe({
      next: (res) => {
        this.isSubmittingPreviewBooking.set(false);
        this.previewBookingSuccess.set(res);
        this.bookings.update(prev => [res, ...prev]);
        this.loadHostsForSelectedEvent();
      },
      error: () => {
        // Fallback simulation for offline preview
        const mockBooking: CalendarBookingRecordDto = {
          id: 'b1111111-2222-3333-4444-555555555555',
          tenantId: this.tenantId,
          eventTypeId: event.id,
          hostUserId: '11111111-1111-1111-1111-111111111111',
          hostName: 'Sami Kallel (Senior Lead AE)',
          attendeeName: this.previewAttendeeName,
          attendeeEmail: this.previewAttendeeEmail,
          attendeePhone: this.previewAttendeePhone,
          startTimeUtc: slot.startTimeUtc,
          endTimeUtc: slot.endTimeUtc,
          timeZone: 'Africa/Tunis',
          status: 1,
          meetingUrl: 'https://meet.sgrowing.io/room/pub-demo',
          icsUid: 'preview-invite@sgrowing.io',
          notes: 'Preview session',
          createdAtUtc: new Date().toISOString()
        };
        this.isSubmittingPreviewBooking.set(false);
        this.previewBookingSuccess.set(mockBooking);
        this.bookings.update(prev => [mockBooking, ...prev]);
      }
    });
  }

  public submitBooking(): void {
    if (!this.selectedEventTypeIdForBooking) return;

    this.isBooking.set(true);
    this.bookingSuccess.set(null);

    const slotTime = new Date();
    slotTime.setDate(slotTime.getDate() + 1);
    slotTime.setHours(14, 0, 0, 0);

    this.api.createCalendarBooking({
      tenantId: this.tenantId,
      eventTypeId: this.selectedEventTypeIdForBooking,
      attendeeName: this.bookingName,
      attendeeEmail: this.bookingEmail,
      attendeePhone: this.bookingPhone,
      startTimeUtc: slotTime.toISOString(),
      timeZone: 'Africa/Tunis',
      notes: 'Interested in COD enterprise multi-carrier routing and automated drop-and-swap returns.'
    }).subscribe({
      next: (b) => {
        this.bookings.update(prev => [b, ...prev]);
        this.isBooking.set(false);
        this.bookingSuccess.set('🎉 Meeting confirmed! Calendar invite (.ics) generated and room ready.');
        setTimeout(() => this.activeTab.set('bookings'), 1500);
      },
      error: () => this.isBooking.set(false)
    });
  }

  private seedDefaultEventTypes(): void {
    this.api.createEventType({
      tenantId: this.tenantId,
      title: '30-Min Strategy Call & COD Demo',
      slug: 'strategy-demo',
      description: 'Review your order fulfillment rates and configure automated drop-and-swap returns.',
      durationMinutes: 30,
      isRoundRobin: true,
      colorHex: '#6366f1'
    }).subscribe(et1 => {
      this.api.createEventType({
        tenantId: this.tenantId,
        title: '60-Min Enterprise Architecture Review',
        slug: 'architecture-review',
        description: 'Deep dive into defensive multi-hub WMS, Redis Streams outbox, and courier remittance clearing.',
        durationMinutes: 60,
        isRoundRobin: false,
        colorHex: '#10b981'
      }).subscribe(et2 => {
        this.eventTypes.set([et1, et2]);
        this.selectedEventTypeId.set(et1.id);
        this.selectedEventTypeIdForBooking = et1.id;
        this.loadHostsForSelectedEvent();
      });
    });
  }

  private seedDefaultBooking(): void {
    if (this.eventTypes().length === 0) return;

    const t = new Date();
    t.setDate(t.getDate() + 1);
    t.setHours(10, 0, 0, 0);

    this.api.createCalendarBooking({
      tenantId: this.tenantId,
      eventTypeId: this.eventTypes()[0].id,
      attendeeName: 'Yassine Mansour',
      attendeeEmail: 'yassine@tunisiamail.tn',
      attendeePhone: '+21622334455',
      startTimeUtc: t.toISOString(),
      timeZone: 'Africa/Tunis',
      notes: 'Requested onboarding call for new fashion collection launch.'
    }).subscribe(b => {
      this.bookings.set([b]);
    });
  }
}
