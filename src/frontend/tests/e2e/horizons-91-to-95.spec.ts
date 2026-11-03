import { test, expect } from '@playwright/test';

test.describe('Horizons 91 to 95 Enterprise Growth OS E2E Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h91_95');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h91_95',
        name: 'Chief Commercial & FinOps Architect',
        email: 'commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('Horizon 91: Autonomous Sales Pipeline Velocity & Bottleneck Forecasting', async ({ page }) => {
    await page.goto('/intelligence/pipeline-velocity');

    const rootDir = await page.locator('[data-testid="pipeline-velocity-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Sales Pipeline Velocity & Bottleneck Forecasting');
    await expect(page.locator('[data-testid="kpi-total-deals"]')).toContainText('133 Deals');
    await expect(page.locator('[data-testid="kpi-bottlenecks-count"]')).toContainText('3 Flagged');

    // Accelerate security bottleneck stage
    await page.locator('[data-testid="btn-accelerate-stage"]').first().click();
    await expect(page.locator('[data-testid="kpi-bottlenecks-count"]')).toContainText('2 Flagged');
  });

  test('Horizon 92: Autonomous B2B Account-Based Retargeting & Bid-Pacing Autopilot', async ({ page }) => {
    await page.goto('/advertising/bid-pacing');

    const rootDir = await page.locator('[data-testid="bid-pacing-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous B2B ABM Retargeting & Bid-Pacing Autopilot');
    await expect(page.locator('[data-testid="kpi-daily-budget"]')).toContainText('$7,800');
    await expect(page.locator('[data-testid="kpi-throttled-campaigns"]')).toContainText('1');

    // Boost high intent bids
    await page.locator('[data-testid="btn-boost-bids"]').first().click();
    await expect(page.locator('text=1.35x').first()).toBeVisible();
  });

  test('Horizon 93: Autonomous Voice Agent QA & Call Compliance Scoring', async ({ page }) => {
    await page.goto('/voice/call-compliance');

    const rootDir = await page.locator('[data-testid="call-compliance-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Voice Agent QA & Call Compliance Scoring');
    await expect(page.locator('[data-testid="kpi-audited-calls"]')).toContainText('3 Calls');
    await expect(page.locator('[data-testid="kpi-flagged-calls"]')).toContainText('1 Calls');

    // Approve compliance override
    await page.locator('[data-testid="btn-approve-audit"]').first().click();
    await expect(page.locator('[data-testid="kpi-flagged-calls"]')).toContainText('0 Calls');
    await expect(page.locator('[data-testid="compliance-passed-indicator"]').first()).toBeVisible();
  });

  test('Horizon 94: Autonomous Global Tax Nexus Tracking & Remittance Engine', async ({ page }) => {
    await page.goto('/finance/tax-nexus');

    const rootDir = await page.locator('[data-testid="tax-nexus-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Global Tax Nexus Tracking & Remittance Engine');
    await expect(page.locator('[data-testid="kpi-total-jurisdictions"]')).toContainText('5 Global Zones');
    await expect(page.locator('[data-testid="kpi-nexus-breaches"]')).toContainText('1');

    // Complete registration in breached jurisdiction
    await page.locator('[data-testid="btn-register-nexus"]').first().click();
    await expect(page.locator('[data-testid="kpi-nexus-breaches"]')).toContainText('0');
    await expect(page.locator('[data-testid="kpi-collecting-zones"]')).toContainText('2');
  });

  test('Horizon 95: Autonomous Multi-Region Chaos Engineering Mesh', async ({ page }) => {
    await page.goto('/infrastructure/chaos-mesh');

    const rootDir = await page.locator('[data-testid="chaos-mesh-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Multi-Region Chaos Engineering Mesh');
    await expect(page.locator('[data-testid="kpi-active-experiments"]')).toContainText('1 In Flight');

    // Emergency halt active chaos experiment
    await page.locator('[data-testid="btn-halt-chaos"]').first().click();
    await expect(page.locator('[data-testid="kpi-active-experiments"]')).toContainText('0 In Flight');
    await expect(page.locator('[data-testid="steady-state-badge"]').first()).toBeVisible();
  });
});
