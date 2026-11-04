import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { MultiTouchAttributionCockpitComponent } from './multi-touch-attribution-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('MultiTouchAttributionCockpitComponent', () => {
  let component: MultiTouchAttributionCockpitComponent;
  let mockTranslation: TranslationService;

  beforeEach(() => {
    mockTranslation = new TranslationService();
    component = new MultiTouchAttributionCockpitComponent(mockTranslation);
  });

  it('initializes with W-Shaped model and valid portfolio ROAS', () => {
    expect(component.activeModel()).toBe('WShaped');
    expect(component.totalRevenue()).toBe(245000);
    expect(component.blendedRoas()).toBeGreaterThan(3.0);
    expect(component.currentChannelShares().length).toBe(5);
  });

  it('switches attribution models and recalculates channel shares', () => {
    component.selectModel('FirstTouch');
    expect(component.activeModel()).toBe('FirstTouch');
    const seoShare = component.currentChannelShares().find(c => c.channel.includes('Organic Search'));
    expect(seoShare?.weightPercent).toBe(45);

    component.selectModel('LastTouch');
    expect(component.activeModel()).toBe('LastTouch');
    const sdrShare = component.currentChannelShares().find(c => c.channel.includes('Outbound SDR'));
    expect(sdrShare?.weightPercent).toBe(40);
  });

  it('ensures weights total 100% across all models', () => {
    for (const model of component.models) {
      component.selectModel(model);
      const totalWeight = component.currentChannelShares().reduce((sum, c) => sum + c.weightPercent, 0);
      expect(totalWeight).toBe(100);
    }
  });

  it('returns human-friendly model labels', () => {
    expect(component.getModelLabel('WShaped')).toContain('W-Shaped');
    expect(component.getModelLabel('TimeDecay')).toContain('Time-Decay');
  });

  it('should recalculate blendedRoas when totalSpend is updated', () => {
    component.totalSpend.set(49000);
    expect(component.blendedRoas()).toBe(5.0);
    component.totalSpend.set(0);
    expect(component.blendedRoas()).toBe(245000);
  });
});
