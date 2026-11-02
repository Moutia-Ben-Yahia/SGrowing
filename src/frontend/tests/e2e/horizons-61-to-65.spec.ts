import { test, expect } from '@playwright/test';

test.describe('Horizons 61 to 65 Enterprise Growth OS E2E Suites', () => {

  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h61_65');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h61_65',
        name: 'Chief Commercial & FinOps Architect',
        email: 'commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('Horizon 61: Autonomous B2B Account Lead Enrichment & Waterfall Provider Aggregator', async ({ page }) => {
    await page.goto('/intelligence/waterfall-enrichment');

    const rootDir = await page.locator('[data-testid="waterfall-enrichment-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous B2B Lead Enrichment & Waterfall Aggregator');
    await expect(page.locator('[data-testid="stat-match-rate"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-avg-latency"]')).toBeVisible();

    // Trigger waterfall cascade and re-verify deliverability
    await page.click('[data-testid="enrich-lead-btn"]');
    await expect(page.locator('[data-testid="stat-verified-count"]')).toContainText('3');

    await page.locator('[data-testid="verify-mx-btn"]').first().click();
    await expect(page.locator('text=DeliverableWithMx').first()).toBeVisible();
  });

  test('Horizon 62: Autonomous AI Social Proof & Video Testimonial Collector Mesh', async ({ page }) => {
    await page.goto('/content/social-proof');

    const rootDir = await page.locator('[data-testid="social-proof-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Social Proof & Video Testimonial Mesh');
    await expect(page.locator('[data-testid="stat-approved-count"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-avg-rating"]')).toBeVisible();

    // Request new video testimonial and publish Wall of Love widget
    await page.click('[data-testid="request-testimonial-btn"]');
    await expect(page.locator('[data-testid="stat-approved-count"]')).toContainText('3 / 3');

    await page.click('[data-testid="publish-widget-btn"]');
    await expect(page.locator('[data-testid="widget-preview-box"]')).toBeVisible();
    await expect(page.locator('[data-testid="widget-preview-box"]')).toContainText('Wall of Love widget is live');
  });

  test('Horizon 63: Autonomous Multi-Channel Cart Abandonment & Dynamic Incentive Escrow', async ({ page }) => {
    await page.goto('/commerce/cart-rescue');

    const rootDir = await page.locator('[data-testid="cart-rescue-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Margin-Preserving Cart Rescue');
    await expect(page.locator('[data-testid="stat-recoverable-gmv"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-active-rescues"]')).toBeVisible();

    // Evaluate cart elasticity and dispatch conversational WhatsApp nudge
    await page.click('[data-testid="evaluate-cart-btn"]');
    await expect(page.locator('[data-testid="stat-active-rescues"]')).toContainText('3');

    await page.click('[data-testid="dispatch-nudge-btn"]');
    await expect(page.locator('[data-testid="nudge-preview-box"]')).toBeVisible();
    await expect(page.locator('[data-testid="nudge-preview-box"]')).toContainText('WhatsApp Dispatched');
  });

  test('Horizon 64: Autonomous Partner Ecosystem Co-Selling & Deal Registration Mesh', async ({ page }) => {
    await page.goto('/partnerships/deal-registration');

    const rootDir = await page.locator('[data-testid="deal-registration-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Partner Co-Selling & Deal Registration Mesh');
    await expect(page.locator('[data-testid="stat-total-arr"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-locked-count"]')).toBeVisible();

    // Register new partner deal and approve 90-day exclusivity lock
    await page.click('[data-testid="register-deal-btn"]');
    await expect(page.locator('[data-testid="stat-locked-count"]')).toContainText('2');

    // Select the pending review deal and approve lock
    await page.locator('[data-testid="deal-row-deal_partner_702"]').click();
    await page.click('[data-testid="approve-deal-btn"]');
    await expect(page.locator('text=Exclusivity Active').first()).toBeVisible();
    await expect(page.locator('[data-testid="stat-locked-count"]')).toContainText('3');
  });

  test('Horizon 65: Autonomous API Gateway Semantic Caching & Vector Query Deduplication', async ({ page }) => {
    await page.goto('/infrastructure/semantic-cache');

    const rootDir = await page.locator('[data-testid="semantic-cache-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous API Gateway Semantic Caching & Deduplication');
    await expect(page.locator('[data-testid="stat-hit-rate"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-dollars-saved"]')).toBeVisible();

    // Test semantic vector query and verify sub-5ms cache hit
    await page.click('[data-testid="query-cache-btn"]');
    await expect(page.locator('[data-testid="cache-result-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="cache-result-card"]')).toContainText('Semantic Cache Hit');

    // Flush cache
    await page.click('[data-testid="flush-cache-btn"]');
    await expect(page.locator('[data-testid="cache-result-card"]')).toContainText('Cold Upstream Miss');
  });

});
