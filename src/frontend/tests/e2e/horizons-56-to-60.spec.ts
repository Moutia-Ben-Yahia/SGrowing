import { test, expect } from '@playwright/test';

test.describe('Horizons 56 to 60 Enterprise Growth OS E2E Suites', () => {

  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h56_60');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h56_60',
        name: 'Chief Revenue & Systems Architect',
        email: 'commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('Horizon 56: Autonomous B2B Buyer Intent De-Anonymization & Reverse-IP Geolocation Graph', async ({ page }) => {
    await page.goto('/intelligence/de-anonymization');

    const rootDir = await page.locator('[data-testid="de-anonymization-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous B2B Buyer Intent De-Anonymization');
    await expect(page.locator('[data-testid="stat-active-accounts"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-ready-to-buy"]')).toBeVisible();

    // Simulate traffic de-anonymization and AE alert dispatch
    await page.click('[data-testid="simulate-lookup-btn"]');
    await expect(page.locator('[data-testid="stat-active-accounts"]')).toContainText('3');

    await page.click('[data-testid="dispatch-alert-btn"]');
    await expect(page.locator('[data-testid="alert-preview-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="alert-preview-card"]')).toContainText('Alert Dispatched');
  });

  test('Horizon 57: Real-Time Dynamic Cohort Sync & DSP Audience Push Mesh', async ({ page }) => {
    await page.goto('/advertising/audience-sync');

    const rootDir = await page.locator('[data-testid="audience-sync-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Real-Time Dynamic Cohort Sync & DSP Audience Mesh');
    await expect(page.locator('[data-testid="stat-hashed-records"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-connected-dsps"]')).toBeVisible();

    // Stream cohort sync and trigger buffered retry
    await page.click('[data-testid="stream-sync-btn"]');
    await expect(page.locator('[data-testid="sync-status-badge"]').first()).toContainText('StreamedToDsp');

    await page.click('[data-testid="retry-sync-btn"]');
    await expect(page.locator('text=RecoveredAndStreamed').first()).toBeVisible();
  });

  test('Horizon 58: Enterprise Voice AI Multi-Turn Sales Objection Handling & Call Scoring', async ({ page }) => {
    await page.goto('/voice/objection-scoring');

    const rootDir = await page.locator('[data-testid="objection-scoring-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Enterprise Voice AI Objection Handling & Call Scoring');
    await expect(page.locator('[data-testid="stat-avg-score"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-sql-rate"]')).toBeVisible();

    // Test AI objection rebuttal and check psychological framing
    await page.click('[data-testid="generate-rebuttal-btn"]');
    await expect(page.locator('[data-testid="rebuttal-result-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="rebuttal-result-card"]')).toContainText('Budget and Pricing');
  });

  test('Horizon 59: Autonomous SEO Dynamic Topical Authority Mesh & Internal Link Optimizer', async ({ page }) => {
    await page.goto('/seo/topical-authority');

    const rootDir = await page.locator('[data-testid="topical-authority-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous SEO Dynamic Topical Authority Mesh');
    await expect(page.locator('[data-testid="stat-overall-authority"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-total-clusters"]')).toBeVisible();

    // Audit topical clusters and inject contextual internal link anchor
    await page.click('[data-testid="analyze-cluster-btn"]');
    await expect(page.locator('[data-testid="stat-total-clusters"]')).toContainText('3');

    await page.locator('[data-testid="inject-link-btn"]').first().click();
    await expect(page.locator('text=✓ Injected').first()).toBeVisible();
  });

  test('Horizon 60: Autonomous Multi-Tenant Feature Flagging & Canary Rollout Mesh', async ({ page }) => {
    await page.goto('/infrastructure/feature-flags');

    const rootDir = await page.locator('[data-testid="feature-flags-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Feature Flagging & Canary Mesh');
    await expect(page.locator('[data-testid="stat-active-flags"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-canary-active"]')).toBeVisible();

    // Advance canary ring to PublicGA
    await page.click('[data-testid="advance-ring-btn"]');
    await expect(page.locator('[data-testid="flag-status-badge"]').first()).toContainText('PublicGA');

    // Trigger emergency kill switch
    await page.click('[data-testid="emergency-kill-btn"]');
    await expect(page.locator('[data-testid="flag-status-badge"]').first()).toContainText('Disabled');
  });

});
