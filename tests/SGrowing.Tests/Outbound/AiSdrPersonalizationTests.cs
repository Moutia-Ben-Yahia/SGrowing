using FluentAssertions;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Outbound;

public class AiSdrPersonalizationTests
{
    [Fact]
    public async Task GeneratePersonalization_ShouldSynthesizeCustomIcebreakerAndProblemStatement()
    {
        var service = new AiSdrService();
        var input = new SdrPersonalizationInput(
            FirstName: "Gwen",
            LastName: "Stacy",
            Company: "Oscorp BioTech",
            Industry: "Biotechnology",
            Role: "VP of Research"
        );

        var output = await service.GeneratePersonalizationAsync(input);

        output.Should().NotBeNull();
        output.Icebreaker.Should().Contain("Oscorp BioTech");
        output.ProblemStatement.Should().NotBeNullOrWhiteSpace();
        output.CallToAction.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RenderTemplate_ShouldReplaceAllTemplateVariables()
    {
        var service = new AiSdrService();
        var contact = Contact.Create(Guid.NewGuid(), "Peter", "Parker", "peter@dailybugle.com");
        var company = new Company { Name = "Daily Bugle", Industry = "Media" };
        var sdrOutput = new SdrPersonalizationOutput(
            Icebreaker: "Saw your recent coverage of the New York tech ecosystem.",
            ProblemStatement: "Managing high-volume outbound messaging without burning domain reputation is difficult.",
            CallToAction: "Are you open to a brief 10-minute chat this Thursday?"
        );

        var template = "Hi {{first_name}}, {{icebreaker}} {{problem_statement}} {{cta}}";
        var rendered = service.RenderTemplate(template, contact, company, sdrOutput);

        rendered.Should().Be("Hi Peter, Saw your recent coverage of the New York tech ecosystem. Managing high-volume outbound messaging without burning domain reputation is difficult. Are you open to a brief 10-minute chat this Thursday?");
    }

    [Fact]
    public async Task GeneratePersonalization_WithEmptyCompanyAndIndustry_UsesFallbacks()
    {
        var service = new AiSdrService();
        var input = new SdrPersonalizationInput(
            FirstName: "Miles",
            LastName: "Morales",
            Company: "  ",
            Industry: null,
            Role: "Student"
        );

        var output = await service.GeneratePersonalizationAsync(input);

        output.Should().NotBeNull();
        output.Icebreaker.Should().Contain("your organization");
        output.Icebreaker.Should().Contain("the sector");
        output.ProblemStatement.Should().Contain("the sector leaders");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RenderTemplate_WithNullOrEmptyTemplate_ReturnsEmptyString(string? template)
    {
        var service = new AiSdrService();
        var contact = Contact.Create(Guid.NewGuid(), "Tony", "Stark", "tony@stark.com");

        var rendered = service.RenderTemplate(template!, contact, null, null);

        rendered.Should().BeEmpty();
    }

    [Fact]
    public void RenderTemplate_WithNullCompanyAndSdrOutput_UsesFallbacks()
    {
        var service = new AiSdrService();
        var contact = Contact.Create(Guid.NewGuid(), "Steve", "Rogers", "steve@avengers.org");
        var template = "Hello {{first_name}} {{last_name}} at {{company}}! {{icebreaker}} {{cta}}";

        var rendered = service.RenderTemplate(template, contact, null, null);

        rendered.Should().Be("Hello Steve Rogers at your company!");
    }
}
