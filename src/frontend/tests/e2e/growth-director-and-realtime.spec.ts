import { test, expect } from '@playwright/test';

test.describe('Growth Director Mission Cockpit E2E: Horizon 5 & 6', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_jwt_token_for_e2e_tests');
      localStorage.setItem('sgrowing_tenant_id', '00000000-0000-0000-0000-000000000001');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'user_e2e_director',
        email: 'growth_director@sgrowing.io',
        name: 'Growth Director E2E'
      }));
    });
  });

  test('Horizon 5 & 6: Autonomous AI Growth Director Mission Cockpit & HITL Execution', async ({ page }) => {
    await page.goto('/agents/growth-director');

    // 1. Strict LTR check
    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    // 2. Header and metadata
    await expect(page.locator('header')).toContainText('Growth Director Mission Cockpit & HITL Guardrails');
    await expect(page.locator('header')).toContainText('Domain J3');

    // 3. KPI Metrics
    await expect(page.locator('text=Total Allocated Budget').first()).toBeVisible();
    await expect(page.locator('text=Projected CAC').first()).toBeVisible();
    await expect(page.locator('text=Projected Pipeline Value').first()).toBeVisible();

    // 4. Verify 4 Coordinated Agent Cards
    await expect(page.locator('text=SEO Keyword Gap Audit').first()).toBeVisible();
    await expect(page.locator('text=High-Converting Copy Draft').first()).toBeVisible();
    await expect(page.locator('text=Meta & Google CAPI Campaign Allocation').first()).toBeVisible();
    await expect(page.locator('text=Nurture DAG Workflow Configuration').first()).toBeVisible();

    // 5. Verify HITL Guardrail Warning Alert
    await expect(page.locator('text=Human-in-the-Loop Financial Guardrail Alert')).toBeVisible();
    await expect(page.locator('text=Requires HITL Review').first()).toBeVisible();

    // 6. Execute 1-Click HITL Approval
    const approveBtn = page.locator('[data-testid="approve-and-execute-btn"]');
    await expect(approveBtn).toBeVisible();
    await approveBtn.click();

    // Verify all tasks executed and alert clears
    await expect(page.locator('text=Human-in-the-Loop Financial Guardrail Alert')).not.toBeVisible();
    await expect(page.locator('text=Active Execution').first()).toBeVisible();
  });
});
