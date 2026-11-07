// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SupportEscalationDeskStudioComponent } from './support-escalation-desk-studio.component';

describe('SupportEscalationDeskStudioComponent', () => {
  it('should initialize with 5 seeded support tickets and correct initial KPIs', () => {
    const comp = new SupportEscalationDeskStudioComponent();
    expect(comp.tickets().length).toBe(5);
    expect(comp.activeTicketsCount()).toBe(4);
    expect(comp.warningSlaCount()).toBeGreaterThanOrEqual(1);
    expect(comp.escalatedCount()).toBe(2);
    expect(comp.resolvedTodayCount()).toBe(1);
    expect(comp.slaComplianceRate()).toBeGreaterThan(0);
  });

  it('should correctly filter tickets by tier columns', () => {
    const comp = new SupportEscalationDeskStudioComponent();
    const tier1 = comp.getTierTickets('Tier1_Frontline');
    const tier2 = comp.getTierTickets('Tier2_TechnicalSpecialist');
    const tier3 = comp.getTierTickets('Tier3_EngineeringOps');
    const exec = comp.getTierTickets('ExecutiveOnCall');

    expect(tier1.length).toBe(3); // TICK-1041, TICK-1044, TICK-1040 (resolved)
    expect(tier2.length).toBe(1); // TICK-1042
    expect(tier3.length).toBe(1); // TICK-1043
    expect(exec.length).toBe(0);

    expect(tier2[0].ticketNumber).toBe('TICK-2026-1042');
    expect(tier3[0].ticketNumber).toBe('TICK-2026-1043');
  });

  it('should filter tickets by search query and priority', () => {
    const comp = new SupportEscalationDeskStudioComponent();

    // Search filter
    comp.searchQuery.set('RedSea');
    const filteredBySearch = comp.getTierTickets('Tier1_Frontline');
    expect(filteredBySearch.length).toBe(1);
    expect(filteredBySearch[0].companyName).toBe('RedSea Logistics KSA');

    // Reset search, apply priority filter
    comp.searchQuery.set('');
    comp.selectedPriorityFilter.set('Urgent');
    const tier2Urgent = comp.getTierTickets('Tier2_TechnicalSpecialist');
    expect(tier2Urgent.length).toBe(1);

    const tier1Urgent = comp.getTierTickets('Tier1_Frontline');
    expect(tier1Urgent.length).toBe(0);
  });

  it('should open AI macro copilot with tailored empathy response based on customer sentiment', () => {
    const comp = new SupportEscalationDeskStudioComponent();
    const ticket1 = comp.tickets().find(t => t.ticketNumber === 'TICK-2026-1041')!;

    comp.openMacroCopilot(ticket1);

    expect(comp.activeMacroTicket()).not.toBeNull();
    expect(comp.activeMacroTicket()?.ticketId).toBe(ticket1.ticketId);

    const macro = comp.currentMacro();
    expect(macro).not.toBeNull();
    expect(macro?.macroTitle).toContain('Specialist Deep-Dive');
    expect(macro?.recommendedResponse).toContain('Tariq Al-Otaibi');
    expect(macro?.confidenceScore).toBeGreaterThanOrEqual(0.9);
    expect(macro?.actionSteps.length).toBeGreaterThan(0);
    expect(comp.editableMacroResponse).toBe(macro!.recommendedResponse);

    comp.closeMacroCopilot();
    expect(comp.activeMacroTicket()).toBeNull();
    expect(comp.currentMacro()).toBeNull();
  });

  it('should apply AI macro copilot response to ticket and update resolution logs', () => {
    const comp = new SupportEscalationDeskStudioComponent();
    const ticket1 = comp.tickets().find(t => t.ticketNumber === 'TICK-2026-1041')!;

    comp.openMacroCopilot(ticket1);
    comp.editableMacroResponse = 'Custom personalized response dispatched to Tariq.';
    comp.applyMacroToTicket(ticket1);

    expect(comp.activeMacroTicket()).toBeNull();

    const updated = comp.tickets().find(t => t.ticketId === ticket1.ticketId)!;
    expect(updated.resolutionNotes).toBe('Custom personalized response dispatched to Tariq.');
    expect(updated.escalationLogs.some(l => l.triggeredBy === 'AI Copilot Dispatcher')).toBe(true);
  });

  it('should escalate ticket to Tier 2 with reason and update agent and state', () => {
    const comp = new SupportEscalationDeskStudioComponent();
    const ticket1 = comp.tickets().find(t => t.ticketNumber === 'TICK-2026-1041')!;
    expect(ticket1.currentTier).toBe('Tier1_Frontline');

    comp.openEscalateModal(ticket1);
    expect(comp.escalatingTicket()?.ticketId).toBe(ticket1.ticketId);

    comp.targetEscalateTier = 'Tier2_TechnicalSpecialist';
    comp.escalateReason = 'Latency degradation exceeded 500ms webhook SLA threshold.';
    comp.confirmEscalate(ticket1);

    expect(comp.escalatingTicket()).toBeNull();

    const updated = comp.tickets().find(t => t.ticketId === ticket1.ticketId)!;
    expect(updated.currentTier).toBe('Tier2_TechnicalSpecialist');
    expect(updated.state).toBe('EscalatedTier2');
    expect(updated.assignedAgent).toContain('Tier 2');
    expect(updated.escalationLogs.some(l => l.reason.includes('Latency degradation'))).toBe(true);
    expect(updated.tags).toContain('Escalated-Tier2_TechnicalSpecialist');
  });

  it('should resolve an active ticket and update KPI counts', () => {
    const comp = new SupportEscalationDeskStudioComponent();
    const initialActive = comp.activeTicketsCount();
    const initialResolved = comp.resolvedTodayCount();
    const ticket1 = comp.tickets().find(t => t.ticketNumber === 'TICK-2026-1041')!;

    comp.openResolveModal(ticket1);
    expect(comp.resolvingTicket()?.ticketId).toBe(ticket1.ticketId);

    comp.resolveNotes = 'Webhook retry queue flushed and ack verified.';
    comp.confirmResolve(ticket1);

    expect(comp.resolvingTicket()).toBeNull();

    const updated = comp.tickets().find(t => t.ticketId === ticket1.ticketId)!;
    expect(updated.state).toBe('Resolved');
    expect(updated.resolvedAtUtc).not.toBeNull();
    expect(updated.resolutionNotes).toContain('Webhook retry queue flushed');

    expect(comp.activeTicketsCount()).toBe(initialActive - 1);
    expect(comp.resolvedTodayCount()).toBe(initialResolved + 1);
  });

  it('should create a new ticket and prepend to tickets signal', () => {
    const comp = new SupportEscalationDeskStudioComponent();
    const initialCount = comp.tickets().length;

    comp.openCreateModal();
    expect(comp.showCreateModal()).toBe(true);

    comp.newSubject = 'Payment Gateway Timeout on STC Pay Checkout';
    comp.newCustomerName = 'Mansour Al-Ghamdi';
    comp.newCompanyName = 'STC Pay Merchant Cloud';
    comp.newPriority = 'Urgent';
    comp.newSentiment = 'CriticalChurnRisk';
    comp.newCategory = 'PaymentGateway';
    comp.newInitialNotes = 'Repeated HTTP 504 on capture endpoint.';

    comp.submitCreateTicket();

    expect(comp.showCreateModal()).toBe(false);
    expect(comp.tickets().length).toBe(initialCount + 1);

    const created = comp.tickets()[0];
    expect(created.subject).toBe('Payment Gateway Timeout on STC Pay Checkout');
    expect(created.customerName).toBe('Mansour Al-Ghamdi');
    expect(created.priority).toBe('Urgent');
    expect(created.state).toBe('WarningSlaDueSoon');
    expect(created.escalationLogs.length).toBe(1);
  });

  it('should trigger automated SLA evaluation and reset state back to defaults', () => {
    const comp = new SupportEscalationDeskStudioComponent();

    // Trigger radar evaluation
    comp.triggerAutomatedSlaEvaluation();
    expect(comp.tickets().length).toBe(5);

    // Filter and reset
    comp.searchQuery.set('TestQuery');
    comp.resetToDefaults();
    expect(comp.searchQuery()).toBe('');
    expect(comp.tickets().length).toBe(5);
  });
});
