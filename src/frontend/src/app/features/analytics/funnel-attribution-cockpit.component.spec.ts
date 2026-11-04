// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { FunnelAttributionCockpitComponent } from './funnel-attribution-cockpit.component';

describe('FunnelAttributionCockpitComponent', () => {
  let component: FunnelAttributionCockpitComponent;

  beforeEach(() => {
    component = new FunnelAttributionCockpitComponent();
    component.ngOnInit();
  });

  it('should initialize with default Time-Decay attribution model and funnel stages', () => {
    expect(component).toBeTruthy();
    expect(component.selectedModel()).toBe('TimeDecay');
    expect(component.currentModelLabel()).toContain('Time-Decay');
    expect(component.funnelStages.length).toBe(5);
    expect(component.kpiTotalRevenue()).toBe(142500);
    expect(component.kpiBlendedCac()).toBe(320);
    expect(component.kpiBlendedRoas()).toBe(4.8);
  });

  it('should switch attribution models and dynamically re-weight channel revenue and CAC', () => {
    // 1. FirstTouch Model
    component.selectedModel.set('FirstTouch');
    expect(component.kpiTotalRevenue()).toBe(138000);
    expect(component.kpiBlendedCac()).toBe(345);
    const firstTouchChannels = component.activeChannelData();
    expect(firstTouchChannels[0].attributedWeight).toBe(0.45); // Cold outreach gets highest acquisition credit

    // 2. LastTouch Model
    component.selectedModel.set('LastTouch');
    expect(component.kpiTotalRevenue()).toBe(146000);
    expect(component.kpiBlendedCac()).toBe(310);
    const lastTouchChannels = component.activeChannelData();
    const linkedin = lastTouchChannels.find(c => c.channel.includes('LinkedIn'));
    expect(linkedin?.attributedWeight).toBe(0.40); // LinkedIn sales navigator gets closing credit

    // 3. Linear Model
    component.selectedModel.set('Linear');
    const linearChannels = component.activeChannelData();
    expect(linearChannels.every(c => c.attributedWeight === 0.25)).toBe(true);
  });

  it('should calculate touchpoint weight across stages according to active model', () => {
    const totalTouchpoints = 4;

    component.selectedModel.set('FirstTouch');
    expect(component.calculateWeightForModel(0, totalTouchpoints)).toBe(1.0);
    expect(component.calculateWeightForModel(1, totalTouchpoints)).toBe(0.0);

    component.selectedModel.set('LastTouch');
    expect(component.calculateWeightForModel(0, totalTouchpoints)).toBe(0.0);
    expect(component.calculateWeightForModel(3, totalTouchpoints)).toBe(1.0);

    component.selectedModel.set('Linear');
    expect(component.calculateWeightForModel(1, totalTouchpoints)).toBe(0.25);
  });

  it('should provide deal journey touchpoints breakdown with cost and timestamps', () => {
    const journey = component.dealJourney();
    expect(journey.length).toBe(4);
    expect(journey[0].stage).toBe('Discovery');
    expect(journey[3].stage).toBe('Closing');
  });

  it('should compute correct currentModelDescription for each attribution model', () => {
    component.selectedModel.set('TimeDecay');
    expect(component.currentModelDescription()).toContain('half-life exponential decay');

    component.selectedModel.set('FirstTouch');
    expect(component.currentModelDescription()).toContain('initial discovery touchpoint');

    component.selectedModel.set('LastTouch');
    expect(component.currentModelDescription()).toContain('final touchpoint');

    component.selectedModel.set('Linear');
    expect(component.currentModelDescription()).toContain('equal distribution');
  });
});
