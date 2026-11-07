// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { CodCourierReconciliationStudioComponent } from './cod-courier-reconciliation-studio.component';

describe('CodCourierReconciliationStudioComponent', () => {
  let component: CodCourierReconciliationStudioComponent;

  beforeEach(() => {
    component = new CodCourierReconciliationStudioComponent();
  });

  it('should initialize component with seeded regional courier batches and calculate initial KPIs', () => {
    expect(component).toBeTruthy();
    expect(component.batches().length).toBe(3);

    // Initial total parcels: 5 SMSA + 4 Aramex + 4 SPL = 13
    expect(component.totalParcelsCount()).toBe(13);

    // Initial cash remitted: 1680 (SMSA) + 1600 (Aramex) + 1020 (SPL) = 4300 SAR
    expect(component.totalCashRemitted()).toBe(4300.00);

    // Initial discrepancy leakage: 100 (SMSA) + 440 (Aramex) + 35 (SPL) = 575 SAR
    expect(component.discrepancyLeakage()).toBe(575.00);

    // Matched parcels: 4 SMSA + 2 Aramex + 3 SPL = 9
    expect(component.matchedParcelsCount()).toBe(9);
    expect(component.discrepantItemsCount()).toBe(4);

    // Clean match rate: 9 / 13 = ~69.23%
    expect(component.cleanMatchRate()).toBeCloseTo(69.23, 1);

    // Open disputes: 1 seeded for Aramex
    expect(component.openDisputesCount()).toBe(1);
    expect(component.disputes().length).toBe(1);
  });

  it('should calculate courier performance KPIs for regional carriers (SMSA, Aramex, SPL, JTExpress)', () => {
    const kpis = component.courierPerformance();
    expect(kpis.length).toBe(4);

    const smsa = kpis.find(c => c.courierCode === 'SMSA');
    expect(smsa).toBeDefined();
    expect(smsa?.courierName).toBe('SMSA Express');
    expect(smsa?.deliveredParcels).toBe(5);
    expect(smsa?.totalCashRemittedSar).toBe(1680.00);
    expect(smsa?.discrepancyRatePercent).toBe(20); // 1 out of 5 items discrepant

    const aramex = kpis.find(c => c.courierCode === 'Aramex');
    expect(aramex).toBeDefined();
    expect(aramex?.courierName).toBe('Aramex');
    expect(aramex?.deliveredParcels).toBe(3); // 1 lost in transit excluded
    expect(aramex?.totalCashRemittedSar).toBe(1600.00);
    expect(aramex?.discrepancyRatePercent).toBe(50); // 2 out of 4 discrepant (lost + overpaid)

    const spl = kpis.find(c => c.courierCode === 'SPL');
    expect(spl).toBeDefined();
    expect(spl?.deliveredParcels).toBe(4);
    expect(spl?.totalCashRemittedSar).toBe(1020.00);
    expect(spl?.discrepancyRatePercent).toBe(25); // 1 RTO disputed out of 4
  });

  it('should filter ledger items by status (All, DiscrepantOnly, Underpaid, LostInTransit, RtoFeeDisputed, Matched)', () => {
    // Default All
    expect(component.filteredLedgerItems().length).toBe(13);

    // Discrepant Only (4 items)
    component.setStatusFilter('DiscrepantOnly');
    expect(component.selectedStatusFilter()).toBe('DiscrepantOnly');
    const discrepant = component.filteredLedgerItems();
    expect(discrepant.length).toBe(4);
    expect(discrepant.every(i => i.status !== 'Matched')).toBe(true);

    // Underpaid
    component.setStatusFilter('Underpaid');
    const underpaid = component.filteredLedgerItems();
    expect(underpaid.length).toBe(1);
    expect(underpaid[0].trackingNumber).toBe('SMSA-901827103');

    // Lost in Transit
    component.setStatusFilter('LostInTransit');
    const lost = component.filteredLedgerItems();
    expect(lost.length).toBe(1);
    expect(lost[0].trackingNumber).toBe('ARX-992014111');

    // RTO Disputed
    component.setStatusFilter('RtoFeeDisputed');
    const rto = component.filteredLedgerItems();
    expect(rto.length).toBe(1);
    expect(rto[0].trackingNumber).toBe('SPL-330192842');

    // Matched (9 items)
    component.setStatusFilter('Matched');
    const matched = component.filteredLedgerItems();
    expect(matched.length).toBe(9);
    expect(matched.every(i => i.status === 'Matched')).toBe(true);

    // Reset All
    component.setStatusFilter('All');
    expect(component.filteredLedgerItems().length).toBe(13);
  });

  it('should filter ledger items by courier provider and search query', () => {
    // Filter by SMSA
    component.setCourierFilter('SMSA');
    expect(component.selectedCourierFilter()).toBe('SMSA');
    const smsaItems = component.filteredLedgerItems();
    expect(smsaItems.length).toBe(5);
    expect(smsaItems.every(i => i.courier === 'SMSA')).toBe(true);

    // Filter by Aramex
    component.setCourierFilter('Aramex');
    const arxItems = component.filteredLedgerItems();
    expect(arxItems.length).toBe(4);

    // Reset carrier filter
    component.setCourierFilter('All');

    // Search by tracking number
    component.searchTerm.set('SMSA-901827103');
    const searched = component.filteredLedgerItems();
    expect(searched.length).toBe(1);
    expect(searched[0].orderNumber).toBe('ORD-SA-8814');

    // Search by order number
    component.searchTerm.set('ORD-SA-9102');
    const searchedByOrder = component.filteredLedgerItems();
    expect(searchedByOrder.length).toBe(1);
    expect(searchedByOrder[0].trackingNumber).toBe('ARX-992014111');

    // Clear search
    component.searchTerm.set('');
    expect(component.filteredLedgerItems().length).toBe(13);
  });

  it('should open dispute modal, file formal dispute claim and update open dispute counters', () => {
    const underpaidItem = component.allLedgerItems().find(i => i.trackingNumber === 'SMSA-901827103')!;
    expect(underpaidItem).toBeDefined();
    expect(underpaidItem.isDisputed).toBe(false);

    // Open modal
    component.openDisputeModal(underpaidItem);
    expect(component.isDisputeModalOpen()).toBe(true);
    expect(component.activeDisputeItem()?.trackingNumber).toBe('SMSA-901827103');

    // File dispute
    const claim = component.fileDispute(underpaidItem, 'SMSA short-remittance formal audit claim');
    expect(claim).toBeDefined();
    expect(claim.trackingNumber).toBe('SMSA-901827103');
    expect(claim.courier).toBe('SMSA');
    expect(claim.disputedAmountSar).toBe(100.00);
    expect(claim.status).toBe('Open');

    // Verify dispute counter incremented
    expect(component.openDisputesCount()).toBe(2);

    // Verify item in ledger now has isDisputed = true
    const updatedItem = component.allLedgerItems().find(i => i.trackingNumber === 'SMSA-901827103');
    expect(updatedItem?.isDisputed).toBe(true);

    component.closeDisputeModal();
    expect(component.isDisputeModalOpen()).toBe(false);
  });

  it('should ingest raw courier remittance batch and update KPIs and ledger in real time', () => {
    const initialCash = component.totalCashRemitted();
    const initialParcels = component.totalParcelsCount();

    const newBatch = component.ingestBatch({
      batchReference: 'JT-TEST-REMIT-01',
      courier: 'JTExpress',
      items: [
        { trackingNumber: 'JT-1001', orderNumber: 'ORD-SA-8001', expectedCodAmountSar: 300.00, remittedAmountSar: 300.00, courierFeeSar: 24.00, isDelivered: true, isReturned: false },
        { trackingNumber: 'JT-1002', orderNumber: 'ORD-SA-8002', expectedCodAmountSar: 450.00, remittedAmountSar: 400.00, courierFeeSar: 24.00, isDelivered: true, isReturned: false },
        { trackingNumber: 'JT-1003', orderNumber: 'UNKNOWN-01', remittedAmountSar: 120.00, courierFeeSar: 24.00, isDelivered: true, isReturned: false }
      ]
    });

    expect(newBatch).toBeDefined();
    expect(component.batches().length).toBe(4);
    expect(component.totalParcelsCount()).toBe(initialParcels + 3);
    expect(component.totalCashRemitted()).toBe(initialCash + 300 + 400 + 120);

    const jtItems = component.allLedgerItems().filter(i => i.courier === 'JTExpress');
    expect(jtItems.length).toBe(3);

    const matched = jtItems.find(i => i.trackingNumber === 'JT-1001');
    expect(matched?.status).toBe('Matched');

    const underpaid = jtItems.find(i => i.trackingNumber === 'JT-1002');
    expect(underpaid?.status).toBe('Underpaid');
    expect(underpaid?.discrepancyAmountSar).toBe(50.00);

    const unmatched = jtItems.find(i => i.trackingNumber === 'JT-1003');
    expect(unmatched?.status).toBe('UnmatchedWaybill');
  });

  it('should load simulation presets correctly', () => {
    component.loadSimulationPreset('smsa');
    expect(component.uploadFormCarrier).toBe('SMSA');
    expect(component.uploadFormJson).toContain('SMSA-911002931');

    component.loadSimulationPreset('aramex');
    expect(component.uploadFormCarrier).toBe('Aramex');
    expect(component.uploadFormJson).toContain('ARX-880192311');

    component.loadSimulationPreset('spl');
    expect(component.uploadFormCarrier).toBe('SPL');
    expect(component.uploadFormJson).toContain('SPL-552019481');

    component.loadSimulationPreset('jt');
    expect(component.uploadFormCarrier).toBe('JTExpress');
    expect(component.uploadFormJson).toContain('JT-771029341');
  });
});
