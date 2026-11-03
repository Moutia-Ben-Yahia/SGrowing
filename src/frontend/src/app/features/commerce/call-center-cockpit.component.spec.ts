// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { CallCenterCockpitComponent } from './call-center-cockpit.component';

describe('CallCenterCockpitComponent (Anti-Collision Sliding Lock & Margin Cockpit)', () => {
  let component: CallCenterCockpitComponent;

  beforeEach(() => {
    vi.useFakeTimers();
    component = new CallCenterCockpitComponent();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should initialize with queue orders and compute default margin metrics', () => {
    expect(component).toBeTruthy();
    expect(component.orders().length).toBe(4);
    expect(component.activeTab()).toBe('queue');
    expect(component.currentAgentName()).toBe('Operator Sarah');

    expect(component.computedReturnRiskCost()).toBeGreaterThan(0);
    expect(component.computedTotalCosts()).toBeGreaterThan(0);
    expect(component.computedNetMargin()).toBeGreaterThan(0);
    expect(component.computedMarginPercent()).toBeGreaterThan(0);
  });

  it('should switch navigation tabs reactively', () => {
    component.activeTab.set('calculator');
    expect(component.activeTab()).toBe('calculator');

    component.activeTab.set('queue');
    expect(component.activeTab()).toBe('queue');
  });

  it('should acquire lock for an order setting sliding 180s expiration', () => {
    const order = component.orders()[0];
    component.acquireLock(order);

    expect(order.isLocked).toBe(true);
    expect(order.lockedByAgentName).toBe('Operator Sarah');
    expect(order.lockExpiresInSec).toBe(180);
    expect(component.lockedCount()).toBeGreaterThanOrEqual(1);
  });

  it('should renew and release lock for an order', () => {
    const order = component.orders()[0];
    component.acquireLock(order);
    order.lockExpiresInSec = 45;

    component.renewLock(order);
    expect(order.lockExpiresInSec).toBe(180);

    component.releaseLock(order);
    expect(order.isLocked).toBe(false);
    expect(order.lockedByAgentName).toBeNull();
    expect(order.lockExpiresInSec).toBe(0);
  });

  it('should confirm order, release its lock and remove from dialer queue', () => {
    const initialCount = component.orders().length;
    const order = component.orders()[0];
    component.acquireLock(order);

    component.confirmOrder(order);

    expect(order.callStatus).toBe('Confirmed');
    expect(component.orders().length).toBe(initialCount - 1);
    expect(component.orders().find(o => o.id === order.id)).toBeUndefined();
  });

  it('should trigger collision alert when attempting to dial an actively locked order', () => {
    const lockedOrder = component.orders().find(o => o.isLocked);
    expect(lockedOrder).toBeDefined();

    component.attemptCollision(lockedOrder!);
    expect(component.collisionAlert()).toContain('Call collision avoided');
    expect(component.collisionAlert()).toContain(lockedOrder!.lockedByAgentName!);
  });
});
