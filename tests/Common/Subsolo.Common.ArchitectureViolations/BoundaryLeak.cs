// Viola: o módulo Peeker usa o Domain do módulo Peeked (só o IntegrationEvents de outro módulo é permitido).
namespace Subsolo.Modules.Peeker.Application
{
    public sealed class ListBands
    {
        public Peeked.Domain.Band? Band { get; set; }
    }
}

namespace Subsolo.Modules.Peeked.Domain
{
    public sealed class Band;
}
