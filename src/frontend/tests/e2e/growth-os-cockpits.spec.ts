import { test, expect } from '@playwright/test';

test.describe('Growth OS Cockpits E2E: CDP Identity Graph, Ad CAPI ROAS & DAG Telemetry', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_jwt_token_for_e2e_tests');
      localStorage.setItem('sgrowing_tenant_id', '00000000-0000-0000-0000-000000000001');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'user_e2e_growth',
        email: 'growth_lead@sgrowing.io',
        name: 'Growth Lead E2E'
      }));
    });
  });

  test('Horizon 1 Track 1: Customer Data Platform Identity Graph & Session Stitcher', async ({ page }) => {
    await page.goto('/crm/identity-graph');

    // 1. Strict LTR check
    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    // 2. Header and metadata
    await expect(page.locator('header')).toContainText('Identity Graph & Visitor Session Stitcher Cockpit');
    await expect(page.locator('header')).toContainText('customer_data.identities');

    // 3. KPI Metrics
    await expect(page.locator('text=Resolved Profiles').first()).toBeVisible();
    await expect(page.locator('text=Stitched Sessions').first()).toBeVisible();
    await expect(page.locator('text=Deterministic Match Rate').first()).toBeVisible();

    // 4. Exercise Stitching Workbench
    await page.locator('input[placeholder*="cookie_anon"]').fill('cookie_e2e_visitor_77');
    await page.locator('input[placeholder*="user@enterprise.com"]').fill('e2e_buyer@growth.com');

    const stitchBtn = page.locator('[data-testid="stitch-identity-btn"]');
    await expect(stitchBtn).toBeVisible();
    await stitchBtn.click();

    // Verify node added to cluster list and audit log
    await expect(page.locator('text=e2e_buyer@growth.com').first()).toBeVisible();
    await expect(page.locator('text=cookie_e2e_visitor_77').first()).toBeVisible();
    await expect(page.locator('text=Stitched cookie_e2e_visitor_77 to canonical profile').first()).toBeVisible();
  });

  test('Horizon 1 Track 2: Meta & Google Ads CAPI Conversion Sync & ROAS Optimizer', async ({ page }) => {
    await page.goto('/marketing/ad-capi-roas');

    // 1. Strict LTR check
    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    // 2. Header and metadata
    await expect(page.locator('header')).toContainText('CAPI Conversion Hub & ROAS Budget Allocator');
    await expect(page.locator('header')).toContainText('advertising.conversions');

    // 3. KPI Metrics
    await expect(page.locator('text=Blended Portfolio ROAS').first()).toBeVisible();
    await expect(page.locator('text=Total Active Spend').first()).toBeVisible();
    await expect(page.locator('text=Verified CAPI Conversions').first()).toBeVisible();

    // 4. Test Live CAPI Event Dispatch
    const dispatchBtn = page.locator('[data-testid="simulate-capi-event-btn"]');
    await expect(dispatchBtn).toBeVisible();
    await dispatchBtn.click();

    // Verify feed updated
    await expect(page.locator('text=Meta CAPI').first()).toBeVisible();
    await expect(page.locator('text=199.99').first()).toBeVisible();

    // 5. Test Budget Optimizer
    const optimizeBtn = page.locator('[data-testid="optimize-budgets-btn"]');
    await expect(optimizeBtn).toBeVisible();
    await optimizeBtn.click();

    // Verify Pause campaign displays $0 recommended spend
    await expect(page.locator('text=Pause').first()).toBeVisible();
    await expect(page.locator('text=$0').first()).toBeVisible();

    // 6. Test Audience Hash Sync
    const syncAudienceBtn = page.locator('[data-testid="sync-audience-hashes-btn"]');
    await expect(syncAudienceBtn).toBeVisible();
    await syncAudienceBtn.click();

    await expect(page.locator('text=Generated SHA-256 Payload Preview')).toBeVisible();
  });

  test('Horizon 1 Track 3: Marketing Automation Visual DAG Simulator & Telemetry Inspector', async ({ page }) => {
    await page.goto('/workflows/dag-telemetry');

    // 1. Strict LTR check
    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    // 2. Header and metadata
    await expect(page.locator('header')).toContainText('Visual DAG Workflow Node Simulator & Execution Telemetry Studio');
    await expect(page.locator('header')).toContainText('automation.workflows');

    // 3. Topology status & initial state
    await expect(page.locator('text=Valid Acyclic DAG').first()).toBeVisible();

    // 4. Test Cycle Detection
    const injectCycleBtn = page.locator('[data-testid="inject-cycle-btn"]');
    await expect(injectCycleBtn).toBeVisible();
    await injectCycleBtn.click();

    // Verify warning banner appears
    await expect(page.locator('text=Circular Dependency Cycle Detected!')).toBeVisible();

    // Repair cycle
    const repairBtn = page.locator('[data-testid="repair-cycle-btn"]');
    await expect(repairBtn).toBeVisible();
    await repairBtn.click();

    await expect(page.locator('text=Circular Dependency Cycle Detected!')).not.toBeVisible();
    await expect(page.locator('text=Valid Acyclic DAG').first()).toBeVisible();

    // 5. Run Dry-Run Simulation
    const runSimBtn = page.locator('[data-testid="run-simulation-btn"]');
    await expect(runSimBtn).toBeVisible();
    await runSimBtn.click();

    // Verify step trace updated with condition met & goal achieved
    await expect(page.locator('text=ConditionMet').first()).toBeVisible();
    await expect(page.locator('text=GoalAchieved').first()).toBeVisible();
    await expect(page.locator('text=alert-ae').first()).toBeVisible();
  });
});
