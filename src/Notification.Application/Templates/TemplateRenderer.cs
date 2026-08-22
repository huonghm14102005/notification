using System.Net;
using System.Text.RegularExpressions;
namespace Notification.Application.Templates;

public interface ITemplateRenderer { RenderedContent Render(TemplateDefinition template, IReadOnlyDictionary<string, string> data); }
public sealed partial class TemplateRenderer : ITemplateRenderer
{
    [GeneratedRegex(@"\{\{([A-Za-z][A-Za-z0-9_]{0,63})\}\}")] private static partial Regex Tokens();
    public static string[] Validate(string subject, string? textBody, string? htmlBody, IEnumerable<string> variables)
    {
        if (subject.Length is < 1 or > 998 || subject.Any(char.IsControl) || textBody is null && htmlBody is null || textBody is not null && (textBody.Length is < 1 or > 100000 || Unsafe(textBody)) || htmlBody is not null && (htmlBody.Length is < 1 or > 100000 || Unsafe(htmlBody))) throw new TemplateOperationException("VALIDATION_FAILED");
        var vars=variables.Order(StringComparer.Ordinal).ToArray(); if (vars.Length>50 || vars.Any(v=>!Regex.IsMatch(v,@"^[A-Za-z][A-Za-z0-9_]{0,63}$")) || vars.Distinct(StringComparer.Ordinal).Count()!=vars.Length) throw new TemplateOperationException("VALIDATION_FAILED");
        var all=subject+(textBody??"")+(htmlBody??""); var scrub=Tokens().Replace(all,""); if (scrub.Contains("{{",StringComparison.Ordinal)||scrub.Contains("}}",StringComparison.Ordinal)) throw new TemplateOperationException("TEMPLATE_SYNTAX_INVALID");
        var used=Tokens().Matches(all).Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(); if(!used.SequenceEqual(vars,StringComparer.Ordinal)) throw new TemplateOperationException("TEMPLATE_SYNTAX_INVALID"); return vars;
    }
    public static string[] Validate(string subject,string body,IEnumerable<string> variables)=>Validate(subject,body,null,variables);
    public RenderedContent Render(TemplateDefinition t,IReadOnlyDictionary<string,string> data)
    {
        var missing=t.Variables.Where(x=>!data.ContainsKey(x)).Order().ToArray(); if(missing.Length>0) throw new TemplateOperationException("TEMPLATE_VARIABLE_MISSING",missing); var unknown=data.Keys.Where(x=>!t.Variables.Contains(x,StringComparer.Ordinal)).Order().ToArray(); if(unknown.Length>0) throw new TemplateOperationException("TEMPLATE_VARIABLE_UNKNOWN",unknown); if(data.Values.Any(x=>x.Length>10000)) throw new TemplateOperationException("TEMPLATE_RENDER_TOO_LARGE");
        string Replace(string input,bool html)=>Tokens().Replace(input,m=>html?WebUtility.HtmlEncode(data[m.Groups[1].Value]):data[m.Groups[1].Value]); var subject=Replace(t.Subject,false); var text=t.TextBody is null?null:Replace(t.TextBody,false); var html=t.HtmlBody is null?null:Replace(t.HtmlBody,true);
        if(subject.Length>998||subject.Any(char.IsControl)||text is not null&&(text.Length>100000||Unsafe(text))||html is not null&&(html.Length>100000||Unsafe(html))||(text?.Length??0)+(html?.Length??0)>150000) throw new TemplateOperationException("TEMPLATE_RENDER_TOO_LARGE"); return new(subject,text,html);
    }
    private static bool Unsafe(string value)=>value.Any(c=>char.IsControl(c)&&c is not '\t' and not '\r' and not '\n');
}
