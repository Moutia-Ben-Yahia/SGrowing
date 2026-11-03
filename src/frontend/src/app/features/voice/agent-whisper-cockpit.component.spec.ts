// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { AgentWhisperCockpitComponent } from './agent-whisper-cockpit.component';

describe('AgentWhisperCockpitComponent', () => {
  it('should initialize with correct default metrics and live sessions', () => {
    const comp = new AgentWhisperCockpitComponent();
    expect(comp.activeCalls()).toBe(3);
    expect(comp.totalWhispers()).toBe(1840);
    expect(comp.adherenceRate()).toBe(94.2);
    expect(comp.winRateLift()).toBe(28.5);
    expect(comp.sessions().length).toBe(3);
  });

  it('should increment dispatched whispers on speech analysis', () => {
    const comp = new AgentWhisperCockpitComponent();
    comp.simulateSpeechAnalysis();
    expect(comp.totalWhispers()).toBe(1841);
  });

  it('should update session whisper on concession approval', () => {
    const comp = new AgentWhisperCockpitComponent();
    comp.applyAutonomousConcession();
    const sess = comp.sessions().find(s => s.callSessionId === 'CALL-8841');
    expect(sess?.activeWhisperPrompt).toContain('Concession Approved');
  });
});
