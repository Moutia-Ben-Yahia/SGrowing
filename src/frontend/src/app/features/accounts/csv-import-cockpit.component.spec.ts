// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { CsvImportCockpitComponent } from './csv-import-cockpit.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { of } from 'rxjs';

describe('CsvImportCockpitComponent', () => {
  let component: CsvImportCockpitComponent;
  let apiServiceMock: any;
  let authServiceMock: any;

  const mockPreview = {
    totalRows: 3,
    validRows: 3,
    errorRows: 0,
    detectedHeaders: ['Work Email', 'First Name', 'Last Name', 'Phone', 'Company'],
    sampleRows: [
      { 'Work Email': 'clara@saasventures.io', 'First Name': 'Clara' }
    ]
  };

  const mockExecution = {
    totalProcessed: 3,
    insertedCount: 2,
    updatedCount: 1,
    errorCount: 0,
    wasRolledBack: false,
    statusSummary: '3 contacts processed successfully'
  };

  beforeEach(() => {
    apiServiceMock = {
      previewCsvImport: vi.fn().mockReturnValue(of(mockPreview)),
      executeCsvImport: vi.fn().mockReturnValue(of(mockExecution))
    };

    authServiceMock = {
      currentUser: signal({ id: 'user-1', tenantId: 'tenant-csv-44' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock },
      { provide: AuthService, useValue: authServiceMock }
    ]);

    injector.runInContext(() => {
      component = new CsvImportCockpitComponent();
    });
  });

  it('should initialize with default CSV payload, comma delimiter, and standard mappings', () => {
    expect(component).toBeTruthy();
    expect(component.csvRaw).toContain('clara@saasventures.io');
    expect(component.delimiter).toBe(',');
    expect(component.mapEmail).toBe('Work Email');
    expect(component.isPreviewing()).toBe(false);
    expect(component.isImporting()).toBe(false);
  });

  it('should preview and parse raw CSV data', () => {
    component.previewCsv();

    expect(apiServiceMock.previewCsvImport).toHaveBeenCalledWith(component.csvRaw, ',');
    expect(component.previewData()).toEqual(mockPreview);
    expect(component.isPreviewing()).toBe(false);
  });

  it('should execute transactional batch import with configured column mapping', () => {
    component.previewData.set(mockPreview as any);
    component.executeImport();

    expect(apiServiceMock.executeCsvImport).toHaveBeenCalledWith('tenant-csv-44', expect.objectContaining({
      csvContent: component.csvRaw,
      options: expect.objectContaining({
        delimiter: ',',
        rollbackOnError: true
      })
    }));
    expect(component.executionResult()).toEqual(mockExecution);
    expect(component.isImporting()).toBe(false);
  });

  it('should allow modifying delimiters and merge strategies', () => {
    component.delimiter = ';';
    component.mergeStrategy = 1;

    expect(component.delimiter).toBe(';');
    expect(component.mergeStrategy).toBe(1);
  });
});
