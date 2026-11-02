import { test, expect } from '@playwright/test';

test.describe('Horizons 15 to 20 Enterprise Growth OS E2E Suites', () => {

  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h15_20');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_enterprise_h15_20',
        name: 'Chief Growth Officer',
        email: 'cgo@growth-os.enterprise',
        roles: ['SuperAdmin', 'OrganizationOwner']
      }));
    });
  });

  test('Horizon 15: Visual Funnel Builder & Multi-Armed Bandit Split Engine', async ({ page }) => {
    await page.goto('/funnels/builder');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Visual Funnel Builder & Multi-Armed Bandit (MAB) Split Engine');
    await expect(page.locator('text=Page Block Hierarchy')).toBeVisible();

    // Verify initial block count
    await expect(page.locator('text=Autonomous AI Growth Platform').first()).toBeVisible();

    // Add new Hero block
    await page.click('button:text("+ Add Hero Block")');
    await expect(page.locator('text=New Hero Headline')).toBeVisible();

    // Trigger traffic burst
    await page.click('button:text("Simulate 50 Visitors")');
    await expect(page.locator('text=Bayesian Significance Achieved')).toBeVisible();
  });

  test('Horizon 16: Mailbox Warmup Swarm & Deliverability Radar', async ({ page }) => {
    await page.goto('/outreach/warmup');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Mailbox Warmup Swarm & Deliverability Radar');
    await expect(page.locator('text=alex.mercer@sgrowing.io')).toBeVisible();

    // DNS Directives visible
    await expect(page.locator('text=SPF').first()).toBeVisible();
    await expect(page.locator('text=DKIM').first()).toBeVisible();

    // Trigger P2P Exchange
    await page.click('button:text("Simulate P2P Exchange")');
    await expect(page.locator('text=100% Pass')).toBeVisible();
  });

  test('Horizon 17: CRM Custom Objects Studio & Metadata Schema Engine', async ({ page }) => {
    await page.goto('/crm/custom-objects');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('CRM Custom Objects Studio & Metadata Schema Engine');
    await expect(page.locator('text=Commercial Property').first()).toBeVisible();

    // Switch schema
    await page.click('text=Fleet Vehicle');
    await expect(page.locator('text=Fleet Vehicle Attributes Schema')).toBeVisible();

    // Create demo schema
    await page.click('button:text("+ New Custom Entity Schema")');
    await expect(page.locator('text=Agency Partner').first()).toBeVisible();

    // Resolve duplicate candidate
    await page.click('button:text("Merge into Master Record")');
    await expect(page.locator('text=No duplicate records detected')).toBeVisible();
  });

  test('Horizon 18: Stateful Event Mesh & DAG Workflow Execution Cockpit', async ({ page }) => {
    await page.goto('/workflows/executor');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Stateful Event Mesh & DAG Workflow Execution Cockpit');
    await expect(page.locator('text=Inbound Lead Qualification & CAPI Dispatch').first()).toBeVisible();

    // Trigger a new workflow run
    await page.click('button:text("Trigger Webhook Workflow")');
    await expect(page.locator('text=Real-Time Abandoned Cart Recovery').first()).toBeVisible();

    // Switch to failed run and replay step
    await page.click('text=Enterprise Quote Signed Handler');
    await page.click('button:text("Replay Step")');
    await expect(page.locator('text=Completed').first()).toBeVisible();
  });

  test('Horizon 19: Subscription Billing, Tax & Smart Dunning Studio', async ({ page }) => {
    await page.goto('/billing/dunning');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Subscription Billing, Tax & Smart Dunning Studio');
    await expect(page.locator('text=SaaS Metrics Labs')).toBeVisible();

    // Retry gateway payment on past due invoice
    await page.click('button:text("Retry Gateway")');
    await expect(page.locator('text=Settled & Reconciled').first()).toBeVisible();

    // Generate test invoice
    await page.click('button:text("+ Generate Test Invoice")');
    await expect(page.locator('text=Nova Retail Corp')).toBeVisible();
  });

  test('Horizon 20: Autonomous Multi-Agent Swarm Orchestration Cockpit', async ({ page }) => {
    await page.goto('/ai/swarm');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Multi-Agent Swarm Orchestration Cockpit');
    await expect(page.locator('text=Enterprise Q4 DACH Market Expansion')).toBeVisible();
    await expect(page.locator('text=Target Account ICP Mining')).toBeVisible();

    // Authorize high-risk task
    await page.click('button:text("Authorize Execution")');
    await expect(page.locator('text=Mission Status').locator('..').locator('text=Completed')).toBeVisible();
  });

});
