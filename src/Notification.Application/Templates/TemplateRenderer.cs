using System.Text.RegularExpressions;

namespace Notification.Application.Templates;

public interface ITemplateRenderer { RenderedContent Render(TemplateDefinition template, IReadOnlyDictionary<string, string> data); }
public sealed partial class TemplateRenderer : ITemplateRenderer
{
    [GeneratedRegex(@"\{\{([A-Za-z][A-Za-z0-9_]{0,63})\}\}")] private static partial Regex Tokens();
    public static string[] Validate(string subject, string body, IEnumerable<string> variables)
    {
        if (subject.Length is < 1 or > 998 || subject.Any(char.IsControl) || body.Length is < 1 or > 100000 || body.Any(c => char.IsControl(c) && c is not '\t' and not '\r' and not '\n')) throw new TemplateOperationException("VALIDATION_FAILED");
        var vars = variables.Order(StringComparer.Ordinal).ToArray(); if (vars.Length > 50 || vars.Any(v => !Regex.IsMatch(v, @"^[A-Za-z][A-Za-z0-9_]{0,63}$")) || vars.Distinct(StringComparer.Ordinal).Count() != vars.Length) throw new TemplateOperationException("VALIDATION_FAILED");
        var scrub = Tokens().Replace(subject + body, ""); if (scrub.Contains("{{", StringComparison.Ordinal) || scrub.Contains("}}", StringComparison.Ordinal)) throw new TemplateOperationException("TEMPLATE_SYNTAX_INVALID");
        var used = Tokens().Matches(subject + body).Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(); if (!used.SequenceEqual(vars, StringComparer.Ordinal)) throw new TemplateOperationException("TEMPLATE_SYNTAX_INVALID"); return vars;
    }
    public RenderedContent Render(TemplateDefinition t, IReadOnlyDictionary<string, string> data)
    {
        var missing = t.Variables.Where(x => !data.ContainsKey(x)).Order().ToArray(); if (missing.Length > 0) throw new TemplateOperationException("TEMPLATE_VARIABLE_MISSING", missing);
        var unknown = data.Keys.Where(x => !t.Variables.Contains(x, StringComparer.Ordinal)).Order().ToArray(); if (unknown.Length > 0) throw new TemplateOperationException("TEMPLATE_VARIABLE_UNKNOWN", unknown);
        string Replace(string input) => Tokens().Replace(input, m => data[m.Groups[1].Value]); var subject = Replace(t.Subject); var body = Replace(t.Body);
        if (subject.Length > 998 || subject.Any(char.IsControl) || body.Length > 100000 || body.Any(c => char.IsControl(c) && c is not '\t' and not '\r' and not '\n') || data.Values.Any(x => x.Length > 10000)) throw new TemplateOperationException("TEMPLATE_RENDER_TOO_LARGE"); return new(subject, body);
    }
}
