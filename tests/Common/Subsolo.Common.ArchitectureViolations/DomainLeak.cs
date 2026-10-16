using Microsoft.EntityFrameworkCore;

// Viola: o Domain depende do Infrastructure do próprio módulo e do EF Core.
namespace Subsolo.Modules.DomainLeak.Domain
{
    public sealed class Show
    {
        public Infrastructure.ShowRecord? Record { get; set; }

        public DbSet<Show>? Shows { get; set; }
    }
}

namespace Subsolo.Modules.DomainLeak.Infrastructure
{
    public sealed class ShowRecord;
}
