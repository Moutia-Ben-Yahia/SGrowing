// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { SemanticCacheCockpitComponent } from './semantic-cache-cockpit.component';

describe('SemanticCacheCockpitComponent (Horizon 65)', () => {
  let component: SemanticCacheCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new SemanticCacheCockpitComponent();
  });

  it('should initialize with hot vector entries and FinOps metrics', () => {
    expect(component).toBeTruthy();
    expect(component.entries().length).toBeGreaterThanOrEqual(2);
    expect(component.hitRate()).toBeGreaterThan(70);
    expect(component.dollarsSaved()).toBeGreaterThan(1000);
  });

  it('should test semantic query and return cache hit', () => {
    component.testSemanticQuery();
    const result = component.queryResult();
    expect(result).toBeTruthy();
    expect(result?.isHit).toBe(true);
    expect(result?.latencyMs).toBeLessThan(10);
    expect(result?.similarity).toBeGreaterThanOrEqual(0.94);
  });

  it('should flush semantic vector cache', () => {
    component.flushCache();
    expect(component.entries().length).toBe(0);
    expect(component.queryResult()?.isHit).toBe(false);
  });

  it('should increment dollarsSaved on testSemanticQuery invocation', () => {
    const initialSaved = component.dollarsSaved();
    component.testSemanticQuery();
    expect(component.dollarsSaved()).toBeCloseTo(initialSaved + 0.16, 2);
  });
});
