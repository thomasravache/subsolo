import {
  browserApi as api,
  csrfHeaders,
  failure,
  guarded,
  type ApiResult,
} from "./browser";
import type { components } from "./schema";

/**
 * SPEC-0030 — chamadas do navegador à API de avisos (§6), pela base comum de `./browser` (mesmo domínio, cookie de
 * sessão HttpOnly, `X-CSRF` em toda mutação): a chave pública VAPID, a inscrição do aparelho, as preferências e o
 * descadastro do e-mail (este sem sessão).
 */
export type NotificationPreferences =
  components["schemas"]["NotificationPreferencesResponse"];

/** Os quatro interruptores (o corpo do `PUT`). */
export type PreferenceFlags = Omit<NotificationPreferences, "pushDevices">;

/** A inscrição do navegador como a API recebe (`PushSubscription.toJSON()`, só endpoint e chaves). */
export interface PushSubscriptionBody {
  endpoint: string;
  keys: { p256dh: string; auth: string };
}

/** `GET /api/v1/push/public-key`: a chave pública VAPID (base64url). */
export function getPushPublicKey(): Promise<ApiResult<string>> {
  return guarded(async () => {
    const { data, response, error } = await api.GET("/api/v1/push/public-key", {
      cache: "no-store",
    });
    return data
      ? { ok: true, value: data.publicKey }
      : failure(response.status, error);
  });
}

/** `POST /api/v1/me/push-subscriptions` (204). */
export function savePushSubscription(
  subscription: PushSubscriptionBody,
): Promise<ApiResult> {
  return guarded(async () => {
    const { response, error } = await api.POST(
      "/api/v1/me/push-subscriptions",
      { body: subscription, headers: await csrfHeaders() },
    );
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}

/** `GET /api/v1/me/notification-preferences`. */
export function getNotificationPreferences(): Promise<
  ApiResult<NotificationPreferences>
> {
  return guarded(async () => {
    const { data, response, error } = await api.GET(
      "/api/v1/me/notification-preferences",
      { cache: "no-store" },
    );
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}

/** `PUT /api/v1/me/notification-preferences` com os quatro interruptores; devolve as preferências gravadas. */
export function updateNotificationPreferences(
  flags: PreferenceFlags,
): Promise<ApiResult<NotificationPreferences>> {
  return guarded(async () => {
    const { data, response, error } = await api.PUT(
      "/api/v1/me/notification-preferences",
      {
        body: {
          newShowPush: flags.newShowPush,
          dailyDigestEmail: flags.dailyDigestEmail,
          reminderPush: flags.reminderPush,
          reminderEmail: flags.reminderEmail,
        },
        headers: await csrfHeaders(),
      },
    );
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}

/** `POST /api/v1/notifications/unsubscribe` (sem sessão; 204 ou 400 `notifications.invalid_token`). */
export function unsubscribeEmail(token: string): Promise<ApiResult> {
  return guarded(async () => {
    const { response, error } = await api.POST(
      "/api/v1/notifications/unsubscribe",
      { body: { token }, headers: await csrfHeaders() },
    );
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}
