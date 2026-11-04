using SGrowing.Api.Modules.Support.Localization;
using Xunit;

namespace SGrowing.Tests.Support;

public class SupportLocalizationTests
{
    private readonly SupportLocalizationEngine _engine = new();

    [Fact]
    public void SeededMessage_ContainsPreservedBrandTermsAndHighPoliteness()
    {
        var msg = _engine.GetMessageById("msg_support_401");

        Assert.NotNull(msg);
        Assert.Equal("en", msg.SourceLanguage);
        Assert.Equal("de", msg.TargetLanguage);
        Assert.Contains("SGrowing", msg.LocalizedText);
        Assert.Contains("SOC2 Type II", msg.LocalizedText);
        Assert.True(msg.NuanceAudit.PolitenessIndex > 95.0);
        Assert.True(msg.LatencyMilliseconds < 200);
    }

    [Fact]
    public void TranslateMessage_ToJapanese_RespectsKeigoFormality()
    {
        var request = new TranslateSupportMessageRequest(
            "msg_test_ja",
            "en",
            "ja",
            "We have verified your account with SGrowing.",
            FormalityLevel.FormalExecutive
        );

        var result = _engine.TranslateMessage(request);

        Assert.NotNull(result);
        Assert.Equal("ja", result.TargetLanguage);
        Assert.True(result.NuanceAudit.HonorificCompliant);
        Assert.Contains("SGrowing", result.NuanceAudit.PreservedBrandTerms);
    }

    [Fact]
    public void GetGlossary_ReturnsProtectedVocabulary()
    {
        var glossary = _engine.GetGlossary();

        Assert.NotEmpty(glossary);
        Assert.Contains(glossary, g => g.SourceTerm == "Growth OS");
    }

    [Fact]
    public void TranslateMessage_ToFrenchAndSpanish_ReturnsLocalizedGreetings()
    {
        var frRequest = new TranslateSupportMessageRequest("msg_fr", "en", "fr", "Hello from SGrowing", FormalityLevel.PoliteBusiness);
        var frResult = _engine.TranslateMessage(frRequest);
        Assert.Contains("Nous vous remercions", frResult.LocalizedText);

        var esRequest = new TranslateSupportMessageRequest("msg_es", "en", "es", "Hello from SGrowing", FormalityLevel.PoliteBusiness);
        var esResult = _engine.TranslateMessage(esRequest);
        Assert.Contains("Gracias por comunicarse", esResult.LocalizedText);
    }

    [Fact]
    public void TranslateMessage_FallbackLanguage_FormatsWithBrackets()
    {
        var itRequest = new TranslateSupportMessageRequest("msg_it", "en", "it", "Welcome to the service", FormalityLevel.CasualWarm);
        var itResult = _engine.TranslateMessage(itRequest);

        Assert.StartsWith("[Localized (IT - CasualWarm)]", itResult.LocalizedText);
        Assert.Contains("Welcome to the service", itResult.LocalizedText);
    }

    [Fact]
    public void GetMessageById_WhenNotFound_ReturnsNull()
    {
        var msg = _engine.GetMessageById("msg_unknown_123");
        Assert.Null(msg);
    }

    [Fact]
    public void TranslateMessage_ToGerman_DifferentiatesExecutiveVsCasualFormality()
    {
        var execReq = new TranslateSupportMessageRequest("msg_de_exec", "en", "de", "Thank you for contacting support", FormalityLevel.FormalExecutive);
        var execRes = _engine.TranslateMessage(execReq);

        Assert.Contains("Vielen Dank für Ihre Kontaktaufnahme", execRes.LocalizedText);
        Assert.Equal(98.5, execRes.NuanceAudit.PolitenessIndex);

        var casualReq = new TranslateSupportMessageRequest("msg_de_cas", "en", "de", "Thank you for contacting support", FormalityLevel.CasualWarm);
        var casualRes = _engine.TranslateMessage(casualReq);

        Assert.Contains("Danke, dass du den SGrowing Support kontaktiert hast", casualRes.LocalizedText);
        Assert.Equal(89.0, casualRes.NuanceAudit.PolitenessIndex);
    }

    [Fact]
    public void GetMessageById_IsCaseInsensitive_ReturnsCachedMessage()
    {
        var msg = _engine.GetMessageById("MSG_SUPPORT_401");

        Assert.NotNull(msg);
        Assert.Equal("de", msg.TargetLanguage);
        Assert.Equal("en", msg.SourceLanguage);
    }
}
