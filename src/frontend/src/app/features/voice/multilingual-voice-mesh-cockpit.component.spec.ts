// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { MultilingualVoiceMeshCockpitComponent } from './multilingual-voice-mesh-cockpit.component';

describe('MultilingualVoiceMeshCockpitComponent', () => {
  it('should initialize with sessions and voice profiles', () => {
    const comp = new MultilingualVoiceMeshCockpitComponent();
    expect(comp.sessions().length).toBe(2);
    expect(comp.profiles().length).toBe(2);
    expect(comp.timbreSimilarityScore()).toBeGreaterThan(0.9);
  });

  it('should register voice clone and prepend new profile', () => {
    const comp = new MultilingualVoiceMeshCockpitComponent();
    comp.registerVoiceClone();
    expect(comp.profiles().length).toBe(3);
    expect(comp.profiles()[0].speakerName).toContain('Chiara Rossi');
  });

  it('should verify sub-200ms latency across live translation sessions and vocal pitch benchmarks', () => {
    const comp = new MultilingualVoiceMeshCockpitComponent();
    expect(comp.sessions().every(s => s.currentLatencyMs < 200.0)).toBe(true);
    expect(comp.sessions().every(s => s.streamStatus === 'STREAMING_ACTIVE')).toBe(true);

    const alexProfile = comp.profiles().find(p => p.profileId === 'VCL-ALEX-01');
    expect(alexProfile?.pitchBaseHz).toBe(124.0);
    expect(alexProfile?.timbreResonanceScore).toBeGreaterThan(0.98);
  });

  it('should allow updating timbreSimilarityScore and adding translation session', () => {
    const comp = new MultilingualVoiceMeshCockpitComponent();
    comp.timbreSimilarityScore.set(0.995);
    expect(comp.timbreSimilarityScore()).toBe(0.995);

    comp.sessions.update(s => [...s, {
      sessionId: 'TRN-SES-403',
      sourceLanguageCode: 'es-ES',
      targetLanguageCode: 'en-US',
      speakerName: 'Mateo Gomez',
      currentLatencyMs: 155.0,
      chunksTranslated: 100,
      streamStatus: 'STREAMING_ACTIVE'
    }]);
    expect(comp.sessions().length).toBe(3);
  });

  it('should allow clearing and repopulating voice profiles', () => {
    const comp = new MultilingualVoiceMeshCockpitComponent();
    comp.profiles.set([]);
    expect(comp.profiles().length).toBe(0);

    comp.registerVoiceClone();
    expect(comp.profiles().length).toBe(1);
    expect(comp.profiles()[0].timbreResonanceScore).toBe(0.985);
  });
});
