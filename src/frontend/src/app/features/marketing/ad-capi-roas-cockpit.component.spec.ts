// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { AdCapiRoasCockpitComponent } from './ad-capi-roas-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('AdCapiRoasCockpitComponent (Domain C6 Advertising CAPI & ROAS Optimizer)', () => {
  let component: AdCapiRoasCockpitComponent;
  let translation: TranslationService;

  beforeEach(() => {
    localStorage.clear();
    translation = new TranslationService();
    translation.setLocale('en-US');
    component = new AdCapiRoasCockpitComponent(translation);
  });

  it('should initialize with baseline campaigns and calculated blended ROAS', () => {
    expect(component.campaigns().length).toBe(4);
    expect(component.totalSpend()).toBe(45000);
    expect(component.blendedRoas()).toBe('3.14'); // 141200 / 45000 = 3.1377... -> '3.14'
    expect(component.totalConversions()).toBe(1280);
    expect(component.capiEvents().length).toBe(2);
  });

  it('should simulate a server-to-server CAPI conversion and update feed and metrics', () => {
    const initialConversions = component.totalConversions();
    const initialFeedCount = component.capiEvents().length;

    component.simulateCapiEvent();

    expect(component.totalConversions()).toBe(initialConversions + 1);
    expect(component.capiEvents().length).toBe(initialFeedCount + 1);
    expect(component.capiEvents()[0].eventName).toBe('Purchase');
    expect(component.capiEvents()[0].platform).toBe('Meta CAPI');
    expect(component.capiEvents()[0].conversionValue).toBe(199.99);
  });

  it('should optimize budgets by trimming and pausing underperformers and scaling top performer', () => {
    component.optimizeBudgets();

    const scaleCamp = component.campaigns().find(c => c.action === 'Scale');
    const pausedCamp = component.campaigns().find(c => c.action === 'Pause');
    const trimmedCamp = component.campaigns().find(c => c.action === 'Trim');

    expect(scaleCamp).toBeDefined();
    expect(pausedCamp).toBeDefined();
    expect(trimmedCamp).toBeDefined();

    // Paused campaign should have 0 recommended spend
    expect(pausedCamp!.recommendedSpend).toBe(0);

    // Trimmed campaign should have 70% of 8000 = 5600
    expect(trimmedCamp!.recommendedSpend).toBe(5600);

    // Scale campaign should have received 4000 + 2400 = 6400 added to its 15000 = 21400
    expect(scaleCamp!.recommendedSpend).toBe(21400);
  });

  it('should sync audience hashes from raw text lines', () => {
    component.rawContactList = `test1@example.com
test2@example.com`;
    component.syncAudienceHashes();

    expect(component.previewHashes().length).toBe(2);
    expect(component.previewHashes()[0]).toHaveLength(64);
  });
});
