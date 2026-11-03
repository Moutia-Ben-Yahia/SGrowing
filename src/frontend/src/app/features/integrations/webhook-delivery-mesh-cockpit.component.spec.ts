// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
  WebhookDeliveryMeshCockpitComponent,
  MeshSubscriptionUi,
  CircuitBreakerUi
} from './webhook-delivery-mesh-cockpit.component';

describe('WebhookDeliveryMeshCockpitComponent (Webhook Delivery Mesh & Circuit Breaker)', () => {
  let component: WebhookDeliveryMeshCockpitComponent;

  beforeEach(() => {
    component = new WebhookDeliveryMeshCockpitComponent();
    component.ngOnInit();
  });

  it('should initialize with default subscriptions, circuit breakers, and telemetry', () => {
    expect(component).toBeTruthy();
    expect(component.subscriptions().length).toBeGreaterThan(0);
    expect(component.circuitBreakers().length).toBeGreaterThan(0);
    expect(component.closedBreakersCount()).toBeGreaterThan(0);
    expect(component.activeDlqItems().length).toBeGreaterThan(0);
  });

  it('should filter subscriptions by active/paused status and search query', () => {
    component.activeFilter.set('active');
    expect(component.filteredSubscriptions().every(s => s.isActive)).toBe(true);

    component.activeFilter.set('all');
    component.searchQuery = 'inventory';
    expect(component.filteredSubscriptions().length).toBe(1);
    expect(component.filteredSubscriptions()[0].name).toContain('Warehouse');
  });

  it('should trip and reset circuit breakers', () => {
    const cb = component.circuitBreakers()[0];
    expect(cb.state).toBe('Closed');

    component.tripBreaker(cb.targetUrl);
    let updated = component.circuitBreakers().find(c => c.targetUrl === cb.targetUrl)!;
    expect(updated.state).toBe('Open');

    component.resetBreaker(cb.targetUrl);
    updated = component.circuitBreakers().find(c => c.targetUrl === cb.targetUrl)!;
    expect(updated.state).toBe('Closed');
  });

  it('should reset all circuit breakers simultaneously', () => {
    const cb = component.circuitBreakers()[0];
    component.tripBreaker(cb.targetUrl);
    expect(component.openBreakersCount()).toBeGreaterThan(0);

    component.resetAllBreakers();
    expect(component.openBreakersCount()).toBe(0);
  });

  it('should toggle and delete subscription', () => {
    const sub = component.subscriptions()[0];
    const initialActive = sub.isActive;

    component.toggleSubscription(sub.id);
    let updated = component.subscriptions().find(s => s.id === sub.id)!;
    expect(updated.isActive).toBe(!initialActive);

    const totalBefore = component.subscriptions().length;
    component.deleteSubscription(sub.id);
    expect(component.subscriptions().length).toBe(totalBefore - 1);
  });

  it('should toggle reveal secret state', () => {
    const sub = component.subscriptions()[0];
    expect(sub.isSecretRevealed).toBeFalsy();

    component.toggleRevealSecret(sub);
    expect(sub.isSecretRevealed).toBe(true);

    component.toggleRevealSecret(sub);
    expect(sub.isSecretRevealed).toBe(false);
  });
});
