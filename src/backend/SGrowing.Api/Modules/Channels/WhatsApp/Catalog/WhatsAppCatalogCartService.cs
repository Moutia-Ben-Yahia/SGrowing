using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Channels.WhatsApp.Catalog;

#region Enums & Models

public enum CartState
{
    Empty,
    Active,
    DiscountApplied,
    CheckoutPending,
    OrderPlaced,
    Expired,
    Cancelled
}

public enum DiscountType
{
    Percentage,
    Fixed
}

public enum PaymentMethod
{
    Stripe,
    CashOnDelivery
}

public class CatalogProduct
{
    public string Sku { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int StockAvailable { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class CartItem
{
    public string Sku { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal LineTotal => Math.Round(UnitPrice * Quantity, 2);
    public string ImageUrl { get; set; } = string.Empty;
}

public class CouponRule
{
    public string Code { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public decimal MinimumOrderValue { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ExpiryDate { get; set; }
}

public class AppliedCoupon
{
    public string Code { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public decimal DiscountAmount { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class StockReservation
{
    public string ReservationId { get; set; } = Guid.NewGuid().ToString("N");
    public string CustomerPhone { get; set; } = string.Empty;
    public Dictionary<string, int> ReservedItems { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset ReservedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsReleased { get; set; }
    public bool IsCommitted { get; set; }
}

public class InChatCart
{
    public string CustomerPhone { get; set; } = string.Empty;
    public CartState State { get; set; } = CartState.Empty;
    public List<CartItem> Items { get; set; } = new();
    public decimal Subtotal => Math.Round(Items.Sum(i => i.LineTotal), 2);
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount => Math.Max(0m, Math.Round(Subtotal - DiscountAmount, 2));
    public string Currency => Items.FirstOrDefault()?.Currency ?? "USD";
    public AppliedCoupon? AppliedCoupon { get; set; }
    public string? ReservationId { get; set; }
    public DateTimeOffset? ReservationExpiresAt { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentLink { get; set; }
    public string? ConfirmationStanza { get; set; }
    public string? ShippingAddress { get; set; }
    public PaymentMethod? SelectedPaymentMethod { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

#endregion

#region Request & Response DTOs

public class FormatInteractiveMessageRequest
{
    public string CustomerPhone { get; set; } = string.Empty;
    public string CatalogId { get; set; } = "CATALOG_DEFAULT";
    public string InteractiveType { get; set; } = "product_list"; // "product_list" or "single_product"
    public List<string> Skus { get; set; } = new();
    public string? HeaderText { get; set; }
    public string? BodyText { get; set; }
    public string? FooterText { get; set; }
    public string? SectionTitle { get; set; }
}

public class UpdateCartRequest
{
    public string CustomerPhone { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsDelta { get; set; } = false;
}

public class ApplyCouponRequest
{
    public string CustomerPhone { get; set; } = string.Empty;
    public string CouponCode { get; set; } = string.Empty;
}

public class ReserveCheckoutRequest
{
    public string CustomerPhone { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Stripe;
    public int TtlMinutes { get; set; } = 15;
    public string? ShippingAddress { get; set; }
    public bool AutoConfirm { get; set; } = false;
}

public class CheckoutResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentLink { get; set; }
    public string? InvoiceStanza { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public InChatCart? Cart { get; set; }
}

public class CouponValidationResult
{
    public bool IsValid { get; set; }
    public string? Message { get; set; }
    public CouponRule? Coupon { get; set; }
    public decimal ComputedDiscount { get; set; }
}

#endregion

#region Meta Interactive Message Schema DTOs

public class WhatsAppInteractiveHeader
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class WhatsAppInteractiveBody
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class WhatsAppInteractiveFooter
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class WhatsAppProductItem
{
    [JsonPropertyName("product_retailer_id")]
    public string ProductRetailerId { get; set; } = string.Empty;
}

public class WhatsAppProductSection
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("product_items")]
    public List<WhatsAppProductItem> ProductItems { get; set; } = new();
}

public class WhatsAppInteractiveAction
{
    [JsonPropertyName("catalog_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CatalogId { get; set; }

    [JsonPropertyName("product_retailer_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProductRetailerId { get; set; }

    [JsonPropertyName("sections")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<WhatsAppProductSection>? Sections { get; set; }
}

public class WhatsAppInteractiveContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "product_list";

    [JsonPropertyName("header")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WhatsAppInteractiveHeader? Header { get; set; }

    [JsonPropertyName("body")]
    public WhatsAppInteractiveBody Body { get; set; } = new();

    [JsonPropertyName("footer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WhatsAppInteractiveFooter? Footer { get; set; }

    [JsonPropertyName("action")]
    public WhatsAppInteractiveAction Action { get; set; } = new();
}

public class WhatsAppInteractivePayload
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = "whatsapp";

    [JsonPropertyName("recipient_type")]
    public string RecipientType { get; set; } = "individual";

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "interactive";

    [JsonPropertyName("interactive")]
    public WhatsAppInteractiveContent Interactive { get; set; } = new();
}

#endregion

#region Service Interface

public interface IWhatsAppCatalogCartService
{
    // Product Catalog
    IEnumerable<CatalogProduct> GetProducts(string? category = null, string? search = null);
    CatalogProduct? GetProductBySku(string sku);
    CatalogProduct AddOrUpdateProduct(CatalogProduct product);
    bool UpdateStock(string sku, int delta);

    // Meta Interactive Message Formatting
    WhatsAppInteractivePayload FormatInteractiveMessage(FormatInteractiveMessageRequest request);
    string FormatInteractiveMessageJson(FormatInteractiveMessageRequest request);

    // Cart Management & State Machine
    InChatCart GetCart(string customerPhone);
    InChatCart UpdateCart(UpdateCartRequest request);
    InChatCart ClearCart(string customerPhone);

    // Coupons
    IEnumerable<CouponRule> GetCoupons();
    CouponRule AddOrUpdateCoupon(CouponRule coupon);
    CouponValidationResult ValidateCoupon(string code, decimal subtotal);
    (bool Success, string Message, InChatCart Cart) ApplyCoupon(ApplyCouponRequest request);

    // Reservations & Checkout
    CheckoutResult ReserveCheckout(ReserveCheckoutRequest request);
    (bool Success, string Message, InChatCart Cart) ConfirmOrder(string customerPhone);
    int ReleaseExpiredReservations(DateTimeOffset? asOfUtc = null);
}

#endregion

#region Service Implementation

public class WhatsAppCatalogCartService : IWhatsAppCatalogCartService
{
    private readonly ConcurrentDictionary<string, CatalogProduct> _products = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CouponRule> _coupons = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, InChatCart> _carts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, StockReservation> _reservations = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _syncLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WhatsAppCatalogCartService()
    {
        SeedProducts();
        SeedCoupons();
    }

    private void SeedProducts()
    {
        var initialProducts = new[]
        {
            new CatalogProduct
            {
                Sku = "PRO-SAAS",
                Title = "Pro SaaS Plan",
                Description = "Monthly multi-tenant workspace with advanced automated workflows and omnichannel orchestration",
                Category = "Subscription",
                UnitPrice = 99.00m,
                Currency = "USD",
                StockAvailable = 100,
                ImageUrl = "https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=600",
                IsActive = true
            },
            new CatalogProduct
            {
                Sku = "GROWTH-AI",
                Title = "Growth AI Bundle",
                Description = "AI autonomous copilot, voice synthesis & predictive customer intent engine with 1M tokens",
                Category = "Add-on",
                UnitPrice = 149.00m,
                Currency = "USD",
                StockAvailable = 50,
                ImageUrl = "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=600",
                IsActive = true
            },
            new CatalogProduct
            {
                Sku = "ACCEL-ONB",
                Title = "Onboarding Accelerator",
                Description = "White-glove CRM data migration, custom domain setup and SLA onboarding session with engineers",
                Category = "Services",
                UnitPrice = 299.00m,
                Currency = "USD",
                StockAvailable = 20,
                ImageUrl = "https://images.unsplash.com/photo-1552664730-d307ca884978?w=600",
                IsActive = true
            },
            new CatalogProduct
            {
                Sku = "ENT-SLA",
                Title = "Enterprise SLA Pack",
                Description = "24/7 dedicated telephone routing, 15-minute response SLA & priority dispatch guarantee",
                Category = "Support",
                UnitPrice = 499.00m,
                Currency = "USD",
                StockAvailable = 15,
                ImageUrl = "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?w=600",
                IsActive = true
            }
        };

        foreach (var p in initialProducts)
        {
            _products[p.Sku] = p;
        }
    }

    private void SeedCoupons()
    {
        var initialCoupons = new[]
        {
            new CouponRule
            {
                Code = "GROWTH20",
                Type = DiscountType.Percentage,
                Value = 20m,
                MinimumOrderValue = 50.00m,
                Description = "20% off all orders over $50",
                IsActive = true
            },
            new CouponRule
            {
                Code = "WELCOME50",
                Type = DiscountType.Fixed,
                Value = 50.00m,
                MinimumOrderValue = 100.00m,
                Description = "$50 off your order of $100 or more",
                IsActive = true
            },
            new CouponRule
            {
                Code = "FLASH10",
                Type = DiscountType.Percentage,
                Value = 10m,
                MinimumOrderValue = 0.00m,
                Description = "10% flash discount with no minimum purchase",
                IsActive = true
            },
            new CouponRule
            {
                Code = "ENTERPRISE100",
                Type = DiscountType.Fixed,
                Value = 100.00m,
                MinimumOrderValue = 300.00m,
                Description = "$100 enterprise discount on orders over $300",
                IsActive = true
            }
        };

        foreach (var c in initialCoupons)
        {
            _coupons[c.Code] = c;
        }
    }

    #region Catalog Management

    public IEnumerable<CatalogProduct> GetProducts(string? category = null, string? search = null)
    {
        var query = _products.Values.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(p => p.Title).ToList();
    }

    public CatalogProduct? GetProductBySku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        return _products.TryGetValue(sku, out var product) ? product : null;
    }

    public CatalogProduct AddOrUpdateProduct(CatalogProduct product)
    {
        if (string.IsNullOrWhiteSpace(product.Sku))
            throw new ArgumentException("SKU is required", nameof(product));

        _products[product.Sku] = product;
        return product;
    }

    public bool UpdateStock(string sku, int delta)
    {
        lock (_syncLock)
        {
            if (!_products.TryGetValue(sku, out var product))
                return false;

            var newStock = product.StockAvailable + delta;
            if (newStock < 0) return false;

            product.StockAvailable = newStock;
            return true;
        }
    }

    #endregion

    #region Meta Interactive Message Formatter

    public WhatsAppInteractivePayload FormatInteractiveMessage(FormatInteractiveMessageRequest request)
    {
        var isSingle = string.Equals(request.InteractiveType, "single_product", StringComparison.OrdinalIgnoreCase);

        var payload = new WhatsAppInteractivePayload
        {
            MessagingProduct = "whatsapp",
            RecipientType = "individual",
            To = request.CustomerPhone,
            Type = "interactive",
            Interactive = new WhatsAppInteractiveContent
            {
                Type = isSingle ? "single_product" : "product_list",
                Body = new WhatsAppInteractiveBody
                {
                    Text = !string.IsNullOrWhiteSpace(request.BodyText)
                        ? request.BodyText
                        : (isSingle ? "Explore this product from our official WhatsApp Catalog." : "Browse our catalog below and add products directly into your in-chat cart.")
                },
                Footer = !string.IsNullOrWhiteSpace(request.FooterText)
                    ? new WhatsAppInteractiveFooter { Text = request.FooterText }
                    : new WhatsAppInteractiveFooter { Text = "SGrowing • Instant Checkout Available" },
                Action = new WhatsAppInteractiveAction
                {
                    CatalogId = request.CatalogId
                }
            }
        };

        if (isSingle)
        {
            var singleSku = request.Skus.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(singleSku))
            {
                var firstProd = _products.Values.FirstOrDefault();
                singleSku = firstProd?.Sku ?? "PRO-SAAS";
            }
            payload.Interactive.Action.ProductRetailerId = singleSku;
        }
        else
        {
            payload.Interactive.Header = new WhatsAppInteractiveHeader
            {
                Type = "text",
                Text = !string.IsNullOrWhiteSpace(request.HeaderText)
                    ? request.HeaderText
                    : "⚡ Featured Business Solutions"
            };

            var skusToInclude = request.Skus != null && request.Skus.Count > 0
                ? request.Skus
                : _products.Keys.ToList();

            var sectionTitle = !string.IsNullOrWhiteSpace(request.SectionTitle)
                ? request.SectionTitle
                : "Popular Products";

            var section = new WhatsAppProductSection
            {
                Title = sectionTitle,
                ProductItems = skusToInclude
                    .Where(sku => _products.ContainsKey(sku))
                    .Select(sku => new WhatsAppProductItem { ProductRetailerId = sku })
                    .ToList()
            };

            if (section.ProductItems.Count == 0 && _products.Count > 0)
            {
                section.ProductItems = _products.Keys.Select(k => new WhatsAppProductItem { ProductRetailerId = k }).ToList();
            }

            payload.Interactive.Action.Sections = new List<WhatsAppProductSection> { section };
        }

        return payload;
    }

    public string FormatInteractiveMessageJson(FormatInteractiveMessageRequest request)
    {
        var payload = FormatInteractiveMessage(request);
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    #endregion

    #region Cart Operations & State Machine

    public InChatCart GetCart(string customerPhone)
    {
        lock (_syncLock)
        {
            ReleaseExpiredReservationsInternal(DateTimeOffset.UtcNow);

            var cart = _carts.GetOrAdd(customerPhone, phone => new InChatCart
            {
                CustomerPhone = phone,
                State = CartState.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            return CloneCart(cart);
        }
    }

    public InChatCart UpdateCart(UpdateCartRequest request)
    {
        lock (_syncLock)
        {
            ReleaseExpiredReservationsInternal(DateTimeOffset.UtcNow);

            if (string.IsNullOrWhiteSpace(request.CustomerPhone))
                throw new ArgumentException("Customer phone is required", nameof(request));

            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new ArgumentException("SKU is required", nameof(request));

            if (!_products.TryGetValue(request.Sku, out var product))
                throw new KeyNotFoundException($"Product with SKU '{request.Sku}' not found in catalog.");

            var cart = _carts.GetOrAdd(request.CustomerPhone, phone => new InChatCart
            {
                CustomerPhone = phone,
                State = CartState.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            });

            // If cart is currently in CheckoutPending or OrderPlaced, updating it cancels or resets pending checkout
            if (cart.State == CartState.CheckoutPending && !string.IsNullOrEmpty(cart.ReservationId))
            {
                ReleaseReservationInternal(cart.ReservationId);
                cart.ReservationId = null;
                cart.ReservationExpiresAt = null;
                cart.OrderId = null;
            }

            var existingItem = cart.Items.FirstOrDefault(i => string.Equals(i.Sku, request.Sku, StringComparison.OrdinalIgnoreCase));
            var newQuantity = request.IsDelta
                ? (existingItem?.Quantity ?? 0) + request.Quantity
                : request.Quantity;

            if (newQuantity <= 0)
            {
                if (existingItem != null)
                {
                    cart.Items.Remove(existingItem);
                }
            }
            else
            {
                if (newQuantity > product.StockAvailable)
                {
                    throw new InvalidOperationException($"Cannot add {newQuantity} units of {product.Title} ({product.Sku}). Only {product.StockAvailable} units available in stock.");
                }

                if (existingItem != null)
                {
                    existingItem.Quantity = newQuantity;
                    existingItem.UnitPrice = product.UnitPrice;
                    existingItem.Title = product.Title;
                    existingItem.Currency = product.Currency;
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        Sku = product.Sku,
                        Title = product.Title,
                        Quantity = newQuantity,
                        UnitPrice = product.UnitPrice,
                        Currency = product.Currency,
                        ImageUrl = product.ImageUrl
                    });
                }
            }

            cart.UpdatedAt = DateTimeOffset.UtcNow;

            // Update State
            if (cart.Items.Count == 0)
            {
                cart.State = CartState.Empty;
                cart.AppliedCoupon = null;
                cart.DiscountAmount = 0m;
            }
            else
            {
                // Re-evaluate coupon if present
                if (cart.AppliedCoupon != null)
                {
                    var validation = ValidateCoupon(cart.AppliedCoupon.Code, cart.Subtotal);
                    if (validation.IsValid)
                    {
                        cart.DiscountAmount = validation.ComputedDiscount;
                        cart.AppliedCoupon.DiscountAmount = validation.ComputedDiscount;
                        cart.State = CartState.DiscountApplied;
                    }
                    else
                    {
                        cart.AppliedCoupon = null;
                        cart.DiscountAmount = 0m;
                        cart.State = CartState.Active;
                    }
                }
                else
                {
                    cart.State = CartState.Active;
                }
            }

            return CloneCart(cart);
        }
    }

    public InChatCart ClearCart(string customerPhone)
    {
        lock (_syncLock)
        {
            if (_carts.TryGetValue(customerPhone, out var cart))
            {
                if (cart.State == CartState.CheckoutPending && !string.IsNullOrEmpty(cart.ReservationId))
                {
                    ReleaseReservationInternal(cart.ReservationId);
                }

                cart.Items.Clear();
                cart.AppliedCoupon = null;
                cart.DiscountAmount = 0m;
                cart.ReservationId = null;
                cart.ReservationExpiresAt = null;
                cart.OrderId = null;
                cart.PaymentLink = null;
                cart.ConfirmationStanza = null;
                cart.State = CartState.Cancelled;
                cart.UpdatedAt = DateTimeOffset.UtcNow;

                return CloneCart(cart);
            }

            var empty = new InChatCart
            {
                CustomerPhone = customerPhone,
                State = CartState.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _carts[customerPhone] = empty;
            return CloneCart(empty);
        }
    }

    #endregion

    #region Coupon Engine

    public IEnumerable<CouponRule> GetCoupons()
    {
        return _coupons.Values.OrderBy(c => c.Code).ToList();
    }

    public CouponRule AddOrUpdateCoupon(CouponRule coupon)
    {
        if (string.IsNullOrWhiteSpace(coupon.Code))
            throw new ArgumentException("Coupon code is required", nameof(coupon));

        _coupons[coupon.Code] = coupon;
        return coupon;
    }

    public CouponValidationResult ValidateCoupon(string code, decimal subtotal)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new CouponValidationResult { IsValid = false, Message = "Coupon code is empty" };
        }

        if (!_coupons.TryGetValue(code.Trim(), out var rule) || !rule.IsActive)
        {
            return new CouponValidationResult { IsValid = false, Message = $"Coupon code '{code}' is invalid or inactive" };
        }

        if (rule.ExpiryDate.HasValue && rule.ExpiryDate.Value < DateTimeOffset.UtcNow)
        {
            return new CouponValidationResult { IsValid = false, Message = $"Coupon code '{rule.Code}' has expired" };
        }

        if (subtotal < rule.MinimumOrderValue)
        {
            return new CouponValidationResult
            {
                IsValid = false,
                Message = $"Order subtotal of {subtotal:C} does not meet the minimum order requirement of {rule.MinimumOrderValue:C} for coupon '{rule.Code}'",
                Coupon = rule
            };
        }

        decimal discount = 0m;
        if (rule.Type == DiscountType.Percentage)
        {
            discount = Math.Round(subtotal * (rule.Value / 100m), 2);
        }
        else if (rule.Type == DiscountType.Fixed)
        {
            discount = Math.Min(rule.Value, subtotal);
        }

        return new CouponValidationResult
        {
            IsValid = true,
            Coupon = rule,
            ComputedDiscount = discount,
            Message = $"Applied discount: {rule.Description}"
        };
    }

    public (bool Success, string Message, InChatCart Cart) ApplyCoupon(ApplyCouponRequest request)
    {
        lock (_syncLock)
        {
            ReleaseExpiredReservationsInternal(DateTimeOffset.UtcNow);

            var cart = _carts.GetOrAdd(request.CustomerPhone, phone => new InChatCart
            {
                CustomerPhone = phone,
                State = CartState.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            });

            if (cart.Items.Count == 0)
            {
                return (false, "Cannot apply coupon to an empty cart.", CloneCart(cart));
            }

            var validation = ValidateCoupon(request.CouponCode, cart.Subtotal);
            if (!validation.IsValid)
            {
                return (false, validation.Message ?? "Coupon code is invalid", CloneCart(cart));
            }

            cart.AppliedCoupon = new AppliedCoupon
            {
                Code = validation.Coupon!.Code,
                Type = validation.Coupon.Type,
                Value = validation.Coupon.Value,
                DiscountAmount = validation.ComputedDiscount,
                Description = validation.Coupon.Description
            };
            cart.DiscountAmount = validation.ComputedDiscount;
            cart.State = CartState.DiscountApplied;
            cart.UpdatedAt = DateTimeOffset.UtcNow;

            return (true, $"Coupon '{validation.Coupon.Code}' successfully applied! Discount: ${validation.ComputedDiscount:F2}", CloneCart(cart));
        }
    }

    #endregion

    #region Stock Reservation & Checkout

    public CheckoutResult ReserveCheckout(ReserveCheckoutRequest request)
    {
        lock (_syncLock)
        {
            ReleaseExpiredReservationsInternal(DateTimeOffset.UtcNow);

            if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            {
                return new CheckoutResult { Success = false, Message = "Customer phone number is required." };
            }

            if (!_carts.TryGetValue(request.CustomerPhone, out var cart) || cart.Items.Count == 0)
            {
                return new CheckoutResult { Success = false, Message = "Cannot checkout an empty cart." };
            }

            // Release any existing reservation for this cart before new lock
            if (!string.IsNullOrEmpty(cart.ReservationId))
            {
                ReleaseReservationInternal(cart.ReservationId);
                cart.ReservationId = null;
            }

            // Verify stock availability for all items
            foreach (var item in cart.Items)
            {
                if (!_products.TryGetValue(item.Sku, out var product) || product.StockAvailable < item.Quantity)
                {
                    var available = product?.StockAvailable ?? 0;
                    return new CheckoutResult
                    {
                        Success = false,
                        Message = $"Insufficient stock for {item.Title} ({item.Sku}). Requested: {item.Quantity}, Available: {available}."
                    };
                }
            }

            // Decrement available stock
            var reservedDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in cart.Items)
            {
                var product = _products[item.Sku];
                product.StockAvailable -= item.Quantity;
                reservedDict[item.Sku] = item.Quantity;
            }

            var ttl = request.TtlMinutes > 0 ? request.TtlMinutes : 15;
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(ttl);

            var reservation = new StockReservation
            {
                ReservationId = Guid.NewGuid().ToString("N"),
                CustomerPhone = request.CustomerPhone,
                ReservedItems = reservedDict,
                ReservedAt = DateTimeOffset.UtcNow,
                ExpiresAt = expiresAt,
                IsReleased = false,
                IsCommitted = false
            };

            _reservations[reservation.ReservationId] = reservation;

            var orderId = GenerateOrderNumber();
            var paymentLink = $"https://checkout.stripe.com/c/pay/cs_live_{Guid.NewGuid():N}?order={orderId}&phone={Uri.EscapeDataString(request.CustomerPhone)}";
            var invoiceStanza = GenerateInvoiceStanza(orderId, cart, request.PaymentMethod, expiresAt, request.ShippingAddress, paymentLink);

            cart.ReservationId = reservation.ReservationId;
            cart.ReservationExpiresAt = expiresAt;
            cart.OrderId = orderId;
            cart.PaymentLink = paymentLink;
            cart.ConfirmationStanza = invoiceStanza;
            cart.SelectedPaymentMethod = request.PaymentMethod;
            cart.ShippingAddress = request.ShippingAddress;
            cart.UpdatedAt = DateTimeOffset.UtcNow;

            if (request.AutoConfirm)
            {
                reservation.IsCommitted = true;
                cart.State = CartState.OrderPlaced;
            }
            else
            {
                cart.State = CartState.CheckoutPending;
            }

            return new CheckoutResult
            {
                Success = true,
                OrderId = orderId,
                PaymentLink = paymentLink,
                InvoiceStanza = invoiceStanza,
                ExpiresAt = expiresAt,
                Cart = CloneCart(cart),
                Message = request.AutoConfirm
                    ? $"Order {orderId} confirmed and placed successfully!"
                    : $"Stock reserved for {ttl} minutes. Order {orderId} ready for checkout."
            };
        }
    }

    public (bool Success, string Message, InChatCart Cart) ConfirmOrder(string customerPhone)
    {
        lock (_syncLock)
        {
            ReleaseExpiredReservationsInternal(DateTimeOffset.UtcNow);

            if (!_carts.TryGetValue(customerPhone, out var cart))
            {
                throw new KeyNotFoundException($"No cart found for phone {customerPhone}");
            }

            if (cart.State == CartState.OrderPlaced)
            {
                return (true, $"Order {cart.OrderId} is already placed.", CloneCart(cart));
            }

            if (cart.State != CartState.CheckoutPending || string.IsNullOrEmpty(cart.ReservationId))
            {
                return (false, "Cart must be in CheckoutPending state with an active stock reservation to confirm.", CloneCart(cart));
            }

            if (_reservations.TryGetValue(cart.ReservationId, out var res))
            {
                res.IsCommitted = true;
            }

            cart.State = CartState.OrderPlaced;
            cart.UpdatedAt = DateTimeOffset.UtcNow;

            return (true, $"Order {cart.OrderId} successfully placed!", CloneCart(cart));
        }
    }

    public int ReleaseExpiredReservations(DateTimeOffset? asOfUtc = null)
    {
        lock (_syncLock)
        {
            return ReleaseExpiredReservationsInternal(asOfUtc ?? DateTimeOffset.UtcNow);
        }
    }

    private int ReleaseExpiredReservationsInternal(DateTimeOffset now)
    {
        int releasedCount = 0;

        foreach (var res in _reservations.Values)
        {
            if (!res.IsReleased && !res.IsCommitted && res.ExpiresAt <= now)
            {
                ReleaseReservationInternal(res.ReservationId);
                releasedCount++;

                // If cart was tied to this reservation, mark it expired
                if (_carts.TryGetValue(res.CustomerPhone, out var cart) &&
                    string.Equals(cart.ReservationId, res.ReservationId, StringComparison.OrdinalIgnoreCase))
                {
                    cart.State = CartState.Expired;
                    cart.ReservationId = null;
                    cart.ReservationExpiresAt = null;
                    cart.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        return releasedCount;
    }

    private void ReleaseReservationInternal(string reservationId)
    {
        if (_reservations.TryGetValue(reservationId, out var res) && !res.IsReleased && !res.IsCommitted)
        {
            foreach (var kvp in res.ReservedItems)
            {
                if (_products.TryGetValue(kvp.Key, out var product))
                {
                    product.StockAvailable += kvp.Value;
                }
            }
            res.IsReleased = true;
        }
    }

    public string GenerateOrderNumber()
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randBytes = new byte[3];
        RandomNumberGenerator.Fill(randBytes);
        var hexPart = Convert.ToHexString(randBytes).ToUpperInvariant();
        return $"ORD-WA-{datePart}-{hexPart}";
    }

    private string GenerateInvoiceStanza(
        string orderId,
        InChatCart cart,
        PaymentMethod paymentMethod,
        DateTimeOffset expiresAt,
        string? shippingAddress,
        string paymentLink)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📦 *ORDER INVOICE & CONFIRMATION*");
        sb.AppendLine($"──────────────────────────────");
        sb.AppendLine($"🔖 *Order ID:* `{orderId}`");
        sb.AppendLine($"👤 *Customer Phone:* {cart.CustomerPhone}");
        sb.AppendLine($"💳 *Payment Method:* {(paymentMethod == PaymentMethod.Stripe ? "Instant Online Card (Stripe)" : "Cash on Delivery (COD)")}");
        if (!string.IsNullOrWhiteSpace(shippingAddress))
        {
            sb.AppendLine($"📍 *Shipping Address:* {shippingAddress}");
        }
        sb.AppendLine($"⏱️ *Stock Hold Expires:* {expiresAt:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine($"──────────────────────────────");
        sb.AppendLine($"*Cart Items:*");

        foreach (var item in cart.Items)
        {
            sb.AppendLine($"• {item.Quantity}x *{item.Title}* ({item.Sku}) - ${item.LineTotal:F2}");
        }

        sb.AppendLine($"──────────────────────────────");
        sb.AppendLine($"Subtotal: ${cart.Subtotal:F2}");
        if (cart.DiscountAmount > 0)
        {
            sb.AppendLine($"Discount ({cart.AppliedCoupon?.Code}): -${cart.DiscountAmount:F2}");
        }
        sb.AppendLine($"*Total Payable:* *${cart.TotalAmount:F2} {cart.Currency}*");
        sb.AppendLine($"──────────────────────────────");

        if (paymentMethod == PaymentMethod.Stripe)
        {
            sb.AppendLine($"🔗 *Click below to pay via Stripe Secure Checkout:*");
            sb.AppendLine($"{paymentLink}");
            sb.AppendLine($"_Please complete payment before your hold expires._");
        }
        else
        {
            sb.AppendLine($"🚚 *Cash On Delivery Selected.*");
            sb.AppendLine($"Please prepare exact cash upon delivery.");
            sb.AppendLine($"Reply *CONFIRM* to verify your phone and address immediately.");
        }

        return sb.ToString();
    }

    private static InChatCart CloneCart(InChatCart source)
    {
        return new InChatCart
        {
            CustomerPhone = source.CustomerPhone,
            State = source.State,
            Items = source.Items.Select(i => new CartItem
            {
                Sku = i.Sku,
                Title = i.Title,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Currency = i.Currency,
                ImageUrl = i.ImageUrl
            }).ToList(),
            DiscountAmount = source.DiscountAmount,
            AppliedCoupon = source.AppliedCoupon != null
                ? new AppliedCoupon
                {
                    Code = source.AppliedCoupon.Code,
                    Type = source.AppliedCoupon.Type,
                    Value = source.AppliedCoupon.Value,
                    DiscountAmount = source.AppliedCoupon.DiscountAmount,
                    Description = source.AppliedCoupon.Description
                }
                : null,
            ReservationId = source.ReservationId,
            ReservationExpiresAt = source.ReservationExpiresAt,
            OrderId = source.OrderId,
            PaymentLink = source.PaymentLink,
            ConfirmationStanza = source.ConfirmationStanza,
            ShippingAddress = source.ShippingAddress,
            SelectedPaymentMethod = source.SelectedPaymentMethod,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }

    #endregion
}

#endregion
