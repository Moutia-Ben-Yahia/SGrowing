// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { AutonomousFunnelCockpitComponent } from './autonomous-funnel-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('AutonomousFunnelCockpitComponent', () => {
  let component: AutonomousFunnelCockpitComponent;
  let translationServiceMock: any;

  beforeEach(() => {
    translationServiceMock = {
      currentLang: signal('en'),
      translate: vi.fn((key: string) => key),
      setLanguage: vi.fn()
    };

    const injector = createEnvironmentInjector([
      { provide: TranslationService, useValue: translationServiceMock }
    ]);

    injector.runInContext(() => {
      component = new AutonomousFunnelCockpitComponent();
    });
  });

  it('should initialize with 15 completed steps and 100% progress', () => {
    expect(component).toBeTruthy();
    expect(component.steps().length).toBe(15);
    expect(component.overallProgress()).toBe(100);
    expect(component.currentExecutingStep()).toBe(15);
    expect(component.isRunning()).toBe(false);
  });

  it('should compute conversion rates and lift correctly', () => {
    // 140 views, 16 conv = (16/140)*100 = ~11.43%
    expect(component.conversionRateA()).toBeCloseTo(11.43, 1);
    // 145 views, 28 conv = (28/145)*100 = ~19.31%
    expect(component.conversionRateB()).toBeCloseTo(19.31, 1);
    // Lift = ((19.31 - 11.43) / 11.43) * 100 = ~68.9%
    expect(component.conversionLift()).toBeGreaterThan(50);
  });

  it('should simulate visitors and update counts', () => {
    const prevViewsA = component.viewsA();
    const prevViewsB = component.viewsB();
    const prevConvA = component.conversionsA();
    const prevConvB = component.conversionsB();

    component.simulateVisitors();

    expect(component.viewsA()).toBeGreaterThan(prevViewsA);
    expect(component.viewsB()).toBeGreaterThan(prevViewsB);
    expect(component.conversionsA()).toBeGreaterThanOrEqual(prevConvA);
    expect(component.conversionsB()).toBeGreaterThanOrEqual(prevConvB);
    expect(component.confidenceScore()).toBe(98.4);
  });

  it('should promote winner and lock traffic split to 100%', () => {
    expect(component.winnerPromoted()).toBe(false);

    component.promoteWinner();

    expect(component.trafficSplitRatio()).toBe(100);
    expect(component.winnerPromoted()).toBe(true);
  });

  it('should trigger pipeline and initialize execution steps', () => {
    vi.useFakeTimers();

    component.triggerPipeline();

    expect(component.isRunning()).toBe(true);
    expect(component.currentExecutingStep()).toBe(1);
    expect(component.steps()[0].status).toBe('Pending');

    // Advance 1 interval tick
    vi.advanceTimersByTime(200);
    expect(component.steps()[0].status).toBe('Running');

    // Fast-forward interval to complete all 15 steps
    vi.advanceTimersByTime(200 * 15);

    expect(component.isRunning()).toBe(false);
    expect(component.overallProgress()).toBe(100);
    expect(component.steps().every(s => s.status === 'Completed')).toBe(true);

    vi.useRealTimers();
  });
});
