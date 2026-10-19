import Link from "next/link";

import type { ShowDetail } from "@/api/show-detail";
import { Badge } from "@/components/ui/Badge";
import { buttonClasses } from "@/components/ui/Button";
import { mainContentId } from "@/components/ui/shell";

import { ShareButton } from "./ShareButton";
import { ArrowLeftIcon, ExternalIcon, PinIcon, TicketIcon } from "./show-icons";

/** Medidas do flyer sem medida gravada (flyers antigos): proporção 3:4 (Emenda v2, b). */
const fallbackFlyer = { width: 1080, height: 1440 } as const;

/** Dia da semana, dia, mês e hora ("21h", "21h30") do início do show no fuso da cidade. */
function showDate(iso: string, timeZone: string) {
  const parts = new Intl.DateTimeFormat("pt-BR", {
    timeZone,
    weekday: "short",
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(new Date(iso));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    (parts.find((item) => item.type === type)?.value ?? "").replace(".", "");
  const minute = part("minute");
  return {
    date: `${part("weekday")} ${part("day")} ${part("month")}`,
    time: `${part("hour")}h${minute === "00" ? "" : minute}`,
  };
}

/** Título de seção no estilo do Stitch: barra de destaque e rótulo em maiúsculas. */
function SectionTitle({ id, children }: { id: string; children: string }) {
  return (
    <h2
      id={id}
      className="flex items-center gap-2 font-display text-xl uppercase leading-none tracking-wide text-text"
    >
      <span aria-hidden="true" className="h-5 w-1 bg-accent" />
      {children}
    </h2>
  );
}

export interface ShowPageProps {
  show: ShowDetail;
  /** URL canônica da página (vai no "Compartilhar"). */
  url: string;
}

/**
 * SPEC-0024 — a página do show (telas do Stitch "Página do Show - Noite da Peste II", celular e desktop): o flyer
 * inteiro (`object-contain`) sobre o próprio flyer desfocado, a data e a hora no fuso da cidade, o selo de cancelado ou
 * adiado, o título, os estilos, o line-up, a casa, o preço, "Ingresso" (nova aba, `noopener noreferrer`) e
 * "Compartilhar". Ajustes do §6: sem "Vou", "Quem vai", "Seguir" e "Denunciar" (specs próprias) e sem links de perfil
 * (a página do perfil ainda não existe).
 */
export function ShowPage({ show, url }: ShowPageProps) {
  const when = showDate(show.startsAt, show.city.timeZone);
  const cancelled = show.status === "cancelled";
  const flyer = {
    width: show.flyer.width ?? fallbackFlyer.width,
    height: show.flyer.height ?? fallbackFlyer.height,
  };
  const bands = show.lineup.length;
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-6 px-4 py-6 md:py-8"
    >
      <Link
        href={`/${show.city.slug}`}
        className="inline-flex min-h-11 w-fit items-center gap-2 font-label text-xs font-bold uppercase tracking-wider text-text-muted hover:text-text"
      >
        <ArrowLeftIcon />
        Agenda {show.city.name}
      </Link>

      <div className="grid gap-8 md:grid-cols-[minmax(0,5fr)_minmax(0,7fr)] md:items-start">
        <section
          aria-label="Flyer"
          className="relative isolate flex justify-center overflow-hidden rounded-md border border-border bg-bg md:sticky md:top-6"
        >
          {/* eslint-disable-next-line @next/next/no-img-element -- fundo decorativo; o next/image injeta estilo inline, barrado pela CSP (ADR-0017). */}
          <img
            src={show.flyer.thumbUrl}
            alt=""
            aria-hidden="true"
            width={400}
            height={Math.round((400 * flyer.height) / flyer.width)}
            decoding="async"
            className="absolute inset-0 -z-10 h-full w-full scale-110 object-cover opacity-40 blur-2xl"
          />
          {/* eslint-disable-next-line @next/next/no-img-element -- o next/image injeta style="color:transparent", barrado pela CSP sem estilo inline (SPEC-0010/ADR-0017). */}
          <img
            src={show.flyer.fullUrl}
            alt={`Flyer de ${show.title}`}
            width={flyer.width}
            height={flyer.height}
            fetchPriority="high"
            decoding="async"
            className={`h-auto max-h-[75vh] w-full object-contain ${cancelled ? "grayscale" : ""}`}
          />
        </section>

        <div className="flex flex-col gap-6">
          <header className="flex flex-col gap-3">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
              <time
                dateTime={show.startsAt}
                className="flex items-baseline gap-3"
              >
                <span
                  className={`font-display text-3xl uppercase leading-none md:text-4xl ${cancelled ? "text-text-muted" : "text-accent"}`}
                >
                  {when.date}
                </span>
                <span className="font-label text-sm font-bold uppercase tracking-wider text-text">
                  {when.time}
                </span>
              </time>
              {show.status === "cancelled" && <Badge kind="cancelled" />}
              {show.status === "postponed" && <Badge kind="postponed" />}
            </div>
            <h1
              className={`break-words font-display text-4xl uppercase leading-none tracking-wide md:text-6xl ${cancelled ? "text-text-muted line-through" : "text-text"}`}
            >
              {show.title}
            </h1>
            <ul className="flex flex-wrap gap-1.5" aria-label="Estilos">
              {show.styles.map((style) => (
                <li key={style.slug}>
                  <Badge kind="style">{style.name}</Badge>
                </li>
              ))}
            </ul>
          </header>

          <section
            aria-labelledby="ingresso"
            className="flex flex-col gap-4 rounded-md border border-border bg-surface p-4 md:p-6"
          >
            <h2 id="ingresso" className="sr-only">
              Ingresso
            </h2>
            <div className="flex items-start gap-3">
              <TicketIcon className="mt-1 shrink-0 text-accent" />
              <div className="flex flex-col gap-1">
                <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
                  Entrada
                </p>
                <p className="font-display text-2xl leading-tight text-text">
                  {show.price}
                </p>
              </div>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              {show.ticketUrl && (
                <a
                  href={show.ticketUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={buttonClasses("primary", "lg")}
                >
                  Ingresso
                  <ExternalIcon />
                  <span className="sr-only">(abre em nova aba)</span>
                </a>
              )}
              <ShareButton title={show.title} url={url} />
            </div>
          </section>

          <section aria-labelledby="line-up" className="flex flex-col gap-3">
            <div className="flex items-baseline justify-between gap-2 border-b border-border pb-2">
              <SectionTitle id="line-up">Line-up</SectionTitle>
              <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
                {bands} {bands === 1 ? "banda" : "bandas"}
              </p>
            </div>
            <ul className="flex flex-wrap items-baseline gap-x-2 gap-y-1 font-display text-2xl uppercase leading-tight text-text md:text-3xl">
              {show.lineup.map((band, index) => (
                <li key={`${index}-${band.name}`} className="break-words">
                  {band.name}
                  {index < bands - 1 && (
                    <span aria-hidden="true" className="ml-2 text-accent">
                      {" "}
                      /
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </section>

          <section aria-labelledby="casa" className="flex flex-col gap-3">
            <div className="border-b border-border pb-2">
              <SectionTitle id="casa">Casa</SectionTitle>
            </div>
            <p className="flex items-center gap-2 text-lg text-text">
              <PinIcon className="shrink-0 text-accent" />
              {show.venue.name} · {show.city.name}
            </p>
          </section>
        </div>
      </div>
    </main>
  );
}
