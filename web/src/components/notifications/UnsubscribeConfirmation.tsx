"use client";

import { useEffect, useState } from "react";

import { unsubscribeEmail } from "@/api/notifications";

import { noticeMessages } from "./messages";

type Kind = keyof typeof noticeMessages.unsubscribed;

/**
 * O tipo de e-mail do token (`{payload}.{assinatura}`, payload = base64url de `{"u":"<id>","k":"digest"|"reminder"}`),
 * ou `undefined` fora do formato. Só para o texto da confirmação: quem confere a assinatura é a API.
 */
export function tokenKind(token: string | undefined): Kind | undefined {
  const parts = token?.split(".");
  if (parts?.length !== 2 || !parts[0] || !parts[1]) {
    return undefined;
  }
  try {
    const base64 = parts[0].replaceAll("-", "+").replaceAll("_", "/");
    const payload = JSON.parse(
      atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, "=")),
    ) as { k?: unknown };
    return payload.k === "digest" || payload.k === "reminder"
      ? payload.k
      : undefined;
  } catch {
    return undefined;
  }
}

/**
 * SPEC-0030 — o conteúdo da página `/avisos/descadastrar?token=…`: manda o token a `POST
 * /api/v1/notifications/unsubscribe` (com `X-CSRF`) e confirma pelo tipo lido do próprio token (Emenda de 2026-10-23):
 * "Pronto: você não recebe mais o resumo diário por e-mail." ou "… o lembrete na véspera por e-mail."; token fora do
 * formato ou recusado pela API → "Este link de descadastro é inválido.".
 */
export function UnsubscribeConfirmation({
  token,
}: {
  token: string | undefined;
}) {
  const kind = tokenKind(token);
  const [result, setResult] = useState<"pending" | "done" | "invalid">(
    kind ? "pending" : "invalid",
  );

  useEffect(() => {
    if (!kind || !token) {
      return;
    }
    let current = true;
    void unsubscribeEmail(token).then((response) => {
      if (current) {
        setResult(response.ok ? "done" : "invalid");
      }
    });
    return () => {
      current = false;
    };
  }, [kind, token]);

  const text =
    result === "done" && kind
      ? noticeMessages.unsubscribed[kind]
      : result === "invalid"
        ? noticeMessages.invalidLink
        : noticeMessages.unsubscribing;

  return (
    <p role="status" className="text-lg text-text">
      {text}
    </p>
  );
}
