import type { Browser } from "@playwright/test";

import { expect } from "../fixtures";
import { appURL } from "../agenda/environment";
import {
  approvedProducer,
  localDate,
  mutate,
  uniqueSuffix,
  uploadFlyer,
  type Producer,
} from "./journey";

/**
 * SPEC-0024 — preparação das jornadas da página do show na pilha do Compose: um produtor aprovado (num contexto só da
 * preparação, para a página do teste continuar anônima, como o fã) cadastra pela API um show com flyer na cidade nova
 * dele e, se pedido, o cancela. Nomes e cidade próprios de cada execução.
 */
export interface PublishedShow {
  id: string;
  slug: string;
  title: string;
  /** `/{cidade}/{show}`. */
  path: string;
  flyerId: string;
  /** Início local (São Paulo) no formato do cadastro (`aaaa-mm-ddThh:mm`). */
  startsLocal: string;
  venue: string;
  lineup: string[];
  price: string;
  ticketUrl: string | null;
}

export interface ShowSetup {
  producer: Producer;
  shows: PublishedShow[];
}

export interface ShowSpec {
  title: string;
  ticketUrl?: string | null;
  cancelled?: boolean;
}

/** O produtor e os shows de `specs` (cada um com um flyer próprio), cadastrados pela API. */
export async function publishShows(
  browser: Browser,
  prefix: string,
  specs: (suffix: string) => ShowSpec[],
): Promise<ShowSetup> {
  const context = await browser.newContext({
    baseURL: appURL,
    ignoreHTTPSErrors: true,
  });
  try {
    const page = await context.newPage();
    const producer = await approvedProducer(page, browser, prefix);
    const shows: PublishedShow[] = [];
    let day = 18;
    for (const spec of specs(uniqueSuffix().slice(-5))) {
      const flyer = await uploadFlyer(page, spec.title);
      const startsLocal = `${localDate(day++)}T22:00`;
      const venue = "Porão do Rock";
      const lineup = ["Cova Rasa", "Necrocorpo"];
      const price = "R$ 30 / R$ 40 na hora";
      const ticketUrl =
        spec.ticketUrl === undefined
          ? "https://ingressos.example/noite-da-peste"
          : spec.ticketUrl;
      const created = (await mutate(page.request, "post", "/api/v1/shows", {
        organizerProfileId: producer.profileId,
        title: spec.title,
        city: producer.city.slug,
        startsLocal,
        venue: { name: venue },
        lineup: lineup.map((name) => ({ name })),
        styles: ["death"],
        price,
        ticketUrl,
        flyerId: flyer.flyerId,
      })) as { id: string; slug: string };
      if (spec.cancelled) {
        // 200 sem corpo: fora do `mutate`, que lê o JSON da resposta.
        const antiforgery = await page.request.get("/api/v1/antiforgery");
        const { token } = (await antiforgery.json()) as { token: string };
        const cancel = await page.request.post(
          `/api/v1/shows/${created.id}/status`,
          { data: { status: "cancelled" }, headers: { "X-CSRF": token } },
        );
        expect(
          cancel.ok(),
          `POST /api/v1/shows/${created.id}/status: ${cancel.status()} ${await cancel.text()}`,
        ).toBe(true);
      }
      shows.push({
        id: created.id,
        slug: created.slug,
        title: spec.title,
        path: `/${producer.city.slug}/${created.slug}`,
        flyerId: flyer.flyerId,
        startsLocal,
        venue,
        lineup,
        price,
        ticketUrl,
      });
    }
    return { producer, shows };
  } finally {
    await context.close();
  }
}

/** O instante (ms) de `startsLocal` em São Paulo (UTC−3, sem horário de verão). */
export function instantOf(startsLocal: string): number {
  return Date.parse(`${startsLocal}:00-03:00`);
}

/** Os `<meta>` do HTML como lista de atributos (nome → valor), na ordem. */
export function metaTags(html: string): Record<string, string>[] {
  return [...html.matchAll(/<meta\b([^>]*)>/gi)].map((match) => {
    const attributes: Record<string, string> = {};
    for (const attribute of (match[1] ?? "").matchAll(
      /([a-zA-Z:-]+)\s*=\s*"([^"]*)"/g,
    )) {
      attributes[(attribute[1] ?? "").toLowerCase()] = decodeEntities(
        attribute[2] ?? "",
      );
    }
    return attributes;
  });
}

/** O `content` do `<meta property|name=key>` (ou `undefined`). */
export function meta(html: string, key: string): string | undefined {
  return metaTags(html).find(
    (tag) => tag["property"] === key || tag["name"] === key,
  )?.["content"];
}

/** O `href` do `<link rel="canonical">` (ou `undefined`). */
export function canonicalOf(html: string): string | undefined {
  const tag = [...html.matchAll(/<link\b([^>]*)>/gi)]
    .map((match) => match[1] ?? "")
    .find((attributes) => /\brel\s*=\s*"canonical"/i.test(attributes));
  const href = tag ? /\bhref\s*=\s*"([^"]*)"/i.exec(tag)?.[1] : undefined;
  return href === undefined ? undefined : decodeEntities(href);
}

/** O texto do `<title>` (ou `undefined`). */
export function titleOf(html: string): string | undefined {
  const match = /<title\b[^>]*>([\s\S]*?)<\/title>/i.exec(html);
  return match ? decodeEntities(match[1] ?? "") : undefined;
}

function decodeEntities(text: string): string {
  return text
    .replaceAll("&quot;", '"')
    .replaceAll("&#x27;", "'")
    .replaceAll("&#39;", "'")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&amp;", "&");
}
