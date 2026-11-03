// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { Asc606RevenueCockpitComponent } from './asc606-revenue-cockpit.component';

describe('Asc606RevenueCockpitComponent', () => {
  it('should initialize with contracts and deferred revenue metrics', () => {
    const comp = new Asc606RevenueCockpitComponent();
    expect(comp.contracts().length).toBe(3);
    expect(comp.totalDeferredRevenue()).toBe(570000);
    expect(comp.totalRecognizedRevenue()).toBe(150000);
    expect(comp.monthlyRunRate()).toBe(60000);
  });

  it('should select a contract', () => {
    const comp = new Asc606RevenueCockpitComponent();
    const second = comp.contracts()[1];
    comp.selectContract(second);
    expect(comp.selectedContract()?.customerName).toBe('BioHealth Pharma Systems');
  });

  it('should recognize monthly tranche, decreasing deferred and increasing recognized revenue', () => {
    const comp = new Asc606RevenueCockpitComponent();
    comp.recognizeTranche('cntr_rev_01');

    const contract = comp.contracts().find(c => c.contractId === 'cntr_rev_01');
    expect(contract?.deferredRevenue).toBe(160000); // 180000 - 20000
    expect(contract?.recognizedRevenue).toBe(80000); // 60000 + 20000
    expect(comp.totalDeferredRevenue()).toBe(550000);
    expect(comp.totalRecognizedRevenue()).toBe(170000);
  });
});
