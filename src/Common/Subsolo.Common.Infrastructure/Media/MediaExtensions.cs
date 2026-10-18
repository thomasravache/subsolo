using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Subsolo.Common.Application.Media;

namespace Subsolo.Common.Infrastructure.Media;

/// <summary>SPEC-0022 (ADR-0015) — registro da mídia (disco em <c>Media:Root</c> e NetVips) e a entrega em desenvolvimento.</summary>
public static class MediaExtensions
{
    /// <summary><see cref="IFileStorage"/> em disco e <see cref="IImageProcessor"/> com o NetVips, ambos únicos no processo.</summary>
    public static IServiceCollection AddMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MediaOptions>().Bind(configuration.GetSection(MediaOptions.SectionName));
        services.TryAddSingleton<IFileStorage, DiskFileStorage>();
        services.TryAddSingleton<IImageProcessor, NetVipsImageProcessor>();
        return services;
    }

    /// <summary>
    /// Só no desenvolvimento (no Compose e em produção quem serve <c>/media</c> é o Caddy): serve
    /// <c>/media/flyers/*</c> do <c>Media:Root/flyers</c> para qualquer um, com cache imutável e <c>nosniff</c>; o que
    /// não existe (ou sairia do diretório) dá 404 ali mesmo, antes da autenticação. Sem <c>Media:Root</c>, nada é servido.
    /// </summary>
    public static IApplicationBuilder UseMediaFiles(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var options = app.ApplicationServices.GetRequiredService<IOptions<MediaOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.Root))
        {
            return app;
        }

        var flyers = Path.Combine(DiskFileStorage.RootOf(options), MediaPaths.Flyers);
        Directory.CreateDirectory(flyers);
        var files = new PhysicalFileProvider(flyers);
        return app.Map(MediaPaths.PublicUrl(MediaPaths.Flyers), branch =>
        {
            branch.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = files,
                OnPrepareResponse = context =>
                {
                    context.Context.Response.Headers.CacheControl = MediaPaths.ImmutableCacheControl;
                    context.Context.Response.Headers.XContentTypeOptions = "nosniff";
                },
            });
            branch.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            });
        });
    }
}
