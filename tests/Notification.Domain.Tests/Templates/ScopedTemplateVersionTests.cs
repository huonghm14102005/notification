using Notification.Domain.Templates;
namespace Notification.Domain.Tests.Templates;

public sealed class ScopedTemplateVersionTests
{
    [Fact] public void PublishedVersionIsImmutableAndCanBeCloned()
    {
        var now=DateTimeOffset.UtcNow;var x=new ContentTemplate(Guid.NewGuid(),Guid.NewGuid(),"score","source",Guid.NewGuid(),"user",1,"Subject","Text",null,[],now);x.Publish(now);Assert.Throws<InvalidOperationException>(()=>x.UpdateDraft("Changed",null,false,null,false,null,now));var clone=x.CloneDraft(Guid.NewGuid(),2,now.AddMinutes(1));Assert.Equal(2,clone.Version);Assert.Equal(TemplateStatus.Draft,clone.Status);Assert.Equal("Subject",clone.Subject);
    }
    [Fact] public void RetiredVersionCannotBePublishedAgain()
    {
        var now=DateTimeOffset.UtcNow;var x=new ContentTemplate(Guid.NewGuid(),Guid.NewGuid(),"score","tenant",null,"system",1,"Subject","Text",null,[],now);x.Publish(now);x.Retire(now);Assert.Throws<InvalidOperationException>(()=>x.Publish(now));
    }
}
