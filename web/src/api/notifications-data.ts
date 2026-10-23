import { headers } from "next/headers";
import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";
import type { NotificationPreferences } from "./notifications";

/**
 * SPEC-0030 — as preferências de aviso no servidor (`GET /api/v1/me/notification-preferences`, com o cookie da
 * requisição recebida repassado à API, como `getMyFollowsForPage`). Com a API fora do ar ou com erro, `undefined`: a
 * página segue de pé, com o aviso de indisponível no lugar dos interruptores.
 */
export async function getNotificationPreferencesForPage(): Promise<
  NotificationPreferences | undefined
> {
  try {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/me/notification-preferences", {
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store",
      headers: { cookie: (await headers()).get("cookie") ?? "" },
    });
    return data;
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return undefined;
  }
}
