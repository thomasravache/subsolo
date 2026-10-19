"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useId, useRef, useState, type KeyboardEvent } from "react";

import type { CitySummary } from "@/api/client";
import { changeShowStatus, type MyShow, type ShowStatus } from "@/api/shows";
import { Badge } from "@/components/ui/Badge";
import { buttonClasses } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";

import {
  CancelShowDialog,
  type CancelShowDialogHandle,
} from "./CancelShowDialog";
import {
  editShowPath,
  showDateParts,
  showErrorMessage,
  showMessages,
} from "./messages";

export interface MyShowsListProps {
  upcoming: MyShow[];
  past: MyShow[];
  cities: CitySummary[];
}

type Tab = "upcoming" | "past";

const tabs: { key: Tab; label: string }[] = [
  { key: "upcoming", label: "Próximos" },
  { key: "past", label: "Passados" },
];

const actionClass = buttonClasses("secondary", "md", "flex-1 px-2");

/**
 * SPEC-0023 — "Meus shows" (tela "Meus Shows - Produtor - Subsolo" do Stitch, sem o selo "produtor verificado"): abas
 * "Próximos" e "Passados" (padrão de abas da WAI-ARIA, setas trocam a aba), cada show num cartão (`article`) com o
 * flyer, a data em bloco no fuso da cidade, o nome, a casa e a cidade e o selo ADIADO/CANCELADO, e as ações "Editar",
 * "Adiar" e "Cancelar show" (com a confirmação em diálogo). Vazio → "Nenhum show cadastrado ainda.".
 */
export function MyShowsList({ upcoming, past, cities }: MyShowsListProps) {
  const router = useRouter();
  const id = useId();
  const [tab, setTab] = useState<Tab>("upcoming");
  const [statuses, setStatuses] = useState<Record<string, ShowStatus>>({});
  const [busy, setBusy] = useState<string | null>(null);
  const [status, setStatus] = useState("");
  const [error, setError] = useState<string | null>(null);
  const dialogs = useRef<Record<string, CancelShowDialogHandle | null>>({});
  const tabRefs = useRef<Record<Tab, HTMLButtonElement | null>>({
    upcoming: null,
    past: null,
  });

  const lists: Record<Tab, MyShow[]> = { upcoming, past };
  const shown = lists[tab];
  const statusOf = (show: MyShow) => statuses[show.id] ?? show.status;
  const cityName = (slug: string) =>
    cities.find((city) => city.slug === slug)?.name ?? slug;

  function onTabKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    if (event.key !== "ArrowRight" && event.key !== "ArrowLeft") {
      return;
    }
    event.preventDefault();
    const next: Tab = tab === "upcoming" ? "past" : "upcoming";
    setTab(next);
    tabRefs.current[next]?.focus();
  }

  async function change(show: MyShow, next: ShowStatus): Promise<boolean> {
    setBusy(show.id);
    setError(null);
    setStatus("");
    const result = await changeShowStatus(show.id, next);
    setBusy(null);
    if (!result.ok) {
      setError(showErrorMessage(result));
      return false;
    }
    setStatuses((current) => ({ ...current, [show.id]: next }));
    setStatus(
      next === "cancelled" ? showMessages.cancelled : showMessages.postponed,
    );
    router.refresh();
    return true;
  }

  return (
    <div className="flex flex-col gap-4">
      <div
        role="tablist"
        aria-label="Período"
        className="flex gap-6 border-b border-border"
      >
        {tabs.map(({ key, label }) => (
          <button
            key={key}
            ref={(element) => {
              tabRefs.current[key] = element;
            }}
            id={`${id}-aba-${key}`}
            type="button"
            role="tab"
            aria-selected={tab === key}
            aria-controls={`${id}-painel`}
            tabIndex={tab === key ? 0 : -1}
            onClick={() => setTab(key)}
            onKeyDown={onTabKeyDown}
            className={`-mb-px min-h-11 border-b-2 pb-2 font-label text-sm font-bold uppercase tracking-wider transition-colors ${
              tab === key
                ? "border-accent text-text"
                : "border-transparent text-text-muted hover:text-text"
            }`}
          >
            {label} ({lists[key].length})
          </button>
        ))}
      </div>

      <p role="status" className="text-sm text-text empty:hidden">
        {status}
      </p>
      {error && (
        <p role="alert" className="text-sm font-medium text-danger">
          {error}
        </p>
      )}

      <div
        id={`${id}-painel`}
        role="tabpanel"
        aria-labelledby={`${id}-aba-${tab}`}
        tabIndex={0}
      >
        {shown.length === 0 ? (
          <EmptyState
            title={
              tab === "upcoming" || upcoming.length + past.length === 0
                ? showMessages.empty
                : showMessages.emptyPast
            }
            hint={showMessages.emptyHint}
          />
        ) : (
          <ul className="flex flex-col gap-3">
            {shown.map((show) => {
              const current = statusOf(show);
              // SPEC-0024 (H23-06): o fuso da cidade vem em cada item de GET /api/v1/me/shows.
              const when = showDateParts(show.startsAt, show.timeZone);
              const cancelled = current === "cancelled";
              const titleId = `${id}-show-${show.id}`;
              return (
                <li key={show.id} className="flex flex-col">
                  <article
                    aria-labelledby={titleId}
                    className="flex flex-col gap-3 rounded-md border border-border bg-surface p-3"
                  >
                    <div className="flex gap-3">
                      <div
                        className={`flex h-28 w-20 shrink-0 items-center justify-center overflow-hidden rounded-xs border border-border bg-bg ${cancelled ? "grayscale" : ""}`}
                      >
                        {/* eslint-disable-next-line @next/next/no-img-element -- o next/image injeta style inline, barrado pela CSP (ADR-0017). */}
                        <img
                          src={show.flyerThumbUrl}
                          alt={`Flyer de ${show.title}`}
                          width={400}
                          height={500}
                          loading="lazy"
                          decoding="async"
                          className="h-full w-full object-contain"
                        />
                      </div>
                      <div className="flex min-w-0 flex-1 flex-col justify-between gap-2">
                        <div className="flex flex-col gap-1">
                          {current !== "scheduled" && (
                            <span className="self-start">
                              <Badge
                                kind={
                                  current === "cancelled"
                                    ? "cancelled"
                                    : "postponed"
                                }
                              />
                            </span>
                          )}
                          <h2
                            id={titleId}
                            className={`font-display text-xl uppercase leading-tight ${cancelled ? "text-text-muted line-through" : "text-text"}`}
                          >
                            {show.title}
                          </h2>
                          <p className="font-label text-xs uppercase tracking-wide text-text-muted">
                            {show.venueName} · {cityName(show.city)}
                          </p>
                        </div>
                        <time
                          dateTime={show.startsAt}
                          className="flex items-baseline gap-2"
                        >
                          <span
                            className={`font-display text-2xl leading-none ${cancelled ? "text-text-muted" : "text-accent"}`}
                          >
                            {when.day}
                          </span>
                          <span className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
                            {when.month} · {when.weekday} · {when.time}
                          </span>
                        </time>
                      </div>
                    </div>
                    <div className="flex gap-2 border-t border-border pt-3">
                      <Link
                        href={editShowPath(show.id)}
                        className={actionClass}
                      >
                        Editar
                      </Link>
                      {current === "scheduled" && (
                        <button
                          type="button"
                          disabled={busy === show.id}
                          onClick={() => void change(show, "postponed")}
                          className={actionClass}
                        >
                          Adiar
                        </button>
                      )}
                      {!cancelled && (
                        <button
                          type="button"
                          disabled={busy === show.id}
                          onClick={(event) =>
                            dialogs.current[show.id]?.open(event.currentTarget)
                          }
                          className={actionClass}
                        >
                          Cancelar show
                        </button>
                      )}
                    </div>
                  </article>
                  <CancelShowDialog
                    ref={(handle) => {
                      dialogs.current[show.id] = handle;
                    }}
                    id={`${id}-cancelar-${show.id}`}
                    title={show.title}
                    busy={busy === show.id}
                    onConfirm={() => change(show, "cancelled")}
                  />
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}
