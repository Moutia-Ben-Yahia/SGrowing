// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { SyntheticDataStudioComponent } from './synthetic-data-studio.component';

describe('SyntheticDataStudioComponent (Differential Privacy & Data Masking Studio)', () => {
  let component: SyntheticDataStudioComponent;

  beforeEach(() => {
    component = new SyntheticDataStudioComponent();
  });

  it('should initialize with default configuration and schema columns', () => {
    expect(component).toBeTruthy();
    expect(component.tableName).toBe('CoreCustomers');
    expect(component.rowCount).toBe(50);
    expect(component.epsilon).toBe(0.5);
    expect(component.columns().length).toBe(6);
    expect(component.sampleRows().length).toBe(5);
  });

  it('should compute privacy protection summary counts correctly', () => {
    expect(component.directCount()).toBe(4); // CustomerName, WorkEmail, MobilePhone, TaxIdentifier
    expect(component.dpCount()).toBe(1); // AccountBalance
  });

  it('should generate new synthetic dataset rows upon request', () => {
    const originalFirstId = component.sampleRows()[0].id;
    component.generateDataset();

    expect(component.sampleRows().length).toBe(5);
    const newFirstId = component.sampleRows()[0].id;
    expect(newFirstId).not.toBe(originalFirstId);
    expect(component.sampleRows()[0].name).toBeTruthy();
    expect(component.sampleRows()[0].email).toContain('@sandbox.corp');
  });

  it('should return appropriate style badge classes for column classifications', () => {
    expect(component.getClassificationBadge('DirectIdentifier')).toContain('text-rose-300');
    expect(component.getClassificationBadge('QuasiIdentifier')).toContain('text-amber-300');
    expect(component.getClassificationBadge('SensitiveAttribute')).toContain('text-cyan-300');
    expect(component.getClassificationBadge('NonSensitive')).toContain('text-slate-400');
  });

  it('should compute zero direct and dp counts when columns list is empty', () => {
    component.columns.set([]);
    expect(component.directCount()).toBe(0);
    expect(component.dpCount()).toBe(0);
  });
});
