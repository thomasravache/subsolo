import type { Browser, BrowserContext, Page } from "@playwright/test";

import { appURL } from "./environment";
import {
  approvedProducer,
  localDate,
  mutate,
  uniqueSuffix,
  uploadFlyer,
  type Producer,
} from "../shows/journey";

/**
 * SPEC-0025 — preparação das jornadas dos filtros da agenda na pilha do Compose: um produtor aprovado (num contexto à
 * parte, para a página do teste continuar anônima, como o fã) cadastra pela API, na cidade nova dele, shows com estilos
 * e datas escolhidos pelo teste (cada um com um flyer próprio). O contexto do produtor fica aberto para o teste usá-lo
 * como artista; quem chama fecha com `close()`.
 */
export interface AgendaShowSpec {
  title: string;
  /** Dias a partir de hoje (data local de São Paulo). */
  day: number;
  /** Hora local (`hh:mm`). */
  time: string;
  styles: string[];
}

export interface AgendaShow extends AgendaShowSpec {
  id: string;
  slug: string;
  /** `aaaa-mm-dd` local. */
  date: string;
}

export interface AgendaSetup {
  producer: Producer;
  /** Página logada do produtor (papel `artist`). */
  producerPage: Page;
  shows: AgendaShow[];
  close(): Promise<void>;
}

export async function publishAgenda(
  browser: Browser,
  prefix: string,
  specs: (suffix: string) => AgendaShowSpec[],
): Promise<AgendaSetup> {
  const context: BrowserContext = await browser.newContext({
    baseURL: appURL,
    ignoreHTTPSErrors: true,
  });
  try {
    const page = await context.newPage();
    const producer = await approvedProducer(page, browser, prefix);
    const shows: AgendaShow[] = [];
    for (const spec of specs(uniqueSuffix().slice(-5))) {
      const flyer = await uploadFlyer(page, spec.title);
      const date = localDate(spec.day);
      const created = (await mutate(page.request, "post", "/api/v1/shows", {
        organizerProfileId: producer.profileId,
        title: spec.title,
        city: producer.city.slug,
        startsLocal: `${date}T${spec.time}`,
        venue: { name: "Porão do Rock" },
        lineup: [{ name: "Cova Rasa" }, { name: "Necrocorpo" }],
        styles: spec.styles,
        price: "R$ 30",
        ticketUrl: null,
        flyerId: flyer.flyerId,
      })) as { id: string; slug: string };
      shows.push({ ...spec, id: created.id, slug: created.slug, date });
    }
    return {
      producer,
      producerPage: page,
      shows,
      close: () => context.close(),
    };
  } catch (error) {
    await context.close();
    throw error;
  }
}

/** Dias de hoje (São Paulo) até o próximo domingo (0 no domingo). */
export function daysUntilSunday(): number {
  const weekday = new Date(`${localDate(0)}T12:00:00Z`).getUTCDay();
  return (7 - weekday) % 7;
}
