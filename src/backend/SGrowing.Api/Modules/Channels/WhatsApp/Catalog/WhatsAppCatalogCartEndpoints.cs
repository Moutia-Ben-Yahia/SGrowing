using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Channels.WhatsApp.Catalog;

public record ConfirmOrderApiRequest(string CustomerPhone);

public static class WhatsAppCatalogCartEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppCatalogCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels/whatsapp/catalog")
            .WithTags("WhatsApp Interactive Catalog & In-Chat Cart Engine");

        // 1. GET /products
        group.MapGet("/products", (
            [FromQuery] string? category,
            [FromQuery] string? search,
            IWhatsAppCatalogCartService service) =>
        {
            var products = service.GetProducts(category, search);
            return Results.Ok(products);
        })
        .WithName("GetWhatsAppCatalogProducts")
        .WithSummary("Retrieve catalog products with optional category and search filters");

        // 2. POST /products
        group.MapPost("/products", (
            [FromBody] CatalogProduct product,
            IWhatsAppCatalogCartService service) =>
        {
            if (string.IsNullOrWhiteSpace(product.Sku) || string.IsNullOrWhiteSpace(product.Title))
            {
                return Results.BadRequest(new { error = "SKU and Title are required." });
            }

            var saved = service.AddOrUpdateProduct(product);
            return Results.Ok(saved);
        })
        .WithName("AddOrUpdateWhatsAppCatalogProduct")
        .WithSummary("Add or update a product in the thread-safe WhatsApp catalog");

        // 3. POST /format-interactive-message
        group.MapPost("/format-interactive-message", (
            [FromBody] FormatInteractiveMessageRequest request,
            IWhatsAppCatalogCartService service) =>
        {
            var payload = service.FormatInteractiveMessage(request);
            var json = service.FormatInteractiveMessageJson(request);

            return Results.Ok(new
            {
                Payload = payload,
                RawJson = json
            });
        })
        .WithName("FormatWhatsAppInteractiveMessage")
        .WithSummary("Produce valid Meta WhatsApp Interactive JSON stanzas for product_list or single_product");

        // 4. POST /cart/update
        group.MapPost("/cart/update", (
            [FromBody] UpdateCartRequest request,
            IWhatsAppCatalogCartService service) =>
        {
            try
            {
                var cart = service.UpdateCart(request);
                return Results.Ok(cart);
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { error = knf.Message });
            }
            catch (InvalidOperationException ioe)
            {
                return Results.BadRequest(new { error = ioe.Message });
            }
            catch (ArgumentException ae)
            {
                return Results.BadRequest(new { error = ae.Message });
            }
        })
        .WithName("UpdateWhatsAppInChatCart")
        .WithSummary("Add, remove, or adjust SKU quantities in customer's WhatsApp in-chat cart");

        // 5. POST /cart/apply-coupon
        group.MapPost("/cart/apply-coupon", (
            [FromBody] ApplyCouponRequest request,
            IWhatsAppCatalogCartService service) =>
        {
            var result = service.ApplyCoupon(request);
            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Message, cart = result.Cart });
            }

            return Results.Ok(new { message = result.Message, cart = result.Cart });
        })
        .WithName("ApplyCouponToWhatsAppCart")
        .WithSummary("Validate and apply coupon code (percentage or fixed discount) with min-order check");

        // 6. POST /cart/reserve-checkout
        group.MapPost("/cart/reserve-checkout", (
            [FromBody] ReserveCheckoutRequest request,
            IWhatsAppCatalogCartService service) =>
        {
            var result = service.ReserveCheckout(request);
            if (!result.Success)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        })
        .WithName("ReserveWhatsAppCartCheckout")
        .WithSummary("Lock inventory with TTL, generate order number and invoice stanza/payment link");

        // 7. GET /cart/{customerPhone}
        group.MapGet("/cart/{customerPhone}", (
            string customerPhone,
            IWhatsAppCatalogCartService service) =>
        {
            var cart = service.GetCart(customerPhone);
            return Results.Ok(cart);
        })
        .WithName("GetWhatsAppInChatCart")
        .WithSummary("Retrieve active in-chat cart state for a customer phone number");

        // 8. POST /cart/{customerPhone}/clear
        group.MapPost("/cart/{customerPhone}/clear", (
            string customerPhone,
            IWhatsAppCatalogCartService service) =>
        {
            var cart = service.ClearCart(customerPhone);
            return Results.Ok(cart);
        })
        .WithName("ClearWhatsAppInChatCart")
        .WithSummary("Clear customer cart and release any active stock reservations");

        // 9. POST /cart/confirm
        group.MapPost("/cart/confirm", (
            [FromBody] ConfirmOrderApiRequest request,
            IWhatsAppCatalogCartService service) =>
        {
            var result = service.ConfirmOrder(request.CustomerPhone);
            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Message, cart = result.Cart });
            }

            return Results.Ok(new { message = result.Message, cart = result.Cart });
        })
        .WithName("ConfirmWhatsAppCartOrder")
        .WithSummary("Transition cart state to OrderPlaced and commit inventory deduction");

        // 10. GET /coupons
        group.MapGet("/coupons", (IWhatsAppCatalogCartService service) =>
        {
            var coupons = service.GetCoupons();
            return Results.Ok(coupons);
        })
        .WithName("GetWhatsAppCatalogCoupons")
        .WithSummary("List all active discount coupon codes and rules");

        return app;
    }
}
