using System.Text.Json;
using System.Text.RegularExpressions;
using SGrowing.Api.Modules.Channels.WhatsApp.Catalog;
using Xunit;

namespace SGrowing.Tests.Channels;

public class WhatsAppCatalogCartServiceTests
{
    private readonly WhatsAppCatalogCartService _service;

    public WhatsAppCatalogCartServiceTests()
    {
        _service = new WhatsAppCatalogCartService();
    }

    #region Catalog Product Retrieval & Filtering

    [Fact]
    public void GetProducts_Should_Return_All_PreSeeded_Active_Products()
    {
        var products = _service.GetProducts().ToList();

        Assert.NotEmpty(products);
        Assert.True(products.Count >= 4);
        Assert.Contains(products, p => p.Sku == "PRO-SAAS");
        Assert.Contains(products, p => p.Sku == "GROWTH-AI");
        Assert.Contains(products, p => p.Sku == "ACCEL-ONB");
        Assert.Contains(products, p => p.Sku == "ENT-SLA");
    }

    [Fact]
    public void GetProducts_WithCategory_Should_Filter_Accurately()
    {
        var subscriptionProducts = _service.GetProducts(category: "Subscription").ToList();

        Assert.NotEmpty(subscriptionProducts);
        Assert.All(subscriptionProducts, p => Assert.Equal("Subscription", p.Category, ignoreCase: true));
        Assert.Contains(subscriptionProducts, p => p.Sku == "PRO-SAAS");
        Assert.DoesNotContain(subscriptionProducts, p => p.Sku == "GROWTH-AI");
    }

    [Fact]
    public void GetProducts_WithSearchQuery_Should_Match_Title_Or_Sku_Or_Description()
    {
        var searchBySku = _service.GetProducts(search: "GROWTH-AI").ToList();
        Assert.Single(searchBySku);
        Assert.Equal("GROWTH-AI", searchBySku[0].Sku);

        var searchByWord = _service.GetProducts(search: "Accelerator").ToList();
        Assert.Single(searchByWord);
        Assert.Equal("ACCEL-ONB", searchByWord[0].Sku);

        var searchByDesc = _service.GetProducts(search: "omnichannel").ToList();
        Assert.Single(searchByDesc);
        Assert.Equal("PRO-SAAS", searchByDesc[0].Sku);
    }

    [Fact]
    public void GetProductBySku_Should_Return_Product_When_Exists()
    {
        var product = _service.GetProductBySku("ENT-SLA");

        Assert.NotNull(product);
        Assert.Equal("Enterprise SLA Pack", product.Title);
        Assert.Equal(499.00m, product.UnitPrice);
        Assert.True(product.StockAvailable > 0);
    }

    [Fact]
    public void GetProductBySku_Should_Return_Null_When_Not_Exists_Or_Empty()
    {
        Assert.Null(_service.GetProductBySku("NON-EXISTENT-SKU"));
        Assert.Null(_service.GetProductBySku(""));
    }

    [Fact]
    public void AddOrUpdateProduct_Should_Insert_New_Product_And_Allow_Retrieval()
    {
        var custom = new CatalogProduct
        {
            Sku = "CUSTOM-DEV-HOURS",
            Title = "Custom Dev Block",
            Description = "10 hours of dedicated senior engineering",
            Category = "Services",
            UnitPrice = 850.00m,
            Currency = "USD",
            StockAvailable = 5
        };

        var created = _service.AddOrUpdateProduct(custom);
        Assert.Equal("CUSTOM-DEV-HOURS", created.Sku);

        var fetched = _service.GetProductBySku("CUSTOM-DEV-HOURS");
        Assert.NotNull(fetched);
        Assert.Equal(850.00m, fetched.UnitPrice);
        Assert.Equal(5, fetched.StockAvailable);
    }

    [Fact]
    public void UpdateStock_Should_Adjust_Inventory_And_Reject_Negative_Stock()
    {
        var sku = "PRO-SAAS";
        var initial = _service.GetProductBySku(sku)!.StockAvailable;

        var incResult = _service.UpdateStock(sku, 15);
        Assert.True(incResult);
        Assert.Equal(initial + 15, _service.GetProductBySku(sku)!.StockAvailable);

        var decResult = _service.UpdateStock(sku, -10);
        Assert.True(decResult);
        Assert.Equal(initial + 5, _service.GetProductBySku(sku)!.StockAvailable);

        // Try subtracting more than available
        var excessiveDec = _service.UpdateStock(sku, -9999);
        Assert.False(excessiveDec);
        Assert.Equal(initial + 5, _service.GetProductBySku(sku)!.StockAvailable);
    }

    #endregion

    #region WhatsApp Interactive Message Formatter

    [Fact]
    public void FormatInteractiveMessage_ProductList_Should_Construct_Valid_Payload()
    {
        var req = new FormatInteractiveMessageRequest
        {
            CustomerPhone = "+1234567890",
            CatalogId = "CAT_WHATSAPP_001",
            InteractiveType = "product_list",
            HeaderText = "Explore Our Products",
            BodyText = "Select an item to view options",
            FooterText = "Powered by SGrowing",
            SectionTitle = "Top Picks",
            Skus = new List<string> { "PRO-SAAS", "GROWTH-AI" }
        };

        var payload = _service.FormatInteractiveMessage(req);

        Assert.Equal("whatsapp", payload.MessagingProduct);
        Assert.Equal("individual", payload.RecipientType);
        Assert.Equal("+1234567890", payload.To);
        Assert.Equal("interactive", payload.Type);
        Assert.Equal("product_list", payload.Interactive.Type);
        Assert.NotNull(payload.Interactive.Header);
        Assert.Equal("Explore Our Products", payload.Interactive.Header.Text);
        Assert.Equal("Select an item to view options", payload.Interactive.Body.Text);
        Assert.NotNull(payload.Interactive.Footer);
        Assert.Equal("Powered by SGrowing", payload.Interactive.Footer.Text);
        Assert.Equal("CAT_WHATSAPP_001", payload.Interactive.Action.CatalogId);
        Assert.NotNull(payload.Interactive.Action.Sections);
        Assert.Single(payload.Interactive.Action.Sections);
        Assert.Equal("Top Picks", payload.Interactive.Action.Sections[0].Title);
        Assert.Equal(2, payload.Interactive.Action.Sections[0].ProductItems.Count);
        Assert.Equal("PRO-SAAS", payload.Interactive.Action.Sections[0].ProductItems[0].ProductRetailerId);
        Assert.Equal("GROWTH-AI", payload.Interactive.Action.Sections[0].ProductItems[1].ProductRetailerId);
    }

    [Fact]
    public void FormatInteractiveMessage_SingleProduct_Should_Omit_Header_And_Use_RetailerId()
    {
        var req = new FormatInteractiveMessageRequest
        {
            CustomerPhone = "+1987654321",
            CatalogId = "CAT_WHATSAPP_001",
            InteractiveType = "single_product",
            BodyText = "Check out our Enterprise SLA Plan",
            FooterText = "Limited Availability",
            Skus = new List<string> { "ENT-SLA" }
        };

        var payload = _service.FormatInteractiveMessage(req);

        Assert.Equal("single_product", payload.Interactive.Type);
        Assert.Null(payload.Interactive.Header);
        Assert.Equal("Check out our Enterprise SLA Plan", payload.Interactive.Body.Text);
        Assert.Equal("CAT_WHATSAPP_001", payload.Interactive.Action.CatalogId);
        Assert.Equal("ENT-SLA", payload.Interactive.Action.ProductRetailerId);
        Assert.Null(payload.Interactive.Action.Sections);
    }

    [Fact]
    public void FormatInteractiveMessageJson_Should_Produce_Valid_Meta_Schema_Json()
    {
        var req = new FormatInteractiveMessageRequest
        {
            CustomerPhone = "+447123456789",
            CatalogId = "CAT_DEFAULT",
            InteractiveType = "product_list",
            Skus = new List<string> { "PRO-SAAS" }
        };

        var json = _service.FormatInteractiveMessageJson(req);

        Assert.NotNull(json);
        Assert.Contains("\"messaging_product\": \"whatsapp\"", json);
        Assert.Contains("\"product_list\"", json);
        Assert.Contains("\"catalog_id\": \"CAT_DEFAULT\"", json);
        Assert.Contains("\"product_retailer_id\": \"PRO-SAAS\"", json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("+447123456789", root.GetProperty("to").GetString());
        var interactive = root.GetProperty("interactive");
        Assert.Equal("product_list", interactive.GetProperty("type").GetString());
    }

    [Fact]
    public void FormatInteractiveMessage_WithEmptySkus_Should_Default_To_All_Catalog_Products()
    {
        var req = new FormatInteractiveMessageRequest
        {
            CustomerPhone = "+1122334455",
            InteractiveType = "product_list",
            Skus = new List<string>()
        };

        var payload = _service.FormatInteractiveMessage(req);
        Assert.NotNull(payload.Interactive.Action.Sections);
        var items = payload.Interactive.Action.Sections[0].ProductItems;
        Assert.True(items.Count >= 4);
    }

    #endregion

    #region In-Chat Cart State Machine & Quantity Modifications

    [Fact]
    public void GetCart_ForNewPhone_Should_Return_Empty_Cart()
    {
        var cart = _service.GetCart("+15550001");

        Assert.NotNull(cart);
        Assert.Equal("+15550001", cart.CustomerPhone);
        Assert.Equal(CartState.Empty, cart.State);
        Assert.Empty(cart.Items);
        Assert.Equal(0m, cart.Subtotal);
        Assert.Equal(0m, cart.TotalAmount);
    }

    [Fact]
    public void UpdateCart_AddProduct_Should_Transition_To_Active_And_Compute_Totals()
    {
        var phone = "+15550002";
        var cart = _service.UpdateCart(new UpdateCartRequest
        {
            CustomerPhone = phone,
            Sku = "PRO-SAAS",
            Quantity = 2,
            IsDelta = false
        });

        Assert.Equal(CartState.Active, cart.State);
        Assert.Single(cart.Items);
        Assert.Equal("PRO-SAAS", cart.Items[0].Sku);
        Assert.Equal(2, cart.Items[0].Quantity);
        Assert.Equal(99.00m, cart.Items[0].UnitPrice);
        Assert.Equal(198.00m, cart.Items[0].LineTotal);
        Assert.Equal(198.00m, cart.Subtotal);
        Assert.Equal(198.00m, cart.TotalAmount);
    }

    [Fact]
    public void UpdateCart_DeltaIncrement_Should_Accumulate_Quantity()
    {
        var phone = "+15550003";

        _service.UpdateCart(new UpdateCartRequest
        {
            CustomerPhone = phone,
            Sku = "GROWTH-AI",
            Quantity = 1
        });

        var updated = _service.UpdateCart(new UpdateCartRequest
        {
            CustomerPhone = phone,
            Sku = "GROWTH-AI",
            Quantity = 2,
            IsDelta = true
        });

        Assert.Single(updated.Items);
        Assert.Equal(3, updated.Items[0].Quantity);
        Assert.Equal(447.00m, updated.Subtotal); // 3 * 149.00m
    }

    [Fact]
    public void UpdateCart_QuantityZero_Should_Remove_Item_And_Set_Empty_If_No_Items_Left()
    {
        var phone = "+15550004";

        _service.UpdateCart(new UpdateCartRequest
        {
            CustomerPhone = phone,
            Sku = "PRO-SAAS",
            Quantity = 1
        });

        var updated = _service.UpdateCart(new UpdateCartRequest
        {
            CustomerPhone = phone,
            Sku = "PRO-SAAS",
            Quantity = 0
        });

        Assert.Empty(updated.Items);
        Assert.Equal(CartState.Empty, updated.State);
        Assert.Equal(0m, updated.Subtotal);
    }

    [Fact]
    public void UpdateCart_WithInvalidSku_Should_Throw_KeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() =>
        {
            _service.UpdateCart(new UpdateCartRequest
            {
                CustomerPhone = "+15550005",
                Sku = "UNKNOWN-PRODUCT-123",
                Quantity = 1
            });
        });
    }

    [Fact]
    public void UpdateCart_ExceedingAvailableStock_Should_Throw_InvalidOperationException()
    {
        var product = _service.GetProductBySku("ENT-SLA")!;
        var requested = product.StockAvailable + 10;

        Assert.Throws<InvalidOperationException>(() =>
        {
            _service.UpdateCart(new UpdateCartRequest
            {
                CustomerPhone = "+15550006",
                Sku = "ENT-SLA",
                Quantity = requested
            });
        });
    }

    [Fact]
    public void UpdateCart_MultipleItems_Should_Aggregate_Correctly()
    {
        var phone = "+15550007";

        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 1 });
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "GROWTH-AI", Quantity = 2 });

        var cart = _service.GetCart(phone);
        Assert.Equal(2, cart.Items.Count);
        // PRO-SAAS = 99.00, GROWTH-AI = 149 * 2 = 298.00 => Total 397.00
        Assert.Equal(397.00m, cart.Subtotal);
        Assert.Equal(397.00m, cart.TotalAmount);
    }

    [Fact]
    public void ClearCart_Should_Reset_Cart_And_Set_State_To_Cancelled()
    {
        var phone = "+15550008";
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 1 });

        var cleared = _service.ClearCart(phone);
        Assert.Empty(cleared.Items);
        Assert.Equal(CartState.Cancelled, cleared.State);
        Assert.Equal(0m, cleared.Subtotal);
        Assert.Equal(0m, cleared.TotalAmount);
    }

    #endregion

    #region Coupon Engine & Discount Validation

    [Fact]
    public void GetCoupons_Should_Return_PreSeeded_Coupons()
    {
        var coupons = _service.GetCoupons().ToList();
        Assert.NotEmpty(coupons);
        Assert.Contains(coupons, c => c.Code == "GROWTH20");
        Assert.Contains(coupons, c => c.Code == "WELCOME50");
        Assert.Contains(coupons, c => c.Code == "FLASH10");
    }

    [Fact]
    public void ApplyCoupon_Percentage_Should_Compute_Accurate_Discount_And_Transition_State()
    {
        var phone = "+15550010";
        // Subtotal = $198.00, Coupon GROWTH20 = 20% off (Min $50)
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 2 });

        var result = _service.ApplyCoupon(new ApplyCouponRequest
        {
            CustomerPhone = phone,
            CouponCode = "GROWTH20"
        });

        Assert.True(result.Success);
        Assert.Equal(CartState.DiscountApplied, result.Cart.State);
        Assert.NotNull(result.Cart.AppliedCoupon);
        Assert.Equal("GROWTH20", result.Cart.AppliedCoupon.Code);
        // 20% of 198.00 = 39.60
        Assert.Equal(39.60m, result.Cart.DiscountAmount);
        Assert.Equal(158.40m, result.Cart.TotalAmount);
    }

    [Fact]
    public void ApplyCoupon_Fixed_Should_Subtract_Fixed_Amount()
    {
        var phone = "+15550011";
        // Subtotal = $149.00, Coupon WELCOME50 = $50 off (Min $100)
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "GROWTH-AI", Quantity = 1 });

        var result = _service.ApplyCoupon(new ApplyCouponRequest
        {
            CustomerPhone = phone,
            CouponCode = "WELCOME50"
        });

        Assert.True(result.Success);
        Assert.Equal(50.00m, result.Cart.DiscountAmount);
        Assert.Equal(99.00m, result.Cart.TotalAmount);
    }

    [Fact]
    public void ApplyCoupon_Below_MinimumOrderValue_Should_Fail()
    {
        var phone = "+15550012";
        // Add a cheap product if available or check minimum: WELCOME50 requires $100 min, PRO-SAAS is $99
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 1 });

        var result = _service.ApplyCoupon(new ApplyCouponRequest
        {
            CustomerPhone = phone,
            CouponCode = "WELCOME50" // Min is $100, cart is $99
        });

        Assert.False(result.Success);
        Assert.Contains("minimum", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Cart.AppliedCoupon);
        Assert.Equal(CartState.Active, result.Cart.State);
    }

    [Fact]
    public void ApplyCoupon_Invalid_Or_Inactive_Code_Should_Fail()
    {
        var phone = "+15550013";
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 1 });

        var result = _service.ApplyCoupon(new ApplyCouponRequest
        {
            CustomerPhone = phone,
            CouponCode = "FAKECODE123"
        });

        Assert.False(result.Success);
        Assert.Contains("invalid", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyCoupon_On_EmptyCart_Should_Fail()
    {
        var result = _service.ApplyCoupon(new ApplyCouponRequest
        {
            CustomerPhone = "+15550014",
            CouponCode = "FLASH10"
        });

        Assert.False(result.Success);
        Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateCart_QuantityDrop_Below_Coupon_MinOrder_Should_Revoke_Coupon()
    {
        var phone = "+15550015";
        // Add 2x PRO-SAAS = $198, apply WELCOME50 (min $100)
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 2 });
        _service.ApplyCoupon(new ApplyCouponRequest { CustomerPhone = phone, CouponCode = "WELCOME50" });

        // Reduce to 1x PRO-SAAS = $99 (< $100 min)
        var updated = _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 1 });

        Assert.Equal(CartState.Active, updated.State);
        Assert.Null(updated.AppliedCoupon);
        Assert.Equal(0m, updated.DiscountAmount);
        Assert.Equal(99.00m, updated.TotalAmount);
    }

    #endregion

    #region Stock Reservation & Checkout Generation

    [Fact]
    public void ReserveCheckout_Should_Lock_Stock_Generate_Order_And_Transition_To_CheckoutPending()
    {
        var phone = "+15550020";
        var initialStock = _service.GetProductBySku("PRO-SAAS")!.StockAvailable;

        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 3 });

        var checkout = _service.ReserveCheckout(new ReserveCheckoutRequest
        {
            CustomerPhone = phone,
            PaymentMethod = PaymentMethod.Stripe,
            TtlMinutes = 15,
            ShippingAddress = "123 Silicon Blvd, Tech City"
        });

        Assert.True(checkout.Success);
        Assert.NotNull(checkout.OrderId);
        Assert.Matches(@"^ORD-WA-\d{8}-[A-F0-9]{6}$", checkout.OrderId);
        Assert.NotNull(checkout.PaymentLink);
        Assert.Contains("stripe.com", checkout.PaymentLink);
        Assert.Contains(checkout.OrderId, checkout.PaymentLink);
        Assert.NotNull(checkout.InvoiceStanza);
        Assert.Contains("ORDER INVOICE", checkout.InvoiceStanza);
        Assert.Equal(CartState.CheckoutPending, checkout.Cart!.State);

        // Verify stock was decremented
        var updatedStock = _service.GetProductBySku("PRO-SAAS")!.StockAvailable;
        Assert.Equal(initialStock - 3, updatedStock);
    }

    [Fact]
    public void ReserveCheckout_With_AutoConfirm_Should_Transition_Directly_To_OrderPlaced()
    {
        var phone = "+15550021";
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "GROWTH-AI", Quantity = 1 });

        var checkout = _service.ReserveCheckout(new ReserveCheckoutRequest
        {
            CustomerPhone = phone,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            AutoConfirm = true
        });

        Assert.True(checkout.Success);
        Assert.Equal(CartState.OrderPlaced, checkout.Cart!.State);
        Assert.Contains("Cash On Delivery", checkout.InvoiceStanza);
    }

    [Fact]
    public void ReserveCheckout_On_EmptyCart_Should_Fail()
    {
        var checkout = _service.ReserveCheckout(new ReserveCheckoutRequest
        {
            CustomerPhone = "+15550022"
        });

        Assert.False(checkout.Success);
        Assert.Contains("empty", checkout.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReserveCheckout_When_Stock_Insufficient_Should_Fail_And_Not_Deduct()
    {
        var phone = "+15550023";
        var product = _service.GetProductBySku("ENT-SLA")!;
        var stockBefore = product.StockAvailable;

        // Add max stock to cart
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "ENT-SLA", Quantity = stockBefore });

        // Simulate another customer purchasing all stock
        _service.UpdateStock("ENT-SLA", -stockBefore);

        var checkout = _service.ReserveCheckout(new ReserveCheckoutRequest
        {
            CustomerPhone = phone
        });

        Assert.False(checkout.Success);
        Assert.Contains("Insufficient stock", checkout.Message);

        // Restore stock
        _service.UpdateStock("ENT-SLA", stockBefore);
    }

    [Fact]
    public void ConfirmOrder_Should_Transition_From_CheckoutPending_To_OrderPlaced()
    {
        var phone = "+15550024";
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 1 });
        var checkout = _service.ReserveCheckout(new ReserveCheckoutRequest { CustomerPhone = phone });

        Assert.Equal(CartState.CheckoutPending, checkout.Cart!.State);

        var confirm = _service.ConfirmOrder(phone);
        Assert.True(confirm.Success);
        Assert.Equal(CartState.OrderPlaced, confirm.Cart.State);
    }

    [Fact]
    public void ConfirmOrder_On_NonPending_Cart_Should_Fail()
    {
        var phone = "+15550025";
        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 1 });

        var confirm = _service.ConfirmOrder(phone);
        Assert.False(confirm.Success);
        Assert.Contains("CheckoutPending", confirm.Message);
    }

    #endregion

    #region Reservation Expiration & Stock Recovery

    [Fact]
    public void ReleaseExpiredReservations_Should_Return_Stock_And_Mark_Cart_Expired()
    {
        var phone = "+15550030";
        var initialStock = _service.GetProductBySku("ACCEL-ONB")!.StockAvailable;

        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "ACCEL-ONB", Quantity = 2 });
        var checkout = _service.ReserveCheckout(new ReserveCheckoutRequest { CustomerPhone = phone, TtlMinutes = 15 });

        Assert.Equal(initialStock - 2, _service.GetProductBySku("ACCEL-ONB")!.StockAvailable);

        // Simulate 20 minutes passing
        var future = DateTimeOffset.UtcNow.AddMinutes(20);
        var released = _service.ReleaseExpiredReservations(future);

        Assert.True(released >= 1);
        Assert.Equal(initialStock, _service.GetProductBySku("ACCEL-ONB")!.StockAvailable);

        var cart = _service.GetCart(phone);
        Assert.Equal(CartState.Expired, cart.State);
    }

    [Fact]
    public void ReleaseExpiredReservations_Should_Not_Release_Active_Reservations()
    {
        var phone = "+15550031";
        var initialStock = _service.GetProductBySku("PRO-SAAS")!.StockAvailable;

        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 1 });
        _service.ReserveCheckout(new ReserveCheckoutRequest { CustomerPhone = phone, TtlMinutes = 30 });

        // Check as of now (0 min passed)
        var released = _service.ReleaseExpiredReservations(DateTimeOffset.UtcNow);
        Assert.Equal(0, released);
        Assert.Equal(initialStock - 1, _service.GetProductBySku("PRO-SAAS")!.StockAvailable);
    }

    [Fact]
    public void ClearCart_During_CheckoutPending_Should_Immediately_Release_Stock()
    {
        var phone = "+15550032";
        var initialStock = _service.GetProductBySku("PRO-SAAS")!.StockAvailable;

        _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 2 });
        _service.ReserveCheckout(new ReserveCheckoutRequest { CustomerPhone = phone });

        Assert.Equal(initialStock - 2, _service.GetProductBySku("PRO-SAAS")!.StockAvailable);

        _service.ClearCart(phone);
        Assert.Equal(initialStock, _service.GetProductBySku("PRO-SAAS")!.StockAvailable);
    }

    #endregion

    #region Concurrency & Thread-Safety Tests

    [Fact]
    public async Task Concurrent_Reservations_On_Limited_Stock_Should_Never_OverAllocate()
    {
        // Add a test product with only 3 units
        var sku = $"LIMITED-ITEM-{Guid.NewGuid():N}";
        _service.AddOrUpdateProduct(new CatalogProduct
        {
            Sku = sku,
            Title = "Super Rare Item",
            UnitPrice = 50.00m,
            StockAvailable = 3
        });

        const int threadCount = 15;
        var tasks = new Task<CheckoutResult>[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            var customerPhone = $"+1999000{i:D2}";
            _service.UpdateCart(new UpdateCartRequest
            {
                CustomerPhone = customerPhone,
                Sku = sku,
                Quantity = 1
            });

            tasks[i] = Task.Run(() => _service.ReserveCheckout(new ReserveCheckoutRequest
            {
                CustomerPhone = customerPhone
            }));
        }

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.Success);
        var failCount = results.Count(r => !r.Success);

        Assert.Equal(3, successCount);
        Assert.Equal(12, failCount);

        var finalStock = _service.GetProductBySku(sku)!.StockAvailable;
        Assert.Equal(0, finalStock);
    }

    [Fact]
    public async Task Concurrent_Cart_Operations_From_Multiple_Customers_Should_Remain_Isolated()
    {
        const int customerCount = 20;
        var tasks = new List<Task>();

        for (int i = 0; i < customerCount; i++)
        {
            var phone = $"+1888000{i:D2}";
            tasks.Add(Task.Run(() =>
            {
                _service.UpdateCart(new UpdateCartRequest { CustomerPhone = phone, Sku = "PRO-SAAS", Quantity = 1 });
                _service.ApplyCoupon(new ApplyCouponRequest { CustomerPhone = phone, CouponCode = "GROWTH20" });
                var cart = _service.GetCart(phone);
                Assert.Equal(CartState.DiscountApplied, cart.State);
                Assert.Equal(99.00m, cart.Subtotal);
                Assert.Equal(19.80m, cart.DiscountAmount);
                Assert.Equal(79.20m, cart.TotalAmount);
            }));
        }

        await Task.WhenAll(tasks);
    }

    #endregion
}
