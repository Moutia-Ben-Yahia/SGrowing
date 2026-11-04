// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { MailboxWarmupRadarComponent } from './mailbox-warmup-radar.component';
import { TranslationService } from '../../core/services/translation.service';

describe('MailboxWarmupRadarComponent', () => {
  let component: MailboxWarmupRadarComponent;

  beforeEach(() => {
    component = new MailboxWarmupRadarComponent(new TranslationService());
  });

  it('should initialize with 2 mailbox accounts and 4 DNS checks', () => {
    expect(component.accounts().length).toBe(2);
    expect(component.dnsRecords().length).toBe(4);
    expect(component.averageReputation()).toBeGreaterThanOrEqual(85);
  });

  it('should compute total daily sent and target', () => {
    expect(component.totalDailySent()).toBe(43);
    expect(component.totalDailyTarget()).toBe(90);
  });

  it('should simulate P2P exchange and boost volume and reputation', () => {
    const initialSent = component.totalDailySent();
    component.triggerP2PExchange();
    expect(component.totalDailySent()).toBeGreaterThan(initialSent);
    expect(component.averageReputation()).toBeGreaterThanOrEqual(88);
  });

  it('should refresh DNS audit records', () => {
    component.runDnsAudit();
    expect(component.dnsRecords()[0].detail).toContain('lookup limit');
  });

  it('should compute averageOpenRate correctly and handle empty accounts list safely', () => {
    expect(component.averageOpenRate()).toBeGreaterThan(0);

    component.accounts.set([]);
    expect(component.averageReputation()).toBe(0);
    expect(component.averageOpenRate()).toBe(0);
    expect(component.totalDailySent()).toBe(0);
    expect(component.totalDailyTarget()).toBe(0);
  });
});
