"use client";

/**
 * SPEC-0030 — o conteúdo da página `/avisos/descadastrar?token=…`: manda o token a `POST
 * /api/v1/notifications/unsubscribe` (com `X-CSRF`) e confirma pelo tipo lido do próprio token (Emenda de 2026-10-23).
 * Esqueleto do G1: ainda não renderiza nada.
 */
export function UnsubscribeConfirmation({
  token,
}: {
  token: string | undefined;
}) {
  void token;
  return null;
}
