// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import {
  LinkedInOutreachStudioComponent,
  getTouchTypeMeta,
  getStepStatusMeta,
  calculatePacingSafety,
  generateLocalAiDraft
} from './linkedin-outreach-studio.component';

describe('LinkedInOutreachStudioComponent (Domain D4, D8 & F2 LinkedIn Cadence Studio)', () => {
  let component: LinkedInOutreachStudioComponent;

  beforeEach(() => {
    component = new LinkedInOutreachStudioComponent();
  });

  it('should initialize component with seeded cadences and prospects', () => {
    expect(component).toBeDefined();
    const cadences = component.cadences();
    expect(cadences.length).toBe(2);

    const gccCadence = cadences[0];
    expect(gccCadence.name).toContain('GCC Enterprise SaaS');
    expect(gccCadence.dailyActionLimit).toBe(40);
    expect(gccCadence.isActive).toBe(true);

    const steps = component.allSteps();
    expect(steps.length).toBeGreaterThanOrEqual(7);
  });

  it('should compute initial top KPIs correctly', () => {
    expect(component.cadences().length).toBe(2);
    expect(component.activeProspectCount()).toBe(5);
    expect(component.totalExecutedTouches()).toBe(3); // 2 Sent + 1 Replied
    expect(component.pendingApprovalCount()).toBe(3); // Noura, Faisal, Sara
    expect(component.avgConnectionRate()).toBeGreaterThan(0);
  });

  it('should filter cadence steps by status correctly', () => {
    // Default filter 'All'
    expect(component.filteredSteps().length).toBe(7);

    // Filter to 'PendingApproval'
    component.onFilterStatus('PendingApproval');
    const pendingSteps = component.filteredSteps();
    expect(pendingSteps.length).toBe(3);
    expect(pendingSteps.every(s => s.status === 'PendingApproval')).toBe(true);

    // Filter to 'Sent'
    component.onFilterStatus('Sent');
    const sentSteps = component.filteredSteps();
    expect(sentSteps.length).toBe(2);
    expect(sentSteps.every(s => s.status === 'Sent')).toBe(true);

    // Filter to 'Replied'
    component.onFilterStatus('Replied');
    const repliedSteps = component.filteredSteps();
    expect(repliedSteps.length).toBe(1);
    expect(repliedSteps[0].status).toBe('Replied');
  });

  it('should switch selected cadence and update quota limit', () => {
    const secondCadenceId = '22222222-2222-2222-2222-222222222223';
    component.onSelectCadence(secondCadenceId);

    expect(component.selectedCadenceId()).toBe(secondCadenceId);
    expect(component.selectedCadence().name).toContain('DIFC & ADGM');
    expect(component.dailyQuotaLimit()).toBe(30);
  });

  it('should execute step, mark as Sent, update timestamp and increment daily quota', () => {
    const initialQuota = component.dailyQuotaUsed();
    const targetStepId = '44444444-4444-4444-4444-444444444404'; // Noura Al-Zahrani

    component.onExecuteStep(targetStepId);

    const updatedStep = component.allSteps().find(s => s.stepId === targetStepId);
    expect(updatedStep).toBeDefined();
    expect(updatedStep!.status).toBe('Sent');
    expect(updatedStep!.executionTimestampUtc).toBeDefined();
    expect(component.dailyQuotaUsed()).toBe(initialQuota + 1);
  });

  it('should mark step as Replied and record response message', () => {
    const targetStepId = '44444444-4444-4444-4444-444444444401'; // Tariq profile view

    component.onMarkAsReplied(targetStepId);

    const updatedStep = component.allSteps().find(s => s.stepId === targetStepId);
    expect(updatedStep).toBeDefined();
    expect(updatedStep!.status).toBe('Replied');
    expect(updatedStep!.responseReceived).toContain('replied');
  });

  it('should calculate anti-bot pacing safety correctly across tiers', () => {
    // Safe
    const safe = calculatePacingSafety(10, 40);
    expect(safe.risk).toBe('Safe');
    expect(safe.percentage).toBe(25);
    expect(safe.status).toContain('Optimal');

    // Moderate
    const moderate = calculatePacingSafety(22, 40);
    expect(moderate.risk).toBe('Moderate');
    expect(moderate.percentage).toBe(55);

    // Warning
    const warning = calculatePacingSafety(34, 40);
    expect(warning.risk).toBe('Warning');
    expect(warning.percentage).toBe(85);

    // Locked
    const locked = calculatePacingSafety(40, 40);
    expect(locked.risk).toBe('Locked');
    expect(locked.percentage).toBe(100);
    expect(locked.status).toContain('Lock Engaged');
  });

  it('should generate bilingual AI outreach messages for English and Arabic', () => {
    // English Connection Request
    const enDraft = generateLocalAiDraft({
      touchType: 'ConnectionRequest',
      prospectName: 'Faisal Bin Rashid',
      companyName: 'Emirates FinTech',
      jobTitle: 'CTO',
      seniority: 'CXO',
      industry: 'FinTech',
      keyPainPoint: 'instant payment settlement latency',
      language: 'en'
    });

    expect(enDraft.draftedMessage).toContain('Faisal Bin Rashid');
    expect(enDraft.draftedMessage).toContain('GCC');
    expect(enDraft.draftedMessage.length).toBeLessThan(300); // LinkedIn 300 char connection invite limit
    expect(enDraft.confidenceScore).toBeGreaterThanOrEqual(0.90);
    expect(enDraft.recommendedHashtags).toContain('#FinTechGCC');

    // Arabic Connection Request
    const arDraft = generateLocalAiDraft({
      touchType: 'ConnectionRequest',
      prospectName: 'طارق المنصور',
      companyName: 'سحابة الاتصالات السعودية',
      jobTitle: 'نائب الرئيس',
      seniority: 'VP',
      industry: 'الاتصالات والبنية السحابية',
      keyPainPoint: 'سيادة البيانات والامتثال لنظام حماية البيانات الشخصية',
      language: 'ar'
    });

    expect(arDraft.draftedMessage).toContain('السلام عليكم');
    expect(arDraft.draftedMessage).toContain('طارق المنصور');
    expect(arDraft.personalizationHook).toContain('رؤية 2030');
    expect(arDraft.pacingWaitMinutes).toBe(30);
  });

  it('should load prospect data from a cadence step into AI drafting sandbox', () => {
    const step = component.allSteps().find(s => s.stepId === '44444444-4444-4444-4444-444444444404')!;
    component.loadProspectToAiSandbox(step);

    expect(component.draftProspectName()).toBe('Noura Al-Zahrani');
    expect(component.draftCompanyName()).toBe('SABIC Digital');
    expect(component.draftJobTitle()).toBe('Head of Enterprise Architecture');
    expect(component.draftSeniority()).toBe('Director');
    expect(component.draftTouchType()).toBe('ConnectionRequest');

    const draft = component.generatedAiDraft();
    expect(draft).not.toBeNull();
    expect(draft!.draftedMessage).toContain('Noura Al-Zahrani');
  });

  it('should create a new cadence and reset daily quota', () => {
    const initialCadenceCount = component.cadences().length;
    component.createNewCadencePrompt();

    expect(component.cadences().length).toBe(initialCadenceCount + 1);
    const newest = component.cadences()[component.cadences().length - 1];
    expect(newest.name).toContain('Scale-Up Growth Cadence');
    expect(component.selectedCadenceId()).toBe(newest.cadenceId);

    // Test quota reset
    component.dailyQuotaUsed.set(38);
    component.resetDailyQuota();
    expect(component.dailyQuotaUsed()).toBe(0);
    expect(component.pacingSafety().risk).toBe('Safe');
  });

  it('should return metadata for touch types and statuses', () => {
    const profileMeta = getTouchTypeMeta('ProfileView');
    expect(profileMeta.label).toBe('Profile View');
    expect(profileMeta.icon).toBe('👀');

    const inMailMeta = getTouchTypeMeta('InMailMessage');
    expect(inMailMeta.label).toBe('Executive InMail');

    const pendingMeta = getStepStatusMeta('PendingApproval');
    expect(pendingMeta.label).toBe('Pending Approval');

    const repliedMeta = getStepStatusMeta('Replied');
    expect(repliedMeta.label).toContain('Replied');
  });
});
