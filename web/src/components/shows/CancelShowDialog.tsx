"use client";

import {
  forwardRef,
  useImperativeHandle,
  useRef,
  type KeyboardEvent,
} from "react";

import { Button, buttonClasses } from "@/components/ui/Button";

import { showMessages } from "./messages";

/** Elementos que recebem o foco pelo Tab dentro do diálogo. */
const focusable =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export interface CancelShowDialogHandle {
  /** Abre o diálogo; ao fechar, o foco volta para `opener`. */
  open: (opener: HTMLElement | null) => void;
}

export interface CancelShowDialogProps {
  /** Id único (o título e o texto do diálogo são ligados por ele). */
  id: string;
  title: string;
  busy: boolean;
  onConfirm: () => Promise<boolean>;
}

/**
 * SPEC-0023 — confirmação de "Cancelar show" ("Meus Shows - Produtor" do Stitch, com o texto do §6): `dialog` modal com
 * rótulo, foco preso (Tab e Shift+Tab giram dentro dele), "Voltar" em foco ao abrir e o foco de volta no botão que o
 * abriu ao fechar (como o "Excluir conta" da SPEC-0019).
 */
export const CancelShowDialog = forwardRef<
  CancelShowDialogHandle,
  CancelShowDialogProps
>(function CancelShowDialog({ id, title, busy, onConfirm }, ref) {
  const dialog = useRef<HTMLDialogElement>(null);
  const back = useRef<HTMLButtonElement>(null);
  const opener = useRef<HTMLElement | null>(null);
  const titleId = `${id}-titulo`;
  const textId = `${id}-texto`;

  useImperativeHandle(ref, () => ({
    open(element) {
      opener.current = element;
      dialog.current?.showModal();
      back.current?.focus();
    },
  }));

  function close() {
    dialog.current?.close();
  }

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

  async function confirm() {
    if (await onConfirm()) {
      close();
    }
  }

  return (
    <dialog
      ref={dialog}
      aria-labelledby={titleId}
      aria-describedby={textId}
      onKeyDown={trapFocus}
      onClose={() => opener.current?.focus()}
      className="m-auto w-[calc(100%-2rem)] max-w-md rounded-md border border-border bg-surface-raised p-6 text-text backdrop:bg-bg/80"
    >
      <div className="flex flex-col gap-4">
        <h2
          id={titleId}
          className="font-display text-3xl uppercase leading-none tracking-wide text-text"
        >
          Cancelar show
        </h2>
        <p className="font-label text-xs uppercase tracking-wider text-text-muted">
          {title}
        </p>
        <p id={textId} className="text-base text-text-muted">
          {showMessages.cancelQuestion}
        </p>
        <div className="flex flex-col gap-2">
          <Button size="lg" disabled={busy} onClick={() => void confirm()}>
            Cancelar show
          </Button>
          <button
            ref={back}
            type="button"
            onClick={close}
            className={buttonClasses("secondary", "lg")}
          >
            Voltar
          </button>
        </div>
      </div>
    </dialog>
  );
});
