import Link from "next/link";

import type { components } from "@/api/schema";

import { Badge } from "./Badge";

/** Situação do show no contrato `ShowSummary` da API. */
export type ShowStatus = components["schemas"]["ShowStatus"];

/**
 * Card de show da agenda, alimentado pelos campos do `ShowSummary` da API.
 * - `href`: página do show;
 * - `flyerSrc`: miniatura do flyer (`flyerThumbUrl`); sem flyer, placeholder escuro com o nome do show;
 * - `date`: início do show em ISO 8601 (`startsAt`), exibido no fuso `timeZone` (IANA) da cidade;
 * - `status`: `cancelled`/`postponed` mostram o `Badge` "Cancelado"/"Adiado" com papel de status.
 */
export interface CardProps {
  href: string;
  flyerSrc?: string | null;
  title: string;
  date: string;
  timeZone: string;
  venue: string;
  lineup: string[];
  styles: string[];
  status: ShowStatus;
  attendingCount: number;
}

interface ShowDate {
  day: string;
  month: string;
  weekday: string;
  time: string;
}

/** Dia, mês, dia da semana e hora ("21h", "21h30") do início do show no fuso da cidade. */
function showDate(iso: string, timeZone: string): ShowDate {
  const parts = new Intl.DateTimeFormat("pt-BR", {
    timeZone,
    day: "2-digit",
    month: "short",
    weekday: "short",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(new Date(iso));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    (parts.find((item) => item.type === type)?.value ?? "").replace(".", "");
  const minute = part("minute");
  return {
    day: part("day"),
    month: part("month"),
    weekday: part("weekday"),
    time: `${part("hour")}h${minute === "00" ? "" : minute}`,
  };
}

/**
 * SPEC-0011 — card de show: flyer inteiro (sem corte, `object-contain` sobre fundo escuro) ou placeholder com o nome,
 * data e hora no fuso da cidade, título (link para o show; o card inteiro é clicável), line-up, casa, estilos,
 * situação e contagem de "vão".
 */
export function Card({
  href,
  flyerSrc,
  title,
  date,
  timeZone,
  venue,
  lineup,
  styles,
  status,
  attendingCount,
}: CardProps) {
  const when = showDate(date, timeZone);
  const cancelled = status === "cancelled";
  return (
    <article className="relative flex h-full gap-3 rounded-md border border-border bg-surface p-3 transition-colors focus-within:border-text-muted hover:border-text-muted md:flex-col">
      <div
        className={`flex h-28 w-20 shrink-0 items-center justify-center overflow-hidden rounded-xs border border-border bg-bg md:aspect-[4/5] md:h-auto md:w-full ${cancelled ? "grayscale" : ""}`}
      >
        {flyerSrc ? (
          // eslint-disable-next-line @next/next/no-img-element -- o next/image injeta style="color:transparent", barrado pela CSP sem estilo inline (SPEC-0010/ADR-0017); a imagem já não era otimizada.
          <img
            src={flyerSrc}
            alt={`Flyer de ${title}`}
            width={400}
            height={500}
            loading="lazy"
            decoding="async"
            className="h-full w-full object-contain"
          />
        ) : (
          <span
            aria-hidden="true"
            className="line-clamp-4 break-words p-1.5 text-center font-display text-xs uppercase leading-tight text-text-muted md:p-6 md:text-3xl"
          >
            {title}
          </span>
        )}
      </div>

      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex items-center justify-between gap-2">
          <time dateTime={date} className="flex items-baseline gap-2">
            <span
              className={`font-display text-2xl leading-none ${cancelled ? "text-text-muted" : "text-accent"}`}
            >
              {when.day}
            </span>
            <span className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
              {when.month} · {when.weekday} · {when.time}
            </span>
          </time>
          {status === "cancelled" && <Badge kind="cancelled" />}
          {status === "postponed" && <Badge kind="postponed" />}
        </div>

        <h2
          className={`font-display text-lg uppercase leading-tight ${cancelled ? "text-text-muted line-through" : "text-text"}`}
        >
          <Link
            href={href}
            className="after:absolute after:inset-0 after:content-['']"
          >
            {title}
          </Link>
        </h2>

        <p className="truncate text-sm text-text">
          {lineup.map((band, index) => (
            <span key={band}>
              {index > 0 && <span className="text-text-muted"> / </span>}
              {band}
            </span>
          ))}
        </p>
        <p className="font-label text-xs uppercase tracking-wide text-text-muted">
          {venue}
        </p>

        <div className="mt-auto flex items-center justify-between gap-2 border-t border-border pt-2">
          <ul className="flex flex-wrap gap-1.5" aria-label="Estilos">
            {styles.map((style) => (
              <li key={style}>
                <Badge kind="style">{style}</Badge>
              </li>
            ))}
          </ul>
          <p className="shrink-0 font-label text-xs font-semibold text-text-muted">
            {attendingCount} {attendingCount === 1 ? "vai" : "vão"}
          </p>
        </div>
      </div>
    </article>
  );
}
