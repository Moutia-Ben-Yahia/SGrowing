// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SpeechStreamingMeshCockpitComponent } from './speech-streaming-mesh-cockpit.component';

describe('SpeechStreamingMeshCockpitComponent', () => {
  it('should initialize with sessions and barge-in events', () => {
    const comp = new SpeechStreamingMeshCockpitComponent();
    expect(comp.sessions().length).toBe(2);
    expect(comp.bargeIns().length).toBe(1);
    expect(comp.turnTakingConfidencePct()).toBeGreaterThan(90);
    expect(comp.sessions()[0].currentRoundTripLatencyMs).toBeLessThan(80);
  });

  it('should trigger instant barge-in and prepend event', () => {
    const comp = new SpeechStreamingMeshCockpitComponent();
    comp.triggerBargeIn();
    expect(comp.bargeIns().length).toBe(2);
    expect(comp.bargeIns()[0].userSpeechOnsetLatencyMs).toBeLessThan(50);
  });
});
