// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ScreenDiagnosticCockpitComponent } from './screen-diagnostic-cockpit.component';

describe('ScreenDiagnosticCockpitComponent', () => {
  it('should initialize with correct default metrics and sessions', () => {
    const comp = new ScreenDiagnosticCockpitComponent();
    expect(comp.activeSessions()).toBe(3);
    expect(comp.avgResolutionSeconds()).toBe(95);
    expect(comp.detectionAccuracy()).toBe(97.4);
    expect(comp.sessions().length).toBe(3);
    expect(comp.highlightedElements().length).toBe(4);
  });

  it('should boost detection accuracy on frame analysis', () => {
    const comp = new ScreenDiagnosticCockpitComponent();
    comp.analyzeActiveFrame();
    expect(comp.detectionAccuracy()).toBe(98.6);
  });

  it('should resolve session on cursor trajectory dispatch', () => {
    const comp = new ScreenDiagnosticCockpitComponent();
    comp.generateCursorTrajectory();
    const sess = comp.sessions().find(s => s.sessionId === 'SESS-SCR-401');
    expect(sess?.status).toBe('Resolved');
    expect(sess?.resolutionProgressPct).toBe(100);
  });

  it('should verify highlighted elements confidence and allow updating activeSessions', () => {
    const comp = new ScreenDiagnosticCockpitComponent();
    expect(comp.highlightedElements().every(e => e.confidenceScore > 0.9)).toBe(true);

    comp.activeSessions.set(7);
    comp.guidanceSuccess.set(96.5);
    expect(comp.activeSessions()).toBe(7);
    expect(comp.guidanceSuccess()).toBe(96.5);
  });
});
