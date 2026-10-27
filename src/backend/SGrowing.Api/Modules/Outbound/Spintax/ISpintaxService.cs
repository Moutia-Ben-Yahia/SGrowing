namespace SGrowing.Api.Modules.Outbound.Spintax;

public interface ISpintaxService
{
    string Parse(string template, int? seed = null, IDictionary<string, string>? variables = null);
    string ParseForProspect(string template, string prospectIdOrEmail, IDictionary<string, string>? variables = null);
    IReadOnlyList<string> GenerateVariants(string template, int count, IDictionary<string, string>? variables = null);
    long CalculatePermutationsCount(string template);
}
