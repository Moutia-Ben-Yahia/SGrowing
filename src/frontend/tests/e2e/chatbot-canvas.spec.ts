import { test, expect } from '@playwright/test';

test.describe('Chatbot Canvas & Comment-to-DM Engine E2E', () => {
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

  test('should load canvas, manage DAG nodes, and run live simulator', async ({ page }) => {
    // Navigate to Chatbots studio
    await page.goto('/chatbots');

    // Verify Title & Badges
    await expect(page.locator('header')).toContainText('Visual Chatbot Flow Canvas');
    await expect(page.locator('header')).toContainText('ChatbotX');

    // Verify Flow Metadata inputs
    const flowNameInput = page.locator('input[type="text"]').first();
    await expect(flowNameInput).toHaveValue('VIP E-commerce Qualification Bot');

    // Add DAG Step
    const addMessageBtn = page.getByRole('button', { name: /💬 Message/i });
    await addMessageBtn.click();

    // Verify node was added
    await expect(page.locator('text=Send Message #5')).toBeVisible();

    // Switch to Comment-to-DM Triggers Tab
    await page.getByRole('button', { name: 'Comment-to-DM Triggers' }).click();
    await expect(page.locator('text=Instagram & FB Comment-to-DM Deflection Hook')).toBeVisible();

    // Test Comment Trigger Webhook Button
    const testWebhookBtn = page.getByRole('button', { name: /Test Comment-to-DM Webhook/i });
    await testWebhookBtn.click();
    await expect(page.locator('text=Webhook Execution Success')).toBeVisible();

    // Switch to Live Simulator Tab
    await page.getByRole('button', { name: 'Live Simulator' }).click();
    await expect(page.locator('text=Real-Time Flow Execution Emulator')).toBeVisible();

    // Type a message in the simulator
    const chatInput = page.getByPlaceholder('Type your response to test the chatbot...');
    await chatInput.fill('I want to check Pricing');
    await page.getByRole('button', { name: 'Send' }).click();

    // Verify message sent into message feed
    await expect(page.getByText('I want to check Pricing', { exact: true })).toBeVisible();
  });
});
