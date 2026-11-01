using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public record FlowValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, object?> ExtractedData,
    string? NextAction);

public record InteractiveListRow(
    string Id,
    string Title,
    string? Description = null);

public record InteractiveListSection(
    string Title,
    IReadOnlyList<InteractiveListRow> Rows);

public record InteractiveListMenuRequest(
    string RecipientPhone,
    string Title,
    string Body,
    string ButtonText,
    IReadOnlyList<InteractiveListSection> Sections,
    string? Header = null,
    string? Footer = null);

public record QuickReplyButton(
    string Id,
    string Title);

public record QuickReplyButtonsRequest(
    string RecipientPhone,
    string Body,
    IReadOnlyList<QuickReplyButton> Buttons,
    string? Header = null,
    string? Footer = null);

public record CatalogItemDto(
    string RetailerId,
    string Title,
    decimal Price,
    string Currency = "TND",
    string? Description = null,
    int Quantity = 1);

public record CatalogSectionDto(
    string Title,
    IReadOnlyList<CatalogItemDto> Items);

public record CatalogCartRenderRequest(
    string RecipientPhone,
    string CatalogId,
    string HeaderText,
    string BodyText,
    IReadOnlyList<CatalogSectionDto> Sections,
    string? FooterText = null);

public interface IWhatsAppFlowsAndCatalogService
{
    string GenerateAddressCollectionFlowSchema();
    string GenerateAppointmentBookingFlowSchema();
    string GenerateProductVariantSelectionFlowSchema();
    FlowValidationResult ValidateFlowResponsePayload(string flowType, JsonElement payload);
    FlowValidationResult ValidateFlowResponsePayload(string flowType, string rawJsonPayload);
    string RenderInteractiveListMenu(InteractiveListMenuRequest request);
    string RenderDynamicQuickReplies(QuickReplyButtonsRequest request);
    string RenderNativeCatalogCart(CatalogCartRenderRequest request);
}

public class WhatsAppFlowsAndCatalogService : IWhatsAppFlowsAndCatalogService
{
    private readonly ILogger<WhatsAppFlowsAndCatalogService>? _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public WhatsAppFlowsAndCatalogService(ILogger<WhatsAppFlowsAndCatalogService>? logger = null)
    {
        _logger = logger;
    }

    public string GenerateAddressCollectionFlowSchema()
    {
        var schema = new JsonObject
        {
            ["version"] = "3.0",
            ["data_api_version"] = "3.0",
            ["routing_model"] = new JsonObject
            {
                ["ADDRESS_COLLECTION_SCREEN"] = new JsonArray()
            },
            ["screens"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "ADDRESS_COLLECTION_SCREEN",
                    ["title"] = "Shipping & Doorstep Address",
                    ["terminal"] = true,
                    ["data"] = new JsonObject(),
                    ["layout"] = new JsonObject
                    {
                        ["type"] = "SingleColumnLayout",
                        ["children"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "TextHeading",
                                ["text"] = "Enter Your Delivery Details"
                            },
                            new JsonObject
                            {
                                ["type"] = "TextSubheading",
                                ["text"] = "Please confirm the address for doorstep cash-on-delivery courier dispatch."
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "full_name",
                                ["label"] = "Full Name",
                                ["required"] = true,
                                ["input-type"] = "text"
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "phone_number",
                                ["label"] = "Mobile Phone (e.g., 98123456)",
                                ["required"] = true,
                                ["input-type"] = "phone"
                            },
                            new JsonObject
                            {
                                ["type"] = "Dropdown",
                                ["name"] = "governorate",
                                ["label"] = "Governorate / Region",
                                ["required"] = true,
                                ["data-source"] = new JsonArray
                                {
                                    new JsonObject { ["id"] = "tunis", ["title"] = "Tunis" },
                                    new JsonObject { ["id"] = "ariana", ["title"] = "Ariana" },
                                    new JsonObject { ["id"] = "ben_arous", ["title"] = "Ben Arous" },
                                    new JsonObject { ["id"] = "manouba", ["title"] = "Manouba" },
                                    new JsonObject { ["id"] = "sousse", ["title"] = "Sousse" },
                                    new JsonObject { ["id"] = "sfax", ["title"] = "Sfax" },
                                    new JsonObject { ["id"] = "nabeul", ["title"] = "Nabeul" },
                                    new JsonObject { ["id"] = "bizerte", ["title"] = "Bizerte" },
                                    new JsonObject { ["id"] = "monastir", ["title"] = "Monastir" }
                                }
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "city",
                                ["label"] = "City / Municipality",
                                ["required"] = true,
                                ["input-type"] = "text"
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "street_address",
                                ["label"] = "Street Address & Building / Villa #",
                                ["required"] = true,
                                ["input-type"] = "text"
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "postal_code",
                                ["label"] = "Postal Code (Optional)",
                                ["required"] = false,
                                ["input-type"] = "number"
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "delivery_notes",
                                ["label"] = "Courier Instructions / Landmark",
                                ["required"] = false,
                                ["input-type"] = "text"
                            },
                            new JsonObject
                            {
                                ["type"] = "Footer",
                                ["label"] = "Confirm Address",
                                ["on-click-action"] = new JsonObject
                                {
                                    ["name"] = "complete",
                                    ["payload"] = new JsonObject
                                    {
                                        ["action"] = "address_submitted"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return schema.ToJsonString(JsonOptions);
    }

    public string GenerateAppointmentBookingFlowSchema()
    {
        var schema = new JsonObject
        {
            ["version"] = "3.0",
            ["data_api_version"] = "3.0",
            ["routing_model"] = new JsonObject
            {
                ["APPOINTMENT_BOOKING_SCREEN"] = new JsonArray()
            },
            ["screens"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "APPOINTMENT_BOOKING_SCREEN",
                    ["title"] = "Book Consultation or Support Slot",
                    ["terminal"] = true,
                    ["data"] = new JsonObject(),
                    ["layout"] = new JsonObject
                    {
                        ["type"] = "SingleColumnLayout",
                        ["children"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "TextHeading",
                                ["text"] = "Reserve Your Appointment"
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "customer_name",
                                ["label"] = "Your Name",
                                ["required"] = true,
                                ["input-type"] = "text"
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "customer_phone",
                                ["label"] = "Phone Number",
                                ["required"] = true,
                                ["input-type"] = "phone"
                            },
                            new JsonObject
                            {
                                ["type"] = "Dropdown",
                                ["name"] = "service_type",
                                ["label"] = "Select Service",
                                ["required"] = true,
                                ["data-source"] = new JsonArray
                                {
                                    new JsonObject { ["id"] = "vip_demo", ["title"] = "VIP Product Demo & Consultation" },
                                    new JsonObject { ["id"] = "tech_support", ["title"] = "Technical Diagnostics & Setup" },
                                    new JsonObject { ["id"] = "order_exchange", ["title"] = "COD Order Exchange / Return Inspection" },
                                    new JsonObject { ["id"] = "account_audit", ["title"] = "Account & Marketing Strategy Review" }
                                }
                            },
                            new JsonObject
                            {
                                ["type"] = "DatePicker",
                                ["name"] = "appointment_date",
                                ["label"] = "Select Date",
                                ["required"] = true
                            },
                            new JsonObject
                            {
                                ["type"] = "RadioButtonsGroup",
                                ["name"] = "time_slot",
                                ["label"] = "Preferred Time Window",
                                ["required"] = true,
                                ["data-source"] = new JsonArray
                                {
                                    new JsonObject { ["id"] = "morning_1", ["title"] = "09:00 - 10:30 (Morning)" },
                                    new JsonObject { ["id"] = "morning_2", ["title"] = "11:00 - 12:30 (Late Morning)" },
                                    new JsonObject { ["id"] = "afternoon_1", ["title"] = "14:00 - 15:30 (Afternoon)" },
                                    new JsonObject { ["id"] = "evening_1", ["title"] = "16:00 - 17:30 (Late Afternoon)" }
                                }
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "special_notes",
                                ["label"] = "Specific Goals / Topic Notes",
                                ["required"] = false,
                                ["input-type"] = "text"
                            },
                            new JsonObject
                            {
                                ["type"] = "Footer",
                                ["label"] = "Confirm Booking",
                                ["on-click-action"] = new JsonObject
                                {
                                    ["name"] = "complete",
                                    ["payload"] = new JsonObject
                                    {
                                        ["action"] = "appointment_confirmed"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return schema.ToJsonString(JsonOptions);
    }

    public string GenerateProductVariantSelectionFlowSchema()
    {
        var schema = new JsonObject
        {
            ["version"] = "3.0",
            ["data_api_version"] = "3.0",
            ["routing_model"] = new JsonObject
            {
                ["PRODUCT_VARIANT_SCREEN"] = new JsonArray()
            },
            ["screens"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "PRODUCT_VARIANT_SCREEN",
                    ["title"] = "Select Size, Color & Quantity",
                    ["terminal"] = true,
                    ["data"] = new JsonObject(),
                    ["layout"] = new JsonObject
                    {
                        ["type"] = "SingleColumnLayout",
                        ["children"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "TextHeading",
                                ["text"] = "Configure Your Item"
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "product_id",
                                ["label"] = "Product ID / SKU",
                                ["required"] = true,
                                ["input-type"] = "text"
                            },
                            new JsonObject
                            {
                                ["type"] = "RadioButtonsGroup",
                                ["name"] = "selected_size",
                                ["label"] = "Size / Dimensions",
                                ["required"] = true,
                                ["data-source"] = new JsonArray
                                {
                                    new JsonObject { ["id"] = "s", ["title"] = "Small (S)" },
                                    new JsonObject { ["id"] = "m", ["title"] = "Medium (M)" },
                                    new JsonObject { ["id"] = "l", ["title"] = "Large (L)" },
                                    new JsonObject { ["id"] = "xl", ["title"] = "Extra Large (XL)" }
                                }
                            },
                            new JsonObject
                            {
                                ["type"] = "RadioButtonsGroup",
                                ["name"] = "selected_color",
                                ["label"] = "Color Palette",
                                ["required"] = true,
                                ["data-source"] = new JsonArray
                                {
                                    new JsonObject { ["id"] = "black", ["title"] = "Obsidian Black" },
                                    new JsonObject { ["id"] = "navy", ["title"] = "Navy Blue" },
                                    new JsonObject { ["id"] = "olive", ["title"] = "Forest Olive" }
                                }
                            },
                            new JsonObject
                            {
                                ["type"] = "TextInput",
                                ["name"] = "quantity",
                                ["label"] = "Quantity (1 - 10)",
                                ["required"] = true,
                                ["input-type"] = "number"
                            },
                            new JsonObject
                            {
                                ["type"] = "Dropdown",
                                ["name"] = "shipping_speed",
                                ["label"] = "Courier Delivery Speed",
                                ["required"] = false,
                                ["data-source"] = new JsonArray
                                {
                                    new JsonObject { ["id"] = "standard_48h", ["title"] = "Standard Doorstep (24h - 48h) - 7 TND" },
                                    new JsonObject { ["id"] = "express_same_day", ["title"] = "Express Urgent Same-Day - 12 TND" }
                                }
                            },
                            new JsonObject
                            {
                                ["type"] = "Footer",
                                ["label"] = "Add to Order & Proceed",
                                ["on-click-action"] = new JsonObject
                                {
                                    ["name"] = "complete",
                                    ["payload"] = new JsonObject
                                    {
                                        ["action"] = "variant_selected"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return schema.ToJsonString(JsonOptions);
    }

    public FlowValidationResult ValidateFlowResponsePayload(string flowType, string rawJsonPayload)
    {
        if (string.IsNullOrWhiteSpace(rawJsonPayload))
        {
            return new FlowValidationResult(false, ["Flow payload cannot be empty."], new Dictionary<string, object?>(), null);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJsonPayload);
            return ValidateFlowResponsePayload(flowType, doc.RootElement);
        }
        catch (JsonException ex)
        {
            return new FlowValidationResult(false, [$"Malformed JSON payload: {ex.Message}"], new Dictionary<string, object?>(), null);
        }
    }

    public FlowValidationResult ValidateFlowResponsePayload(string flowType, JsonElement payload)
    {
        var errors = new List<string>();
        var extracted = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Normalize flowType
        string normalizedType = flowType.Trim().ToLowerInvariant();

        // Extract key/values from payload
        if (payload.ValueKind == JsonValueKind.Object)
        {
            // Could be wrapped in a "data" or "response" object
            JsonElement target = payload;
            if (payload.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object)
            {
                target = dataElement;
            }
            else if (payload.TryGetProperty("response", out var respElement) && respElement.ValueKind == JsonValueKind.Object)
            {
                target = respElement;
            }

            foreach (var prop in target.EnumerateObject())
            {
                extracted[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.TryGetInt32(out int i) ? (object)i : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.ToString()
                };
            }
        }
        else
        {
            errors.Add("Flow response payload must be a JSON object.");
            return new FlowValidationResult(false, errors, extracted, null);
        }

        switch (normalizedType)
        {
            case "address":
            case "address_collection":
                ValidateAddressPayload(extracted, errors);
                break;

            case "appointment":
            case "appointment_booking":
                ValidateAppointmentPayload(extracted, errors);
                break;

            case "variant":
            case "product_variant":
                ValidateProductVariantPayload(extracted, errors);
                break;

            default:
                errors.Add($"Unsupported flow type '{flowType}'. Supported: 'address', 'appointment', 'variant'");
                break;
        }

        bool isValid = errors.Count == 0;
        string? nextAction = isValid ? $"{normalizedType}_processed" : "validation_failed";

        return new FlowValidationResult(isValid, errors, extracted, nextAction);
    }

    private static void ValidateAddressPayload(Dictionary<string, object?> data, List<string> errors)
    {
        if (!data.TryGetValue("full_name", out var name) || string.IsNullOrWhiteSpace(name?.ToString()))
            errors.Add("full_name is required.");

        if (!data.TryGetValue("phone_number", out var phone) || string.IsNullOrWhiteSpace(phone?.ToString()))
            errors.Add("phone_number is required.");
        else
        {
            var cleanPhone = new string(phone.ToString()!.Where(char.IsAsciiDigit).ToArray());
            if (cleanPhone.Length < 8) errors.Add("phone_number must contain at least 8 digits.");
        }

        if (!data.TryGetValue("street_address", out var street) || string.IsNullOrWhiteSpace(street?.ToString()))
            errors.Add("street_address is required.");

        if (!data.TryGetValue("governorate", out var gov) || string.IsNullOrWhiteSpace(gov?.ToString()))
            errors.Add("governorate is required.");

        if (!data.TryGetValue("city", out var city) || string.IsNullOrWhiteSpace(city?.ToString()))
            errors.Add("city is required.");
    }

    private static void ValidateAppointmentPayload(Dictionary<string, object?> data, List<string> errors)
    {
        if (!data.TryGetValue("customer_name", out var name) || string.IsNullOrWhiteSpace(name?.ToString()))
            errors.Add("customer_name is required.");

        if (!data.TryGetValue("customer_phone", out var phone) || string.IsNullOrWhiteSpace(phone?.ToString()))
            errors.Add("customer_phone is required.");

        if (!data.TryGetValue("service_type", out var service) || string.IsNullOrWhiteSpace(service?.ToString()))
            errors.Add("service_type is required.");

        if (!data.TryGetValue("appointment_date", out var date) || string.IsNullOrWhiteSpace(date?.ToString()))
            errors.Add("appointment_date is required.");
        else if (DateTime.TryParse(date.ToString(), out var dt))
        {
            if (dt.Date < DateTime.UtcNow.Date)
                errors.Add("appointment_date cannot be in the past.");
        }
        else
        {
            errors.Add("appointment_date must be a valid ISO-8601 date (YYYY-MM-DD).");
        }

        if (!data.TryGetValue("time_slot", out var slot) || string.IsNullOrWhiteSpace(slot?.ToString()))
            errors.Add("time_slot is required.");
    }

    private static void ValidateProductVariantPayload(Dictionary<string, object?> data, List<string> errors)
    {
        if (!data.TryGetValue("product_id", out var pid) || string.IsNullOrWhiteSpace(pid?.ToString()))
            errors.Add("product_id is required.");

        if (!data.TryGetValue("selected_size", out var size) || string.IsNullOrWhiteSpace(size?.ToString()))
            errors.Add("selected_size is required.");

        if (!data.TryGetValue("selected_color", out var color) || string.IsNullOrWhiteSpace(color?.ToString()))
            errors.Add("selected_color is required.");

        if (!data.TryGetValue("quantity", out var qtyObj))
            errors.Add("quantity is required.");
        else if (int.TryParse(qtyObj?.ToString(), out int qty))
        {
            if (qty < 1 || qty > 100) errors.Add("quantity must be between 1 and 100.");
        }
        else
        {
            errors.Add("quantity must be a valid integer.");
        }
    }

    public string RenderInteractiveListMenu(InteractiveListMenuRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RecipientPhone);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Body);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ButtonText);

        var sectionsArray = new JsonArray();
        foreach (var sec in request.Sections)
        {
            var rowsArray = new JsonArray();
            foreach (var r in sec.Rows)
            {
                var rowObj = new JsonObject
                {
                    ["id"] = r.Id,
                    ["title"] = r.Title
                };
                if (!string.IsNullOrWhiteSpace(r.Description))
                    rowObj["description"] = r.Description;
                rowsArray.Add(rowObj);
            }

            sectionsArray.Add(new JsonObject
            {
                ["title"] = sec.Title,
                ["rows"] = rowsArray
            });
        }

        var interactiveObj = new JsonObject
        {
            ["type"] = "list",
            ["body"] = new JsonObject { ["text"] = request.Body },
            ["action"] = new JsonObject
            {
                ["button"] = request.ButtonText,
                ["sections"] = sectionsArray
            }
        };

        if (!string.IsNullOrWhiteSpace(request.Header))
        {
            interactiveObj["header"] = new JsonObject
            {
                ["type"] = "text",
                ["text"] = request.Header
            };
        }

        if (!string.IsNullOrWhiteSpace(request.Footer))
        {
            interactiveObj["footer"] = new JsonObject
            {
                ["text"] = request.Footer
            };
        }

        var root = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = request.RecipientPhone,
            ["type"] = "interactive",
            ["interactive"] = interactiveObj
        };

        return root.ToJsonString(JsonOptions);
    }

    public string RenderDynamicQuickReplies(QuickReplyButtonsRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RecipientPhone);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Body);

        var buttonsArray = new JsonArray();
        int count = 0;
        foreach (var b in request.Buttons)
        {
            if (count >= 3) break; // WhatsApp max 3 quick-reply buttons
            buttonsArray.Add(new JsonObject
            {
                ["type"] = "reply",
                ["reply"] = new JsonObject
                {
                    ["id"] = b.Id,
                    ["title"] = b.Title
                }
            });
            count++;
        }

        var interactiveObj = new JsonObject
        {
            ["type"] = "button",
            ["body"] = new JsonObject { ["text"] = request.Body },
            ["action"] = new JsonObject
            {
                ["buttons"] = buttonsArray
            }
        };

        if (!string.IsNullOrWhiteSpace(request.Header))
        {
            interactiveObj["header"] = new JsonObject
            {
                ["type"] = "text",
                ["text"] = request.Header
            };
        }

        if (!string.IsNullOrWhiteSpace(request.Footer))
        {
            interactiveObj["footer"] = new JsonObject
            {
                ["text"] = request.Footer
            };
        }

        var root = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = request.RecipientPhone,
            ["type"] = "interactive",
            ["interactive"] = interactiveObj
        };

        return root.ToJsonString(JsonOptions);
    }

    public string RenderNativeCatalogCart(CatalogCartRenderRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RecipientPhone);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CatalogId);

        var sectionsArray = new JsonArray();
        decimal totalAmount = 0;
        string currency = "TND";

        foreach (var s in request.Sections)
        {
            var productItemsArray = new JsonArray();
            foreach (var item in s.Items)
            {
                productItemsArray.Add(new JsonObject
                {
                    ["product_retailer_id"] = item.RetailerId
                });
                totalAmount += item.Price * item.Quantity;
                currency = item.Currency;
            }

            sectionsArray.Add(new JsonObject
            {
                ["title"] = s.Title,
                ["product_items"] = productItemsArray
            });
        }

        var interactiveObj = new JsonObject
        {
            ["type"] = "product_list",
            ["header"] = new JsonObject
            {
                ["type"] = "text",
                ["text"] = string.IsNullOrWhiteSpace(request.HeaderText) ? "Our Featured Catalog" : request.HeaderText
            },
            ["body"] = new JsonObject
            {
                ["text"] = string.IsNullOrWhiteSpace(request.BodyText)
                    ? $"Browse our interactive catalog. Order total: {totalAmount:F2} {currency}"
                    : request.BodyText
            },
            ["action"] = new JsonObject
            {
                ["catalog_id"] = request.CatalogId,
                ["sections"] = sectionsArray
            }
        };

        if (!string.IsNullOrWhiteSpace(request.FooterText))
        {
            interactiveObj["footer"] = new JsonObject
            {
                ["text"] = request.FooterText
            };
        }

        var root = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = request.RecipientPhone,
            ["type"] = "interactive",
            ["interactive"] = interactiveObj,
            ["cart_metadata"] = new JsonObject
            {
                ["estimated_total"] = totalAmount,
                ["currency"] = currency,
                ["item_count"] = request.Sections.Sum(sec => sec.Items.Count)
            }
        };

        return root.ToJsonString(JsonOptions);
    }
}
