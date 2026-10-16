using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure.Messaging;

namespace Subsolo.Common.Infrastructure.Database;

/// <summary>SPEC-0007 — aplica as migrações dos módulos registrados (o host chama só em Development).</summary>
public static class ModuleMigrationExtensions
{
    public static async Task MigrateModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        foreach (var module in services.GetServices<ModuleDescriptor>())
        {
            await using var scope = services.CreateAsyncScope();
            var db = (DbContext)scope.ServiceProvider.GetRequiredService(module.DbContextType);
            await db.Database.MigrateAsync(cancellationToken);
        }
    }
}
