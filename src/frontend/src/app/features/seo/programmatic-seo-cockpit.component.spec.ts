// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { ProgrammaticSeoCockpitComponent } from './programmatic-seo-cockpit.component';

describe('ProgrammaticSeoCockpitComponent', () => {
  let component: ProgrammaticSeoCockpitComponent;

  beforeEach(() => {
    component = new ProgrammaticSeoCockpitComponent();
  });

  it('should initialize with landing pages and backlink opportunities', () => {
    expect(component).toBeTruthy();
    expect(component.pages().length).toBeGreaterThanOrEqual(2);
    expect(component.backlinks().length).toBeGreaterThanOrEqual(3);
  });

  it('should broadcast to IndexNow and show confirmation banner', () => {
    component.broadcastIndexNow();

    expect(component.indexNowMessage()).toContain('IndexNow');
    expect(component.indexNowMessage()).toContain('202 Accepted');
  });

  it('should generate batch pages and update list', () => {
    const initial = component.pages().length;
    component.batchGenerate();

    expect(component.pages().length).toBe(initial + 1);
    expect(component.pages().some(p => p.industry === 'Healthcare')).toBe(true);
  });
});
