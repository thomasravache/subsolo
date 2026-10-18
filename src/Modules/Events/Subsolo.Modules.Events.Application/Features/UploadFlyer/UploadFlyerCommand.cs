using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Events.Application.Features.UploadFlyer;

/// <summary>
/// SPEC-0022 — <c>POST /api/v1/flyers</c>: o arquivo enviado (<paramref name="Content"/>, que precisa permitir voltar
/// ao começo, com <paramref name="Length"/> bytes) por <paramref name="UploadedBy"/> (artista ou admin).
/// </summary>
public sealed record UploadFlyerCommand(Stream Content, long Length, Guid UploadedBy) : ICommand<FlyerUploadResult>;

/// <summary>SPEC-0022 — o flyer e se ele foi criado agora (201) ou já existia com o mesmo conteúdo (200).</summary>
public sealed record FlyerUploadResult(FlyerUploadedResponse Flyer, bool Created);

/// <summary>SPEC-0022 — corpo de 201 e 200: o id (SHA-256 do original) e as URLs públicas das variantes.</summary>
public sealed record FlyerUploadedResponse(string FlyerId, string ThumbUrl, string FullUrl, string OgUrl);
