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

  it('should verify Opus and PCM streaming codecs and frame processing throughput', () => {
    const comp = new SpeechStreamingMeshCockpitComponent();
    const codecs = comp.sessions().map(s => s.clientAudioCodec);
    expect(codecs.some(c => c.includes('Opus'))).toBe(true);
    expect(codecs.some(c => c.includes('PCM16'))).toBe(true);

    const totalFrames = comp.sessions().reduce((acc, s) => acc + s.framesProcessed, 0);
    expect(totalFrames).toBeGreaterThan(20000);
    expect(comp.sessions().every(s => s.currentRoundTripLatencyMs < 80.0)).toBe(true);
  });
});
