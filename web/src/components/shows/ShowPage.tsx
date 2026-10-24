import Link from "next/link";

import type { ShowDetail } from "@/api/show-detail";
import { AttendButton } from "@/components/engagement/AttendButton";
import { FlameIcon } from "@/components/engagement/icons";
import { goingCount, wentCount } from "@/components/engagement/messages";
import { ReportShowButton } from "@/components/moderation/ReportShowButton";
import { Badge } from "@/components/ui/Badge";
import { buttonClasses } from "@/components/ui/Button";
import { mainContentId } from "@/components/ui/shell";

import { cityTimeParts } from "./city-time";
import { ShareButton } from "./ShareButton";
import { ArrowLeftIcon, ExternalIcon, PinIcon, TicketIcon } from "./show-icons";

/** Medidas do flyer sem medida gravada (flyers antigos): proporção 3:4 (Emenda v2, b). */
const fallbackFlyer = { width: 1080, height: 1440 } as const;

/** Dia da semana, dia, mês e hora ("21h", "21h30") do início do show no fuso da cidade. */
function showDate(iso: string, timeZone: string) {
  const { weekday, day, month, hour, minute } = cityTimeParts(iso, timeZone);
  return {
    date: `${weekday} ${day} ${month}`,
    time: `${hour}h${minute === "00" ? "" : minute}`,
  };
}

/** Classes do nome ligado à página do perfil (sublinhado no destaque, como os demais links do texto). */
const profileLink =
  "underline decoration-accent decoration-2 underline-offset-4 hover:text-accent";

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
  /** SPEC-0029 — quantos marcaram "vou" (do `Engagement`); `undefined` = contagem indisponível (a página sai sem ela). */
  attendingCount?: number;
  /** SPEC-0029 — o show já começou (sem o botão "Vou"; a contagem vira "{n} foram"). */
  past?: boolean;
}

/**
 * SPEC-0029 — o "vou" do show: agendado e futuro → o botão "Vou" com "{n} vão"; cancelado ou adiado → só "{n} vão";
 * já passou → só "{n} foram". Sem a contagem (indisponível), só o botão (ou nada).
 */
function Attendance({
  show,
  count,
  past,
}: {
  show: ShowDetail;
  count: number | undefined;
  past: boolean;
}) {
  if (show.status === "scheduled" && !past) {
    return (
      <AttendButton
        showId={show.id}
        title={show.title}
        count={count}
        returnTo={`/${show.city.slug}/${show.slug}`}
      />
    );
  }
  if (count === undefined) {
    return null;
  }
  return (
    <p className="flex items-center gap-2 font-label text-sm font-bold uppercase tracking-wider text-text-muted">
      <FlameIcon className="shrink-0 text-accent" />
      {/* SPEC-0036 (H32-03): o espaço que separa a contagem do texto seguinte fica no mesmo nó de texto dela. */}
      <span>{`${past ? wentCount(count) : goingCount(count)} `}</span>
    </p>
  );
}

/**
 * SPEC-0024 — a página do show (telas do Stitch "Página do Show - Noite da Peste II", celular e desktop): o flyer
 * inteiro (`object-contain`) sobre o próprio flyer desfocado, a data e a hora no fuso da cidade, o selo de cancelado ou
 * adiado, o título, os estilos, o line-up, a casa, o preço, "Ingresso" (nova aba, `noopener noreferrer`) e
 * "Compartilhar". Ajustes do §6: sem "Vou", "Quem vai", "Seguir" e "Denunciar" (specs próprias). SPEC-0026: a casa e as
 * bandas com perfil levam às páginas `/casa/{slug}` e `/banda/{slug}`. SPEC-0029: o "Vou" com a contagem "{n} vão" (ou
 * "{n} foram" no show que já passou), nunca "Quem vai" (ADR-0008). SPEC-0033: o "Reportar" no rodapé, que abre a folha
 * "Reportar show" (anônimo vai para `/entrar`).
 */
export function ShowPage({
  show,
  url,
  attendingCount,
  past = false,
}: ShowPageProps) {
  const when = showDate(show.startsAt, show.city.timeZone);
  const cancelled = show.status === "cancelled";
  const flyer = {
    width: show.flyer.width ?? fallbackFlyer.width,
    height: show.flyer.height ?? fallbackFlyer.height,
  };
  const bands = show.lineup.length;
  // Ids únicos por show: a navegação no cliente mantém a página anterior escondida no DOM (Activity, cacheComponents).
  const ids = {
    ticket: `ingresso-${show.id}`,
    lineup: `line-up-${show.id}`,
    venue: `casa-${show.id}`,
  };
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
            aria-labelledby={ids.ticket}
            className="flex flex-col gap-4 rounded-md border border-border bg-surface p-4 md:p-6"
          >
            <h2 id={ids.ticket} className="sr-only">
              Ingresso
            </h2>
            {/*
              SPEC-0032 (H29-03): a contagem ("3 vão") fica num bloco próprio, separada do texto seguinte pela estrutura.
              SPEC-0036 (H32-03): o espaço que mantém "3 vão" e "Entrada" separados no conteúdo textual da página
              (leitores, buscas e o textContent) fica dentro do parágrafo da contagem (aqui ou no botão "Vou"), sem nó
              de texto só com espaço nos blocos da seção.
            */}
            <div>
              <Attendance show={show} count={attendingCount} past={past} />
            </div>
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

          <section aria-labelledby={ids.lineup} className="flex flex-col gap-3">
            <div className="flex items-baseline justify-between gap-2 border-b border-border pb-2">
              <SectionTitle id={ids.lineup}>Line-up</SectionTitle>
              <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
                {bands} {bands === 1 ? "banda" : "bandas"}
              </p>
            </div>
            <ul className="flex flex-wrap items-baseline gap-x-2 gap-y-1 font-display text-2xl uppercase leading-tight text-text md:text-3xl">
              {show.lineup.map((band, index) => (
                <li key={`${index}-${band.name}`} className="break-words">
                  {band.profileSlug ? (
                    <Link
                      href={`/banda/${band.profileSlug}`}
                      className={profileLink}
                    >
                      {band.name}
                    </Link>
                  ) : (
                    band.name
                  )}
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

          <section aria-labelledby={ids.venue} className="flex flex-col gap-3">
            <div className="border-b border-border pb-2">
              <SectionTitle id={ids.venue}>Casa</SectionTitle>
            </div>
            <p className="flex items-center gap-2 text-lg text-text">
              <PinIcon className="shrink-0 text-accent" />
              {show.venue.profileSlug ? (
                <Link
                  href={`/casa/${show.venue.profileSlug}`}
                  className={profileLink}
                >
                  {show.venue.name}
                </Link>
              ) : (
                show.venue.name
              )}{" "}
              · {show.city.name}
            </p>
          </section>
        </div>
      </div>

      <footer className="mt-4 border-t border-border pt-4">
        <ReportShowButton
          showId={show.id}
          title={show.title}
          returnTo={`/${show.city.slug}/${show.slug}`}
        />
      </footer>
    </main>
  );
}
