"use client";

import {
  useEffect,
  useId,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
} from "react";

import { reportShow, type ReportReason } from "@/api/reports";
import { buttonClasses } from "@/components/ui/Button";

import {
  detailsMaxLength,
  reportErrorMessage,
  reportMessages,
  reportReasons,
} from "./messages";

/** Elementos que recebem o foco pelo Tab dentro da folha. */
const focusable =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export interface ReportShowSheetProps {
  /** O show (id da API). */
  showId: string;
  /** O título do show. */
  title: string;
  /** A folha está aberta. */
  open: boolean;
  /** Fechar ("Cancelar", `Esc` ou depois do envio). */
  onClose: () => void;
}

/** Vai para outra página do site (documento novo). */
function navigate(path: string) {
  window.location.assign(new URL(path, window.location.href).href);
}

/**
 * SPEC-0033 — a folha "Reportar show" (tela "Denunciar Show - Subsolo" do Stitch), aberta pelo link "Reportar" da
 * página do show: `dialog` modal com rótulo, foco preso (Tab e Shift+Tab giram dentro dela) e `Esc` fechando; os
 * motivos são um grupo de rádio com rótulo (cartões, com a explicação de cada um), "Conte mais (opcional)" (até 500
 * caracteres, obrigatório em "Outro"), "Cancelar" e "Enviar denúncia". O resultado ("Obrigado. A moderação vai olhar.",
 * "Você já denunciou este show." ou a falha) é anunciado numa região `aria-live`; depois do envio sobra "Fechar".
 */
export function ReportShowSheet({
  showId,
  title,
  open,
  onClose,
}: ReportShowSheetProps) {
  const id = useId();
  const dialog = useRef<HTMLDialogElement>(null);
  const [reason, setReason] = useState<ReportReason | null>(null);
  const [details, setDetails] = useState("");
  const [reasonError, setReasonError] = useState<string | null>(null);
  const [detailsError, setDetailsError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [message, setMessage] = useState("");

  const titleId = `${id}-titulo`;
  const introId = `${id}-texto`;
  const groupId = `${id}-motivo`;
  const reasonErrorId = `${id}-motivo-erro`;
  const detailsId = `${id}-detalhe`;
  const detailsErrorId = `${id}-detalhe-erro`;

  useEffect(() => {
    const element = dialog.current;
    if (!element) {
      return;
    }
    if (open && !element.open) {
      element.showModal();
    } else if (!open && element.open) {
      element.close();
    }
  }, [open]);

  function trapFocus(event: KeyboardEvent<HTMLDialogElement>) {
    if (event.key !== "Tab" || !dialog.current) {
      return;
    }
    const items = Array.from(
      dialog.current.querySelectorAll<HTMLElement>(focusable),
    );
    const first = items.at(0);
    const last = items.at(-1);
    if (!first || !last) {
      return;
    }
    const active = document.activeElement;
    const inside = dialog.current.contains(active);
    if (event.shiftKey && (!inside || active === first)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (!inside || active === last)) {
      event.preventDefault();
      first.focus();
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || done) {
      return;
    }
    setMessage("");
    // O detalhe vai sem espaços nas pontas; vazio → nulo.
    const text = details.trim() || null;
    if (reason === null) {
      setReasonError(reportMessages.chooseReason);
      dialog.current
        ?.querySelector<HTMLInputElement>('input[type="radio"]')
        ?.focus();
      return;
    }
    if (reason === "other" && text === null) {
      setDetailsError(reportMessages.detailsRequired);
      document.getElementById(detailsId)?.focus();
      return;
    }

    setBusy(true);
    const result = await reportShow(showId, { reason, details: text });
    setBusy(false);
    if (result.ok) {
      setDone(true);
      setMessage(reportMessages.thanks);
      return;
    }
    if (result.status === 401) {
      navigate(
        `/entrar?returnTo=${encodeURIComponent(window.location.pathname)}`,
      );
      return;
    }
    const detailsField = Object.keys(result.fields ?? {}).some(
      (name) => name.toLowerCase() === "details",
    );
    if (result.code === "moderation.invalid_report" && detailsField) {
      setDetailsError(
        text && text.length > detailsMaxLength
          ? reportMessages.detailsTooLong
          : reportMessages.detailsRequired,
      );
      return;
    }
    if (result.code === "moderation.already_reported") {
      setDone(true);
    }
    setMessage(reportErrorMessage(result));
  }

  return (
    <dialog
      ref={dialog}
      aria-labelledby={titleId}
      aria-describedby={introId}
      onKeyDown={trapFocus}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      className="mx-auto mb-0 mt-auto max-h-[90dvh] w-full max-w-lg overflow-hidden rounded-t-xl border border-border bg-surface p-0 text-text backdrop:bg-bg/80 sm:m-auto sm:rounded-xl"
    >
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex max-h-[90dvh] flex-col"
      >
        <header className="flex h-16 shrink-0 items-center justify-between border-b border-border px-4">
          <h2
            id={titleId}
            className="font-display text-2xl uppercase tracking-wide text-text"
          >
            Reportar show
          </h2>
          <button
            type="button"
            aria-label="Fechar"
            onClick={onClose}
            className="inline-flex min-h-11 min-w-11 items-center justify-center text-accent hover:text-text"
          >
            <svg
              viewBox="0 0 24 24"
              width="24"
              height="24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="square"
              aria-hidden="true"
              focusable="false"
            >
              <path d="M6 6l12 12M18 6L6 18" />
            </svg>
          </button>
        </header>

        <div className="flex flex-col gap-6 overflow-y-auto p-4 sm:p-6">
          <p id={introId} className="text-sm text-text-muted">
            {reportMessages.intro} <span className="sr-only">{title}. </span>
            {reportMessages.introMore}
          </p>

          <div className="flex flex-col gap-3">
            <p id={groupId} className="sr-only">
              Motivo da denúncia
            </p>
            <div
              role="radiogroup"
              aria-labelledby={groupId}
              aria-describedby={reasonError ? reasonErrorId : undefined}
              className="flex flex-col gap-3"
            >
              {reportReasons.map((item) => (
                <label
                  key={item.value}
                  className="flex cursor-pointer items-start gap-3 rounded-md border border-border bg-surface-raised p-4 transition-colors hover:border-accent has-[:checked]:border-accent has-[:focus-visible]:outline has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-accent"
                >
                  <input
                    type="radio"
                    name={`${id}-motivo`}
                    value={item.value}
                    checked={reason === item.value}
                    disabled={done}
                    onChange={() => {
                      setReason(item.value);
                      setReasonError(null);
                    }}
                    className="mt-0.5 size-4 shrink-0 accent-accent"
                  />
                  <span className="flex flex-col gap-1 text-sm">
                    <span className="font-label font-bold uppercase tracking-wider text-text">
                      {item.label}
                    </span>
                    <span className="text-xs text-text-muted">{item.hint}</span>
                  </span>
                </label>
              ))}
            </div>
            {reasonError && (
              <p id={reasonErrorId} className="text-sm font-medium text-danger">
                {reasonError}
              </p>
            )}
          </div>

          <div className="flex flex-col gap-2">
            <label
              htmlFor={detailsId}
              className="font-label text-xs uppercase tracking-wider text-text-muted"
            >
              Conte mais (opcional)
            </label>
            <textarea
              id={detailsId}
              rows={3}
              maxLength={detailsMaxLength}
              value={details}
              disabled={done}
              placeholder="Explique os detalhes do problema..."
              aria-invalid={detailsError ? true : undefined}
              aria-describedby={detailsError ? detailsErrorId : undefined}
              onChange={(event) => {
                setDetails(event.target.value);
                setDetailsError(null);
              }}
              className="w-full resize-none rounded-md border border-border bg-surface-raised p-3 text-base text-text placeholder:text-text-muted focus:border-accent aria-[invalid=true]:border-danger"
            />
            {detailsError && (
              <p
                id={detailsErrorId}
                className="text-sm font-medium text-danger"
              >
                {detailsError}
              </p>
            )}
          </div>

          <p
            aria-live="polite"
            className="text-sm font-medium text-text empty:hidden"
          >
            {message}
          </p>
        </div>

        <footer className="flex shrink-0 flex-col gap-3 border-t border-border p-4 sm:flex-row">
          {done ? (
            <button
              type="button"
              onClick={onClose}
              className={buttonClasses("secondary", "lg", "w-full sm:flex-1")}
            >
              Fechar
            </button>
          ) : (
            <>
              <button
                type="button"
                onClick={onClose}
                className={buttonClasses("secondary", "lg", "w-full sm:flex-1")}
              >
                Cancelar
              </button>
              <button
                type="submit"
                disabled={busy}
                className={buttonClasses("primary", "lg", "w-full sm:flex-1")}
              >
                Enviar denúncia
              </button>
            </>
          )}
        </footer>
      </form>
    </dialog>
  );
}
