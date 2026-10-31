// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RateLimitingCockpitComponent } from './rate-limiting-cockpit.component';

describe('RateLimitingCockpitComponent (Adaptive Token Bucket & Penalty Ban Ledger)', () => {
  let component: RateLimitingCockpitComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue({ subscribe: () => {} }),
      post: vi.fn().mockImplementation((url: string, body: any) => ({
        subscribe: (handlers: any) => {
          if (handlers?.next) handlers.next(body);
          else if (typeof handlers === 'function') handlers(body);
        }
      })),
      put: vi.fn().mockReturnValue({ subscribe: () => {} }),
      delete: vi.fn().mockImplementation(() => ({
        subscribe: (handlers: any) => {
          if (handlers?.next) handlers.next({});
          else if (typeof handlers === 'function') handlers({});
        }
      }))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new RateLimitingCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize with telemetry stats and default tier policies', () => {
    const telemetry = component.telemetry();
    expect(telemetry.totalRequestsAnalyzed).toBeGreaterThan(100000);
    expect(telemetry.rejectionRatePercentage).toBeGreaterThan(0);
    expect(telemetry.topThrottledClients.length).toBeGreaterThanOrEqual(3);

    const tiers = component.tierList();
    expect(tiers.length).toBe(5);
    expect(tiers.some(t => t.tierName === 'FreeTier')).toBe(true);
    expect(tiers.some(t => t.tierName === 'EnterpriseTier')).toBe(true);
  });

  it('should switch active navigation tabs reactively', () => {
    expect(component.activeTab()).toBe('cockpit');

    component.activeTab.set('blacklist');
    expect(component.activeTab()).toBe('blacklist');

    component.activeTab.set('whitelist');
    expect(component.activeTab()).toBe('whitelist');

    component.activeTab.set('policies');
    expect(component.activeTab()).toBe('policies');
  });

  it('should add manual ban entry to blacklist and increment count', () => {
    const initialCount = component.blacklist().length;

    component.newBanKey.set('198.51.100.99');
    component.newBanType.set('IP');
    component.newBanReason.set('DDoS probe pattern');
    component.addManualBan();

    expect(component.blacklist().length).toBe(initialCount + 1);
    const added = component.blacklist().find(b => b.key === '198.51.100.99');
    expect(added).toBeDefined();
    expect(added!.reason).toBe('DDoS probe pattern');
    expect(component.toastMessage()).toContain('198.51.100.99');
  });

  it('should remove ban entry from blacklist using unbanKey', () => {
    const targetKey = component.blacklist()[0].key;
    const initialCount = component.blacklist().length;

    component.unbanKey(targetKey);

    expect(component.blacklist().length).toBe(initialCount - 1);
    expect(component.blacklist().some(b => b.key === targetKey)).toBe(false);
  });

  it('should add manual whitelist entry and update trusted list', () => {
    const initialCount = component.whitelist().length;

    component.newWhiteKey.set('10.200.0.5');
    component.newWhiteType.set('IP');
    component.newWhiteReason.set('Internal Monitoring Service');
    component.addManualWhitelist();

    expect(component.whitelist().length).toBe(initialCount + 1);
    const added = component.whitelist().find(w => w.key === '10.200.0.5');
    expect(added).toBeDefined();
    expect(component.toastMessage()).toContain('10.200.0.5');
  });

  it('should remove entry from whitelist', () => {
    const targetKey = component.whitelist()[0].key;
    const initialCount = component.whitelist().length;

    component.removeFromWhitelist(targetKey);

    expect(component.whitelist().length).toBe(initialCount - 1);
    expect(component.whitelist().some(w => w.key === targetKey)).toBe(false);
  });
});
