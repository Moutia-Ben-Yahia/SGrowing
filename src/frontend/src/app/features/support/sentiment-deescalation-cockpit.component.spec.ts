// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SentimentDeescalationCockpitComponent } from './sentiment-deescalation-cockpit.component';

describe('SentimentDeescalationCockpitComponent', () => {
  it('initializes with seed customer incidents and calculates metrics', () => {
    const comp = new SentimentDeescalationCockpitComponent();
    expect(comp.incidents().length).toBe(3);
    expect(comp.criticalCount()).toBe(2);
    expect(comp.resolvedCount()).toBe(1);
    expect(comp.avgSentiment()).toBeCloseTo(-0.21, 2);
  });

  it('applies playbook to de-escalate customer incident', () => {
    const comp = new SentimentDeescalationCockpitComponent();
    const criticalInc = comp.incidents().find(i => i.incidentId === 'inc-sd-501');
    expect(criticalInc?.status).toBe('ActiveIntervention');

    comp.applyPlaybook(criticalInc!);
    const updated = comp.incidents().find(i => i.incidentId === 'inc-sd-501');

    expect(updated?.status).toBe('Deescalated');
    expect(updated?.riskTier).toBe('Low');
    expect(updated?.sentimentScore).toBe(0.45);
    expect(comp.criticalCount()).toBe(1);
    expect(comp.resolvedCount()).toBe(2);
  });

  it('simulates a new critical churn risk incident', () => {
    const comp = new SentimentDeescalationCockpitComponent();
    const beforeCount = comp.incidents().length;
    comp.simulateCriticalIncident();

    expect(comp.incidents().length).toBe(beforeCount + 1);
    expect(comp.incidents()[0].customerName).toBe('Pacific Commerce Ltd');
    expect(comp.incidents()[0].riskTier).toBe('Critical');
  });

  it('should return 0 metrics when incidents list is empty', () => {
    const comp = new SentimentDeescalationCockpitComponent();
    comp.incidents.set([]);

    expect(comp.criticalCount()).toBe(0);
    expect(comp.resolvedCount()).toBe(0);
    expect(comp.avgSentiment()).toBe(0);
  });

  it('should handle applyPlaybook for unknown incident without mutating existing list', () => {
    const comp = new SentimentDeescalationCockpitComponent();
    const initial = [...comp.incidents()];
    comp.applyPlaybook({
      incidentId: 'unknown-id-999',
      customerName: 'Ghost Corp',
      channel: 'Zendesk',
      sentimentScore: -0.5,
      riskTier: 'Critical',
      triggerReason: 'None',
      recommendedPlaybook: 'None',
      status: 'ActiveIntervention',
      timestamp: '2026-11-03T01:00:00Z'
    });

    expect(comp.incidents().length).toBe(initial.length);
    expect(comp.incidents()).toEqual(initial);
  });
});
