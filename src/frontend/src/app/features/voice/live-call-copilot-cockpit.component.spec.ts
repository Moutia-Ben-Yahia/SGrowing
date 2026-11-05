// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { LiveCallCopilotCockpitComponent } from './live-call-copilot-cockpit.component';

describe('LiveCallCopilotCockpitComponent', () => {
  let component: LiveCallCopilotCockpitComponent;

  beforeEach(() => {
    component = new LiveCallCopilotCockpitComponent();
  });

  it('should initialize with diarized utterances and calculate average sentiment', () => {
    expect(component).toBeTruthy();
    expect(component.utterances().length).toBeGreaterThanOrEqual(3);
    expect(component.averageSentiment()).toBeDefined();
    expect(component.activeBattlecards().length).toBeGreaterThanOrEqual(1);
  });

  it('should stream simulated utterance and surface competitor battlecard', () => {
    component.simulatedUtterance = 'We are testing HubSpot right now.';
    component.sendUtterance();

    expect(component.utterances().length).toBe(4);
    expect(component.activeBattlecards().some(c => c.keyword === 'hubspot')).toBe(true);
  });

  it('should end call and generate executive summary with action items', () => {
    component.endAndSummarizeCall();

    expect(component.callSummary()).toBeTruthy();
    expect(component.callSummary()?.actionItems.length).toBeGreaterThanOrEqual(2);
    expect(component.callSummary()?.summary).toContain('Nexus Innovations');
  });

  it('should compute averageSentiment as 0 when utterances array is empty and allow updating prospectCompany', () => {
    component.prospectCompany.set('Acme Global Services');
    expect(component.prospectCompany()).toBe('Acme Global Services');

    component.utterances.set([]);
    expect(component.averageSentiment()).toBe(0);
  });

  it('should assign negative sentiment when simulated utterance contains negative keywords', () => {
    component.simulatedUtterance = 'This looks too expensive for our current tier.';
    component.sendUtterance();

    const latest = component.utterances()[component.utterances().length - 1];
    expect(latest.sentiment).toBe(-0.50);
  });
});
