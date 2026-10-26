using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public record CheckoutOrderConfirmationDto(
    string OrderNumber,
    string SessionKey,
    string CustomerName,
    string CustomerPhone,
    string FullDeliveryAddress,
    decimal TotalAmountDueOnDoorstep,
    int EstimatedDeliveryDays,
    string Status
);

public interface ICodCheckoutService
{
    Task<CodCheckoutSession> CreateSessionAsync(Guid tenantId, string cartItemsJson, decimal subtotal);
    Task<List<WilayaZone>> GetWilayasAsync(string countryCode = "TN");
    Task<CodCheckoutSession> UpdateCustomerDetailsAsync(
        Guid tenantId,
        string sessionKey,
        string customerName,
        string customerPhone,
        string wilayaCode,
        string delegationName,
        string streetAddress);
    Task<string> SendPhoneOtpAsync(Guid tenantId, string sessionKey);
    Task<bool> VerifyPhoneOtpAsync(Guid tenantId, string sessionKey, string otpInput);
    Task<CodCheckoutSession> ApplyUpsellOfferAsync(Guid tenantId, string sessionKey, string upsellSku, decimal upsellAmount);
    Task<CheckoutOrderConfirmationDto> CompleteCodOrderAsync(Guid tenantId, string sessionKey);
}

public class CodCheckoutService : ICodCheckoutService
{
    private readonly SGrowingDbContext _db;

    public CodCheckoutService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CodCheckoutSession> CreateSessionAsync(Guid tenantId, string cartItemsJson, decimal subtotal)
    {
        var session = CodCheckoutSession.Create(tenantId, cartItemsJson, subtotal);
        _db.CodCheckoutSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<List<WilayaZone>> GetWilayasAsync(string countryCode = "TN")
    {
        var list = await _db.WilayaZones
            .Where(w => w.CountryCode == countryCode.ToUpper().Trim() && w.IsActive)
            .OrderBy(w => w.NameFr)
            .ToListAsync();

        if (list.Count == 0 && countryCode.ToUpper().Trim() == "TN")
        {
            // Seed standard Tunisian governorates
            var defaultZones = new List<WilayaZone>
            {
                WilayaZone.Create("TN-11", "تونس", "Tunis", "TN", 7m, 1, new() { "Tunis Ville", "La Marsa", "Carthage", "Le Bardo", "El Menzah" }),
                WilayaZone.Create("TN-12", "أريانة", "Ariana", "TN", 7m, 1, new() { "Ariana Ville", "Ennasr", "Soukra", "Raoued" }),
                WilayaZone.Create("TN-13", "بن عروس", "Ben Arous", "TN", 7m, 1, new() { "Megrine", "Rades", "Hammam Lif", "Mornag" }),
                WilayaZone.Create("TN-31", "سوسة", "Sousse", "TN", 8m, 2, new() { "Sousse Ville", "Kantaoui", "Hammam Sousse", "Msaken" }),
                WilayaZone.Create("TN-41", "صفاقس", "Sfax", "TN", 8m, 2, new() { "Sfax Ville", "Sakiet Ezzit", "Sakiet Eddaier", "Thyna" })
            };
            _db.WilayaZones.AddRange(defaultZones);
            await _db.SaveChangesAsync();
            return defaultZones;
        }

        return list;
    }

    public async Task<CodCheckoutSession> UpdateCustomerDetailsAsync(
        Guid tenantId,
        string sessionKey,
        string customerName,
        string customerPhone,
        string wilayaCode,
        string delegationName,
        string streetAddress)
    {
        var session = await _db.CodCheckoutSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionKey == sessionKey);

        if (session == null) throw new InvalidOperationException($"Checkout session {sessionKey} not found");

        var wilaya = await _db.WilayaZones.FirstOrDefaultAsync(w => w.Code == wilayaCode.ToUpper().Trim());

        session.CustomerName = customerName.Trim();
        session.CustomerPhone = customerPhone.Trim();
        session.WilayaCode = wilayaCode.Trim().ToUpperInvariant();
        session.WilayaName = wilaya?.NameFr ?? wilayaCode;
        session.DelegationName = delegationName.Trim();
        session.StreetAddress = streetAddress.Trim();
        if (wilaya != null)
        {
            session.ShippingFee = wilaya.StandardShippingFee;
        }

        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<string> SendPhoneOtpAsync(Guid tenantId, string sessionKey)
    {
        var session = await _db.CodCheckoutSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionKey == sessionKey);

        if (session == null) throw new InvalidOperationException($"Checkout session {sessionKey} not found");

        // Generate deterministic or random 4-digit OTP
        var otp = $"{Random.Shared.Next(1000, 9999)}";
        session.OtpCode = otp;
        session.Status = CheckoutSessionStatus.OtpSent;

        await _db.SaveChangesAsync();
        return otp;
    }

    public async Task<bool> VerifyPhoneOtpAsync(Guid tenantId, string sessionKey, string otpInput)
    {
        var session = await _db.CodCheckoutSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionKey == sessionKey);

        if (session == null) return false;

        if (session.OtpCode == otpInput.Trim())
        {
            session.IsOtpVerified = true;
            session.Status = CheckoutSessionStatus.OtpVerified;
            await _db.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<CodCheckoutSession> ApplyUpsellOfferAsync(Guid tenantId, string sessionKey, string upsellSku, decimal upsellAmount)
    {
        var session = await _db.CodCheckoutSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionKey == sessionKey);

        if (session == null) throw new InvalidOperationException($"Checkout session {sessionKey} not found");

        session.UpsellOfferAcceptedSku = upsellSku.Trim();
        session.UpsellAmount = upsellAmount;
        session.Subtotal += upsellAmount;

        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<CheckoutOrderConfirmationDto> CompleteCodOrderAsync(Guid tenantId, string sessionKey)
    {
        var session = await _db.CodCheckoutSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionKey == sessionKey);

        if (session == null) throw new InvalidOperationException($"Checkout session {sessionKey} not found");

        var orderNumber = $"COD-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
        session.PlacedOrderId = orderNumber;
        session.Status = CheckoutSessionStatus.OrderPlaced;
        session.CompletedAtUtc = DateTime.UtcNow;

        SGrowingTelemetry.OrdersPlacedCounter.Add(1);
        await _db.SaveChangesAsync();

        var wilaya = await _db.WilayaZones.FirstOrDefaultAsync(w => w.Code == session.WilayaCode);
        int estDays = wilaya?.EstimatedDeliveryDays ?? 2;

        return new CheckoutOrderConfirmationDto(
            orderNumber,
            session.SessionKey,
            session.CustomerName,
            session.CustomerPhone,
            $"{session.StreetAddress}, {session.DelegationName}, {session.WilayaName}",
            session.TotalAmount,
            estDays,
            "Confirmed"
        );
    }
}
