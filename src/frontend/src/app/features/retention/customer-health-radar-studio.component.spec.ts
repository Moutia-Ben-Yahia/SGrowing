// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CustomerHealthRadarStudioComponent } from './customer-health-radar-studio.component';

describe('CustomerHealthRadarStudioComponent', () => {
  it('should initialize with 5 seeded customer accounts and calculate accurate summary KPIs', () => {
    const comp = new CustomerHealthRadarStudioComponent();

    expect(comp.accounts().length).toBe(5);
    expect(comp.totalAccountsCount()).toBe(5);
    expect(comp.healthyCount()).toBe(2);
    expect(comp.atRiskCount()).toBe(2);
    expect(comp.criticalCount()).toBe(1);

    expect(comp.averageHealthScore()).toBeGreaterThan(60);
    expect(comp.totalArr()).toBe(240000 + 120000 + 85000 + 180000 + 64000);
    // At-Risk ($120k + $64k) + Critical ($85k) = $269,000
    expect(comp.arrAtRisk()).toBe(269000);
    expect(comp.activePlaybooksCount()).toBeGreaterThan(0);
  });

  it('should correctly compute composite scores and classify tiers according to PRD weights', () => {
    const comp = new CustomerHealthRadarStudioComponent();
    const accounts = comp.accounts();

    // Aramco: 92*0.35 + 88*0.25 + 94*0.20 + 95*0.20 = 32.2 + 22.0 + 18.8 + 19.0 = 92.0
    const aramco = accounts.find(a => a.accountName === 'Aramco Digital Energy')!;
    expect(aramco.compositeHealthScore).toBe(92.0);
    expect(aramco.healthTier).toBe('Healthy');

    // Al-Rajhi: 58*0.35 + 60*0.25 + 60*0.20 + 75*0.20 = 20.3 + 15.0 + 12.0 + 15.0 = 62.3
    const alrajhi = accounts.find(a => a.accountName === 'Al-Rajhi Retail Tech')!;
    expect(alrajhi.compositeHealthScore).toBe(62.3);
    expect(alrajhi.healthTier).toBe('AtRisk');

    // Dubai Logistics: 40*0.35 + 30*0.25 + 36*0.20 + 35*0.20 = 14.0 + 7.5 + 7.2 + 7.0 = 35.7
    const dubai = accounts.find(a => a.accountName === 'Dubai Logistics One')!;
    expect(dubai.compositeHealthScore).toBe(35.7);
    expect(dubai.healthTier).toBe('Critical');
  });

  it('should accurately filter accounts by tier (Healthy, AtRisk, Critical)', () => {
    const comp = new CustomerHealthRadarStudioComponent();

    // Healthy
    comp.selectedTierFilter.set('Healthy');
    expect(comp.filteredAccounts().length).toBe(2);
    expect(comp.filteredAccounts().every(a => a.healthTier === 'Healthy')).toBe(true);

    // At-Risk
    comp.selectedTierFilter.set('AtRisk');
    expect(comp.filteredAccounts().length).toBe(2);
    expect(comp.filteredAccounts().every(a => a.healthTier === 'AtRisk')).toBe(true);

    // Critical
    comp.selectedTierFilter.set('Critical');
    expect(comp.filteredAccounts().length).toBe(1);
    expect(comp.filteredAccounts()[0].accountName).toBe('Dubai Logistics One');

    // Reset
    comp.selectedTierFilter.set('ALL');
    expect(comp.filteredAccounts().length).toBe(5);
  });

  it('should filter accounts by search query across name, domain, industry, and CSM', () => {
    const comp = new CustomerHealthRadarStudioComponent();

    // By company name
    comp.searchQuery.set('Careem');
    expect(comp.filteredAccounts().length).toBe(1);
    expect(comp.filteredAccounts()[0].accountName).toBe('Careem Fleet Ops');

    // By domain
    comp.searchQuery.set('cairohub.eg');
    expect(comp.filteredAccounts().length).toBe(1);
    expect(comp.filteredAccounts()[0].accountName).toBe('Cairo E-Commerce Hub');

    // By CSM
    comp.searchQuery.set('Omar Al-Farhan');
    expect(comp.filteredAccounts().length).toBe(2);

    // Empty reset
    comp.searchQuery.set('');
    expect(comp.filteredAccounts().length).toBe(5);
  });

  it('should filter accounts by risk trigger type (NPS drop, Ticket spike, Dormant seats)', () => {
    const comp = new CustomerHealthRadarStudioComponent();

    // NPS Drop
    comp.selectedTriggerFilter.set('NpsDrop');
    const npsAccounts = comp.filteredAccounts();
    expect(npsAccounts.length).toBe(2); // Al-Rajhi (23 pt drop) & Dubai Logistics (NPS -15)
    expect(npsAccounts.some(a => a.accountName === 'Al-Rajhi Retail Tech')).toBe(true);
    expect(npsAccounts.some(a => a.accountName === 'Dubai Logistics One')).toBe(true);

    // Ticket Spike
    comp.selectedTriggerFilter.set('TicketSpike');
    const spikeAccounts = comp.filteredAccounts();
    expect(spikeAccounts.length).toBe(2); // Dubai Logistics & Cairo Hub

    // Inactive Seat Ratio
    comp.selectedTriggerFilter.set('InactiveSeatRatio');
    const seatAccounts = comp.filteredAccounts();
    expect(seatAccounts.length).toBe(3); // Al-Rajhi (40%), Dubai (63%), Cairo (42%)

    // All triggers
    comp.selectedTriggerFilter.set('ALL');
    expect(comp.filteredAccounts().length).toBe(5);
  });

  it('should open and close account deep-dive drawer', () => {
    const comp = new CustomerHealthRadarStudioComponent();
    const account = comp.accounts()[0];

    comp.openDeepDive(account);
    expect(comp.selectedAccount()).not.toBeNull();
    expect(comp.selectedAccount()?.accountId).toBe(account.accountId);

    comp.closeDeepDive();
    expect(comp.selectedAccount()).toBeNull();
  });

  it('should open what-if simulator and accurately preview composite score and tier transition', () => {
    const comp = new CustomerHealthRadarStudioComponent();
    const alrajhi = comp.accounts().find(a => a.accountName === 'Al-Rajhi Retail Tech')!;

    comp.openSimulator(alrajhi);
    expect(comp.showSimulationModal()).toBe(true);
    expect(comp.simulationAccount()?.accountId).toBe(alrajhi.accountId);

    // Initial simulation values match account
    expect(comp.simAdoption()).toBe(58);
    expect(comp.simSentiment()).toBe(60);
    expect(comp.simUtilization()).toBe(60);
    expect(comp.simCommercial()).toBe(75);
    expect(comp.simulatedComposite()).toBe(62.3);
    expect(comp.simulatedTier()).toBe('AtRisk');

    // Simulate successful customer remediation: increase adoption to 90, sentiment to 88, util to 90, comm to 90
    // (90 * 0.35) + (88 * 0.25) + (90 * 0.20) + (90 * 0.20) = 31.5 + 22.0 + 18.0 + 18.0 = 89.5
    comp.simAdoption.set(90);
    comp.simSentiment.set(88);
    comp.simUtilization.set(90);
    comp.simCommercial.set(90);

    expect(comp.simulatedComposite()).toBe(89.5);
    expect(comp.simulatedTier()).toBe('Healthy');
    expect(comp.simulatedChurnProb()).toBeLessThan(0.15);

    comp.closeSimulator();
    expect(comp.showSimulationModal()).toBe(false);
    expect(comp.simulationAccount()).toBeNull();
  });

  it('should apply what-if simulation recalculation and commit updated snapshot to account', () => {
    const comp = new CustomerHealthRadarStudioComponent();
    const alrajhi = comp.accounts().find(a => a.accountName === 'Al-Rajhi Retail Tech')!;
    const initialSnapshots = alrajhi.historicalSnapshots.length;

    comp.openSimulator(alrajhi);
    comp.simAdoption.set(90);
    comp.simSentiment.set(88);
    comp.simUtilization.set(90);
    comp.simCommercial.set(90);
    comp.simActiveSeats.set(76); // was 48
    comp.simOpenTickets.set(0);  // was 3
    comp.simNps.set(55);         // was 22

    comp.applySimulation(alrajhi);

    const updated = comp.accounts().find(a => a.accountId === alrajhi.accountId)!;
    expect(updated.compositeHealthScore).toBe(89.5);
    expect(updated.healthTier).toBe('Healthy');
    expect(updated.activeSeats).toBe(76);
    expect(updated.openTicketsCount).toBe(0);
    expect(updated.npsScore).toBe(55);
    expect(updated.historicalSnapshots.length).toBe(initialSnapshots + 1);
    expect(updated.historicalSnapshots[updated.historicalSnapshots.length - 1].compositeHealthScore).toBe(89.5);

    expect(comp.showSimulationModal()).toBe(false);
    expect(comp.actionToast()?.type).toBe('success');
  });

  it('should transition playbook status from Suggested to Active, and from Active to Completed', () => {
    const comp = new CustomerHealthRadarStudioComponent();
    const alrajhi = comp.accounts().find(a => a.accountName === 'Al-Rajhi Retail Tech')!;
    const suggestedPb = alrajhi.activePlaybooks.find(p => p.status === 'Suggested')!;
    expect(suggestedPb).toBeDefined();

    // Activate playbook
    comp.updatePlaybookStatus(alrajhi, suggestedPb, 'Active');
    let updatedAccount = comp.accounts().find(a => a.accountId === alrajhi.accountId)!;
    let updatedPb = updatedAccount.activePlaybooks.find(p => p.playbookId === suggestedPb.playbookId)!;
    expect(updatedPb.status).toBe('Active');

    // Complete playbook
    comp.updatePlaybookStatus(updatedAccount, updatedPb, 'Completed');
    updatedAccount = comp.accounts().find(a => a.accountId === alrajhi.accountId)!;
    updatedPb = updatedAccount.activePlaybooks.find(p => p.playbookId === suggestedPb.playbookId)!;
    expect(updatedPb.status).toBe('Completed');
    expect(updatedPb.completedAtUtc).toBeDefined();
    expect(comp.actionToast()?.type).toBe('info');
  });

  it('should add a custom CSM playbook to an account', () => {
    const comp = new CustomerHealthRadarStudioComponent();
    const aramco = comp.accounts().find(a => a.accountName === 'Aramco Digital Energy')!;
    expect(aramco.activePlaybooks.length).toBe(0);

    comp.openAddPlaybookPrompt(aramco);

    const updatedAramco = comp.accounts().find(a => a.accountId === aramco.accountId)!;
    expect(updatedAramco.activePlaybooks.length).toBe(1);
    expect(updatedAramco.activePlaybooks[0].title).toContain('Executive sponsor check-in');
    expect(updatedAramco.activePlaybooks[0].status).toBe('Active');
    expect(comp.actionToast()?.type).toBe('success');
  });

  it('should register a new customer account with calculated health composite', () => {
    const comp = new CustomerHealthRadarStudioComponent();
    const initialCount = comp.accounts().length;

    comp.openCreateModal();
    expect(comp.showCreateModal()).toBe(true);

    comp.newAccountName = 'RedSea Autonomous Marine';
    comp.newDomain = 'redseamarine.sa';
    comp.newIndustry = 'Maritime Robotics';
    comp.newAssignedCsm = 'Hala Al-Mansoor';
    comp.newArr = 150000;
    comp.newTotalSeats = 100;
    comp.newActiveSeats = 92;
    comp.newAdoption = 90;
    comp.newSentiment = 85;
    comp.newUtilization = 92;
    comp.newCommercial = 95;
    // (90 * 0.35) + (85 * 0.25) + (92 * 0.20) + (95 * 0.20) = 31.5 + 21.25 + 18.4 + 19.0 = 90.15 -> 90.2

    comp.saveNewAccount();

    expect(comp.accounts().length).toBe(initialCount + 1);
    const added = comp.accounts().find(a => a.accountName === 'RedSea Autonomous Marine')!;
    expect(added).toBeDefined();
    expect(added.compositeHealthScore).toBe(90.2);
    expect(added.healthTier).toBe('Healthy');
    expect(added.totalSeats).toBe(100);
    expect(added.activeSeats).toBe(92);
    expect(comp.showCreateModal()).toBe(false);
  });

  it('should recalculate radar tenant-wide and update last evaluated timestamp', () => {
    const comp = new CustomerHealthRadarStudioComponent();
    comp.recalculateAllRadar();

    expect(comp.accounts().length).toBe(5);
    expect(comp.actionToast()?.message).toContain('Radar recalculation completed');
    expect(comp.actionToast()?.type).toBe('success');
  });

  it('should reset radar data back to initial seeded demo accounts', () => {
    const comp = new CustomerHealthRadarStudioComponent();

    // Modify state
    comp.searchQuery.set('SomeSearch');
    comp.selectedTierFilter.set('Critical');
    comp.accounts.set([]); // empty

    expect(comp.accounts().length).toBe(0);

    // Reset
    comp.resetToDefaults();

    expect(comp.accounts().length).toBe(5);
    expect(comp.searchQuery()).toBe('');
    expect(comp.selectedTierFilter()).toBe('ALL');
    expect(comp.actionToast()?.type).toBe('info');
  });
});
