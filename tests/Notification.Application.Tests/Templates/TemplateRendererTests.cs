using Notification.Application.Templates;
namespace Notification.Application.Tests.Templates;

public sealed class TemplateRendererTests
{
    [Fact] public void RendersEveryOccurrenceOnce() { var t = new TemplateDefinition(Guid.NewGuid(), "key", "Hi {{name}}", "{{name}}={{value}} {{value}}", ["name", "value"]); var r = new TemplateRenderer().Render(t, new Dictionary<string, string> { { "name", "{{value}}" }, { "value", "42" } }); Assert.Equal("Hi {{value}}", r.Subject); Assert.Equal("{{value}}=42 42", r.Body); }
    [Fact] public void MissingVariableIsReported() { var t = new TemplateDefinition(Guid.NewGuid(), "key", "{{name}}", "Body", ["name"]); var e = Assert.Throws<TemplateOperationException>(() => new TemplateRenderer().Render(t, new Dictionary<string, string>())); Assert.Equal("TEMPLATE_VARIABLE_MISSING", e.Code); Assert.Equal(["name"], e.Names); }
    [Fact] public void UnknownVariableIsReported() { var t = new TemplateDefinition(Guid.NewGuid(), "key", "Subject", "Body", []); var e = Assert.Throws<TemplateOperationException>(() => new TemplateRenderer().Render(t, new Dictionary<string, string> { { "extra", "x" } })); Assert.Equal("TEMPLATE_VARIABLE_UNKNOWN", e.Code); }
    [Fact] public void SyntaxMustEqualDeclaredVariables() { var e = Assert.Throws<TemplateOperationException>(() => TemplateRenderer.Validate("Hi {{name}}", "Body", [])); Assert.Equal("TEMPLATE_SYNTAX_INVALID", e.Code); }
    [Fact] public void HtmlVariablesAreEncodedButTextIsNot() { var t = new TemplateDefinition(Guid.NewGuid(), "key", 2, "Hi {{name}}", "Text {{value}}", "<b>{{value}}</b>", ["name", "value"]); var r = new TemplateRenderer().Render(t, new Dictionary<string, string> { ["name"] = "An", ["value"] = "<script>&'\"" }); Assert.Equal("Text <script>&'\"", r.TextBody); Assert.Equal("<b>&lt;script&gt;&amp;&#39;&quot;</b>", r.HtmlBody); }
    [Fact] public void AtLeastOneBodyIsRequired() { var e = Assert.Throws<TemplateOperationException>(() => TemplateRenderer.Validate("Subject", null, null, [])); Assert.Equal("VALIDATION_FAILED", e.Code); }
}
