// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { LeadRoutingCapacityStudioComponent } from './lead-routing-capacity-studio.component';

describe('LeadRoutingCapacityStudioComponent', () => {
  it('should initialize with seeded SDR roster and SLA radar assignments', () => {
    const comp = new LeadRoutingCapacityStudioComponent();
    expect(comp.sdrs().length).toBe(4);
    expect(comp.totalSdrCount()).toBe(4);
    expect(comp.onlineSdrCount()).toBe(3); // Tariq, Reem, Zaid are Online; Sarah is Away
    expect(comp.assignments().length).toBe(4);
    expect(comp.totalActiveRoutedLeads()).toBe(3); // 3 uncontacted, 1 contacted
    expect(comp.readinessRate()).toBe(75);
    expect(comp.slaCompliancePercent()).toBeGreaterThan(0);
    expect(comp.avgFirstContactMinutes()).toBeGreaterThan(0);
  });

  it('should update SDR availability status when toggled', () => {
    const comp = new LeadRoutingCapacityStudioComponent();
    const sarahId = '55555555-1111-1111-1111-111111111111';

    // Sarah starts as Away
    expect(comp.sdrs().find(s => s.sdrId === sarahId)?.status).toBe('Away');

    comp.updateRepStatus(sarahId, 'Online');

    expect(comp.sdrs().find(s => s.sdrId === sarahId)?.status).toBe('Online');
    expect(comp.onlineSdrCount()).toBe(4);
    expect(comp.readinessRate()).toBe(100);
  });

  it('should simulate inbound lead routing and assign to optimal matching SDR', () => {
    const comp = new LeadRoutingCapacityStudioComponent();
    comp.simLeadName = 'Adel Al-Mutairi';
    comp.simLeadEmail = 'adel@riyadhsaas.sa';
    comp.simCompanyName = 'Riyadh SaaS Innovations';
    comp.simTerritory = 'Riyadh';
    comp.simSkill = 'B2B SaaS';

    const tariqBefore = comp.sdrs().find(s => s.repName === 'Tariq Al-Ghamdi');
    const initialLeads = tariqBefore!.currentActiveLeads;
    const initialAssignmentsCount = comp.assignments().length;

    comp.simulateInboundLead();

    expect(comp.lastDecision()).not.toBeNull();
    const decision = comp.lastDecision()!;
    expect(decision.assignedSdr.repName).toBe('Tariq Al-Ghamdi');
    expect(decision.matchTier).toBe('TerritoryAndSkill');

    // Tariq active leads incremented
    const tariqAfter = comp.sdrs().find(s => s.repName === 'Tariq Al-Ghamdi');
    expect(tariqAfter!.currentActiveLeads).toBe(initialLeads + 1);

    // New assignment added to radar
    expect(comp.assignments().length).toBe(initialAssignmentsCount + 1);
    expect(comp.assignments()[0].leadName).toBe('Adel Al-Mutairi');
    expect(comp.assignments()[0].sdrName).toBe('Tariq Al-Ghamdi');
    expect(comp.assignments()[0].slaStatus).toBe('WithinSla');
  });

  it('should simulate overflow routing when primary rep is at max capacity', () => {
    const comp = new LeadRoutingCapacityStudioComponent();

    // Fill Tariq to capacity (15)
    comp.sdrs.update(list =>
      list.map(s => s.repName === 'Tariq Al-Ghamdi' ? { ...s, currentActiveLeads: 15 } : s)
    );

    comp.simTerritory = 'Riyadh';
    comp.simSkill = 'B2B SaaS';
    comp.simulateInboundLead();

    expect(comp.lastDecision()).not.toBeNull();
    // Overflowed to another online rep with capacity
    expect(comp.lastDecision()!.assignedSdr.repName).not.toBe('Tariq Al-Ghamdi');
  });

  it('should log first contact, update SLA status, and decrement SDR active lead load', () => {
    const comp = new LeadRoutingCapacityStudioComponent();
    const withinSlaAssignmentId = 'aaaaaaaa-1111-1111-1111-111111111111'; // Omar Al-Falih assigned to Tariq

    const tariqBefore = comp.sdrs().find(s => s.repName === 'Tariq Al-Ghamdi');
    const leadsBefore = tariqBefore!.currentActiveLeads;

    comp.logFirstContact(withinSlaAssignmentId);

    const assignmentAfter = comp.assignments().find(a => a.assignmentId === withinSlaAssignmentId);
    expect(assignmentAfter!.firstContactedAtUtc).not.toBeNull();
    expect(assignmentAfter!.slaStatus).toBe('WithinSla');

    const tariqAfter = comp.sdrs().find(s => s.repName === 'Tariq Al-Ghamdi');
    expect(tariqAfter!.currentActiveLeads).toBe(leadsBefore - 1);
  });

  it('should evaluate SLA breaches and trigger reassignment for overdue leads', () => {
    const comp = new LeadRoutingCapacityStudioComponent();
    const breachId = 'cccccccc-1111-1111-1111-111111111111'; // Assigned to Zaid, overdue by 10m

    comp.checkAndTriggerSlaBreaches();

    const breachAssignment = comp.assignments().find(a => a.assignmentId === breachId);
    expect(breachAssignment).toBeDefined();
    expect(breachAssignment!.slaStatus).toBe('Reassigned');
    expect(breachAssignment!.reassignedFromSdrId).toBe('44444444-1111-1111-1111-111111111111');
    expect(breachAssignment!.sdrName).not.toBe('Zaid Al-Otaibi');
  });

  it('should reset state back to default seeded roster', () => {
    const comp = new LeadRoutingCapacityStudioComponent();
    comp.updateRepStatus('22222222-1111-1111-1111-111111111111', 'Offline');
    expect(comp.onlineSdrCount()).toBe(2);

    comp.resetToDefaults();
    expect(comp.onlineSdrCount()).toBe(3);
    expect(comp.lastDecision()).toBeNull();
  });
});
