// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { JourneyStudioComponent } from './journey-studio.component';
import { ApiService } from '../../core/services/api.service';

describe('JourneyStudioComponent', () => {
  let component: JourneyStudioComponent;
  let mockApi: any;

  beforeEach(() => {
    mockApi = {
      getJourneys: vi.fn().mockReturnValue(of([
        {
          id: 'j-custom-1',
          tenantId: '00000000-0000-0000-0000-000000000001',
          name: 'Post-Delivery Review Follow-up',
          triggerEvent: 'delivery.completed',
          status: 2,
          createdAtUtc: '2026-10-25T10:00:00Z',
          steps: [
            {
              id: 's-10',
              stepOrder: 1,
              stepType: 2, // WhatsApp
              subjectTemplate: '',
              bodyTemplate: 'Hi {{first_name}}, how was your courier experience?',
              delaySeconds: 1800
            }
          ]
        }
      ])),
      triggerEvent: vi.fn().mockReturnValue(of({})),
      dispatchJourneys: vi.fn().mockReturnValue(of({ processed: 8 }))
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi }
      ]
    });

    component = runInInjectionContext(injector, () => new JourneyStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load customer journeys from ApiService', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getJourneys).toHaveBeenCalled();
    const list = component.journeys();
    expect(list.length).toBe(1);
    expect(list[0].name).toBe('Post-Delivery Review Follow-up');
  });

  it('should fall back to pre-seeded mock journeys when ApiService throws or returns empty', () => {
    mockApi.getJourneys.mockReturnValue(throwError(() => new Error('Server error')));

    const injector = Injector.create({
      providers: [{ provide: ApiService, useValue: mockApi }]
    });

    const fallback = runInInjectionContext(injector, () => new JourneyStudioComponent());
    fallback.ngOnInit();

    expect(fallback.journeys().length).toBe(2);
    expect(fallback.journeys()[0].name).toContain('New User Onboarding');
  });

  it('should simulate ingesting an event and set feedback banner', () => {
    component.simulateEvent('user.signed_up');

    expect(mockApi.triggerEvent).toHaveBeenCalledWith(
      expect.any(String),
      expect.any(String),
      'user.signed_up'
    );
    expect(component.feedbackMessage()).toContain('user.signed_up');
  });

  it('should trigger journey dispatch and report processed count', () => {
    component.triggerDispatch();

    expect(mockApi.dispatchJourneys).toHaveBeenCalled();
    expect(component.isDispatching()).toBe(false);
    expect(component.feedbackMessage()).toContain('Dispatched 8 pending journey step messages');
  });

  it('should handle dispatch failure gracefully with simulated message', () => {
    mockApi.dispatchJourneys.mockReturnValue(throwError(() => new Error('Offline')));

    component.triggerDispatch();

    expect(component.isDispatching()).toBe(false);
    expect(component.feedbackMessage()).toContain('Simulated journey dispatch completed');
  });
});
