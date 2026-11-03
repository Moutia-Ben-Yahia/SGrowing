// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { AgentAssistCockpitComponent } from './agent-assist-cockpit.component';

describe('AgentAssistCockpitComponent', () => {
  it('initializes with live audio transcript and default battlecard', () => {
    const comp = new AgentAssistCockpitComponent();
    expect(comp.repTalkPercentage()).toBe(40.9);
    expect(comp.transcripts().length).toBe(2);
    expect(comp.battlecards().length).toBe(1);
    expect(comp.battlecards()[0].competitor).toBe('HubSpot');
  });

  it('triggers competitor battlecard and appends to active cards', () => {
    const comp = new AgentAssistCockpitComponent();
    comp.triggerSalesforceBattlecard();
    expect(comp.battlecards().length).toBe(2);
    expect(comp.battlecards().some(b => b.competitor === 'Salesforce')).toBe(true);
  });
});
