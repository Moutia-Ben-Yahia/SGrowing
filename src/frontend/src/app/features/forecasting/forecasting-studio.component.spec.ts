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
});
