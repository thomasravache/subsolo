import type { Browser } from "@playwright/test";

import { appURL } from "../agenda/environment";
import { signIn } from "../auth/journey";
import {
  adminEmail,
  approvedProducer,
  localDate,
  mutate,
  uniqueSuffix,
  uploadFlyer,
  type Producer,
} from "../shows/journey";

/**
 * SPEC-0026 — preparação das jornadas do perfil público na pilha do Compose, pela API (num contexto só da preparação,
 * para a página do teste continuar anônima, como o fã): um produtor aprovado (cidade nova, SPEC-0023); o admin cria,
 * sem dono, uma banda com os três links, uma casa e uma banda sem shows; o produtor cadastra um show com a casa e a banda
 * pelos perfis (e uma banda por nome) e flyer. Nomes ASCII, com sufixo próprio por execução: o slug do perfil é o nome
 * em minúsculas com hífens (SPEC-0021).
 */
export interface PublicProfileRef {
  name: string;
  slug: string;
  /** `/banda/{slug}` ou `/casa/{slug}`. */
  path: string;
}

export interface ProfileSetup {
  producer: Producer;
  band: PublicProfileRef & {
    instagram: string;
    bandcamp: string;
    metalArchives: string;
  };
  venue: PublicProfileRef;
  bandWithoutShows: PublicProfileRef;
  show: { title: string; path: string; flyerId: string };
}

/** O slug de um nome ASCII (minúsculas, cada sequência fora de `[a-z0-9]` vira um hífen). */
export function slugOf(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

export async function publishProfiles(
  browser: Browser,
  prefix: string,
): Promise<ProfileSetup> {
  const context = await browser.newContext({
    baseURL: appURL,
    ignoreHTTPSErrors: true,
  });
  const adminContext = await browser.newContext({
    baseURL: appURL,
    ignoreHTTPSErrors: true,
  });
  try {
    const page = await context.newPage();
    const producer = await approvedProducer(page, browser, prefix);
    const adminPage = await adminContext.newPage();
    await signIn(adminPage, adminEmail, "/conta");

    const suffix = uniqueSuffix();
    const handle = `necrose${suffix}`;
    const band = {
      name: `Necrose ${suffix}`,
      instagram: `https://www.instagram.com/${handle}`,
      bandcamp: `https://${handle}.bandcamp.com`,
      metalArchives: `https://www.metal-archives.com/bands/Necrose/${suffix}`,
    };
    const venueName = `Porao Imundo ${suffix}`;
    const quietName = `Silencio Fetido ${suffix}`;
    const ownerless = async (
      type: "band" | "venue",
      name: string,
      links: Record<string, string> = {},
    ) =>
      (
        (await mutate(adminPage.request, "post", "/api/v1/admin/profiles", {
          type,
          name,
          city: producer.city.slug,
          styles: type === "band" ? ["death"] : [],
          links,
        })) as { profileId: string }
      ).profileId;
    const bandId = await ownerless("band", band.name, {
      instagram: band.instagram,
      bandcamp: band.bandcamp,
      metalArchives: band.metalArchives,
    });
    const venueId = await ownerless("venue", venueName);
    await ownerless("band", quietName);

    const title = `Noite Podre ${suffix.slice(-5)}`;
    const flyer = await uploadFlyer(page, title);
    const created = (await mutate(page.request, "post", "/api/v1/shows", {
      organizerProfileId: producer.profileId,
      title,
      city: producer.city.slug,
      startsLocal: `${localDate(21)}T22:00`,
      venue: { profileId: venueId },
      lineup: [{ profileId: bandId }, { name: "Cova Rasa" }],
      styles: ["death"],
      price: "R$ 30",
      ticketUrl: null,
      flyerId: flyer.flyerId,
    })) as { id: string; slug: string };

    const ref = (kind: "banda" | "casa", name: string): PublicProfileRef => ({
      name,
      slug: slugOf(name),
      path: `/${kind}/${slugOf(name)}`,
    });
    return {
      producer,
      band: { ...band, ...ref("banda", band.name) },
      venue: ref("casa", venueName),
      bandWithoutShows: ref("banda", quietName),
      show: {
        title,
        path: `/${producer.city.slug}/${created.slug}`,
        flyerId: flyer.flyerId,
      },
    };
  } finally {
    await adminContext.close();
    await context.close();
  }
}
