import { test, expect } from '@playwright/test';

test.describe('Enterprise Horizons Cockpits E2E', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_jwt_token_for_e2e_tests');
      localStorage.setItem('sgrowing_tenant_id', '00000000-0000-0000-0000-000000000001');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'user_e2e_1',
        email: 'admin@sgrowing.io',
        name: 'Enterprise Admin'
      }));
    });
  });

  test('Horizon 1: Self-Serve Enterprise Onboarding Wizard', async ({ page }) => {
    await page.goto('/onboarding');
    await expect(page.locator('header')).toContainText('Self-Serve Enterprise Onboarding Wizard');
    await expect(page.locator('text=Interactive Checklist • 4 Steps to Launch')).toBeVisible();

    // Verify 4 steps are visible in navigator
    await expect(page.locator('h3:has-text("Connect Domain & DNS")')).toBeVisible();
    await expect(page.locator('h3:has-text("Setup Mailbox Warmup")')).toBeVisible();
    await expect(page.locator('h3:has-text("Import CSV Leads")')).toBeVisible();
    await expect(page.locator('h3:has-text("Launch Pipeline")')).toBeVisible();

    // Step 1: DNS verification
    const verifyDnsBtn = page.locator('button:has-text("Verify DNS Records")');
    await expect(verifyDnsBtn).toBeVisible();
    await verifyDnsBtn.click();
    await expect(page.locator('text=Domain DNS records validated: SPF pass, DKIM 2048-bit pass, DMARC p=reject verified.')).toBeVisible();

    // Proceed to Step 2
    await page.locator('button:has-text("Next: Setup Mailbox Warmup →")').click();
    await expect(page.locator('text=Step 2: Configure Mailbox P2P Warmup & Ramp Plan')).toBeVisible();

    // Proceed to Step 3
    await page.locator('button:has-text("Next: Import Leads CSV →")').click();
    await expect(page.locator('text=Step 3: Import Target Accounts & Leads CSV')).toBeVisible();

    // Load sample CSV leads
    const loadCsvBtn = page.locator('button:has-text("Load Sample Prospects")');
    await expect(loadCsvBtn).toBeVisible();
    await loadCsvBtn.click();
    await expect(page.locator('text=Karim Trabelsi')).toBeVisible();
    await expect(page.locator('text=Maghreb Retail')).toBeVisible();
    await expect(page.locator('text=MX Verified').first()).toBeVisible();

    // Proceed to Step 4
    await page.locator('button:has-text("Next: Launch Pipeline →")').click();
    await expect(page.locator('text=Step 4: Activate Outreach Pipeline & Autonomous SDR')).toBeVisible();

    // Launch pipeline
    const launchBtn = page.locator('button:has-text("Launch Pipeline Now")');
    await expect(launchBtn).toBeVisible();
    await launchBtn.click();

    // Verify success banner and link
    await expect(page.locator('text=Pipeline Activated & Running!')).toBeVisible();
    await expect(page.locator('a:has-text("View Mission Cockpit →")')).toBeVisible();
  });

  test('Horizon 2: Autonomous AI SDR & RevOps Mission Cockpit', async ({ page }) => {
    await page.goto('/agents/missions');
    await expect(page.locator('header')).toContainText('Autonomous AI SDR & RevOps Mission Cockpit');

    // Budget Guardrail Gauge
    await expect(page.locator('text=Budget Guardrail')).toBeVisible();
    await expect(page.locator('text=Hard-Cap Enforced')).toBeVisible();

    // MCP 5-step timeline verification
    await expect(page.locator('text=MCP Tool Execution Loop Timeline')).toBeVisible();
    await expect(page.getByText('crm_search_leads', { exact: true })).toBeVisible();
    await expect(page.getByText('deliverability_run_placement_test', { exact: true })).toBeVisible();
    await expect(page.getByText('outbound_send_spintax_campaign', { exact: true })).toBeVisible();
    await expect(page.getByText('contracts_dispatch_for_signature', { exact: true })).toBeVisible();
    await expect(page.getByText('courier_generate_waybill', { exact: true })).toBeVisible();

    // Human Approval Gate on Step 3
    const approveBtn = page.locator('button:has-text("Approve Step")');
    await expect(approveBtn).toBeVisible();
    await approveBtn.click({ force: true });

    // After approval, button disappears and step updates to Completed
    await expect(page.locator('button:has-text("Approve Step")')).not.toBeVisible();

    // Reasoning Trace
    await expect(page.locator('text=Autonomous Agent Reasoning Trace & Audit Stream')).toBeVisible();
  });

  test('Horizon 3: Outward Webhook Delivery Engine & Public SDK', async ({ page }) => {
    await page.goto('/developer/webhooks');
    await expect(page.locator('header')).toContainText('Outward Webhook Delivery Engine & Public SDK');

    // KPI cards
    await expect(page.locator('text=Active Endpoints')).toBeVisible();
    await expect(page.getByText('Dead-Letter Queue (DLQ)', { exact: true })).toBeVisible();

    // Registered endpoints list
    await expect(page.locator('text=Registered Outward Webhook Endpoints')).toBeVisible();
    await expect(page.locator('span.font-mono:has-text("https://api.merchant-platform.io/events/sgrowing")')).toBeVisible();

    // Dead-Letter Queue (DLQ)
    await expect(page.locator('text=Dead-Letter Queue (DLQ) Management')).toBeVisible();
    await expect(page.locator('text=Gateway Timeout (504)')).toBeVisible();

    // Retry single DLQ item
    const retryBtn = page.locator('button:has-text("Retry")').first();
    await expect(retryBtn).toBeVisible();
    await retryBtn.click();

    // Test Dispatch button
    const testDispatchBtn = page.locator('button:has-text("Test Dispatch")');
    await expect(testDispatchBtn).toBeVisible();
    await testDispatchBtn.click();

    // Live dispatch stream table
    await expect(page.locator('text=Live Dispatch Stream & Audit Trail')).toBeVisible();
    await expect(page.locator('text=HTTP 200').first()).toBeVisible();

    // Official TypeScript SDK card
    await expect(page.getByText('npm i @sgrowing/sdk')).toBeVisible();
    await expect(page.locator('pre:has-text("verifyWebhookSignature")')).toBeVisible();
  });

  test('Horizon 4: Multi-Touch Attribution & Funnel Analytics Cockpit', async ({ page }) => {
    await page.goto('/analytics/attribution');
    await expect(page.locator('header')).toContainText('Multi-Touch Attribution & Funnel Analytics Cockpit');
    await expect(page.locator('text=PostgreSQL Partitioning Active')).toBeVisible();

    // Attribution models selector
    await expect(page.locator('button:has-text("Time-Decay")')).toBeVisible();
    await expect(page.locator('button:has-text("First-Touch")')).toBeVisible();
    await expect(page.locator('button:has-text("Last-Touch")')).toBeVisible();
    await expect(page.locator('button:has-text("Linear")')).toBeVisible();

    // Switch to First-Touch
    await page.locator('button:has-text("First-Touch")').click();
    await expect(page.locator('text=100% credit granted to the initial discovery touchpoint')).toBeVisible();

    // Switch to Last-Touch
    await page.locator('button:has-text("Last-Touch")').click();
    await expect(page.locator('text=100% credit granted to the final touchpoint prior to contract signing')).toBeVisible();

    // Switch back to Time-Decay
    await page.locator('button:has-text("Time-Decay")').click();
    await expect(page.locator('text=half-life exponential decay')).toBeVisible();

    // Executive KPIs
    await expect(page.locator('text=Attributed Closed Revenue')).toBeVisible();
    await expect(page.locator('text=Blended CAC')).toBeVisible();
    await expect(page.locator('text=Blended ROAS')).toBeVisible();

    // Funnel conversion dropoff flow
    await expect(page.locator('text=Enterprise Revenue Funnel Conversion Dropoff')).toBeVisible();
    await expect(page.locator('text=Closed-Won Customers')).toBeVisible();

    // Deal journey inspector
    await expect(page.locator('text=Inspect Deal Journey')).toBeVisible();
    await expect(page.locator('text=Maghreb Retail Enterprise Contract')).toBeVisible();
  });
});
