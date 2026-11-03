// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SocialContentCalendarStudioComponent } from './social-content-calendar-studio.component';

describe('SocialContentCalendarStudioComponent', () => {
  let component: SocialContentCalendarStudioComponent;
  let httpMock: any;
  let routerMock: any;

  beforeEach(() => {
    httpMock = {
      get: vi.fn().mockImplementation(() => throwError(() => new Error('Offline fallback'))),
      post: vi.fn().mockReturnValue(of({ isValid: true, errors: [], warnings: [], platformResults: {} }))
    };

    routerMock = {
      navigate: vi.fn()
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpMock },
      { provide: Router, useValue: routerMock }
    ]);

    injector.runInContext(() => {
      component = new SocialContentCalendarStudioComponent();
      component.ngOnInit();
    });
  });

  it('initializes and populates fallback connected accounts and scheduled posts', () => {
    expect(component.connectedAccounts().length).toBeGreaterThan(0);
    expect(component.allPosts().length).toBeGreaterThan(0);
    expect(component.isLoading()).toBe(false);
  });

  it('computes calendar days for the current month view', () => {
    const days = component.calendarDays();
    expect(days.length).toBeGreaterThanOrEqual(28);
    expect(days.some(d => d.isToday)).toBe(true);
  });

  it('navigates through months with changeMonth and returns to today with goToToday', () => {
    const initialMonth = component.currentDate().getMonth();

    component.changeMonth(1);
    expect(component.currentDate().getMonth()).toBe((initialMonth + 1) % 12);

    component.changeMonth(-1);
    expect(component.currentDate().getMonth()).toBe(initialMonth);

    component.goToToday();
    const today = new Date();
    expect(component.currentDate().getDate()).toBe(today.getDate());
  });

  it('toggles accounts drawer visibility', () => {
    expect(component.showAccountsDrawer()).toBe(false);

    component.toggleAccountsDrawer();
    expect(component.showAccountsDrawer()).toBe(true);

    component.toggleAccountsDrawer();
    expect(component.showAccountsDrawer()).toBe(false);
  });

  it('initializes composer modal with default platforms and media', () => {
    component.openComposerModal();

    expect(component.composerTargetPlatforms()).toEqual(['X', 'LinkedIn']);
    expect(component.composerMedia().length).toBe(1);
    expect(component.composerTagsInput).toContain('SaaS');
    expect(component.activeOverrideTab()).toBe('BASE');
  });
});
