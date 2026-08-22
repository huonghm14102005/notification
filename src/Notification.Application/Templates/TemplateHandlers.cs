using System.Globalization;
using System.Text;
using Notification.Application.Abstractions.Time;
using Notification.Domain.Templates;
namespace Notification.Application.Templates;

public sealed class TemplateHandlers(ITemplateRepository repository,IClock clock)
{
    public async Task<TemplateItem> CreateAsync(Guid tid,CreateTemplateCommand c,CancellationToken ct)
    {
        var code=c.TemplateCode.Trim().ToLowerInvariant(); if(c.Scope==TemplateScope.Source && (c.SourceDeviceId is null || !await repository.SourceDeviceIsEligibleAsync(tid,c.SourceDeviceId.Value,ct))) throw new TemplateOperationException("NOT_FOUND");
        if(await repository.FamilyExistsAsync(tid,c.Scope,c.SourceDeviceId,code,ct)) throw new TemplateOperationException("TEMPLATE_CODE_EXISTS");
        var subject=c.Subject.Trim(); var vars=TemplateRenderer.Validate(subject,c.TextBody,c.HtmlBody,c.Variables); var x=new ContentTemplate(Guid.NewGuid(),tid,code,c.Scope,c.SourceDeviceId,c.Audience,1,subject,c.TextBody,c.HtmlBody,vars,clock.UtcNow); await repository.AddAsync(x,ct); return Map(x);
    }
    public async Task<TemplateItem> GetAsync(Guid tid,Guid id,CancellationToken ct)=>Map(await repository.FindByIdAsync(tid,id,ct)??throw new TemplateOperationException("NOT_FOUND"));
    public async Task<TemplateItem> GetLegacyAsync(Guid tid,string code,CancellationToken ct)=>Map(await repository.FindLegacyAsync(tid,code.Trim().ToLowerInvariant(),ct)??throw new TemplateOperationException("NOT_FOUND"));
    public Task<TemplatePage> ListAsync(Guid tid,string? scope,Guid? source,string? audience,string? status,int limit,string? cursor,CancellationToken ct){DateTimeOffset? at=null;Guid? id=null;if(cursor is not null)try{var p=Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');at=DateTimeOffset.Parse(p[0],CultureInfo.InvariantCulture);id=Guid.Parse(p[1]);}catch{throw new TemplateOperationException("VALIDATION_FAILED");}return repository.ListAsync(tid,scope,source,audience,status,limit,at,id,ct);}
    public async Task<TemplateItem> UpdateAsync(Guid tid,Guid id,UpdateTemplateCommand c,CancellationToken ct){var x=await repository.FindByIdAsync(tid,id,ct)??throw new TemplateOperationException("NOT_FOUND");if(x.Status!=TemplateStatus.Draft)throw new TemplateOperationException("TEMPLATE_INVALID_STATE");var subject=c.Subject?.Trim()??x.Subject;var text=c.SetTextBody?c.TextBody:x.TextBody;var html=c.SetHtmlBody?c.HtmlBody:x.HtmlBody;var vars=TemplateRenderer.Validate(subject,text,html,c.Variables??x.Variables);try{x.UpdateDraft(c.Subject?.Trim(),c.TextBody,c.SetTextBody,c.HtmlBody,c.SetHtmlBody,c.Variables is null?null:vars,clock.UtcNow);}catch(InvalidOperationException){throw new TemplateOperationException("TEMPLATE_INVALID_STATE");}await repository.SaveAsync(ct);return Map(x);}
    public async Task<TemplateItem> CloneAsync(Guid tid,Guid id,CancellationToken ct){var x=await repository.FindByIdAsync(tid,id,ct)??throw new TemplateOperationException("NOT_FOUND");try{var clone=x.CloneDraft(Guid.NewGuid(),await repository.GetNextVersionAsync(x,ct),clock.UtcNow);await repository.AddAsync(clone,ct);return Map(clone);}catch(InvalidOperationException){throw new TemplateOperationException("TEMPLATE_INVALID_STATE");}}
    public async Task<TemplateItem> PublishAsync(Guid tid,Guid id,CancellationToken ct){var x=await repository.FindByIdAsync(tid,id,ct)??throw new TemplateOperationException("NOT_FOUND");try{await repository.PublishAsync(x,clock.UtcNow,ct);return Map(x);}catch(InvalidOperationException){throw new TemplateOperationException("TEMPLATE_INVALID_STATE");}}
    public async Task<TemplateItem> RetireAsync(Guid tid,Guid id,CancellationToken ct){var x=await repository.FindByIdAsync(tid,id,ct)??throw new TemplateOperationException("NOT_FOUND");try{x.Retire(clock.UtcNow);}catch(InvalidOperationException){throw new TemplateOperationException("TEMPLATE_INVALID_STATE");}await repository.SaveAsync(ct);return Map(x);}
    public static TemplateItem Map(ContentTemplate x)=>new(x.Id,x.TemplateCode,x.Scope,x.SourceDeviceId,x.Audience,x.Version,x.Subject,x.TextBody,x.HtmlBody,x.Variables,x.Status,x.CreatedAt,x.UpdatedAt,x.PublishedAt,x.RetiredAt);
}
