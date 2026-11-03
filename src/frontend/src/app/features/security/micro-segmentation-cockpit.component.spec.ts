// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { MicroSegmentationCockpitComponent } from './micro-segmentation-cockpit.component';

describe('MicroSegmentationCockpitComponent', () => {
  it('initializes with seed SPIFFE service identities and policies', () => {
    const comp = new MicroSegmentationCockpitComponent();
    expect(comp.services().length).toBe(4);
    expect(comp.rules().length).toBe(3);
    expect(comp.interceptedCount()).toBe(142);
    expect(comp.services().every(s => s.isMtlsActive)).toBe(true);
  });

  it('rotates certificate for service and extends expiration date', () => {
    const comp = new MicroSegmentationCockpitComponent();
    const svc = comp.services()[0];
    const initialExpiry = svc.certExpiresAt;

    comp.rotateCert(svc);
    expect(comp.services()[0].certExpiresAt).not.toBe(initialExpiry);
    expect(comp.services()[0].certExpiresAt).toBe('2026-12-15T12:00:00Z');
  });

  it('simulates rogue lateral traversal attempt and increments intercepted count', () => {
    const comp = new MicroSegmentationCockpitComponent();
    const beforeCount = comp.interceptedCount();

    comp.testRogueCall();
    expect(comp.interceptedCount()).toBe(beforeCount + 1);
  });
});
