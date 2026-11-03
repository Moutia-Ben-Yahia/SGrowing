// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { VoiceIvrTreeCockpitComponent } from './voice-ivr-tree-cockpit.component';

describe('VoiceIvrTreeCockpitComponent', () => {
  let component: VoiceIvrTreeCockpitComponent;

  beforeEach(() => {
    component = new VoiceIvrTreeCockpitComponent();
  });

  it('should initialize with 4 IVR nodes and default campaigns', () => {
    expect(component).toBeTruthy();
    expect(component.nodes().length).toBe(4);
    expect(component.campaigns().length).toBe(1);
    expect(component.totalMinutes()).toBeGreaterThan(1000);
  });

  it('should simulate DTMF call traversal and update transcript log', () => {
    component.simulatedInput = '1, Support';
    component.simulateCall();

    expect(component.callLogs().length).toBeGreaterThan(0);
    expect(component.callLogs()[2]).toContain('1, Support');
  });

  it('should launch new outbound broadcast campaign', () => {
    const initialCount = component.campaigns().length;
    component.newCampaignName = 'Spring Product Keynote Invite';
    component.newCampaignTargetCount = 1500;
    component.launchCampaign();

    expect(component.campaigns().length).toBe(initialCount + 1);
    expect(component.campaigns()[0].name).toBe('Spring Product Keynote Invite');
    expect(component.campaigns()[0].targetCount).toBe(1500);
  });
});
