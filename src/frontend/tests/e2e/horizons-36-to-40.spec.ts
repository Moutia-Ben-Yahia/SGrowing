import { test, expect } from '@playwright/test';

test.describe('Horizons 36 to 40 Enterprise Growth OS E2E Suites', () => {

  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h36_40');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_enterprise_h36_40',
        name: 'Executive Architect & RevOps Officer',
        email: 'officer@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner']
      }));
    });
  });

  test('Horizon 36: Visual Customer Lifecycle Journey Orchestrator & Fatigue Gate Mesh', async ({ page }) => {
    await page.goto('/lifecycle/journey-orchestrator');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Visual Customer Lifecycle Journey Orchestrator');
    await expect(page.locator('[data-testid="journey-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="journey-stages-flow"]')).toBeVisible();
    await expect(page.locator('[data-testid="recent-enrollments-list"]')).toBeVisible();

    // Test fatigue pre-flight simulator
    await page.click('[data-testid="btn-test-fatigue"]');
    await expect(page.locator('[data-testid="fatigue-result-card"]')).toBeVisible();

    // Add journey enrollment modal
    await page.click('[data-testid="btn-open-enroll"]');
    await expect(page.locator('text=Enroll Target Contact in Journey').first()).toBeVisible();
    await page.click('[data-testid="btn-submit-enroll"]');
    await expect(page.locator('[data-testid="recent-enrollments-list"]')).toContainText('cto@acme.corp');
  });

  test('Horizon 37: Drag-and-Drop Landing Page & Funnel Canvas Builder', async ({ page }) => {
    await page.goto('/funnels/canvas-builder');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Drag-and-Drop Funnel & Landing Page Canvas');
    await expect(page.locator('[data-testid="canvas-blocks-list"]')).toBeVisible();

    // Switch viewports
    await page.click('[data-testid="btn-viewport-mobile"]');
    await page.click('[data-testid="btn-viewport-desktop"]');

    // Add Pricing Element
    await page.click('[data-testid="btn-add-pricing"]');
    await expect(page.locator('[data-testid="canvas-blocks-list"]')).toContainText('Pricing');

    // Compile and export HTML
    await page.click('[data-testid="btn-export-html"]');
    await expect(page.locator('[data-testid="exported-html-code"]')).toBeVisible();
    await expect(page.locator('[data-testid="exported-html-code"]')).toContainText('funnel-root');
  });

  test('Horizon 38: Multi-Warehouse Proximity Order Allocation & Split-Fulfillment Engine', async ({ page }) => {
    await page.goto('/commerce/fulfillment-matrix');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Multi-Warehouse Proximity Order Allocation');
    await expect(page.locator('[data-testid="fulfillment-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="warehouses-list"]')).toBeVisible();
    await expect(page.locator('[data-testid="recent-plans-list"]')).toBeVisible();
    await expect(page.locator('text=Newark Regional Hub').first()).toBeVisible();

    // Simulate order allocation
    await page.click('[data-testid="btn-plan-order"]');
    await expect(page.locator('[data-testid="recent-plans-list"]')).toContainText('San Francisco, CA');
  });

  test('Horizon 39: Enterprise BI Report Builder & Monte Carlo ARR Forecaster', async ({ page }) => {
    await page.goto('/analytics/bi-report-builder');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Enterprise BI Report Builder & ARR Forecaster');
    await expect(page.locator('[data-testid="bi-forecaster-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="table-scenarios"]')).toBeVisible();
    await expect(page.locator('text=P50 (Base Case)').first()).toBeVisible();

    // Run Monte Carlo simulation
    await page.click('[data-testid="btn-run-simulation"]');
    await expect(page.locator('[data-testid="kpi-p50-arr"]')).toBeVisible();

    // Switch to Pivot Builder tab
    await page.click('[data-testid="tab-pivot-builder"]');
    await expect(page.locator('[data-testid="saved-reports-list"]')).toBeVisible();
    await expect(page.locator('[data-testid="pivot-breakdown-table"]')).toContainText('Paid Search');
  });

  test('Horizon 40: Bi-Directional Webhook Event Mesh & Real-Time Replay Debugger', async ({ page }) => {
    await page.goto('/developer/webhook-mesh');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Bi-Directional Webhook Event Mesh & Replay Debugger');
    await expect(page.locator('[data-testid="webhook-mesh-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="endpoints-list"]')).toBeVisible();
    await expect(page.locator('[data-testid="delivery-logs-table"]')).toBeVisible();
    await expect(page.locator('text=order.allocated').first()).toBeVisible();

    // Emit synthetic test event
    await page.click('[data-testid="btn-emit-event"]');
    await expect(page.locator('[data-testid="delivery-logs-table"]')).toContainText('lead.qualified');

    // Replay event
    await page.locator('[data-testid="btn-replay-event"]').first().click();
    await expect(page.locator('[data-testid="kpi-dlq-count"]')).toBeVisible();
  });
});
