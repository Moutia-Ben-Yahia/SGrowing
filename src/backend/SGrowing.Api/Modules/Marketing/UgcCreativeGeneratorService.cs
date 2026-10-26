using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public interface IUgcCreativeGeneratorService
{
    Task<List<UgcVideoScript>> GenerateScriptsForProductAsync(
        Guid tenantId,
        string sku,
        string productName,
        string language = "Darija",
        string targetAudience = "Shoppers 18-35");
    Task<List<UgcVideoScript>> GetScriptsByProductAsync(Guid tenantId, string sku);
}

public class UgcCreativeGeneratorService : IUgcCreativeGeneratorService
{
    private readonly SGrowingDbContext _db;

    public UgcCreativeGeneratorService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<List<UgcVideoScript>> GenerateScriptsForProductAsync(
        Guid tenantId,
        string sku,
        string productName,
        string language = "Darija",
        string targetAudience = "Shoppers 18-35")
    {
        var existing = await _db.UgcVideoScripts
            .Where(s => s.TenantId == tenantId && s.ProductSku == sku.Trim() && s.Language == language.Trim())
            .ToListAsync();

        if (existing.Count >= 5) return existing;

        var generated = new List<UgcVideoScript>();

        // Angle 1: Problem Agitator
        var scenes1 = new List<UgcScriptScene>
        {
            new(1, "Creator rubs forehead looking exhausted and holding broken competitor product", "Show frustration close-up to camera", 
                language == "French" ? "Marre des produits qui cassent après 3 jours d'utilisation ?" : "Feddit mel sel3a li techriha w ba3d 3 ayem tfassed ?", 
                "⚠️ Ne gaspillez plus votre argent", 4),
            new(2, "Unboxes new product glowing with premium finish", "Hold product to natural light, smile with relief", 
                language == "French" ? "Voici la solution qui a changé ma routine quotidienne." : "Chouf el farq ! Hadha elli baddalli el routine mte3i.", 
                "✨ Qualité Premium 100% Garantie", 6),
            new(3, "Demonstrates product in real-life action showing durability", "Active hands-on demo with sound effects", 
                language == "French" ? "Une fabrication robuste conçue pour durer des années." : "Khedma s7i7a w matérial pro mouch le3b.", 
                "🔥 Conçu pour durer", 8),
            new(4, "Holds package with delivery note showing Cash On Delivery", "Tap delivery box reassuring viewer", 
                language == "French" ? "Et le meilleur : vous payez uniquement à la livraison chez vous !" : "W a7sen 7aja : tkhaless ken ki tousellek l bab eddar !", 
                "🛵 Paiement à la livraison (COD)", 7)
        };
        generated.Add(UgcVideoScript.Create(
            tenantId, sku, productName, language, targetAudience,
            UgcHookAngle.ProblemAgitator,
            language == "French" ? "Arrêtez d'acheter n'importe quoi !" : "Barra erte7 mel sel3a el maghchoucha !",
            scenes1,
            language == "French" ? "Cliquez sur le lien ci-dessous avant rupture de stock !" : "Enzel 3al lien louta qbal ma toufa el quantité !"
        ));

        // Angle 2: Competitor Callout
        var scenes2 = new List<UgcScriptScene>
        {
            new(1, "Split-screen comparison: Generic brand vs Our product", "Side by side visual comparison",
                language == "French" ? "Pourquoi payer 200 DT pour une marque qui ne tient pas ses promesses ?" : "3lech tdfa3 200 DT fi marqua w hiya zéro qualité ?",
                "❌ Marque X vs ✅ SGrowing", 5),
            new(2, "Shows texture and craftsmanship details", "Macro lens zoom on stitch or ingredients",
                language == "French" ? "Ici, vous avez exactement la même formule aux normes internationales." : "Hna 3andek nafes el formule b a3la standard mondial.",
                "💎 Formule Certifiée", 8),
            new(3, "Holding phone showing 5-star customer reviews", "Scroll through real WhatsApp customer feedback",
                language == "French" ? "Plus de 4 500 avis 5 étoiles partout en Tunisie." : "Akther men 4,500 client fer7anin fi kol blasa.",
                "⭐⭐⭐⭐⭐ +4,500 Avis Clients", 7)
        };
        generated.Add(UgcVideoScript.Create(
            tenantId, sku, productName, language, targetAudience,
            UgcHookAngle.CompetitorCallout,
            language == "French" ? "Ne vous faites plus avoir par les grandes marques !" : "Matkhallihomch yekhdh3ouk b assemi el marques !",
            scenes2,
            language == "French" ? "Profitez de l'offre spéciale dès aujourd'hui !" : "Ekhtaf el promo tawwa w khalles f dar !"
        ));

        // Angle 3: Cultural COD Reassurance (Safety First)
        var scenes3 = new List<UgcScriptScene>
        {
            new(1, "Doorbell rings, driver in uniform delivers branded parcel", "Friendly greeting at doorstep with smile",
                language == "French" ? "Peur de commander sur Internet ? J'avais le même doute !" : "Khaief mel aachat en ligne ? Kont kifek bedhabt !",
                "🛡️ Zéro Risque Garanti", 4),
            new(2, "Customer opens parcel in front of driver, inspects item", "Unbox right there, driver gives receipt",
                language == "French" ? "Tu ouvres le colis, tu vérifies la conformité, et ensuite tu payes !" : "T7ell el colis, tchouf el commande mte3ek, w ba3d tkhaless !",
                "📦 Ouvre ton colis avant de payer", 9),
            new(3, "Close-up of money handover with printed invoice", "Hand over cash, receive stamped receipt",
                language == "French" ? "Livraison rapide 24h/48h partout avec Yalidine et Aramex." : "Tousellek fi 24h/48h fi ay blasa ma3 Yalidine w Aramex.",
                "🚀 Livraison Express 24-48H", 7)
        };
        generated.Add(UgcVideoScript.Create(
            tenantId, sku, productName, language, targetAudience,
            UgcHookAngle.CulturalCodReassurance,
            language == "French" ? "Commandez les yeux fermés : Paiement après vérification !" : "Comandi w rassek marfou3 : Khalles ken ba3d matthabbet !",
            scenes3,
            language == "French" ? "Remplissez le formulaire en 10 secondes !" : "3abbi el formulaire fi 10 thweni !"
        ));

        // Angle 4: Unboxing & ASMR Demonstration
        var scenes4 = new List<UgcScriptScene>
        {
            new(1, "ASMR box cutter sound opening carton", "Clean aesthetic table setting, crisp audio",
                language == "French" ? "Écoutez ce son satisfaisant..." : "Asma3li sawt el unboxing...",
                "🎧 Son ASMR Unboxing", 5),
            new(2, "Lifting product with sleek luxury ribbon", "Slow motion peel of protective film",
                language == "French" ? "L'emballage et les finitions sont tout simplement impeccables." : "El emballage wel finition luxe 3al lekher.",
                "✨ Finition Haute Gamme", 8),
            new(3, "First usage application with visible instant result", "Direct application to skin/use case",
                language == "French" ? "Résultat instantané dès la première minute d'essai." : "Ennatija tdhor mel dqiqa loula.",
                "⚡ Efficacité Immédiate", 8)
        };
        generated.Add(UgcVideoScript.Create(
            tenantId, sku, productName, language, targetAudience,
            UgcHookAngle.UnboxingDemonstration,
            language == "French" ? "Ce déballage va vous laisser bouche bée !" : "L'unboxing hadha bech yebhhrek !",
            scenes4,
            language == "French" ? "Commandez votre exemplaire original maintenant !" : "A5lat 3al originale tawwa !"
        ));

        // Angle 5: Scarcity Flash Deal
        var scenes5 = new List<UgcScriptScene>
        {
            new(1, "Creator points to ticking countdown clock and warehouse shelves", "Energetic fast-paced hook",
                language == "French" ? "Alerte : il ne reste que 47 pièces disponibles au prix promo !" : "Attention : bqaw ken 47 piéce bel soum el promo !",
                "⏳ Dernières 47 Pièces en Stock", 4),
            new(2, "Shows free bonus gift included in the offer", "Pulls out complimentary accessory from box",
                language == "French" ? "Aujourd'hui seulement : le deuxième article offert ou -50% !" : "El youm barka : el 9at3a ethanya b noss soum + cadeau !",
                "🎁 Cadeau Exclusif Offert", 7),
            new(3, "Shows express dispatch trucks leaving warehouse", "Trucks driving off with parcels",
                language == "French" ? "Toutes les commandes passées avant 16h partent aujourd'hui." : "Kol commande qbal 16h tokhrej lyoum bedhabt.",
                "🚚 Départ Immédiat Aujourd'hui", 6)
        };
        generated.Add(UgcVideoScript.Create(
            tenantId, sku, productName, language, targetAudience,
            UgcHookAngle.ScarcityFlashDeal,
            language == "French" ? "Dernier appel pour l'offre flash !" : "Aakher forssa fi l'offre flash !",
            scenes5,
            language == "French" ? "Validez votre commande maintenant !" : "Akedh el commande mte3ek tawwa !"
        ));

        _db.UgcVideoScripts.AddRange(generated);
        await _db.SaveChangesAsync();
        return generated;
    }

    public async Task<List<UgcVideoScript>> GetScriptsByProductAsync(Guid tenantId, string sku)
    {
        return await _db.UgcVideoScripts
            .Where(s => s.TenantId == tenantId && s.ProductSku == sku.Trim())
            .OrderBy(s => s.HookAngle)
            .ToListAsync();
    }
}
