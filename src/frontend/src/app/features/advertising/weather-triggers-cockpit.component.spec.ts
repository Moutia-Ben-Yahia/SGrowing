// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { WeatherTriggersCockpitComponent } from './weather-triggers-cockpit.component';

describe('WeatherTriggersCockpitComponent', () => {
  it('should initialize with active rules, metros, and observations', () => {
    const comp = new WeatherTriggersCockpitComponent();
    expect(comp.activeRulesCount()).toBe(2);
    expect(comp.monitoredMetrosCount()).toBe(2);
    expect(comp.triggeredEventsCount()).toBe(1);
    expect(comp.roasLiftMultiplier()).toBe(1.42);
    expect(comp.rules().length).toBe(2);
    expect(comp.observations().length).toBe(2);
  });

  it('should trigger new weather campaign rule and increment count', () => {
    const comp = new WeatherTriggersCockpitComponent();
    const initialRules = comp.activeRulesCount();
    comp.triggerNewCampaign();

    expect(comp.activeRulesCount()).toBe(initialRules + 1);
    expect(comp.rules()[initialRules].conditionType).toBe('FREEZE_BELOW_0C');
    expect(comp.rules()[initialRules].geoTargetMetro).toBe('US_CHICAGO_IL');
  });
});
