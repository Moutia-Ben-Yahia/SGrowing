import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppMarketplaceCockpitComponent } from './app-marketplace-cockpit.component';

describe('AppMarketplaceCockpitComponent', () => {
  let component: AppMarketplaceCockpitComponent;
  let fixture: ComponentFixture<AppMarketplaceCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppMarketplaceCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppMarketplaceCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and initialize catalog', () => {
    expect(component).toBeTruthy();
    expect(component.catalog().length).toBeGreaterThanOrEqual(5);
  });

  it('should filter apps by category', () => {
    component.selectedCategory.set('CRM');
    expect(component.filteredCatalog().every(a => a.category === 'CRM')).toBe(true);

    component.selectedCategory.set('AI');
    expect(component.filteredCatalog().every(a => a.category === 'AI')).toBe(true);
  });

  it('should toggle install and uninstall', () => {
    const shopifyApp = component.catalog().find(a => a.appId === 'app_shopify_commerce')!;
    expect(component.isInstalled('app_shopify_commerce')).toBe(false);

    component.openInstallModal(shopifyApp);
    component.tempApiKey = 'test_token';
    component.confirmInstall();

    expect(component.isInstalled('app_shopify_commerce')).toBe(true);

    component.uninstallApp('app_shopify_commerce');
    expect(component.isInstalled('app_shopify_commerce')).toBe(false);
  });
});
