// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { TranslationService } from '../../core/services/translation.service';
import { AgentMemoryStudioComponent } from './agent-memory-studio.component';

describe('AgentMemoryStudioComponent', () => {
  let component: AgentMemoryStudioComponent;

  beforeEach(() => {
    const mockTranslation = {
      t: (key: string) => key
    };

    const injector = Injector.create({
      providers: [
        { provide: TranslationService, useValue: mockTranslation }
      ]
    });

    component = runInInjectionContext(injector, () => new AgentMemoryStudioComponent());
  });

  it('should create the memory studio component with 5 initial seeded facts', () => {
    expect(component).toBeTruthy();
    expect(component.memories().length).toBe(5);
    expect(component.totalFactCount()).toBe(5);
    expect(component.verifiedCount()).toBe(3);
    expect(component.brandCount()).toBe(1);
    expect(component.businessCount()).toBe(1);
    expect(component.campaignCount()).toBe(1);
    expect(component.customerCount()).toBe(1);
    expect(component.operationalCount()).toBe(1);
  });

  it('should filter memories by category (BrandMemory, BusinessMemory, CampaignMemory, CustomerContext)', () => {
    // Brand Memory filter
    component.selectedCategory.set('BrandMemory');
    let filtered = component.filteredMemories();
    expect(filtered.length).toBe(1);
    expect(filtered[0].category).toBe('BrandMemory');
    expect(filtered[0].key).toBe('Brand Voice Guidelines');

    // Business Memory filter
    component.selectedCategory.set('BusinessMemory');
    filtered = component.filteredMemories();
    expect(filtered.length).toBe(1);
    expect(filtered[0].category).toBe('BusinessMemory');
    expect(filtered[0].value).toContain('Saudi 15% VAT');

    // Campaign Memory filter
    component.selectedCategory.set('CampaignMemory');
    filtered = component.filteredMemories();
    expect(filtered.length).toBe(1);
    expect(filtered[0].category).toBe('CampaignMemory');
    expect(filtered[0].value).toContain('Ramadan discount max 25%');

    // Reset to All
    component.selectedCategory.set('All');
    expect(component.filteredMemories().length).toBe(5);
  });

  it('should filter memories by search query across keys, values, and tags', () => {
    // Search by key keyword "VAT"
    component.searchQuery.set('VAT');
    let results = component.filteredMemories();
    expect(results.length).toBe(1);
    expect(results[0].key).toBe('Saudi Business Regulations');

    // Search by value keyword "WhatsApp"
    component.searchQuery.set('WhatsApp');
    results = component.filteredMemories();
    expect(results.length).toBe(1);
    expect(results[0].category).toBe('CustomerContext');

    // Search by tag "arabic"
    component.searchQuery.set('arabic');
    results = component.filteredMemories();
    expect(results.length).toBe(1);
    expect(results[0].category).toBe('BrandMemory');

    // Non-existent search
    component.searchQuery.set('NonExistentKeywordXYZ');
    results = component.filteredMemories();
    expect(results.length).toBe(0);
  });

  it('should filter memories by confidence level', () => {
    // Verified Only
    component.selectedConfidence.set('Verified');
    let filtered = component.filteredMemories();
    expect(filtered.length).toBe(3);
    expect(filtered.every(m => m.confidence === 'Verified')).toBe(true);

    // High & Verified
    component.selectedConfidence.set('High');
    filtered = component.filteredMemories();
    expect(filtered.length).toBe(5); // all initial items are Verified or High

    // Low
    component.selectedConfidence.set('Low');
    filtered = component.filteredMemories();
    expect(filtered.length).toBe(0);
  });

  it('should store a new agent memory fact through modal workflow', () => {
    component.openStoreModal();
    expect(component.showStoreModal()).toBe(true);

    component.newCategory = 'BusinessMemory';
    component.newKey = 'Saudi SASO Customs Certification';
    component.newValue = 'All electrical products require valid SASO certificate before port customs release.';
    component.newConfidence = 'Verified';
    component.newSourceAgent = 'CustomsComplianceAgent';
    component.newTtlHours = 72;
    component.newTags = 'saso, customs, saudi';

    component.storeFact();

    expect(component.showStoreModal()).toBe(false);
    expect(component.memories().length).toBe(6);
    expect(component.totalFactCount()).toBe(6);
    expect(component.businessCount()).toBe(2);

    const created = component.memories().find(m => m.key === 'Saudi SASO Customs Certification');
    expect(created).toBeDefined();
    expect(created?.tags).toEqual(['saso', 'customs', 'saudi']);
    expect(created?.expiresAtUtc).toBeTruthy();
    expect(component.notificationMessage()).toContain('Stored fact');
  });

  it('should delete an agent memory fact by ID', () => {
    const toDelete = component.memories()[0];
    component.deleteFact(toDelete.id);

    expect(component.memories().length).toBe(4);
    expect(component.memories().some(m => m.id === toDelete.id)).toBe(false);
    expect(component.notificationMessage()).toContain('Deleted fact');
  });

  it('should prune expired memories', () => {
    // Insert an expired memory
    const expiredEntry = {
      id: 'mem_expired_test',
      tenantId: '00000000-0000-0000-0000-000000000001',
      category: 'CampaignMemory' as const,
      key: 'Expired Flash Promo',
      value: 'Promo ended yesterday',
      confidence: 'Low' as const,
      sourceAgent: 'PromoAgent',
      createdAtUtc: new Date(Date.now() - 7200000).toISOString(),
      expiresAtUtc: new Date(Date.now() - 3600000).toISOString(), // expired 1h ago
      tags: ['promo'],
      metadata: {}
    };

    component.memories.update(items => [expiredEntry, ...items]);
    expect(component.memories().length).toBe(6);

    component.pruneExpired();

    expect(component.memories().length).toBe(5);
    expect(component.memories().some(m => m.id === 'mem_expired_test')).toBe(false);
    expect(component.notificationMessage()).toContain('Pruned 1 expired fact');
  });

  it('should generate hierarchical LLM system prompt context in preview drawer', () => {
    component.openPromptPreview();
    expect(component.showPromptDrawer()).toBe(true);

    const fullPrompt = component.promptPreviewContext();
    expect(fullPrompt).toContain('### SYSTEM AGENT MEMORY CONTEXT ###');
    expect(fullPrompt).toContain('[Brand Voice & Tone Memory]');
    expect(fullPrompt).toContain('Direct, culturally resonant, respectful in Arabic & English');
    expect(fullPrompt).toContain('[Business Rules & Operational Policy Memory]');
    expect(fullPrompt).toContain('Saudi 15% VAT, 24h COD delivery in Riyadh, 3-day return policy');
    expect(fullPrompt).toContain('[Campaign & Growth Playbook Memory]');
    expect(fullPrompt).toContain('Ramadan discount max 25%, ROAS target 3.8x');
    expect(fullPrompt).toContain('[Customer Context & Tier SLA Memory]');
    expect(fullPrompt).toContain('VIP tier requires dedicated WhatsApp support');

    // Simulate topic query filtering inside drawer
    component.previewTopicQuery.set('Ramadan');
    const ramadanOnlyPrompt = component.promptPreviewContext();
    expect(ramadanOnlyPrompt).toContain('Ramadan Campaign Guardrails');
    expect(ramadanOnlyPrompt).not.toContain('Brand Voice Guidelines');

    // Close preview
    component.closePromptPreview();
    expect(component.showPromptDrawer()).toBe(false);
  });
});
