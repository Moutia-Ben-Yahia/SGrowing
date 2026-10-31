using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM;

public static class CrmEndpoints
{
    public static IEndpointRouteBuilder MapCrmEndpoints(this IEndpointRouteBuilder app)
    {
        // Contacts
        app.MapPost("/api/crm/contacts", async ([FromBody] CreateContactRequest req, CreateContactHandler handler) =>
        {
            var res = await handler.HandleAsync(req);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        app.MapGet("/api/crm/contacts/{id:guid}", async (Guid id, [FromQuery] Guid tenantId, GetContactHandler handler) =>
        {
            var res = await handler.HandleAsync(tenantId, id);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.NotFound(new { error = res.Error });
        });

        app.MapGet("/api/crm/contacts", async ([FromQuery] Guid tenantId, [FromQuery] string? query, SearchContactsHandler handler) =>
        {
            var res = await handler.HandleAsync(new SearchContactsQuery(tenantId, query ?? ""));
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        // Deals / Pipeline
        app.MapGet("/api/crm/deals", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var deals = await db.Deals.Where(d => d.TenantId == tenantId).OrderByDescending(d => d.CreatedAtUtc).ToListAsync();
            return Results.Ok(deals);
        });

        // Identity 360
        app.MapPost("/api/crm/resolve-contact", async ([FromQuery] Guid tenantId, [FromBody] ResolveContactDto req, IdentityResolverService resolver) =>
        {
            var res = await resolver.ResolveOrCreateContactAsync(tenantId, req.Email, req.Phone, req.FirstName, req.LastName);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        app.MapGet("/api/crm/contacts/{id:guid}/timeline", async (Guid id, [FromQuery] Guid tenantId, IdentityResolverService resolver) =>
        {
            var res = await resolver.GetUnifiedCustomerTimelineAsync(tenantId, id);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.NotFound(new { error = res.Error });
        });

        // B2B CRM Accounts & Companies
        app.MapGet("/api/crm/companies", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var companies = await db.Companies.Where(c => c.TenantId == tenantId).ToListAsync();
            return Results.Ok(companies);
        });

        app.MapPost("/api/crm/companies", async ([FromQuery] Guid tenantId, [FromBody] CreateCompanyDto req, CompanyAccountService companyService) =>
        {
            var company = await companyService.CreateCompanyAsync(tenantId, req.Name, req.Domain, req.Industry, req.Revenue, req.ParentCompanyId);
            return Results.Ok(company);
        });

        app.MapPost("/api/crm/companies/associate", async ([FromQuery] Guid tenantId, [FromBody] AssociateCompanyDto req, CompanyAccountService companyService) =>
        {
            var company = await companyService.AssociateContactByDomainAsync(tenantId, req.ContactId, req.ContactEmail);
            return company != null ? Results.Ok(company) : Results.NotFound(new { error = "No company found matching contact email domain." });
        });

        // B2B Lead Enrichment & In-Chat E-Signatures
        app.MapPost("/api/crm/leads/enrich", async ([FromBody] EnrichLeadDto req, LeadEnrichmentService enrichmentService) =>
        {
            var profile = await enrichmentService.EnrichLeadAsync(req.TenantId, req.DomainOrEmail);
            return Results.Ok(profile);
        });

        app.MapPost("/api/crm/contracts", async ([FromBody] CreateContractDto req, ContractSigningService contractService) =>
        {
            var contract = await contractService.CreateContractAsync(
                req.TenantId, req.Title, req.ContractType, req.SignerName, req.SignerEmail, req.ContractValue, req.CompanyId, req.DealId);
            return Results.Ok(contract);
        });

        app.MapPost("/api/crm/contracts/{id:guid}/dispatch", async (Guid id, [FromQuery] Guid tenantId, ContractSigningService contractService) =>
        {
            var dispatch = await contractService.DispatchInChatContractAsync(tenantId, id);
            return Results.Ok(dispatch);
        });

        app.MapGet("/api/crm/contracts/sign/{token}", async (string token, ContractSigningService contractService) =>
        {
            var contract = await contractService.GetAndMarkViewedAsync(token);
            return contract != null ? Results.Ok(contract) : Results.NotFound();
        });

        app.MapPost("/api/crm/contracts/sign/{token}", async (string token, [FromBody] SignContractDto req, ContractSigningService contractService) =>
        {
            var result = await contractService.CompleteSigningAsync(token, req.SignerSignatureName, req.SignedPdfUrl);
            return Results.Ok(result);
        });

        // Customer Identity Graph 360
        app.MapPost("/api/crm/customers/resolve", async ([FromBody] ResolveCustomerDto req, ICustomerIdentityGraphService idGraph) =>
        {
            var profile = await idGraph.ResolveCustomerAsync(req.TenantId, req.Name, req.Phone, req.Email, req.ChannelType, req.ChannelValue);
            return Results.Ok(profile);
        });

        app.MapGet("/api/crm/customers/{id:guid}/360", async (Guid id, [FromQuery] Guid tenantId, ICustomerIdentityGraphService idGraph) =>
        {
            var detail = await idGraph.GetCustomer360Async(tenantId, id);
            return Results.Ok(detail);
        });

        app.MapPost("/api/crm/customers/merge", async ([FromBody] MergeCustomersDto req, ICustomerIdentityGraphService idGraph) =>
        {
            var merged = await idGraph.MergeCustomerProfilesAsync(req.TenantId, req.PrimaryCustomerId, req.DuplicateCustomerId, req.PerformedBy ?? "Admin");
            return Results.Ok(merged);
        });

        return app;
    }
}
