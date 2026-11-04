// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { ForecastingStudioComponent } from './forecasting-studio.component';

describe('ForecastingStudioComponent', () => {
  let component: ForecastingStudioComponent;

  beforeEach(() => {
    component = new ForecastingStudioComponent();
    component.ngOnInit();
  });

  it('should initialize with stage forecast metrics and commission statements', () => {
    expect(component).toBeTruthy();
    expect(component.stageMetrics.length).toBe(4);
    expect(component.stageMetrics[0].stageName).toBe('Won');
    expect(component.stageMetrics[0].probabilityPercent).toBe(100);
    expect(component.statements().length).toBeGreaterThanOrEqual(2);
  });

  it('should approve draft commission statements and stamp approval timestamp', () => {
    const draftStmt = component.statements().find(s => s.status === 'Draft')!;
    expect(draftStmt).toBeDefined();

    component.approveStatement(draftStmt);

    expect(draftStmt.status).toBe('Approved');
    expect(draftStmt.approvedAtUtc).toBeDefined();
    expect(new Date(draftStmt.approvedAtUtc!).getTime()).toBeLessThanOrEqual(Date.now() + 1000);
  });

  it('should calculate weighted value across stages properly', () => {
    const proposal = component.stageMetrics.find(m => m.stageName === 'Proposal')!;
    expect(proposal.weightedValue).toBe(proposal.totalValue * (proposal.probabilityPercent / 100));
  });

  it('should sum total pipeline across all forecasting stages', () => {
    const totalPipeline = component.stageMetrics.reduce((sum, m) => sum + m.totalValue, 0);
    expect(totalPipeline).toBe(125000);
    const totalWeighted = component.stageMetrics.reduce((sum, m) => sum + m.weightedValue, 0);
    expect(totalWeighted).toBe(77500);
  });

  it('should compute total commissions across statements and verify rep metrics', () => {
    const totalCommission = component.statements().reduce((sum, s) => sum + s.calculatedCommission, 0);
    expect(totalCommission).toBe(11500);

    const reps = component.statements().map(s => s.salesRepName);
    expect(reps).toContain('Alex Vance');
    expect(reps).toContain('Jordan Belfort');

    const draft = component.statements()[0];
    component.approveStatement(draft);
    expect(component.statements().every(s => s.status === 'Approved')).toBe(true);
  });
});
