import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { BookingEventTypeDto, CalendarBookingRecordDto, TimeSlotDto } from '../../core/models/calendar.models';

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
              Category 16 • CalNode & Cal.diy Mesh
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
                          <a [href]="'http://localhost:5000/api/calendar/bookings/' + b.id + '/ics?tenantId=' + tenantId" target="_blank" class="px-2 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs border border-slate-700 transition">
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
                    <div class="flex justify-between">
                      <span>Slug:</span>
                      <span class="text-slate-200">/book/{{ et.slug }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span>Routing:</span>
                      <span class="text-emerald-400">{{ et.isRoundRobin ? 'Round-Robin Lead Rotation' : 'Single Host' }}</span>
                    </div>
                  </div>
                </div>
              }
            </div>
          }

          <!-- 3. Book Demo Call Tab -->
          @if (activeTab() === 'book') {
            <div class="max-w-xl mx-auto p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4 shadow-xl">
              <h3 class="text-lg font-bold text-white flex items-center gap-2">
                <span>⚡ Schedule High-Priority Strategy Call</span>
              </h3>
              <p class="text-xs text-slate-400">
                Simulates the public booking experience. The temporal engine checks host calendars, prevents double-booking, and sends meeting invitations.
              </p>

              <div class="space-y-3 pt-2">
                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Select Event Type</label>
                  <select [(ngModel)]="selectedEventTypeId" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white">
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
    </div>
  `
})
export class CalendarStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'bookings' | 'events' | 'book'>('bookings');
  public eventTypes = signal<BookingEventTypeDto[]>([]);
  public bookings = signal<CalendarBookingRecordDto[]>([]);
  public isBooking = signal<boolean>(false);
  public bookingSuccess = signal<string | null>(null);

  public selectedEventTypeId = '';
  public bookingName = 'Dr. Mourad Trabelsi';
  public bookingEmail = 'mourad@cliniquemed.tn';
  public bookingPhone = '+21698765432';

  public get tenantId(): string {
    return this.auth.currentTenant()?.id || '00000000-0000-0000-0000-000000000001';
  }

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    this.api.getEventTypes(this.tenantId).subscribe(types => {
      if (types && types.length > 0) {
        this.eventTypes.set(types);
        this.selectedEventTypeId = types[0].id;
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

  public submitBooking(): void {
    if (!this.selectedEventTypeId) return;

    this.isBooking.set(true);
    this.bookingSuccess.set(null);

    const slotTime = new Date();
    slotTime.setDate(slotTime.getDate() + 1);
    slotTime.setHours(14, 0, 0, 0);

    this.api.createCalendarBooking({
      tenantId: this.tenantId,
      eventTypeId: this.selectedEventTypeId,
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
        this.selectedEventTypeId = et1.id;
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
