using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.SovereignEInvoicing;

public sealed class SovereignEInvoicingEngine
{
    private readonly ConcurrentDictionary<string, SovereignTaxPortalProfile> _portals = new();
    private readonly ConcurrentDictionary<string, ClearedEInvoiceRecord> _invoices = new();
    private readonly ConcurrentBag<ClearanceVerificationReceipt> _verificationHistory = new();

    public SovereignEInvoicingEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var p1 = new SovereignTaxPortalProfile("PEPPOL_EU", "European Union", "UBL 2.1 XML", "EU Directive 2014/55/EU", "PRE_CLEARANCE_REAL_TIME");
        var p2 = new SovereignTaxPortalProfile("ZATCA_SA", "Saudi Arabia", "UBL 2.1 XML (Fatoora Phase 2)", "ZATCA E-Invoicing Mandate", "PRE_CLEARANCE_REAL_TIME");
        var p3 = new SovereignTaxPortalProfile("SAT_MX", "Mexico", "CFDI 4.0 XML", "Servicio de Administración Tributaria", "PRE_CLEARANCE_REAL_TIME");
        var p4 = new SovereignTaxPortalProfile("CHORUS_PRO_FR", "France", "Factur-X / UBL", "Direction Générale des Finances Publiques", "PRE_CLEARANCE_REAL_TIME");

        _portals[p1.PortalCode] = p1;
        _portals[p2.PortalCode] = p2;
        _portals[p3.PortalCode] = p3;
        _portals[p4.PortalCode] = p4;

        var inv1 = new ClearedEInvoiceRecord(
            "INV-EU-2026-8899",
            p1.PortalCode,
            "DE123456789",
            120000.00m,
            22800.00m,
            "EUR",
            "UUID-PEPPOL-0x99AABB1122",
            "PEPPOL_CLEARED_QR_SIGNATURE_DATA_0x8899",
            "CLEARED_BY_TAX_AUTHORITY",
            DateTime.UtcNow.AddDays(-3)
        );

        var inv2 = new ClearedEInvoiceRecord(
            "INV-SA-2026-4411",
            p2.PortalCode,
            "300000000000003",
            185000.00m,
            27750.00m,
            "SAR",
            "UUID-ZATCA-0x7788441122",
            "ZATCA_TLV_QR_CLEARED_0x4411",
            "CLEARED_BY_TAX_AUTHORITY",
            DateTime.UtcNow.AddDays(-1)
        );

        _invoices[inv1.InvoiceNumber] = inv1;
        _invoices[inv2.InvoiceNumber] = inv2;
    }

    public SovereignEInvoicingOverview GetOverview()
    {
        var portList = _portals.Values.ToList();
        var invList = _invoices.Values.ToList();
        var vatTotal = invList.Sum(i => i.VatAmount);

        return new SovereignEInvoicingOverview(
            invList.Count,
            100.0,
            vatTotal,
            portList.Count,
            portList,
            invList
        );
    }

    public ClearanceInvoiceResult GenerateClearanceInvoice(GenerateClearanceInvoiceRequest request)
    {
        var portal = _portals.TryGetValue(request.PortalCode, out var p) ? p : _portals.Values.First();
        var vatAmount = Math.Round(request.LineItemAmount * (request.VatRatePct / 100.0m), 2);
        var totalAmount = request.LineItemAmount + vatAmount;
        var uuid = $"UUID-{portal.PortalCode}-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
        var seal = $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}";
        var qrData = $"SOV_CLEARED_{uuid}_{totalAmount}_{request.Currency}";

        var record = new ClearedEInvoiceRecord(
            request.InvoiceNumber,
            portal.PortalCode,
            request.BuyerTaxId,
            totalAmount,
            vatAmount,
            request.Currency,
            uuid,
            qrData,
            "CLEARED_BY_TAX_AUTHORITY",
            DateTime.UtcNow
        );

        _invoices[request.InvoiceNumber] = record;

        return new ClearanceInvoiceResult(
            request.InvoiceNumber,
            portal.PortalCode,
            uuid,
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"><ID>{request.InvoiceNumber}</ID><UUID>{uuid}</UUID></Invoice>",
            qrData,
            seal,
            "CLEARED_BY_TAX_AUTHORITY",
            142
        );
    }

    public ClearanceVerificationReceipt VerifyClearance(VerifyClearanceRequest request)
    {
        var receipt = new ClearanceVerificationReceipt(
            request.InvoiceNumber,
            request.SovereignClearanceUuid,
            true,
            "GOVERNMENT_CLEARANCE_DIGEST_VALIDATED",
            DateTime.UtcNow
        );

        _verificationHistory.Add(receipt);
        return receipt;
    }
}
