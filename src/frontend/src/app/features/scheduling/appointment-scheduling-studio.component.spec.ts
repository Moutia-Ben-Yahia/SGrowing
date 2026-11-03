// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { AppointmentSchedulingStudioComponent, HostSchedule, Appointment, TimeSlot, RoutingStatsResult } from './appointment-scheduling-studio.component';

describe('AppointmentSchedulingStudioComponent', () => {
  let component: AppointmentSchedulingStudioComponent;
  let mockHttp: any;

  const mockHosts: HostSchedule[] = [
    {
      hostId: 'h1',
      hostName: 'Dr. Karim',
      hostEmail: 'karim@clinic.tn',
      timeZoneId: 'UTC',
      weight: 3,
      isActive: true,
      bufferTime: '00:10:00',
      minimumNotice: '01:00:00',
      workingHours: [],
      externalBusyIntervals: [],
      specialties: ['COD Optimization'],
      weightedRoundRobinAssignments: 5
    },
    {
      hostId: 'h2',
      hostName: 'Sarra Ben',
      hostEmail: 'sarra@clinic.tn',
      timeZoneId: 'UTC',
      weight: 2,
      isActive: true,
      bufferTime: '00:10:00',
      minimumNotice: '01:00:00',
      workingHours: [],
      externalBusyIntervals: [],
      specialties: ['Enterprise Sales'],
      weightedRoundRobinAssignments: 3
    }
  ];

  const mockAppointments: Appointment[] = [
    {
      id: 'apt-1',
      hostId: 'h1',
      hostName: 'Dr. Karim',
      hostEmail: 'karim@clinic.tn',
      title: 'Strategic Consultation',
      attendeeName: 'Sophia Alami',
      attendeeEmail: 'sophia@enterprise.com',
      startUtc: '2026-11-04T10:00:00Z',
      endUtc: '2026-11-04T10:30:00Z',
      durationMinutes: 30,
      prospectTimeZoneId: 'UTC',
      status: 'Confirmed',
      meetingLink: 'https://meet.sgrowing.io/apt-1',
      sequenceNumber: 1,
      topologyUsed: 'WeightedRoundRobin',
      createdAtUtc: '2026-11-03T10:00:00Z',
      updatedAtUtc: '2026-11-03T10:00:00Z'
    }
  ];

  const mockSlots: TimeSlot[] = [
    {
      startUtc: '2026-11-04T10:00:00Z',
      endUtc: '2026-11-04T10:30:00Z',
      startLocalFormatted: '10:00 AM',
      endLocalFormatted: '10:30 AM',
      timeZoneId: 'UTC',
      hostId: 'h1',
      hostName: 'Dr. Karim'
    }
  ];

  const mockRoutingStats: RoutingStatsResult = {
    totalAppointments: 12,
    activeAppointments: 10,
    cancelledAppointments: 2,
    topologyBookingsCount: {
      WeightedRoundRobin: 8,
      LeastBusy: 2,
      Collective: 2
    },
    hostMetrics: [
      {
        hostId: 'h1',
        hostName: 'Dr. Karim',
        hostEmail: 'karim@clinic.tn',
        timeZoneId: 'UTC',
        weight: 3,
        isActive: true,
        confirmedBookingsCount: 6,
        totalAssignedCount: 7,
        utilizationRatio: 0.6
      }
    ]
  };

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/hosts')) return of(mockHosts);
        if (url.includes('/routing-stats')) return of(mockRoutingStats);
        if (url.includes('/slots')) return of(mockSlots);
        return of(mockAppointments);
      }),
      post: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/book')) {
          return of({
            ...mockAppointments[0],
            id: 'apt-new',
            meetingLink: 'https://meet.sgrowing.io/apt-new'
          });
        }
        return of({ success: true });
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new AppointmentSchedulingStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load active hosts, appointments, stats, and time slots', () => {
    expect(component).toBeTruthy();
    expect(mockHttp.get).toHaveBeenCalled();
    expect(component.hosts().length).toBe(2);
    expect(component.appointments().length).toBe(1);
    expect(component.slots().length).toBe(1);
    expect(component.routingStats()?.totalAppointments).toBe(12);
  });

  it('should update booking topology and trigger slots refresh', () => {
    component.setTopology('LeastBusy');
    expect(component.selectedTopology()).toBe('LeastBusy');
    expect(mockHttp.get).toHaveBeenCalledWith(expect.stringContaining('topology=LeastBusy'));
  });

  it('should update session duration and trigger slot recalculation', () => {
    component.setDuration(60);
    expect(component.selectedDuration()).toBe(60);
    expect(mockHttp.get).toHaveBeenCalledWith(expect.stringContaining('duration=60'));
  });

  it('should book selected slot and show confirmation message with meeting room link', () => {
    component.selectedSlot.set(mockSlots[0]);
    component.confirmBooking();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/scheduling/appointments/book',
      expect.objectContaining({
        title: expect.any(String),
        startUtc: mockSlots[0].startUtc,
        durationMinutes: 30
      })
    );
    expect(component.statusMessage()).toContain('Appointment confirmed!');
    expect(component.selectedSlot()).toBeNull();
  });

  it('should handle appointment cancellation when confirmed by user', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    component.cancelAppointment('apt-1');

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/scheduling/appointments/cancel/apt-1',
      expect.objectContaining({ reason: expect.any(String) })
    );
    expect(component.statusMessage()).toBe('Appointment cancelled.');
  });
});
