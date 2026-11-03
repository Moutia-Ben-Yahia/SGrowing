// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of } from 'rxjs';
import { DigitalGoodsStudioComponent } from './digital-goods-studio.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';

describe('DigitalGoodsStudioComponent', () => {
  let component: DigitalGoodsStudioComponent;
  let mockApi: any;
  let mockAuth: any;

  beforeEach(() => {
    mockApi = {
      getDigitalAssets: vi.fn().mockReturnValue(of([
        {
          id: 'asset-1',
          tenantId: 'a0000000-0000-0000-0000-000000000001',
          title: 'Tunisian COD Scaling Blueprint',
          fileName: 'scaling-blueprint.pdf',
          storagePath: 'downloads/blueprints/scaling-blueprint.pdf',
          mimeType: 'application/pdf',
          fileSizeBytes: 8500000,
          maxAllowedDownloads: 5,
          expiryHours: 72
        }
      ])),
      getLicenseKeys: vi.fn().mockReturnValue(of([
        {
          id: 'lic-1',
          tenantId: 'a0000000-0000-0000-0000-000000000001',
          productSku: 'PRO-SUITE',
          licenseKey: 'PRO-9988-7766-5544',
          maxActivations: 5,
          activeCount: 1,
          status: 'Active'
        }
      ])),
      getVariantMatrix: vi.fn().mockReturnValue(of([
        {
          id: 'var-1',
          tenantId: 'a0000000-0000-0000-0000-000000000001',
          baseSku: 'HOODIE-EXP',
          variantSku: 'HOODIE-EXP-M-CHARCOAL',
          attributesJson: '{"Size":"M","Color":"Charcoal"}',
          price: 79.99,
          stockQuantity: 45
        }
      ])),
      createDigitalAsset: vi.fn().mockReturnValue(of({
        id: 'asset-new',
        tenantId: 'a0000000-0000-0000-0000-000000000001',
        title: 'Master Logistics Course',
        fileName: 'course.pdf',
        fileSizeBytes: 12000000,
        maxAllowedDownloads: 10,
        expiryHours: 48
      })),
      issueDownloadToken: vi.fn().mockReturnValue(of({
        tokenId: 'tok-dl-99',
        assetId: 'asset-1',
        downloadUrl: 'https://download.sgrowing.io/asset-1?token=tok-dl-99',
        expiresAtUtc: '2026-11-06T12:00:00Z'
      })),
      generateLicenseKeys: vi.fn().mockReturnValue(of([
        { id: 'lic-new-1', productSku: 'PRO-SUITE', licenseKey: 'KEY-1' }
      ])),
      generateVariantMatrix: vi.fn().mockReturnValue(of([
        { id: 'var-demo-1', variantSku: 'HOODIE-EXP-S-NAVY', price: 79.99, stockQuantity: 20 }
      ]))
    };

    mockAuth = {
      currentTenant: vi.fn().mockReturnValue({ id: 'a0000000-0000-0000-0000-000000000001' })
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi },
        { provide: AuthService, useValue: mockAuth }
      ]
    });

    component = runInInjectionContext(injector, () => new DigitalGoodsStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load digital assets, licenses, and variant matrices', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getDigitalAssets).toHaveBeenCalled();
    expect(mockApi.getLicenseKeys).toHaveBeenCalled();
    expect(mockApi.getVariantMatrix).toHaveBeenCalled();

    expect(component.assets().length).toBe(1);
    expect(component.licenses().length).toBe(1);
    expect(component.variants().length).toBe(1);
  });

  it('should switch navigation tabs between assets, licenses, and matrix', () => {
    component.activeTab.set('licenses');
    expect(component.activeTab()).toBe('licenses');

    component.activeTab.set('matrix');
    expect(component.activeTab()).toBe('matrix');
  });

  it('should create and publish a new digital asset', () => {
    component.newAssetTitle = 'Master Logistics Course';
    component.newAssetFileName = 'course.pdf';

    const countBefore = component.assets().length;
    component.createAsset();

    expect(mockApi.createDigitalAsset).toHaveBeenCalled();
    expect(component.assets().length).toBe(countBefore + 1);
    expect(component.assets()[0].title).toBe('Master Logistics Course');
  });

  it('should issue secure expiring download token for digital asset', () => {
    component.issueToken('asset-1');

    expect(mockApi.issueDownloadToken).toHaveBeenCalledWith({
      tenantId: component.tenantId,
      assetId: 'asset-1'
    });
    expect(component.lastIssuedToken()).toBeDefined();
    expect(component.lastIssuedToken()?.tokenId).toBe('tok-dl-99');
  });

  it('should generate software license keys and PIM variant matrix', () => {
    component.generateLicenses();
    expect(mockApi.generateLicenseKeys).toHaveBeenCalled();
    expect(component.licenses().length).toBeGreaterThan(1);

    component.generateMatrixDemo();
    expect(mockApi.generateVariantMatrix).toHaveBeenCalled();
    expect(component.variants().length).toBe(1);
  });
});
