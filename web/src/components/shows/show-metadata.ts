import type { Metadata } from "next";

import type { ShowDetail } from "@/api/show-detail";

/** Medidas da variante `og` do flyer (ADR-0015). */
const ogImageSize = { width: 1200, height: 630 } as const;

/** Partes da data e da hora do show no fuso da cidade (pt-BR, relógio de 24 h). */
function localParts(iso: string, timeZone: string) {
  const parts = new Intl.DateTimeFormat("pt-BR", {
    timeZone,
    weekday: "short",
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(new Date(iso));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    (parts.find((item) => item.type === type)?.value ?? "").replace(".", "");
  return {
    weekday: part("weekday"),
    day: part("day"),
    month: part("month"),
    hour: part("hour"),
    minute: part("minute"),
  };
}

/** "{ddd dd/MM} {HH'h'mm}" no fuso da cidade (ex.: "sáb 14/11 21h00"). */
export function showWhen(iso: string, timeZone: string): string {
  const { weekday, day, month, hour, minute } = localParts(iso, timeZone);
  return `${weekday} ${day}/${month} ${hour}h${minute}`;
}

/** O line-up separado por " / ". */
export function lineupText(show: ShowDetail): string {
  return show.lineup.map((band) => band.name).join(" / ");
}

/** "{título} · {cidade} · Subsolo"; o cancelado leva o selo na frente (Emenda v2, c). */
export function showTitle(show: ShowDetail): string {
  const title = `${show.title} · ${show.city.name} · Subsolo`;
  return show.status === "cancelled" ? `Cancelado — ${title}` : title;
}

/** O endereço canônico `{App:PublicUrl}/{cidade}/{show}`. */
export function showUrl(show: ShowDetail, publicUrl: string): string {
  return `${publicUrl.replace(/\/+$/, "")}/${show.city.slug}/${show.slug}`;
}

/**
 * SPEC-0024 — os metadados da página `/{cidade}/{show}` (§6): `<title>`, `description` ("{ddd dd/MM} {HH'h'mm} ·
 * {casa} · {line-up}", Emenda v2 c), Open Graph com a variante `og` absoluta por `publicUrl` (o `App:PublicUrl`, que o
 * web recebe em `SUBSOLO_PUBLIC_URL`), `twitter:card` e `canonical`.
 */
export function showMetadata(show: ShowDetail, publicUrl: string): Metadata {
  const base = publicUrl.replace(/\/+$/, "");
  const title = showTitle(show);
  const description = `${showWhen(show.startsAt, show.city.timeZone)} · ${show.venue.name} · ${lineupText(show)}`;
  const url = showUrl(show, base);
  const image = {
    url: `${base}${show.flyer.ogUrl}`,
    ...ogImageSize,
    alt: `Flyer de ${show.title}`,
  };
  return {
    title: { absolute: title },
    description,
    alternates: { canonical: url },
    openGraph: {
      title,
      description,
      url,
      type: "website",
      siteName: "Subsolo",
      locale: "pt_BR",
      images: [image],
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
      images: [image.url],
    },
  };
}
