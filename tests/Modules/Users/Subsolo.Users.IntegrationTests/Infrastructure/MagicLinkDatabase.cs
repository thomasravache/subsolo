using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0016 — leitura direta de <c>users.magic_link_tokens</c>: cada linha inteira em JSON (as colunas são da
/// implementação; o teste procura o hash do token em qualquer coluna e confere que o token não aparece).
/// </summary>
internal static class MagicLinkDatabase
{
    public const string Table = "users.magic_link_tokens";

    /// <summary>Todas as linhas da tabela, cada uma como JSON (<c>row_to_json</c>; <c>bytea</c> vira <c>\x…</c>).</summary>
    public static async Task<IReadOnlyList<string>> RowsAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<string>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand($"select row_to_json(t)::text from {Table} t", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    /// <summary>
    /// SPEC-0019 — grava direto na tabela um link pendente (não usado, válido por 15 minutos) para
    /// <paramref name="normalizedEmail"/>, com um hash aleatório; preparação de teste, sem e-mail.
    /// </summary>
    public static async Task InsertPendingAsync(string connectionString, string normalizedEmail, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            $"""
            insert into {Table} (token_hash, email, return_to, created_at_utc, expires_at_utc, used_at_utc)
            values (@hash, @email, '/conta', now(), now() + interval '15 minutes', null)
            """,
            connection);
        command.Parameters.AddWithValue("hash", Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
        command.Parameters.AddWithValue("email", normalizedEmail);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>SPEC-0019 — os e-mails dos links gravados na tabela (um por linha).</summary>
    public static async Task<IReadOnlyList<string>> EmailsAsync(string connectionString, CancellationToken ct)
    {
        var emails = new List<string>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand($"select email from {Table}", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            emails.Add(reader.GetString(0));
        }

        return emails;
    }

    /// <summary>A linha cujo valor de alguma coluna é o hash SHA-256 de <paramref name="token"/>, ou nulo.</summary>
    public static string? RowOf(IEnumerable<string> rows, string token) =>
        rows.FirstOrDefault(row => Values(row).Any(value => IsHashOf(value, token)));

    /// <summary>O valor é o SHA-256 do token (do texto ou dos bytes), em hex, base64 ou base64url.</summary>
    public static bool IsHashOf(string stored, string token)
    {
        var normalized = stored.StartsWith("\\x", StringComparison.Ordinal) ? stored[2..] : stored;
        return Candidates(token).Any(candidate =>
            string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.TrimEnd('='), normalized.TrimEnd('='), StringComparison.Ordinal));
    }

    private static IEnumerable<string> Values(string row)
    {
        using var document = JsonDocument.Parse(row);
        return [.. document.RootElement.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .Select(property => property.Value.GetString()!)];
    }

    private static IEnumerable<string> Candidates(string token)
    {
        var hashes = new List<byte[]> { SHA256.HashData(Encoding.UTF8.GetBytes(token)) };
        if (token.Length == 64 && token.All(Uri.IsHexDigit))
        {
            hashes.Add(SHA256.HashData(Convert.FromHexString(token)));
        }
        else if (Base64Url.IsValid(token))
        {
            hashes.Add(SHA256.HashData(Base64Url.DecodeFromChars(token)));
        }

        foreach (var hash in hashes)
        {
            yield return Convert.ToHexString(hash);
            yield return Convert.ToBase64String(hash);
            yield return Base64Url.EncodeToString(hash);
        }
    }
}
