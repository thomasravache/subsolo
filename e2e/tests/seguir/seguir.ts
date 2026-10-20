import type { Browser } from "@playwright/test";

import { appURL } from "../agenda/environment";
import { signIn } from "../auth/journey";
import { slugOf } from "../perfis/perfil-publico";
import { adminEmail, mutate, uniqueSuffix } from "../shows/journey";

/**
 * SPEC-0027 — preparação das jornadas de seguir na pilha do Compose, pela API (num contexto só do admin, para a página
 * do teste continuar com a sessão do fã ou anônima): o admin cria a cidade `Curitiba <sufixo>` e, nela, uma banda sem
 * dono (perfil aprovado). Nomes ASCII, com sufixo próprio por execução: a pilha pode ser reaproveitada e as contagens de
 * seguidores começam do zero.
 */
export interface FollowSetup {
  city: { slug: string; name: string };
  band: { id: string; name: string; slug: string; path: string };
}

export async function prepareFollowTargets(
  browser: Browser,
  options: { withBand?: boolean } = {},
): Promise<FollowSetup> {
  const admin = await browser.newContext({
    baseURL: appURL,
    ignoreHTTPSErrors: true,
  });
  try {
    const adminPage = await admin.newPage();
    await signIn(adminPage, adminEmail, "/conta");
    const suffix = uniqueSuffix();
    const cityName = `Curitiba ${suffix}`;
    const city = (await mutate(
      adminPage.request,
      "post",
      "/api/v1/admin/cities",
      {
        name: cityName,
        state: "PR",
        timeZone: "America/Sao_Paulo",
        slug: null,
      },
    )) as { slug: string };
    const bandName = `Necrose ${suffix}`;
    const bandId =
      options.withBand === false
        ? ""
        : (
            (await mutate(adminPage.request, "post", "/api/v1/admin/profiles", {
              type: "band",
              name: bandName,
              city: city.slug,
              styles: ["death"],
              links: {},
            })) as { profileId: string }
          ).profileId;
    return {
      city: { slug: city.slug, name: cityName },
      band: {
        id: bandId,
        name: bandName,
        slug: slugOf(bandName),
        path: `/banda/${slugOf(bandName)}`,
      },
    };
  } finally {
    await admin.close();
  }
}
