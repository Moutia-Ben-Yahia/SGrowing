// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { SocialListeningStudioComponent } from './social-listening-studio.component';

describe('SocialListeningStudioComponent', () => {
  let component: SocialListeningStudioComponent;

  beforeEach(() => {
    const injector = createEnvironmentInjector([]);
    injector.runInContext(() => {
      component = new SocialListeningStudioComponent();
    });
  });

  it('initializes with seed mentions and computes sentiment metrics', () => {
    expect(component.mentions().length).toBeGreaterThan(0);
    expect(component.negativeRatio()).toBeGreaterThan(0);
    expect(component.activeCrisisCount()).toBeGreaterThanOrEqual(1);
    expect(component.currentThreat()).toBe('ElevatedYellow');
  });

  it('filters mentions by selected tab', () => {
    expect(component.selectedTab()).toBe('All');
    expect(component.filteredMentions().length).toBe(component.mentions().length);

    component.selectedTab.set('Negative');
    const negativeCount = component.mentions().filter(m => m.sentiment === 'Negative').length;
    expect(component.filteredMentions().length).toBe(negativeCount);

    component.selectedTab.set('SevereHostile');
    const hostileCount = component.mentions().filter(m => m.sentiment === 'SevereHostile').length;
    expect(component.filteredMentions().length).toBe(hostileCount);
  });

  it('toggles emergency marketing broadcast pause', () => {
    expect(component.isHalted()).toBe(false);

    component.toggleBroadcasts();
    expect(component.isHalted()).toBe(true);

    component.toggleBroadcasts();
    expect(component.isHalted()).toBe(false);
  });

  it('computes styling classes for threat banners, platforms, and sentiment badges', () => {
    expect(component.getThreatBannerClass()).toContain('bg-slate-800/60');

    component.currentThreat.set('CrisisRedEmergency');
    expect(component.getThreatBannerClass()).toContain('border-rose-600/50');

    expect(component.getPlatformBadge('TwitterX')).toContain('text-sky-300');
    expect(component.getPlatformBadge('Trustpilot')).toContain('text-emerald-300');
    expect(component.getSentimentBadge('Positive')).toContain('text-emerald-400');
    expect(component.getSentimentBadge('SevereHostile')).toContain('text-rose-300');
  });

  it('should return 0 negativeRatio when mentions list is empty and handle HighAlertOrange threat', () => {
    component.mentions.set([]);
    expect(component.negativeRatio()).toBe(0);

    component.currentThreat.set('HighAlertOrange');
    expect(component.getThreatBannerClass()).toContain('border-amber-600/50');
  });
});
