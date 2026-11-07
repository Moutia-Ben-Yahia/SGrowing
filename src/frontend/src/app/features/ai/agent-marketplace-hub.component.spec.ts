// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { TranslationService } from '../../core/services/translation.service';
import { AgentMarketplaceHubComponent } from './agent-marketplace-hub.component';

describe('AgentMarketplaceHubComponent', () => {
  let component: AgentMarketplaceHubComponent;

  beforeEach(() => {
    const mockTranslation = {
      t: (key: string) => key
    };

    const injector = Injector.create({
      providers: [
        { provide: TranslationService, useValue: mockTranslation }
      ]
    });

    component = runInInjectionContext(injector, () => new AgentMarketplaceHubComponent());
  });

  it('should create the marketplace hub component with initial 6 listings', () => {
    expect(component).toBeTruthy();
    expect(component.listings().length).toBe(6);
    expect(component.totalCatalogCount()).toBe(6);
    expect(component.officialCount()).toBe(3);
    expect(component.partnerCount()).toBe(2);
    expect(component.communityCount()).toBe(1);
  });

  it('should filter listings by item type (AgentSwarm, WorkflowTemplate, PromptPack)', () => {
    component.selectedType.set('AgentSwarm');
    const swarms = component.filteredListings();
    expect(swarms.length).toBe(3);
    expect(swarms.every(s => s.itemType === 'AgentSwarm')).toBe(true);

    component.selectedType.set('WorkflowTemplate');
    const workflows = component.filteredListings();
    expect(workflows.length).toBe(2);
    expect(workflows.every(w => w.itemType === 'WorkflowTemplate')).toBe(true);

    component.selectedType.set('PromptPack');
    const prompts = component.filteredListings();
    expect(prompts.length).toBe(1);
    expect(prompts[0].name).toBe('Omnichannel Spintax Copy Generator');
  });

  it('should filter listings by tier (Official, VerifiedPartner, Community)', () => {
    component.selectedTier.set('Official');
    const official = component.filteredListings();
    expect(official.length).toBe(3);
    expect(official.every(o => o.tier === 'Official')).toBe(true);

    component.selectedTier.set('VerifiedPartner');
    const partners = component.filteredListings();
    expect(partners.length).toBe(2);
    expect(partners.every(p => p.tier === 'VerifiedPartner')).toBe(true);

    component.selectedTier.set('Community');
    const community = component.filteredListings();
    expect(community.length).toBe(1);
    expect(community[0].name).toBe('Omnichannel Spintax Copy Generator');
  });

  it('should filter listings by search query across name, description, tags, author', () => {
    component.searchQuery.set('ramadan');
    const ramadan = component.filteredListings();
    expect(ramadan.length).toBe(1);
    expect(ramadan[0].slug).toBe('ksa-ramadan-cod-campaign-swarm');

    component.searchQuery.set('salla');
    const salla = component.filteredListings();
    expect(salla.length).toBe(1);
    expect(salla[0].name).toBe('Salla WhatsApp Cart Recovery Engine');

    component.searchQuery.set('nonexistenttermxyz');
    expect(component.filteredListings().length).toBe(0);

    component.resetFilters();
    expect(component.filteredListings().length).toBe(6);
    expect(component.searchQuery()).toBe('');
  });

  it('should open detail modal with schema defaults and allow closing', () => {
    const item = component.listings().find(l => l.id === 'item-b2b-abm-hunter')!;
    component.openDetailModal(item);

    expect(component.isModalOpen()).toBe(true);
    expect(component.selectedListing()?.id).toBe('item-b2b-abm-hunter');
    expect(component.customConfig()['targetIndustry']).toBe('Logistics & E-Commerce');

    component.updateConfigField('targetIndustry', 'Fintech & SaaS');
    expect(component.customConfig()['targetIndustry']).toBe('Fintech & SaaS');

    component.closeModal();
    expect(component.isModalOpen()).toBe(false);
    expect(component.selectedListing()).toBeNull();
  });

  it('should execute installation and increment install count', () => {
    const itemToInstall = component.listings().find(l => l.id === 'item-seo-geo-citability')!;
    const initialInstallCount = itemToInstall.installCount;
    const initialInstalledCount = component.activeInstalledCount();

    component.openDetailModal(itemToInstall);
    component.updateConfigField('crawlIntervalHours', '12');
    component.confirmInstall(itemToInstall);

    expect(component.isModalOpen()).toBe(false);
    expect(component.activeInstalledCount()).toBe(initialInstalledCount + 1);

    const updatedCatalogItem = component.getCatalogItemById('item-seo-geo-citability');
    expect(updatedCatalogItem?.installCount).toBe(initialInstallCount + 1);
    expect(component.isItemInstalled('item-seo-geo-citability')).toBe(true);

    const installed = component.installedItems().find(i => i.listingId === 'item-seo-geo-citability');
    expect(installed).toBeDefined();
    expect(installed?.customSettings['crawlIntervalHours']).toBe('12');
    expect(component.notificationMessage()).toContain('Successfully deployed');
  });

  it('should execute uninstall and deactivate the installed item', () => {
    const initialInstalled = component.installedItems()[0];
    expect(initialInstalled.isActive).toBe(true);

    const initialActiveCount = component.activeInstalledCount();
    component.uninstallItem(initialInstalled.installationId);

    expect(component.activeInstalledCount()).toBe(initialActiveCount - 1);
    expect(component.isItemInstalled(initialInstalled.listingId)).toBe(false);
    expect(component.notificationMessage()).toContain('Uninstalled');
  });

  it('should update custom configuration for already installed items', () => {
    const preInstalled = component.installedItems()[0];
    const catalogItem = component.getCatalogItemById(preInstalled.listingId)!;

    component.openDetailModal(catalogItem);
    component.updateConfigField('dailyBudgetCapSar', '5000');
    component.saveConfiguration();

    expect(component.isModalOpen()).toBe(false);
    const updated = component.installedItems().find(i => i.installationId === preInstalled.installationId);
    expect(updated?.customSettings['dailyBudgetCapSar']).toBe('5000');
    expect(component.notificationMessage()).toContain('Updated configuration settings');
  });
});
