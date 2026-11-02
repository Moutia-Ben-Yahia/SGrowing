import { describe, it, expect, beforeEach } from 'vitest';
import { CustomObjectsStudioComponent } from './custom-objects-studio.component';
import { TranslationService } from '../../core/services/translation.service';

describe('CustomObjectsStudioComponent', () => {
  let component: CustomObjectsStudioComponent;

  beforeEach(() => {
    component = new CustomObjectsStudioComponent(new TranslationService());
  });

  it('should initialize with 2 default schemas and 1 duplicate candidate', () => {
    expect(component.schemas().length).toBe(2);
    expect(component.selectedSchemaId()).toBe('sch_property');
    expect(component.totalRecords()).toBe(212);
    expect(component.duplicates().length).toBe(1);
  });

  it('should switch selected schema', () => {
    component.selectSchema('sch_vehicle');
    expect(component.selectedSchemaId()).toBe('sch_vehicle');
    expect(component.selectedSchema()?.name).toBe('Fleet Vehicle');
  });

  it('should add a new schema', () => {
    component.createDemoSchema();
    expect(component.schemas().length).toBe(3);
    expect(component.selectedSchema()?.name).toBe('Agency Partner');
  });

  it('should add a field to the selected schema', () => {
    const initialFields = component.selectedSchema()!.fields.length;
    component.addFieldToSelected();
    expect(component.selectedSchema()!.fields.length).toBe(initialFields + 1);
  });

  it('should resolve and merge duplicate candidates', () => {
    component.mergeCandidate('rec_01');
    expect(component.duplicates().length).toBe(0);
  });
});
