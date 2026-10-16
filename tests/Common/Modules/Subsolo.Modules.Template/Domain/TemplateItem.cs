using Subsolo.Common.Domain;

namespace Subsolo.Modules.Template.Domain;

/// <summary>Dublê de teste: entidade do módulo Template que levanta um evento de domínio ao ser criada.</summary>
public sealed class TemplateItem : Entity
{
    private TemplateItem(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public static TemplateItem Create(string name)
    {
        var item = new TemplateItem(Guid.NewGuid(), name);
        item.Raise(new TemplateItemCreatedDomainEvent(item.Id, name));
        return item;
    }
}
