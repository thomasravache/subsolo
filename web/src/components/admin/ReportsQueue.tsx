"use client";

import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";

import {
  hideReportedShow,
  keepReportedShow,
  type ReportQueueItem,
  type ReportReason,
} from "@/api/reports";
import type { ApiResult } from "@/api/browser";
import {
  reasonLabel,
  reportCount,
  reportMessages,
  reportReasons,
} from "@/components/moderation/messages";
import { cityTimeParts } from "@/components/shows/city-time";
import { Button, buttonClasses } from "@/components/ui/Button";

import { fieldControl } from "./Field";
import { genericMessage } from "./messages";

export interface ReportsQueueProps {
  initialItems: ReportQueueItem[];
  /** Avisa a quantidade de shows na fila (o rótulo da aba "Denúncias (n)"). */
  onCountChange?: (count: number) => void;
}

/** Tamanho máximo da nota interna do "Ocultar show" (§6). */
const noteMaxLength = 200;

/** O dia da primeira denúncia ("Desde …"), no horário de Brasília. */
const dateFormat = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "short",
  year: "numeric",
  timeZone: "America/Sao_Paulo",
});

/** Elementos que recebem o foco pelo Tab dentro da confirmação. */
const focusable =
  'button:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/** As denúncias de cada motivo presente ("Show falso (2)"), na ordem da folha. */
function reasonsOf(
  item: ReportQueueItem,
): { reason: ReportReason; count: number }[] {
  return reportReasons
    .map(({ value }) => ({ reason: value, count: item.counts[value] }))
    .filter((entry) => entry.count > 0);
}

/** SPEC-0037 (H33-06) — data e hora do show no fuso da cidade ("20 de nov de 2026, 19:30"). */
function showWhen(item: ReportQueueItem): string {
  const { day, month, year, hour, minute } = cityTimeParts(
    item.startsAtUtc,
    item.timeZone,
  );
  return `${day} de ${month} de ${year}, ${hour}:${minute}`;
}

function total(item: ReportQueueItem): number {
  return reportReasons.reduce((sum, { value }) => sum + item.counts[value], 0);
}

/** Abre o `dialog` modal (o `open` cobre o ambiente sem `showModal`). */
function showModal(dialog: HTMLDialogElement | null) {
  if (!dialog || dialog.open) {
    return;
  }
  if (typeof dialog.showModal === "function") {
    dialog.showModal();
  } else {
    dialog.setAttribute("open", "");
  }
}

function closeModal(dialog: HTMLDialogElement | null) {
  if (!dialog?.open) {
    return;
  }
  if (typeof dialog.close === "function") {
    dialog.close();
  } else {
    dialog.removeAttribute("open");
  }
}

/** A mensagem de uma decisão que falhou. */
function decisionError(result: Extract<ApiResult, { ok: false }>): string {
  return result.code === "moderation.no_pending_reports"
    ? reportMessages.alreadyDecided
    : genericMessage(result.status);
}

/**
 * SPEC-0033 — a aba "DENÚNCIAS (n)" da "Fila do admin" (tela "Fila do Admin - Subsolo" do Stitch, com os ajustes do §6):
 * um cartão (`article`) por show denunciado, mais antigo primeiro, com o título, "data • casa", "{n} denúncias" com os
 * motivos e os detalhes mais recentes (texto, nunca HTML), sem quem denunciou; "Manter" decide na hora e "Ocultar show"
 * pede confirmação num `dialog` modal (com a nota interna opcional). O cartão decidido sai da fila e a decisão é
 * anunciada. SPEC-0037: a data e a hora do show saem no fuso da cidade (H33-06) e, depois da decisão, o foco vai para o
 * título do próximo cartão ou para a mensagem de fila vazia (H33-02).
 */
export function ReportsQueue({
  initialItems,
  onCountChange,
}: ReportsQueueProps) {
  const id = useId();
  const [items, setItems] = useState(initialItems);
  const [busy, setBusy] = useState<string | null>(null);
  const [status, setStatus] = useState("");
  const [cardError, setCardError] = useState<{
    showId: string;
    message: string;
  } | null>(null);
  const [hiding, setHiding] = useState<ReportQueueItem | null>(null);
  const [note, setNote] = useState("");
  const confirm = useRef<HTMLDialogElement>(null);
  const opener = useRef<HTMLElement | null>(null);
  const emptyMessage = useRef<HTMLParagraphElement>(null);
  /** SPEC-0037 (H33-02) — depois de uma decisão, o show cujo título recebe o foco (`null` = a fila vazia). */
  const focusAfter = useRef<{ showId: string | null } | undefined>(undefined);

  const confirmTitleId = `${id}-ocultar-titulo`;
  const confirmTextId = `${id}-ocultar-texto`;
  const noteId = `${id}-ocultar-nota`;

  useEffect(() => {
    onCountChange?.(items.length);
  }, [items.length, onCountChange]);

  useEffect(() => {
    const target = focusAfter.current;
    if (!target) {
      return;
    }
    focusAfter.current = undefined;
    const element = target.showId
      ? document.getElementById(`${id}-show-${target.showId}`)
      : emptyMessage.current;
    element?.focus();
  }, [items, id]);

  function decided(showId: string, message: string) {
    setItems((current) => {
      // O foco vai para o título do próximo cartão (ou do anterior, se era o último) ou para a mensagem de fila vazia.
      const index = current.findIndex((item) => item.showId === showId);
      const next = current[index + 1] ?? current[index - 1];
      focusAfter.current = { showId: next?.showId ?? null };
      return current.filter((item) => item.showId !== showId);
    });
    setCardError(null);
    setStatus(message);
  }

  async function keep(item: ReportQueueItem) {
    setBusy(item.showId);
    setStatus("");
    setCardError(null);
    const result = await keepReportedShow(item.showId);
    setBusy(null);
    if (result.ok) {
      decided(item.showId, reportMessages.kept);
    } else {
      setCardError({ showId: item.showId, message: decisionError(result) });
    }
  }

  function askHide(item: ReportQueueItem, element: HTMLElement) {
    opener.current = element;
    setHiding(item);
    setNote("");
    setCardError(null);
    showModal(confirm.current);
  }

  function closeConfirm() {
    closeModal(confirm.current);
    setHiding(null);
  }

  async function hide() {
    if (!hiding || busy) {
      return;
    }
    const item = hiding;
    setBusy(item.showId);
    setStatus("");
    const result = await hideReportedShow(item.showId, note.trim() || null);
    setBusy(null);
    if (result.ok) {
      // O botão que abriu a confirmação sai com o cartão: o foco segue a decisão, não volta para ele.
      opener.current = null;
    }
    closeConfirm();
    if (result.ok) {
      decided(item.showId, reportMessages.hidden);
    } else {
      setCardError({ showId: item.showId, message: decisionError(result) });
      opener.current?.focus();
    }
  }

  function trapFocus(event: KeyboardEvent<HTMLDialogElement>) {
    if (event.key !== "Tab" || !confirm.current) {
      return;
    }
    const controls = Array.from(
      confirm.current.querySelectorAll<HTMLElement>(focusable),
    );
    const first = controls.at(0);
    const last = controls.at(-1);
    if (!first || !last) {
      return;
    }
    const active = document.activeElement;
    const inside = confirm.current.contains(active);
    if (event.shiftKey && (!inside || active === first)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (!inside || active === last)) {
      event.preventDefault();
      first.focus();
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <dialog
        ref={confirm}
        aria-labelledby={confirmTitleId}
        aria-describedby={confirmTextId}
        onKeyDown={trapFocus}
        onCancel={(event) => {
          event.preventDefault();
          closeConfirm();
        }}
        onClose={() => {
          setHiding(null);
          opener.current?.focus();
        }}
        className="m-auto w-[calc(100%-2rem)] max-w-md rounded-md border border-border bg-surface-raised p-6 text-text backdrop:bg-bg/80"
      >
        <div className="flex flex-col gap-4">
          <h2
            id={confirmTitleId}
            className="font-display text-3xl uppercase leading-none tracking-wide text-text"
          >
            Ocultar show
          </h2>
          <p className="font-label text-xs uppercase tracking-wider text-text-muted">
            {hiding?.title}
          </p>
          <p id={confirmTextId} className="text-base text-text-muted">
            {reportMessages.hideQuestion}
          </p>
          <div className="flex flex-col gap-1.5">
            <label
              htmlFor={noteId}
              className="font-label text-xs font-bold uppercase tracking-wider text-text"
            >
              Motivo interno (opcional)
            </label>
            <textarea
              id={noteId}
              rows={2}
              maxLength={noteMaxLength}
              value={note}
              onChange={(event) => setNote(event.target.value)}
              className={`${fieldControl} py-2`}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Button
              size="lg"
              disabled={busy !== null}
              onClick={() => void hide()}
            >
              Ocultar show
            </Button>
            <button
              type="button"
              onClick={closeConfirm}
              className={buttonClasses("secondary", "lg")}
            >
              Voltar
            </button>
          </div>
        </div>
      </dialog>

      <p role="status" className="text-sm text-text empty:hidden">
        {status}
      </p>

      {items.length === 0 ? (
        <p
          ref={emptyMessage}
          tabIndex={-1}
          className="rounded-md border border-dashed border-border px-4 py-10 text-center text-base text-text-muted"
        >
          {reportMessages.empty}
        </p>
      ) : (
        <ul className="grid gap-3 lg:grid-cols-2">
          {items.map((item) => {
            const titleId = `${id}-show-${item.showId}`;
            const count = total(item);
            return (
              <li key={item.showId}>
                <article
                  aria-labelledby={titleId}
                  className="flex h-full flex-col gap-4 rounded-md border border-border bg-surface p-4"
                >
                  <div className="flex items-center gap-2">
                    <span className="rounded-xs bg-accent px-2 py-0.5 font-label text-[10px] font-bold uppercase tracking-wider text-text-on-accent">
                      Denúncia
                    </span>
                    <span className="font-label text-xs uppercase text-text-muted">
                      Desde{" "}
                      {dateFormat.format(new Date(item.firstReportedAtUtc))}
                    </span>
                  </div>
                  <div className="flex flex-col gap-1">
                    <h2
                      id={titleId}
                      tabIndex={-1}
                      className="font-display text-2xl uppercase leading-none tracking-wide text-text"
                    >
                      <a
                        href={`/${item.citySlug}/${item.slug}`}
                        className="hover:text-accent"
                      >
                        {item.title}
                      </a>
                    </h2>
                    <p className="text-sm text-text-muted">
                      {showWhen(item)} • {item.venueName}
                    </p>
                  </div>
                  <div className="flex flex-col gap-2 rounded-xs border border-border bg-surface-raised p-3 text-sm">
                    <p className="font-label font-bold uppercase tracking-wider text-text">
                      {reportCount(count)}
                    </p>
                    <ul className="flex flex-wrap gap-x-3 gap-y-1 text-text-muted">
                      {reasonsOf(item).map((entry) => (
                        <li key={entry.reason}>
                          {reasonLabel(entry.reason)} ({entry.count})
                        </li>
                      ))}
                    </ul>
                    {item.latestDetails.length > 0 && (
                      <ul className="flex flex-col gap-1 border-t border-border pt-2 text-text">
                        {item.latestDetails.map((detail, index) => (
                          <li key={index} className="break-words">
                            “{detail}”
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                  <div className="grid grid-cols-2 gap-2">
                    <Button
                      variant="secondary"
                      size="lg"
                      disabled={busy === item.showId}
                      onClick={() => void keep(item)}
                    >
                      Manter
                    </Button>
                    <Button
                      size="lg"
                      disabled={busy === item.showId}
                      onClick={(event) => askHide(item, event.currentTarget)}
                    >
                      Ocultar show
                    </Button>
                  </div>
                  {cardError?.showId === item.showId && (
                    <p role="alert" className="text-sm font-medium text-danger">
                      {cardError.message}
                    </p>
                  )}
                </article>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
