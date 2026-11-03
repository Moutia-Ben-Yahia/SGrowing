// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { EchoCancellationCockpitComponent } from './echo-cancellation-cockpit.component';

describe('EchoCancellationCockpitComponent', () => {
  it('should initialize with active WebRTC calls, echo suppression, and frames', () => {
    const comp = new EchoCancellationCockpitComponent();
    expect(comp.activeSessionsCount()).toBe(87);
    expect(comp.avgSuppressionDb()).toBe(-55.5);
    expect(comp.doubleTalkAccuracy()).toBe(99.2);
    expect(comp.clarityScore()).toBe(98.5);
    expect(comp.frames().length).toBe(2);
    expect(comp.profiles().length).toBe(2);
  });

  it('should process new DSP frame and update stream', () => {
    const comp = new EchoCancellationCockpitComponent();
    const initialFrames = comp.frames().length;
    comp.processNewFrame();

    expect(comp.frames().length).toBe(initialFrames + 1);
    expect(comp.frames()[initialFrames].callSessionId).toBe('CALL-RTC-7703');
    expect(comp.frames()[initialFrames].doubleTalkDetected).toBe(true);
  });
});
