// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { of } from 'rxjs';
import { createEnvironmentInjector, signal } from '@angular/core';
import { CalendarStudioComponent } from './calendar-studio.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';

describe('CalendarStudioComponent', () => {
  let component: CalendarStudioComponent;
  let mockApi: any;
  let mockAuth: any;

  beforeEach(() => {
    vi.spyOn(window, 'alert').mockImplementation(() => {});
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined)
      }
    });

    mockApi = {
      getEventTypes: vi.fn().mockReturnValue(of([
        {
          id: 'ev-1',
          name: 'Executive Demo',
          slug: 'demo',
          durationMinutes: 30,
          isActive: true
        }
      ])),
      getUpcomingBookings: vi.fn().mockReturnValue(of([
        {
          id: 'b-1',
          eventTypeId: 'ev-1',
          customerName: 'Yassine Triki',
          customerEmail: 'yassine@example.tn',
          status: 'Confirmed',
          startTimeUtc: new Date().toISOString()
        }
      ])),
      getHostWeights: vi.fn().mockReturnValue(of([
        {
          hostUserId: 'host-1',
          hostName: 'Sami Karray',
          weight: 1.0,
          assignedMeetingsCount: 5,
          score: 5.0
        }
      ])),
      updateHostWeights: vi.fn().mockReturnValue(of({ success: true })),
      getAvailableSlots: vi.fn().mockReturnValue(of([
        {
          startTimeUtc: new Date().toISOString(),
          endTimeUtc: new Date().toISOString(),
          hostUserId: 'host-1',
          hostName: 'Sami Karray'
        }
      ]))
    };

    mockAuth = {
      currentTenant: signal({ id: 'tenant-cal-123', slug: 'sgrowing-tn' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: mockApi },
      { provide: AuthService, useValue: mockAuth }
    ]);

    injector.runInContext(() => {
      component = new CalendarStudioComponent();
    });
  });

  it('should initialize with default bookings tab', () => {
    expect(component).toBeTruthy();
    expect(component.activeTab()).toBe('bookings');
  });

  it('should navigate between calendar studio tabs', () => {
    component.activeTab.set('events');
    expect(component.activeTab()).toBe('events');

    component.switchTabToRoundRobin();
    expect(component.activeTab()).toBe('round-robin');

    component.activeTab.set('book');
    expect(component.activeTab()).toBe('book');
  });

  it('should adjust host weight and recalculate round-robin score', () => {
    const host = {
      hostUserId: 'host-1',
      hostName: 'Sami Karray',
      weight: 1.0,
      assignedMeetingsCount: 10,
      score: 10.0
    };

    component.setHostWeight(host, 2.0);
    expect(host.weight).toBe(2.0);
    expect(host.score).toBe(5.0); // 10 / 2.0
  });

  it('should open and close slot preview modal', () => {
    component.openPreviewModal({
      id: 'ev-1',
      name: 'Executive Demo',
      slug: 'demo',
      durationMinutes: 30,
      isActive: true
    } as any);

    expect(component.isPreviewModalOpen()).toBe(true);
    expect(component.previewSlots().length).toBeGreaterThan(0);

    component.closePreviewModal();
    expect(component.isPreviewModalOpen()).toBe(false);
  });
});
