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

  it('validates discovery talk-to-listen pacing and transcript turn sequence', () => {
    const comp = new AgentAssistCockpitComponent();
    expect(comp.repTalkPercentage()).toBeLessThan(55.0);
    expect(100 - comp.repTalkPercentage()).toBeGreaterThan(55.0);

    const speakers = comp.transcripts().map(t => t.speaker);
    expect(speakers).toEqual(['Customer', 'Representative']);
    expect(comp.transcripts()[1].timeSec).toBeGreaterThan(comp.transcripts()[0].timeSec);
  });

  it('should allow toggling pacingAlert and updating repTalkPercentage', () => {
    const comp = new AgentAssistCockpitComponent();
    expect(comp.pacingAlert()).toBe(false);

    comp.pacingAlert.set(true);
    expect(comp.pacingAlert()).toBe(true);

    comp.repTalkPercentage.set(62.5);
    expect(comp.repTalkPercentage()).toBe(62.5);
  });

  it('should allow modifying transcripts and battlecards lists', () => {
    const comp = new AgentAssistCockpitComponent();
    comp.transcripts.set([]);
    comp.battlecards.set([]);
    expect(comp.transcripts().length).toBe(0);
    expect(comp.battlecards().length).toBe(0);

    comp.transcripts.set([
      { id: 't_custom', speaker: 'Customer', text: 'Tell me about pricing', timeSec: 5.0 }
    ]);
    expect(comp.transcripts().length).toBe(1);
    expect(comp.transcripts()[0].text).toBe('Tell me about pricing');
  });
});
