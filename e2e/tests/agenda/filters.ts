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

const WEEKDAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

/** O dia da semana (0 = domingo) e os minutos desde a meia-noite de `now` no relógio de São Paulo. */
function saoPauloClock(now: Date): { weekday: number; minutes: number } {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: "America/Sao_Paulo",
    weekday: "short",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(now);
  const part = (type: Intl.DateTimeFormatPartTypes): string =>
    parts.find((p) => p.type === type)?.value ?? "";
  return {
    weekday: WEEKDAYS.indexOf(part("weekday")),
    minutes: Number(part("hour")) * 60 + Number(part("minute")),
  };
}

/** Dias de hoje (São Paulo) até o próximo domingo (0 no domingo). */
export function daysUntilSunday(now: Date = new Date()): number {
  return (7 - saoPauloClock(now).weekday) % 7;
}

/**
 * SPEC-0042 — os horários (`hh:mm`, São Paulo) dos dois shows de domingo da jornada "Esta semana" (SPEC-0025:E2E-01),
 * para nunca caírem no passado (a API recusa show no passado): fora do domingo e no domingo antes das 22:30, 23:00 e
 * 23:30; no domingo de 22:30 até 23:39, 23:58 e 23:59; a partir das 23:40, `null` (não há horário futuro dentro da
 * semana com margem para a preparação e o teste é pulado).
 */
export function weekShowTimes(now: Date): [string, string] | null {
  const { weekday, minutes } = saoPauloClock(now);
  if (weekday !== 0 || minutes < 22 * 60 + 30) {
    return ["23:00", "23:30"];
  }
  if (minutes < 23 * 60 + 40) {
    return ["23:58", "23:59"];
  }
  return null;
}

/** A data local (São Paulo) daqui a `days` dias no formato do cabeçalho da agenda (`dd/MM`). */
export function dayMonth(days: number): string {
  const [, month, day] = localDate(days).split("-");
  return `${day}/${month}`;
}
