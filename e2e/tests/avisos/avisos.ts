import { createECDH, createHmac, randomBytes, randomUUID } from "node:crypto";

import type { APIRequestContext, Page } from "@playwright/test";

/**
 * SPEC-0030 — apoio das jornadas de avisos na pilha do Compose.
 *
 * - A chave do token de descadastro da pilha de teste (`E2E_UNSUBSCRIBE_KEY`, com este padrão): o `tools/compose-up.sh`
 *   a passa à API como `Notifications__Unsubscribe__SigningKey` (só do servidor; a de produção entra no go-live). O teste
 *   gera um link válido com o formato do token documentado em `UnsubscribeOptions` (§6): `{payload}.{assinatura}`, com
 *   `payload` = base64url do JSON `{"u":"<id>","k":"digest"|"reminder"}` e `assinatura` = base64url do HMAC-SHA256 do
 *   texto de `payload` com a chave.
 * - O Chromium do Playwright não fala com o serviço de push do Google (FCM): o `pushManager.subscribe` real falha. O
 *   script de página {@link fakePushService} troca só o `subscribe`/`getSubscription` do `PushManager` por uma inscrição
 *   com um endpoint do FCM e chaves P-256 válidas geradas aqui; o service worker (`/sw.js`), o manifest, a permissão, a
 *   API e o banco são os de verdade.
 */
export const unsubscribeKey =
  process.env.E2E_UNSUBSCRIBE_KEY ?? "chave-descadastro-e2e-subsolo-local";

export const followingPath = "/seguindo";

export const unsubscribePath = "/avisos/descadastrar";

export const preferencesPath = "/api/v1/me/notification-preferences";

/** Textos e nomes acessíveis do §6. */
export const texts = {
  activate: "Ativar avisos",
  active: "Avisos ativos neste aparelho",
  blocked: "Os avisos estão bloqueados no navegador.",
  newShow: "Show novo de quem eu sigo (na hora)",
  digest: "Resumo diário por e-mail",
  reminderPush: "Lembrete na véspera (no celular)",
  reminderEmail: "Lembrete na véspera (por e-mail)",
} as const;

function base64Url(bytes: Buffer): string {
  return bytes.toString("base64url");
}

/** Um token de descadastro válido para o usuário e o tipo de e-mail. */
export function unsubscribeToken(
  userId: string,
  kind: "digest" | "reminder",
): string {
  const payload = base64Url(
    Buffer.from(JSON.stringify({ u: userId, k: kind }), "utf8"),
  );
  const signature = base64Url(
    createHmac("sha256", Buffer.from(unsubscribeKey, "utf8"))
      .update(payload, "ascii")
      .digest(),
  );
  return `${payload}.${signature}`;
}

/** Uma inscrição de navegador de teste (endpoint do FCM, chaves P-256 e auth válidas). */
export function fakeSubscription() {
  const ecdh = createECDH("prime256v1");
  ecdh.generateKeys();
  return {
    endpoint: `https://fcm.googleapis.com/fcm/send/e2e-${randomUUID()}`,
    keys: {
      p256dh: base64Url(ecdh.getPublicKey()),
      auth: base64Url(randomBytes(16)),
    },
  };
}

/**
 * Troca o `subscribe`/`getSubscription` do `PushManager` (só eles) por uma inscrição fixa; anota em
 * `window.__subsoloPush` as opções recebidas, para o teste conferir a chave pública.
 */
export async function fakePushService(page: Page): Promise<void> {
  const subscription = fakeSubscription();
  await page.addInitScript((json) => {
    const state: { subscribed: boolean; options?: unknown } = {
      subscribed: false,
    };
    (window as unknown as { __subsoloPush: typeof state }).__subsoloPush =
      state;
    if (!("PushManager" in window)) {
      return;
    }
    const fake = {
      endpoint: json.endpoint,
      expirationTime: null,
      options: { userVisibleOnly: true, applicationServerKey: null },
      getKey: () => null,
      toJSON: () => json,
      unsubscribe: async () => {
        state.subscribed = false;
        return true;
      },
    };
    PushManager.prototype.subscribe = async function (options) {
      state.subscribed = true;
      state.options = options;
      return fake as unknown as PushSubscription;
    };
    PushManager.prototype.getSubscription = async function () {
      return state.subscribed ? (fake as unknown as PushSubscription) : null;
    };
  }, subscription);
}

/** Faz o navegador responder "negado" à permissão de notificação (estado e pedido). */
export async function denyNotifications(page: Page): Promise<void> {
  await page.addInitScript(() => {
    if (!("Notification" in window)) {
      return;
    }
    Object.defineProperty(Notification, "permission", {
      configurable: true,
      get: () => "denied",
    });
    Notification.requestPermission = async () => "denied";
  });
}

/** As preferências pela API, com a sessão da página. */
export async function preferences(
  request: APIRequestContext,
): Promise<Record<string, unknown>> {
  const response = await request.get(preferencesPath);
  if (!response.ok()) {
    throw new Error(`GET ${preferencesPath}: ${response.status()}`);
  }
  return (await response.json()) as Record<string, unknown>;
}

/** O id do usuário logado (`GET /api/v1/me`). */
export async function myId(request: APIRequestContext): Promise<string> {
  const response = await request.get("/api/v1/me");
  if (!response.ok()) {
    throw new Error(`GET /api/v1/me: ${response.status()}`);
  }
  return ((await response.json()) as { id: string }).id;
}
