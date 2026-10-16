// Viola: o Application depende do Infrastructure do próprio módulo.
namespace Subsolo.Modules.ApplicationLeak.Application
{
    public sealed class CreateShow
    {
        public Infrastructure.ShowStore? Store { get; set; }
    }
}

namespace Subsolo.Modules.ApplicationLeak.Infrastructure
{
    public sealed class ShowStore;
}
