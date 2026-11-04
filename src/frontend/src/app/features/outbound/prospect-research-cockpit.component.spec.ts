// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { ProspectResearchCockpitComponent } from './prospect-research-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('ProspectResearchCockpitComponent', () => {
  let component: ProspectResearchCockpitComponent;
  let translationMock: any;

  beforeEach(() => {
    translationMock = {
      t: (key: string) => key,
      currentLang: signal('en'),
      dir: signal('ltr'),
      isRtl: signal(false),
      setLanguage: vi.fn()
    };

    const injector = createEnvironmentInjector([
      { provide: TranslationService, useValue: translationMock }
    ]);

    injector.runInContext(() => {
      component = new ProspectResearchCockpitComponent();
    });
  });

  it('initializes with default target domain and populated active brief', () => {
    expect(component.targetDomain).toBe('hyper-logistics.tn');
    expect(component.activeBrief()).toBeDefined();
    expect(component.activeBrief()?.companyName).toBe('HyperLogistics');
    expect(component.activeBrief()?.executiveBios.length).toBeGreaterThanOrEqual(3);
    expect(component.activeBrief()?.techTags.length).toBeGreaterThan(0);
    expect(component.activeBrief()?.salesHooks.length).toBeGreaterThan(0);
  });

  it('updates target domain via setPreset', () => {
    component.setPreset('cloudflow-saas.com');
    expect(component.targetDomain).toBe('cloudflow-saas.com');
  });

  it('toggles scraper vault modal and inspects crawler engine health', () => {
    expect(component.openVaultModal()).toBe(false);

    component.openVaultModal.set(true);
    expect(component.openVaultModal()).toBe(true);

    const engines = component.vaultEngines();
    expect(engines.length).toBe(3);
    expect(engines.some(e => e.provider === 'Tavily')).toBe(true);
    expect(engines.some(e => e.provider === 'Firecrawl')).toBe(true);
    expect(engines.some(e => e.provider === 'Exa')).toBe(true);
  });

  it('executes runDeepCrawl and handles empty domain guards', () => {
    component.targetDomain = '   ';
    component.runDeepCrawl();
    expect(component.isCrawling()).toBe(false);

    component.targetDomain = 'fintech-tunisia.io';
    component.runDeepCrawl();
    expect(component.isCrawling()).toBe(true);
  });

  it('should set toast notification when copyToClipboard or injectIntoCadence is invoked', () => {
    component.injectIntoCadence('Custom AI SDR hook');
    expect(component.toastMessage()).toContain('Injected sales hook into Active Cold Outreach Sequence!');

    component.copyToClipboard('Sales hook sample');
    expect(component.toastMessage()).toBe('Copied sales hook to clipboard!');
  });
});
