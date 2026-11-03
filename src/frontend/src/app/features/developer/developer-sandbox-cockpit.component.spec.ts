// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { DeveloperSandboxCockpitComponent } from './developer-sandbox-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('DeveloperSandboxCockpitComponent', () => {
  let component: DeveloperSandboxCockpitComponent;
  let mockTranslation: TranslationService;

  beforeEach(() => {
    mockTranslation = new TranslationService();
    component = new DeveloperSandboxCockpitComponent(mockTranslation);
  });

  it('initializes with Enterprise tier, 500 burst capacity, and sandbox active', () => {
    expect(component.activeTier()).toBe('Enterprise');
    expect(component.remainingTokens()).toBe(500);
    expect(component.isSandboxMode()).toBe(true);
    expect(component.isRateLimited()).toBe(false);
  });

  it('switches tiers and updates burst quotas accordingly', () => {
    component.selectTier('Starter');
    expect(component.activeTier()).toBe('Starter');
    expect(component.currentQuota().burst).toBe(25);
    expect(component.remainingTokens()).toBe(25);

    component.selectTier('Partner');
    expect(component.activeTier()).toBe('Partner');
    expect(component.currentQuota().burst).toBe(2000);
    expect(component.remainingTokens()).toBe(2000);
  });

  it('consumes tokens correctly when within capacity', () => {
    component.selectTier('Starter'); // 25 tokens
    component.consumeTokens(10);
    expect(component.remainingTokens()).toBe(15);
    expect(component.isRateLimited()).toBe(false);
  });

  it('triggers rate limited state and calculates retry after when tokens exhausted', () => {
    component.selectTier('Starter'); // 25 tokens
    component.consumeTokens(30); // over capacity
    expect(component.isRateLimited()).toBe(true);
    expect(component.retryAfterSeconds()).toBeGreaterThan(0);
  });

  it('resets bucket on refillBucket call', () => {
    component.selectTier('Starter');
    component.consumeTokens(25);
    component.consumeTokens(5); // causes rate limit
    expect(component.isRateLimited()).toBe(true);

    component.refillBucket();
    expect(component.isRateLimited()).toBe(false);
    expect(component.remainingTokens()).toBe(25);
    expect(component.retryAfterSeconds()).toBe(0);
  });

  it('executes simulation and populates lastSimulation state', () => {
    component.selectedEndpoint = '/api/v1/advertising/capi/events';
    component.simulatedLatency.set(60);
    component.executeSimulation();

    const sim = component.lastSimulation();
    expect(sim).not.toBeNull();
    expect(sim?.statusCode).toBe(200);
    expect(sim?.latencyMs).toBe(60);
    expect(sim?.traceId).toMatch(/^sbx_/);
    expect(sim?.responsePayload).toContain('simulated_success');
  });

  it('simulates error 500 when forceFailure is enabled', () => {
    component.forceFailure = true;
    component.executeSimulation();

    const sim = component.lastSimulation();
    expect(sim?.statusCode).toBe(500);
    expect(sim?.responsePayload).toContain('SimulatedSandboxFailure');
  });

  it('filters OpenAPI endpoints by selected role', () => {
    component.selectRole('public');
    expect(component.visibleEndpoints().length).toBe(3);

    component.selectRole('partner');
    expect(component.visibleEndpoints().length).toBe(4);

    component.selectRole('admin');
    expect(component.visibleEndpoints().length).toBe(5);
  });
});
