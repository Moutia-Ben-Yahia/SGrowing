using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface IConversationalCartEngine
{
    InChatCartIntent ParseCartIntent(string messageText, decimal defaultUnitPrice = 65.0m, string currency = "TND");
    Task<InChatOrderResult> CreateInChatOrderAsync(Guid tenantId, Guid conversationId, InChatCartIntent intent, Guid? warehouseId = null);
}

public record InChatOrderResult(CodOrder Order, InChatCartSession Session, string InteractiveReplyPayload);

public class ConversationalCartEngine : IConversationalCartEngine
{
    private readonly SGrowingDbContext _dbContext;
    private readonly IDefensiveWmsService _wmsService;

    private static readonly string[] KnownGovernorates = new[]
    {
        // Tunisia
        "Tunis", "Ariana", "Ben Arous", "Manouba", "Nabeul", "Zaghouan", "Bizerte", "Beja", "Jendouba",
        "Kef", "Siliana", "Kairouan", "Kasserine", "Sidi Bouzid", "Sousse", "Monastir", "Mahdia",
        "Sfax", "Gafsa", "Tozeur", "Kebili", "Gabes", "Medenine", "Tataouine",
        // Egypt
        "Cairo", "Giza", "Alexandria", "Mansoura",
        // Algeria
        "Alger", "Oran", "Constantine", "Annaba"
    };

    public ConversationalCartEngine(SGrowingDbContext dbContext, IDefensiveWmsService wmsService)
    {
        _dbContext = dbContext;
        _wmsService = wmsService;
    }

    public InChatCartIntent ParseCartIntent(string messageText, decimal defaultUnitPrice = 65.0m, string currency = "TND")
    {
        var intent = new InChatCartIntent
        {
            UnitPrice = defaultUnitPrice,
            Currency = currency,
            Quantity = 1,
            EstimatedShippingFee = 7.0m
        };

        // 1. Extract Quantity
        var qtyMatch = Regex.Match(messageText, @"(\b[1-9]\b|\b[1-9][0-9]\b)\s*(pieces|pcs|bouteilles|articles|unités|units|packs|flacons)?", RegexOptions.IgnoreCase);
        if (qtyMatch.Success && int.TryParse(qtyMatch.Groups[1].Value, out int qty))
        {
            intent.Quantity = Math.Max(1, qty);
        }

        // 2. Extract Phone Number
        var phoneMatch = Regex.Match(messageText, @"(\+?\d{8,14})");
        if (phoneMatch.Success)
        {
            intent.PhoneNumber = phoneMatch.Groups[1].Value.Trim();
        }

        // 3. Extract Governorate
        foreach (var gov in KnownGovernorates)
        {
            if (messageText.Contains(gov, StringComparison.OrdinalIgnoreCase))
            {
                intent.Governorate = gov;
                if (gov is "Tataouine" or "Kebili" or "Tozeur" or "Medenine" or "Gafsa")
                {
                    intent.EstimatedShippingFee = 8.0m; // Southern tier surcharge
                }
                break;
            }
        }

        // 4. Extract Customer Name if prefixed
        var nameMatch = Regex.Match(messageText, @"(?:je m'appelle|mon nom est|name is|nom:)\s*([A-Za-zÀ-ÿ\s]{3,30})", RegexOptions.IgnoreCase);
        if (nameMatch.Success)
        {
            intent.CustomerName = nameMatch.Groups[1].Value.Trim();
        }
        else
        {
            intent.CustomerName = "Valued Customer";
        }

        // 5. Product name heuristic
        var productMatch = Regex.Match(messageText, @"(?:pour|acheter|want|commander|order)\s+([A-Za-z0-9\s\-]{3,25})", RegexOptions.IgnoreCase);
        if (productMatch.Success)
        {
            intent.ProductName = productMatch.Groups[1].Value.Trim();
        }
        else
        {
            intent.ProductName = "Custom Fragrance Selection";
        }

        intent.VariantSku = Regex.Replace(intent.ProductName.ToUpperInvariant(), @"[^A-Z0-9]", "-");
        if (string.IsNullOrWhiteSpace(intent.VariantSku))
        {
            intent.VariantSku = "SKU-DEFAULT";
        }

        return intent;
    }

    public async Task<InChatOrderResult> CreateInChatOrderAsync(Guid tenantId, Guid conversationId, InChatCartIntent intent, Guid? warehouseId = null)
    {
        var randomSuffix = Random.Shared.Next(1000, 9999);
        var orderNumber = $"CHAT-{DateTime.UtcNow:yyMMdd}-{randomSuffix}";

        var order = CodOrder.Create(
            tenantId,
            orderNumber,
            intent.CustomerName ?? "Chat Customer",
            intent.PhoneNumber ?? "+21600000000",
            intent.DelegationOrAddress ?? "Address noted in chat",
            intent.Governorate ?? "Tunis",
            "Centre",
            intent.Quantity * intent.UnitPrice,
            countryCode: "TN",
            codCurrency: intent.Currency,
            shippingFee: intent.EstimatedShippingFee,
            itemsJson: JsonSerializer.Serialize(new[]
            {
                new { name = intent.ProductName, sku = intent.VariantSku, quantity = intent.Quantity, price = intent.UnitPrice }
            }),
            notes: "Created via In-Chat Conversational Cart Builder");

        _dbContext.CodOrders.Add(order);

        // Reserve stock in WMS if warehouse available
        if (warehouseId.HasValue && !string.IsNullOrWhiteSpace(intent.VariantSku))
        {
            await _wmsService.ReserveStockOnCheckoutAsync(tenantId, warehouseId.Value, intent.VariantSku, intent.Quantity, order.Id, "InChatBot");
        }

        var session = new InChatCartSession
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            CustomerPhone = intent.PhoneNumber ?? "",
            CustomerName = intent.CustomerName ?? "",
            DraftOrderId = order.Id,
            Status = InChatCartStatus.AwaitingCustomerConfirmation,
            CartItemsJson = order.ItemsJson,
            TotalAmount = intent.TotalAmount,
            Currency = intent.Currency,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.InChatCartSessions.Add(session);

        await _dbContext.SaveChangesAsync();

        var interactiveReplyPayload = JsonSerializer.Serialize(new
        {
            type = "order_summary_card",
            order_number = order.OrderNumber,
            customer = intent.CustomerName,
            product = $"{intent.ProductName} (x{intent.Quantity})",
            sku = intent.VariantSku,
            subtotal = intent.Quantity * intent.UnitPrice,
            shipping = intent.EstimatedShippingFee,
            total = intent.TotalAmount,
            currency = intent.Currency,
            destination = intent.Governorate,
            action_buttons = new[]
            {
                new { id = $"CONFIRM_{order.Id}", text = "✅ Confirm Order Now" },
                new { id = $"MODIFY_{order.Id}", text = "✏️ Change Address" }
            }
        });

        return new InChatOrderResult(order, session, interactiveReplyPayload);
    }
}
