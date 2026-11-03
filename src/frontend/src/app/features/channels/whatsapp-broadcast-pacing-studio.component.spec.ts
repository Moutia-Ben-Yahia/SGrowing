// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { WhatsAppBroadcastPacingStudioComponent } from './whatsapp-broadcast-pacing-studio.component';

describe('WhatsAppBroadcastPacingStudioComponent', () => {
  let component: WhatsAppBroadcastPacingStudioComponent;

  beforeEach(() => {
    component = new WhatsAppBroadcastPacingStudioComponent();
  });

  it('should initialize with 14-day warmup trajectory, safe daily limit and low ban risk', () => {
    expect(component).toBeTruthy();
    expect(component.warmupDays().length).toBe(14);
    expect(component.currentWarmupDay()).toBe(3);
    expect(component.currentDailyLimit()).toBeGreaterThan(50);
    expect(component.banRiskLevel()).toBe('Low');
    expect(component.cooldownMultiplier()).toBe(1.0);
    expect(component.suppressionList().length).toBeGreaterThan(0);
  });

  it('should increase ban risk and activate cooldown multiplier when complaints and blocks surge', () => {
    component.complaintRate.set(1.4);
    component.blockRate.set(3.8);

    expect(component.banRiskIndex()).toBeGreaterThan(60);
    expect(['Elevated', 'Critical']).toContain(component.banRiskLevel());
    expect(component.cooldownMultiplier()).toBeGreaterThanOrEqual(2.0);
  });

  it('should calculate simulated message batches with jitter and batch slices', () => {
    component.campaignRecipients.set(500);
    const batches = component.simulatedBatches();

    expect(batches.length).toBeGreaterThan(1);
    expect(batches[0].batchNumber).toBe(1);
    expect(batches[0].messageCount).toBeGreaterThan(0);
    expect(batches[0].durationSeconds).toBeGreaterThan(0);
  });

  it('should detect opt-out keywords and automatically append phone to suppression list', () => {
    const initialCount = component.suppressionList().length;
    component.testInboundText.set('Please STOP sending me these broadcasts!');
    component.testInterceptMessage();

    expect(component.suppressionList().length).toBe(initialCount + 1);
    const latest = component.suppressionList()[0];
    expect(latest.triggerKeyword).toBe('STOP');
  });
});
