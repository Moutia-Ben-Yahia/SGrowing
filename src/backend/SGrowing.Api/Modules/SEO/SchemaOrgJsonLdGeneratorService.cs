using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SGrowing.Api.Modules.SEO;

public record GeoCoordinatesDto(
    double Latitude,
    double Longitude
);

public record PostalAddressDto(
    string StreetAddress,
    string AddressLocality,
    string? AddressRegion = null,
    string? PostalCode = null,
    string AddressCountry = "DZ"
);

public record LocalBusinessSchemaRequest(
    string Name,
    string Telephone,
    PostalAddressDto Address,
    string PriceRange = "$$",
    IReadOnlyList<string>? OpeningHours = null,
    GeoCoordinatesDto? GeoCoordinates = null,
    string? Url = null,
    string? Image = null,
    string? Description = null,
    string BusinessType = "LocalBusiness"
);

public record FaqItemDto(
    string Question,
    string Answer
);

public record FaqSchemaRequest(
    IReadOnlyList<FaqItemDto> Questions
);

public record AggregateRatingDto(
    double RatingValue,
    int ReviewCount,
    double BestRating = 5.0,
    double WorstRating = 1.0
);

public record ProductOfferDto(
    decimal Price,
    string PriceCurrency = "USD",
    string Availability = "https://schema.org/InStock",
    string? Url = null,
    DateTime? PriceValidUntilUtc = null
);

public record ProductSchemaRequest(
    string Name,
    string Description,
    ProductOfferDto Offer,
    AggregateRatingDto? AggregateRating = null,
    string? Sku = null,
    string? Brand = null,
    string? Image = null,
    string? Category = null
);

public record BreadcrumbItemDto(
    int Position,
    string Name,
    string ItemUrl
);

public record BreadcrumbSchemaRequest(
    IReadOnlyList<BreadcrumbItemDto> Items
);

public record SchemaValidationResult(
    bool IsValid,
    string SchemaType,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string JsonLd
);

public interface ISchemaOrgJsonLdGeneratorService
{
    string GenerateLocalBusinessSchema(LocalBusinessSchemaRequest request);
    string GenerateFaqSchema(FaqSchemaRequest request);
    string GenerateProductSchema(ProductSchemaRequest request);
    string GenerateBreadcrumbSchema(BreadcrumbSchemaRequest request);
    SchemaValidationResult ValidateJsonLd(string jsonLd, string expectedSchemaType);
}

public class SchemaOrgJsonLdGeneratorService : ISchemaOrgJsonLdGeneratorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string GenerateLocalBusinessSchema(LocalBusinessSchemaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var node = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = string.IsNullOrWhiteSpace(request.BusinessType) ? "LocalBusiness" : request.BusinessType,
            ["name"] = request.Name,
            ["telephone"] = request.Telephone,
            ["priceRange"] = request.PriceRange ?? "$$"
        };

        if (!string.IsNullOrWhiteSpace(request.Url))
            node["url"] = request.Url;

        if (!string.IsNullOrWhiteSpace(request.Image))
            node["image"] = request.Image;

        if (!string.IsNullOrWhiteSpace(request.Description))
            node["description"] = request.Description;

        // Address
        if (request.Address is not null)
        {
            var addressNode = new JsonObject
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = request.Address.StreetAddress,
                ["addressLocality"] = request.Address.AddressLocality,
                ["addressCountry"] = request.Address.AddressCountry
            };

            if (!string.IsNullOrWhiteSpace(request.Address.AddressRegion))
                addressNode["addressRegion"] = request.Address.AddressRegion;

            if (!string.IsNullOrWhiteSpace(request.Address.PostalCode))
                addressNode["postalCode"] = request.Address.PostalCode;

            node["address"] = addressNode;
        }

        // Geo coordinates
        if (request.GeoCoordinates is not null)
        {
            node["geo"] = new JsonObject
            {
                ["@type"] = "GeoCoordinates",
                ["latitude"] = request.GeoCoordinates.Latitude,
                ["longitude"] = request.GeoCoordinates.Longitude
            };
        }

        // Opening hours
        if (request.OpeningHours is { Count: > 0 })
        {
            var hoursArray = new JsonArray();
            foreach (var h in request.OpeningHours)
            {
                hoursArray.Add(h);
            }
            node["openingHours"] = hoursArray;
        }

        return node.ToJsonString(JsonOptions);
    }

    public string GenerateFaqSchema(FaqSchemaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var questionsArray = new JsonArray();
        if (request.Questions is not null)
        {
            foreach (var q in request.Questions)
            {
                questionsArray.Add(new JsonObject
                {
                    ["@type"] = "Question",
                    ["name"] = q.Question,
                    ["acceptedAnswer"] = new JsonObject
                    {
                        ["@type"] = "Answer",
                        ["text"] = q.Answer
                    }
                });
            }
        }

        var node = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = questionsArray
        };

        return node.ToJsonString(JsonOptions);
    }

    public string GenerateProductSchema(ProductSchemaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var node = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = request.Name,
            ["description"] = request.Description
        };

        if (!string.IsNullOrWhiteSpace(request.Sku))
            node["sku"] = request.Sku;

        if (!string.IsNullOrWhiteSpace(request.Image))
            node["image"] = request.Image;

        if (!string.IsNullOrWhiteSpace(request.Category))
            node["category"] = request.Category;

        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            node["brand"] = new JsonObject
            {
                ["@type"] = "Brand",
                ["name"] = request.Brand
            };
        }

        // Offer
        if (request.Offer is not null)
        {
            var offerNode = new JsonObject
            {
                ["@type"] = "Offer",
                ["price"] = request.Offer.Price,
                ["priceCurrency"] = request.Offer.PriceCurrency ?? "USD",
                ["availability"] = NormalizeAvailability(request.Offer.Availability)
            };

            if (!string.IsNullOrWhiteSpace(request.Offer.Url))
                offerNode["url"] = request.Offer.Url;

            if (request.Offer.PriceValidUntilUtc.HasValue)
                offerNode["priceValidUntil"] = request.Offer.PriceValidUntilUtc.Value.ToString("yyyy-MM-dd");

            node["offers"] = offerNode;
        }

        // Aggregate Rating
        if (request.AggregateRating is not null)
        {
            node["aggregateRating"] = new JsonObject
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = request.AggregateRating.RatingValue,
                ["reviewCount"] = request.AggregateRating.ReviewCount,
                ["bestRating"] = request.AggregateRating.BestRating,
                ["worstRating"] = request.AggregateRating.WorstRating
            };
        }

        return node.ToJsonString(JsonOptions);
    }

    public string GenerateBreadcrumbSchema(BreadcrumbSchemaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var itemsArray = new JsonArray();
        if (request.Items is not null)
        {
            var ordered = request.Items.OrderBy(i => i.Position).ToList();
            foreach (var item in ordered)
            {
                itemsArray.Add(new JsonObject
                {
                    ["@type"] = "ListItem",
                    ["position"] = item.Position,
                    ["name"] = item.Name,
                    ["item"] = item.ItemUrl
                });
            }
        }

        var node = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = itemsArray
        };

        return node.ToJsonString(JsonOptions);
    }

    public SchemaValidationResult ValidateJsonLd(string jsonLd, string expectedSchemaType)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(jsonLd))
        {
            return new SchemaValidationResult(
                IsValid: false,
                SchemaType: expectedSchemaType,
                Errors: ["JSON-LD content is null or empty."],
                Warnings: warnings,
                JsonLd: string.Empty
            );
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(jsonLd);
            if (root is null)
            {
                return new SchemaValidationResult(
                    IsValid: false,
                    SchemaType: expectedSchemaType,
                    Errors: ["JSON root was null."],
                    Warnings: warnings,
                    JsonLd: jsonLd
                );
            }
        }
        catch (JsonException ex)
        {
            return new SchemaValidationResult(
                IsValid: false,
                SchemaType: expectedSchemaType,
                Errors: [$"Malformed JSON-LD syntax: {ex.Message}"],
                Warnings: warnings,
                JsonLd: jsonLd
            );
        }

        // Verify @context
        var context = root["@context"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(context) || !context.Contains("schema.org", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Missing or invalid '@context'. Expected 'https://schema.org'.");
        }

        // Verify @type
        var type = root["@type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type))
        {
            errors.Add("Missing '@type' in JSON-LD root.");
        }
        else if (!string.Equals(type, expectedSchemaType, StringComparison.OrdinalIgnoreCase))
        {
            // Allow sub-types for LocalBusiness
            var isLocalBusinessSubtype = string.Equals(expectedSchemaType, "LocalBusiness", StringComparison.OrdinalIgnoreCase)
                && (type.EndsWith("Business", StringComparison.OrdinalIgnoreCase) || type.EndsWith("Service", StringComparison.OrdinalIgnoreCase) || type == "Store" || type == "Restaurant");

            if (!isLocalBusinessSubtype)
            {
                warnings.Add($"Root '@type' '{type}' does not strictly match expected type '{expectedSchemaType}'.");
            }
        }

        // Type-specific Google Rich Snippets validation
        var normalizedType = (type ?? expectedSchemaType).ToLowerInvariant();
        if (normalizedType.Contains("business") || normalizedType.Contains("service") || normalizedType.Contains("store"))
        {
            ValidateLocalBusinessRequirements(root, errors, warnings);
        }
        else if (normalizedType.Contains("faq"))
        {
            ValidateFaqRequirements(root, errors, warnings);
        }
        else if (normalizedType.Contains("product"))
        {
            ValidateProductRequirements(root, errors, warnings);
        }
        else if (normalizedType.Contains("breadcrumb"))
        {
            ValidateBreadcrumbRequirements(root, errors, warnings);
        }

        var isValid = errors.Count == 0;
        return new SchemaValidationResult(
            IsValid: isValid,
            SchemaType: type ?? expectedSchemaType,
            Errors: errors,
            Warnings: warnings,
            JsonLd: jsonLd
        );
    }

    private static void ValidateLocalBusinessRequirements(JsonNode root, List<string> errors, List<string> warnings)
    {
        var name = root["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("Google Rich Snippets requires 'name' for LocalBusiness.");

        var phone = root["telephone"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(phone))
            warnings.Add("LocalBusiness telephone is missing or empty.");

        var address = root["address"];
        if (address is null)
        {
            errors.Add("Google Rich Snippets requires 'address' for LocalBusiness.");
        }
        else
        {
            var locality = address["addressLocality"]?.GetValue<string>();
            var street = address["streetAddress"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(locality) && string.IsNullOrWhiteSpace(street))
                errors.Add("Address must include at least 'addressLocality' or 'streetAddress'.");
        }
    }

    private static void ValidateFaqRequirements(JsonNode root, List<string> errors, List<string> warnings)
    {
        var mainEntity = root["mainEntity"] as JsonArray;
        if (mainEntity is null || mainEntity.Count == 0)
        {
            errors.Add("Google FAQ rich snippets require a 'mainEntity' array with at least one Question.");
            return;
        }

        for (int i = 0; i < mainEntity.Count; i++)
        {
            var q = mainEntity[i];
            var qName = q?["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(qName))
            {
                errors.Add($"Question at index {i} has an empty 'name'.");
            }

            var answer = q?["acceptedAnswer"];
            var aText = answer?["text"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(aText))
            {
                errors.Add($"Question at index {i} is missing acceptedAnswer text.");
            }
        }
    }

    private static void ValidateProductRequirements(JsonNode root, List<string> errors, List<string> warnings)
    {
        var name = root["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("Product requires a 'name'.");

        var offers = root["offers"];
        if (offers is null)
        {
            warnings.Add("Product is missing 'offers'. Either 'offers', 'review', or 'aggregateRating' should be specified.");
        }
        else
        {
            var price = offers["price"];
            if (price is null)
                errors.Add("Product offer requires 'price'.");

            var currency = offers["priceCurrency"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(currency))
                errors.Add("Product offer requires 'priceCurrency'.");

            var availability = offers["availability"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(availability))
                warnings.Add("Product offer should specify 'availability'.");
        }
    }

    private static void ValidateBreadcrumbRequirements(JsonNode root, List<string> errors, List<string> warnings)
    {
        var elements = root["itemListElement"] as JsonArray;
        if (elements is null || elements.Count == 0)
        {
            errors.Add("BreadcrumbList requires 'itemListElement' array with at least one item.");
            return;
        }

        for (int i = 0; i < elements.Count; i++)
        {
            var elem = elements[i];
            var pos = elem?["position"]?.GetValue<int>() ?? 0;
            if (pos != (i + 1))
            {
                warnings.Add($"Breadcrumb position at index {i} is {pos}, expected sequential 1-based index {i + 1}.");
            }

            var name = elem?["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
                errors.Add($"Breadcrumb item at position {pos} is missing 'name'.");

            var itemUrl = elem?["item"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(itemUrl))
                errors.Add($"Breadcrumb item at position {pos} is missing 'item' URL.");
        }
    }

    private static string NormalizeAvailability(string? availability)
    {
        if (string.IsNullOrWhiteSpace(availability))
            return "https://schema.org/InStock";

        if (availability.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            availability.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return availability;

        return $"https://schema.org/{availability.Trim()}";
    }
}
