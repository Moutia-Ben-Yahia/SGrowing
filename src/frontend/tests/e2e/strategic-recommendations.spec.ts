import { test, expect } from '@playwright/test';

test.describe('Strategic Recommendations E2E: Ever-Teams, Open-Mercato, Ever-Demand & Ever-Gauzy', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_jwt_token_for_e2e_tests');
      localStorage.setItem('sgrowing_tenant_id', '00000000-0000-0000-0000-000000000001');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'user_e2e_rec',
        email: 'admin@sgrowing.io',
        name: 'Admin E2E'
      }));
    });
  });

  test('Recommendation 1 (ever-teams): Linear-style task kanban board, deal conversion & optimistic updates', async ({ page }) => {
    await page.goto('/projects/tasks');

    // 1. Strict LTR check
    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    // 2. Header and metadata
    await expect(page.locator('header')).toContainText('Sprint Task Engine & Post-Close Onboarding');
    await expect(page.locator('header')).toContainText('Ever Teams');

    // 3. Kanban Columns check
    await expect(page.locator('text=Backlog').first()).toBeVisible();
    await expect(page.locator('text=To Do').first()).toBeVisible();
    await expect(page.locator('text=In Progress').first()).toBeVisible();
    await expect(page.locator('text=In Review').first()).toBeVisible();
    await expect(page.locator('text=Done').first()).toBeVisible();

    // 4. Convert Won Deal to Onboarding Project
    const convertBtn = page.locator('[data-testid="convert-deal-btn"]');
    await expect(convertBtn).toBeVisible();
    await convertBtn.click();

    // Verify toast or generated milestone task
    await expect(page.locator('text=converted into').first()).toBeVisible();
    await expect(page.locator('text=Client Kickoff & Scope Validation').first()).toBeVisible();

    // 5. Create new task via modal
    const addTaskBtn = page.locator('[data-testid="add-task-btn"]');
    await addTaskBtn.click();
    await expect(page.locator('text=Create Sprint Task')).toBeVisible();

    await page.locator('input[placeholder*="Ingest Customer Data"]').fill('Deploy High-Speed Queue Service');
    await page.locator('button:has-text("Create Task")').click();

    await expect(page.locator('text=Deploy High-Speed Queue Service').first()).toBeVisible();

    // 6. Optimistic Move Action
    const moveBtn = page.locator('button[title="Move Forward"]').first();
    if (await moveBtn.isVisible()) {
      await moveBtn.click();
      await expect(page.locator('text=Task moved to').first()).toBeVisible();
    }
  });

  test('Recommendation 2 (open-mercato): Deep AI web scraper cockpit, multi-engine crawl & 3-bullet sales hooks', async ({ page }) => {
    await page.goto('/outbound/research');

    // 1. Strict LTR check
    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    // 2. Header and engine selector
    await expect(page.locator('header')).toContainText('Deep AI Web Research & Prospect Intelligence');
    await expect(page.locator('header')).toContainText('Open Mercato');

    const domainInput = page.locator('[data-testid="research-domain-input"]');
    await expect(domainInput).toBeVisible();
    await expect(domainInput).toHaveValue('hyper-logistics.tn');

    const engineSelect = page.locator('[data-testid="engine-selector"]');
    await expect(engineSelect).toBeVisible();

    // 3. Corporate Overview Card
    await expect(page.locator('text=HyperLogistics').first()).toBeVisible();
    await expect(page.locator('text=Key Decision-Maker Bios').first()).toBeVisible();
    await expect(page.locator('text=Detected Technology Stack').first()).toBeVisible();

    // 4. Personalized 3-Bullet Sales Hooks
    await expect(page.locator('text=Personalized 3-Bullet Outbound SDR Sales Hooks').first()).toBeVisible();
    await expect(page.locator('[data-testid="sales-hook-0"]')).toBeVisible();
    await expect(page.locator('[data-testid="sales-hook-1"]')).toBeVisible();
    await expect(page.locator('[data-testid="sales-hook-2"]')).toBeVisible();

    // 5. Test Copy / Inject actions
    const copyBtn = page.locator('button:has-text("Copy")').first();
    await copyBtn.click();
    await expect(page.locator('text=Copied sales hook to clipboard!')).toBeVisible();

    // 6. Run Deep Web Crawl with www domain to verify robust company name extraction
    await domainInput.fill('www.hyper-logistics.tn');
    const runCrawlBtn = page.locator('[data-testid="run-crawl-btn"]');
    await runCrawlBtn.click();

    // Terminal crawler log appears
    await expect(page.locator('text=Crawler Stream').first()).toBeVisible();
    await expect(page.locator('text=Crawl complete!').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=HYPER-LOGISTICS').first()).toBeVisible();
    await expect(page.locator('text=WWW operates').first()).not.toBeVisible();
  });

  test('Recommendation 3 (ever-demand): Multi-warehouse inventory allocation, proximity routing & stock transfer', async ({ page }) => {
    await page.goto('/commerce/warehouses');

    // 1. Strict LTR check
    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    // 2. Header
    await expect(page.locator('header')).toContainText('Multi-Warehouse Inventory Allocation & Geo-Routing');
    await expect(page.locator('header')).toContainText('Ever Demand');

    // 3. Regional Hubs Cards
    await expect(page.locator('[data-testid="warehouse-card-WH-TUN-01"]')).toBeVisible();
    await expect(page.locator('[data-testid="warehouse-card-WH-SOU-02"]')).toBeVisible();
    await expect(page.locator('[data-testid="warehouse-card-WH-SFX-03"]')).toBeVisible();
    await expect(page.locator('[data-testid="warehouse-card-WH-CAI-04"]')).toBeVisible();
    await expect(page.locator('[data-testid="warehouse-card-WH-CAS-05"]')).toBeVisible();

    // 4. Real-Time Stock Matrix Table
    const stockTable = page.locator('[data-testid="stock-matrix-table"]');
    await expect(stockTable).toBeVisible();
    await expect(stockTable).toContainText('SKU-GROW-PRO');
    await expect(stockTable).toContainText('SKU-SMART-CAM');

    // 5. Evaluate Proximity Route
    const calcRouteBtn = page.locator('[data-testid="calculate-route-btn"]');
    await calcRouteBtn.click();

    // Routing result banner should appear
    const resultBanner = page.locator('[data-testid="routing-result-banner"]');
    await expect(resultBanner).toBeVisible();
    await expect(resultBanner).toContainText('Optimal Fulfillment Hub');
    await expect(resultBanner).toContainText('Tunis Nord Logistics Mega-Hub');
  });

  test('Recommendation 4 (ever-gauzy): Multi-organization tree hierarchy & True Gross Margin tracker', async ({ page }) => {
    await page.goto('/organizations/margin');

    // 1. Strict LTR check
    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    // 2. Header
    await expect(page.locator('header')).toContainText('Multi-Organization Hierarchy & Margin Tracking');
    await expect(page.locator('header')).toContainText('Ever Gauzy');

    // 3. Multi-Tenant Organization Tree (Tenant -> Org -> Branch -> Team)
    const tree = page.locator('[data-testid="org-hierarchy-tree"]');
    await expect(tree).toBeVisible();
    await expect(tree).toContainText('SGrowing Global Holding');
    await expect(tree).toContainText('North Africa Regional Hub');
    await expect(tree).toContainText('High-Velocity COD Outbound SDRs');

    // 4. True Gross Margin Calculator: Formula validation
    // Initial: Revenue 15000, COGS 4500, Courier 1200, Labor 35 * $30 = $1050
    // Margin = 15000 - 4500 - 1200 - 1050 = 8250 (55.00%)
    const marginPct = page.locator('[data-testid="margin-percentage-display"]');
    await expect(marginPct).toContainText('55%');

    // Change revenue to 20000, COGS to 5000, Courier to 1000, Labor to 40 hrs @ $30 ($1200)
    // Margin = 20000 - 5000 - 1000 - 1200 = 12800 (64.00%)
    const revInput = page.locator('[data-testid="calc-revenue-input"]');
    await revInput.fill('20000');

    const cogsInput = page.locator('[data-testid="calc-cogs-input"]');
    await cogsInput.fill('5000');

    const courierInput = page.locator('[data-testid="calc-courier-input"]');
    await courierInput.fill('1000');

    const laborInput = page.locator('[data-testid="calc-labor-hours-input"]');
    await laborInput.fill('40');

    await expect(marginPct).toContainText('64%');

    // 5. Record Deal to Margin Ledger
    const recordBtn = page.locator('[data-testid="record-deal-margin-btn"]');
    await recordBtn.click();

    // Verify toast
    await expect(page.locator('text=recorded with True Gross Margin').first()).toBeVisible();

    // Verify Historical Margin Ledger table
    const ledgerTable = page.locator('[data-testid="margin-ledger-table"]');
    await expect(ledgerTable).toBeVisible();
    await expect(ledgerTable).toContainText('64.00%');

    // 6. Verify Executive Rollup cards dynamically updated
    const consolidatedRev = page.locator('[data-testid="consolidated-revenue"]');
    await expect(consolidatedRev).toBeVisible();
    await expect(consolidatedRev).toContainText('$60,700.00');

    const consolidatedMargin = page.locator('[data-testid="consolidated-true-margin"]');
    await expect(consolidatedMargin).toBeVisible();
    await expect(consolidatedMargin).toContainText('$30,300.00');
  });
});
