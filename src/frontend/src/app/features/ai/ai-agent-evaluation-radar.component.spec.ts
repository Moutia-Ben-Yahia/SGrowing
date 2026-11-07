// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { AiAgentEvaluationRadarComponent } from './ai-agent-evaluation-radar.component';

describe('AiAgentEvaluationRadarComponent', () => {
  let component: AiAgentEvaluationRadarComponent;

  beforeEach(() => {
    const injector = Injector.create({
      providers: []
    });

    component = runInInjectionContext(injector, () => new AiAgentEvaluationRadarComponent());
  });

  it('should create the evaluation radar component with 5 initial agent roles', () => {
    expect(component).toBeTruthy();
    expect(component.scorecards().length).toBe(5);
    expect(component.stages.length).toBe(4);
  });

  it('should compute top KPI cards accurately matching PRD 6.6 & 14', () => {
    // Fully Autonomous Agents (Execute: ResearchAgent, SeoAgent, ApprovalGatekeeper)
    expect(component.fullyAutonomousCount()).toBe(3);

    // Supervised Agents (Approve: AdvertisingAgent, ContentAgent)
    expect(component.supervisedCount()).toBe(2);

    // Average Cost / Task SAR (4.20 + 6.50 + 12.00 + 8.40 + 1.10) / 5 = 6.44 SAR
    expect(component.averageCostPerTaskSar()).toBe(6.44);

    // Global Quality Score ~ 94.2%
    expect(component.globalScore()).toBe(94.2);

    // Total tasks evaluated = 11,600
    expect(component.totalTasksEvaluated()).toBe(11600);
  });

  it('should contain all 5 primary agent personas with calibrated benchmarks', () => {
    const cards = component.scorecards();

    const research = cards.find(c => c.agentRole === 'ResearchAgent');
    expect(research).toBeDefined();
    expect(research?.autonomyLevel).toBe('Execute');
    expect(research?.taskSuccessRate).toBe(98.0);
    expect(research?.hallucinationIndex).toBe(0.02);
    expect(research?.costPerTaskSar).toBe(4.20);

    const seo = cards.find(c => c.agentRole === 'SeoAgent');
    expect(seo).toBeDefined();
    expect(seo?.autonomyLevel).toBe('Execute');
    expect(seo?.taskSuccessRate).toBe(96.0);
    expect(seo?.hallucinationIndex).toBe(0.04);
    expect(seo?.costPerTaskSar).toBe(6.50);

    const ads = cards.find(c => c.agentRole === 'AdvertisingAgent');
    expect(ads).toBeDefined();
    expect(ads?.autonomyLevel).toBe('Approve');
    expect(ads?.taskSuccessRate).toBe(92.0);
    expect(ads?.hallucinationIndex).toBe(0.08);
    expect(ads?.costPerTaskSar).toBe(12.00);

    const content = cards.find(c => c.agentRole === 'ContentAgent');
    expect(content).toBeDefined();
    expect(content?.autonomyLevel).toBe('Approve');
    expect(content?.taskSuccessRate).toBe(94.0);
    expect(content?.hallucinationIndex).toBe(0.05);
    expect(content?.costPerTaskSar).toBe(8.40);

    const gatekeeper = cards.find(c => c.agentRole === 'ApprovalGatekeeper');
    expect(gatekeeper).toBeDefined();
    expect(gatekeeper?.autonomyLevel).toBe('Execute');
    expect(gatekeeper?.toolPrecisionRate).toBe(100.0);
    expect(gatekeeper?.hallucinationIndex).toBe(0.00);
    expect(gatekeeper?.costPerTaskSar).toBe(1.10);
    expect(gatekeeper?.compositeScore).toBe(100.0);
  });

  it('should filter scorecard listings by autonomy level', () => {
    component.filterLevel.set('Execute');
    const autonomous = component.filteredScorecards();
    expect(autonomous.length).toBe(3);
    expect(autonomous.every(a => a.autonomyLevel === 'Execute')).toBe(true);

    component.filterLevel.set('Approve');
    const supervised = component.filteredScorecards();
    expect(supervised.length).toBe(2);
    expect(supervised.every(a => a.autonomyLevel === 'Approve')).toBe(true);

    component.filterLevel.set('ALL');
    expect(component.filteredScorecards().length).toBe(5);
  });

  it('should reactively update vitals when simulating a high-quality run', () => {
    const prevTasks = component.totalTasksEvaluated();
    component.simulatePreset('high-quality');

    expect(component.totalTasksEvaluated()).toBe(prevTasks + 1);
    expect(component.lastSimulationFeedback()).toContain('Simulated 100% Quality Run on ResearchAgent');
  });

  it('should cluster new rejection reasons when simulating a hallucination or policy rejection', () => {
    const initialRejectionTotal = component.totalRejections();
    component.simulatePreset('policy-reject');

    expect(component.totalRejections()).toBe(initialRejectionTotal + 1);
    const topReason = component.rejectionClusters()[0];
    expect(topReason.reason).toBe('Unverified pricing claim against ERP price list');
  });

  it('should evaluate and promote an agent from Approve to Execute when thresholds are met', () => {
    // Manually push AdvertisingAgent into qualification range (>90% composite & >95% approval)
    component.scorecards.update(cards =>
      cards.map(c => c.agentRole === 'AdvertisingAgent'
        ? { ...c, compositeScore: 92.5, approvalRate: 96.0, autonomyLevel: 'Approve' }
        : c
      )
    );

    component.runAutonomyCheck('AdvertisingAgent');

    const updated = component.scorecards().find(c => c.agentRole === 'AdvertisingAgent');
    expect(updated?.autonomyLevel).toBe('Execute');
    expect(component.lastSimulationFeedback()).toContain('Promoted AdvertisingAgent from Approve to Execute');
  });

  it('should evaluate and demote an agent from Execute to Approve if quality degrades', () => {
    // Degrade ResearchAgent below safety baseline
    component.scorecards.update(cards =>
      cards.map(c => c.agentRole === 'ResearchAgent'
        ? { ...c, compositeScore: 88.0, approvalRate: 89.0, autonomyLevel: 'Execute' }
        : c
      )
    );

    component.runAutonomyCheck('ResearchAgent');

    const updated = component.scorecards().find(c => c.agentRole === 'ResearchAgent');
    expect(updated?.autonomyLevel).toBe('Approve');
    expect(component.lastSimulationFeedback()).toContain('Demoted ResearchAgent from Execute to Approve');
  });

  it('should handle custom evaluation simulation submission', () => {
    component.simulationForm = {
      agentRole: 'ContentAgent',
      wasSuccessful: true,
      toolValid: true,
      humanApproved: true,
      hadHallucination: false,
      costSar: 7.90,
      latencyMs: 850,
      rejectionReason: ''
    };

    component.submitCustomSimulation();

    expect(component.lastSimulationFeedback()).toContain('Recorded simulated evaluation on ContentAgent');
    const content = component.scorecards().find(c => c.agentRole === 'ContentAgent');
    expect(content?.totalTasksEvaluated).toBe(1541);
  });
});
