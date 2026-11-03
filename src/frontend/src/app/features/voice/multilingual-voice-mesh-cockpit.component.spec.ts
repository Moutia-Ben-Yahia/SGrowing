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
});
