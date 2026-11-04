// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { CartRescueCockpitComponent } from './cart-rescue-cockpit.component';

describe('CartRescueCockpitComponent (Horizon 63)', () => {
  let component: CartRescueCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new CartRescueCockpitComponent();
  });

  it('should initialize with active cart rescues and calculated GMV', () => {
    expect(component).toBeTruthy();
    expect(component.rescues().length).toBeGreaterThanOrEqual(2);
    expect(component.totalGmv()).toBeGreaterThan(1000);
    expect(component.activeCount()).toBe(2);
  });

  it('should evaluate and stage a new cart rescue', () => {
    const initialCount = component.rescues().length;
    component.evaluateCartRescue();
    expect(component.rescues().length).toBe(initialCount + 1);
    expect(component.selectedRescue()?.email).toBe('sarah@globalcorp.com');
  });

  it('should dispatch WhatsApp nudge and display preview message', () => {
    const cart = component.rescues()[0];
    component.dispatchNudge(cart);
    expect(component.nudgePreview()).toContain('WhatsApp Dispatched');
    expect(component.nudgePreview()).toContain(cart.email);
  });

  it('should allow selecting an active rescue cart', () => {
    const secondCart = component.rescues()[1];
    component.selectRescue(secondCart);
    expect(component.selectedRescue()?.rescueId).toBe(secondCart.rescueId);
  });
});
