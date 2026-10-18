using System.Diagnostics.CodeAnalysis;

namespace Subsolo.Common.Application.Media;

/// <summary>SPEC-0022 — as três variantes do flyer e o hash (SHA-256, hexadecimal) do arquivo original.</summary>
[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Assinatura fixada pelo contrato da SPEC-0022 (§6).")]
public sealed record FlyerImages(byte[] Thumb, byte[] Full, byte[] Og, string ContentHash);
