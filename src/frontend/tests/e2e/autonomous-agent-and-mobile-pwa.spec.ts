import { test, expect } from '@playwright/test';

test.describe('Autonomous AI Agent Hub & Mobile PWA Courier E2E', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_jwt_token_for_e2e_tests');
      localStorage.setItem('sgrowing_tenant_id', '00000000-0000-0000-0000-000000000001');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'user_e2e_1',
        email: 'admin@sgrowing.io',
        name: 'Admin E2E'
      }));
    });
  });

  test('should render Autonomous AI Agent Console and exercise autonomy controls & MCP tools', async ({ page }) => {
    await page.goto('/agents/console');

    // 1. Verify Header & Subtitle
    await expect(page.locator('h1')).toContainText('Autonomous AI Sales Agent Hub');
    await expect(page.locator('text=MCP JSON-RPC v2024-11-05')).toBeVisible();

    // 2. Test Autonomy Mode Toggling
    const autoApproveBtn = page.locator('button:has-text("Auto-Approve")').first();
    const draftOnlyBtn = page.locator('button:has-text("Draft Only")').first();
    const humanInTheLoopBtn = page.locator('button:has-text("Human-in-the-Loop")').first();

    await expect(autoApproveBtn).toBeVisible();
    await autoApproveBtn.click();
    await expect(page.locator('text=● Mode: Auto-Approve')).toBeVisible();

    await draftOnlyBtn.click();
    await expect(page.locator('text=● Mode: Draft Only')).toBeVisible();

    await humanInTheLoopBtn.click();
    await expect(page.locator('text=● Mode: Human-in-the-Loop')).toBeVisible();

    // 3. Test MCP Tool Tester
    await expect(page.locator('text=Interactive MCP Tool Tester')).toBeVisible();

    // Select courier_generate_waybill tool
    const waybillToolBtn = page.locator('button:has-text("courier_generate_waybill")');
    await expect(waybillToolBtn).toBeVisible();
    await waybillToolBtn.click();

    // Verify textarea contains waybill arguments
    const textarea = page.locator('textarea');
    await expect(textarea).toHaveValue(/orderId/);

    // Click execute
    const executeBtn = page.locator('button:has-text("Execute Tool via JSON-RPC")');
    await executeBtn.click();

    // Verify execution output appears
    await expect(page.locator('text=MCP Response (200 OK)')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('pre')).toContainText('toolName');

    // 4. Verify Audit Ledger
    await expect(page.locator('text=Agent Audit Ledger')).toBeVisible();
    await expect(page.locator('text=SGrowing-Sales-Agent-01').first()).toBeVisible();
  });

  test('should open Barcode Scanner and Touchscreen Signature canvas on Courier Companion', async ({ page }) => {
    await page.goto('/courier');

    // 1. Verify Driver Companion Header
    await expect(page.locator('h1')).toContainText('Driver Companion');

    // 2. Click "Scan Parcel" button to open camera scanner
    const scanParcelBtn = page.locator('button:has-text("Scan Parcel")');
    await expect(scanParcelBtn).toBeVisible();
    await scanParcelBtn.click();

    // Verify camera scanner modal
    await expect(page.locator('h3:has-text("Live Barcode & QR Scanner")')).toBeVisible();

    // Simulate scanning a waybill barcode
    const sampleWaybillBtn = page.locator('button:has-text("WB-TN-202610-8819")');
    await expect(sampleWaybillBtn).toBeVisible();
    await sampleWaybillBtn.click();

    // Verify scan captured banner
    await expect(page.locator('text=Scanned Waybill Captured')).toBeVisible();

    // Close camera scanner
    await page.locator('button:has-text("Close Camera Scanner")').click();
    await expect(page.locator('h3:has-text("Live Barcode & QR Scanner")')).not.toBeVisible();

    // 3. Click "Record POD" on a stop to test Touchscreen Signature & GPS Geotag
    const recordPodBtn = page.locator('button:has-text("Record POD")').first();
    await expect(recordPodBtn).toBeVisible();
    await recordPodBtn.click();

    // Verify Touchscreen Signature Component Modal
    await expect(page.locator('h3:has-text("Doorstep Touchscreen Signature")')).toBeVisible();
    await expect(page.locator('canvas')).toBeVisible();
    await expect(page.locator('text=GPS Geotag Stamp')).toBeVisible();
    await expect(page.locator('text=IndexedDB Offline Queue')).toBeVisible();

    // Close POD Modal
    await page.locator('button:has-text("✕")').first().click();
    await expect(page.locator('h3:has-text("Doorstep Touchscreen Signature")')).not.toBeVisible();
  });
});
