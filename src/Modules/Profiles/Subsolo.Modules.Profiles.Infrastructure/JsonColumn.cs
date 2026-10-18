using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>
/// SPEC-0021 — conversão de um valor imutável (record) para uma coluna <c>jsonb</c>: os links do perfil e os dados do
/// perfil pedido (<c>payload</c>). A comparação é pelo JSON, para o rastreador notar a troca do valor.
/// </summary>
internal static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ValueConverter<T, string> Converter<T>()
        where T : class? =>
        new(value => JsonSerializer.Serialize(value, Options), json => JsonSerializer.Deserialize<T>(json, Options)!);

    public static ValueComparer<T> Comparer<T>()
        where T : class? =>
        new(
            (left, right) => Serialize(left) == Serialize(right),
            value => Serialize(value).GetHashCode(StringComparison.Ordinal),
            value => value);

    private static string Serialize<T>(T? value) => value is null ? string.Empty : JsonSerializer.Serialize(value, Options);
}
