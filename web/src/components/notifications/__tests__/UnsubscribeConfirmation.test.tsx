// @vitest-environment jsdom
import { createHmac } from "node:crypto";

import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/**
 * SPEC-0030 UT-03 (página de descadastro, Emenda de 2026-10-23) — `UnsubscribeConfirmation`, o conteúdo de
 * `/avisos/descadastrar?token=…`, com a API respondendo por um `fetch` falso:
 * - token de `digest` aceito (204) → "Pronto: você não recebe mais o resumo diário por e-mail.";
 * - token de `reminder` aceito (204) → "Pronto: você não recebe mais o lembrete na véspera por e-mail.";
 * - o token vai em `POST /api/v1/notifications/unsubscribe` (`{token}`, com `X-CSRF`);
 * - a API recusa (400 `notifications.invalid_token`), o token não tem o formato ou falta → "Este link de descadastro
 *   é inválido.".
 * O tipo é lido do próprio token: `{payload}.{assinatura}`, payload = base64url de `{"u":"<id>","k":"digest"|"reminder"}`.
 */

type Call = { method: string; path: string; headers: Headers; body: string };

const calls: Call[] = [];

const texts = {
  digest: "Pronto: você não recebe mais o resumo diário por e-mail.",
  reminder: "Pronto: você não recebe mais o lembrete na véspera por e-mail.",
  invalid: "Este link de descadastro é inválido.",
} as const;

function token(kind: "digest" | "reminder"): string {
  const payload = Buffer.from(
    JSON.stringify({ u: "0199f0a0-0000-7000-8000-000000000001", k: kind }),
    "utf8",
  ).toString("base64url");
  const signature = createHmac("sha256", "chave-de-teste")
    .update(payload, "ascii")
    .digest("base64url");
  return `${payload}.${signature}`;
}

/** A API: antiforgery e o descadastro (`status` na resposta). */
function stubApi(status: number) {
  vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
    const request =
      typeof input === "object" && "method" in input ? input : undefined;
    const url = new URL(
      typeof input === "string"
        ? input
        : input instanceof URL
          ? input.href
          : input.url,
      window.location.href,
    );
    const method = (init?.method ?? request?.method ?? "GET").toUpperCase();
    const body =
      typeof init?.body === "string"
        ? init.body
        : request && method !== "GET"
          ? await request.clone().text()
          : "";
    calls.push({
      method,
      path: url.pathname,
      headers: new Headers(init?.headers ?? request?.headers),
      body,
    });
    if (url.pathname === "/api/v1/antiforgery") {
      return Response.json({ token: "csrf-token" });
    }
    if (url.pathname === "/api/v1/notifications/unsubscribe") {
      return status === 204
        ? new Response(null, { status: 204 })
        : Response.json(
            { status, code: "notifications.invalid_token" },
            {
              status,
              headers: { "Content-Type": "application/problem+json" },
            },
          );
    }
    throw new TypeError("fetch failed");
  });
}

const unsubscribeCalls = () =>
  calls.filter((call) => call.path === "/api/v1/notifications/unsubscribe");

beforeEach(() => {
  calls.length = 0;
  const Native = globalThis.Request;
  vi.stubGlobal(
    "Request",
    class extends Native {
      constructor(input: RequestInfo | URL, init?: RequestInit) {
        super(
          typeof input === "string"
            ? new URL(input, window.location.href)
            : input,
          init,
        );
      }
    },
  );
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function renderPage(value: string | undefined) {
  const { UnsubscribeConfirmation } =
    await import("@/components/notifications/UnsubscribeConfirmation");
  return render(<UnsubscribeConfirmation token={value} />);
}

describe("UnsubscribeConfirmation — descadastro", () => {
  it.each([
    ["digest", texts.digest],
    ["reminder", texts.reminder],
  ] as const)(
    "SPEC-0030:UT-03 token de %s aceito manda o token com X-CSRF e confirma o tipo",
    async (kind, text) => {
      stubApi(204);
      const value = token(kind);

      await renderPage(value);

      expect(await screen.findByText(text)).toBeTruthy();
      await waitFor(() => expect(unsubscribeCalls()).toHaveLength(1));
      const call = unsubscribeCalls()[0]!;
      expect(call.method).toBe("POST");
      expect(call.headers.get("X-CSRF")).toBe("csrf-token");
      expect(JSON.parse(call.body)).toEqual({ token: value });
    },
  );

  it("SPEC-0030:UT-03 token recusado pela API mostra que o link é inválido", async () => {
    stubApi(400);

    await renderPage(token("digest"));

    expect(await screen.findByText(texts.invalid)).toBeTruthy();
    expect(screen.queryByText(texts.digest)).toBeNull();
  });

  it.each([["sem-ponto-nem-assinatura"], [""], [undefined]])(
    "SPEC-0030:UT-03 token fora do formato (%s) mostra que o link é inválido",
    async (value) => {
      stubApi(400);

      await renderPage(value);

      expect(await screen.findByText(texts.invalid)).toBeTruthy();
      expect(screen.queryByText(/^Pronto:/)).toBeNull();
    },
  );
});
