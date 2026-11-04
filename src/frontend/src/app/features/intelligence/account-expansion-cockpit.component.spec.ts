// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { AccountExpansionCockpitComponent } from './account-expansion-cockpit.component';

describe('AccountExpansionCockpitComponent', () => {
  it('should initialize with accounts and correct computed metrics', () => {
    const comp = new AccountExpansionCockpitComponent();
    expect(comp.accounts().length).toBe(3);
    expect(comp.totalExpansionArr()).toBe(148000);
    expect(comp.expansionReadyCount()).toBe(3);
    expect(comp.proposalsCount()).toBe(0);
  });

  it('should select an account when requested', () => {
    const comp = new AccountExpansionCockpitComponent();
    const second = comp.accounts()[1];
    comp.selectAccount(second);
    expect(comp.selectedAccount()?.accountId).toBe('acc_med_biotech_ai');
  });

  it('should dispatch expansion proposal and update signal state', () => {
    const comp = new AccountExpansionCockpitComponent();
    comp.generateProposal('acc_ent_acme_corp');
    expect(comp.proposalsCount()).toBe(1);

    const updated = comp.accounts().find(a => a.accountId === 'acc_ent_acme_corp');
    expect(updated?.proposalGenerated).toBe(true);
    expect(comp.selectedAccount()?.proposalGenerated).toBe(true);
  });

  it('should compute averageNrr accurately and return 0 for empty accounts', () => {
    const comp = new AccountExpansionCockpitComponent();
    expect(comp.averageNrr()).toBeCloseTo(136.4, 1);
    comp.accounts.set([]);
    expect(comp.averageNrr()).toBe(0);
    expect(comp.totalExpansionArr()).toBe(0);
    expect(comp.expansionReadyCount()).toBe(0);
  });

  it('should generate proposal for non-selected account and maintain selectedAccount integrity', () => {
    const comp = new AccountExpansionCockpitComponent();
    expect(comp.selectedAccount()?.accountId).toBe('acc_ent_acme_corp');

    comp.generateProposal('acc_fin_hyperpay');
    expect(comp.proposalsCount()).toBe(1);
    expect(comp.selectedAccount()?.accountId).toBe('acc_ent_acme_corp');
    expect(comp.selectedAccount()?.proposalGenerated).toBe(false);

    const hyperpay = comp.accounts().find(a => a.accountId === 'acc_fin_hyperpay');
    expect(hyperpay?.proposalGenerated).toBe(true);
  });
});
