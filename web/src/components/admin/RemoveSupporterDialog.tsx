"use client";

import {
  forwardRef,
  useImperativeHandle,
  useRef,
  type KeyboardEvent,
} from "react";

import { Button, buttonClasses } from "@/components/ui/Button";

/** Elementos que recebem o foco pelo Tab dentro do diálogo. */
const focusable =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export interface RemoveSupporterDialogHandle {
  /** Abre o diálogo; ao fechar sem remover, o foco volta para `opener`. */
  open: (opener: HTMLElement | null) => void;
}

export interface RemoveSupporterDialogProps {
  /** Nome do apoiador a remover (no texto da confirmação). */
  name: string;
  busy: boolean;
  /** Erro da remoção, mostrado no diálogo. */
  error: string | null;
  /** Remove; `true` fecha o diálogo. */
  onConfirm: () => Promise<boolean>;
  /** Chamado ao fechar depois de remover (o botão que abriu não existe mais). */
  onRemoved: () => void;
}

/**
 * SPEC-0028 — confirmação de "Remover" apoiador (tela "Admin - Apoiadores" do Stitch): `dialog` modal com rótulo, foco
 * preso (Tab e Shift+Tab giram dentro dele), "Cancelar" em foco ao abrir e o foco de volta no botão que o abriu ao
 * cancelar (como o "Cancelar show" da SPEC-0023).
 */
export const RemoveSupporterDialog = forwardRef<
  RemoveSupporterDialogHandle,
  RemoveSupporterDialogProps
>(function RemoveSupporterDialog(
  { name, busy, error, onConfirm, onRemoved },
  ref,
) {
  const dialog = useRef<HTMLDialogElement>(null);
  const cancel = useRef<HTMLButtonElement>(null);
  const opener = useRef<HTMLElement | null>(null);
  const removed = useRef(false);

  useImperativeHandle(ref, () => ({
    open(element) {
      opener.current = element;
      removed.current = false;
      dialog.current?.showModal();
      cancel.current?.focus();
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
      removed.current = true;
      close();
    }
  }

  function onClose() {
    if (removed.current) {
      onRemoved();
    } else {
      opener.current?.focus();
    }
  }

  return (
    <dialog
      ref={dialog}
      aria-labelledby="remover-apoiador-titulo"
      aria-describedby="remover-apoiador-texto"
      onKeyDown={trapFocus}
      onClose={onClose}
      className="m-auto w-[calc(100%-2rem)] max-w-md rounded-md border border-border bg-surface-raised p-6 text-text backdrop:bg-bg/80"
    >
      <div className="flex flex-col gap-4">
        <h2
          id="remover-apoiador-titulo"
          className="font-display text-3xl uppercase leading-none tracking-wide text-text"
        >
          Confirmar remoção
        </h2>
        <p id="remover-apoiador-texto" className="text-base text-text-muted">
          Remover <strong className="text-text">{name}</strong> da lista de
          apoiadores do Subsolo?
        </p>
        {error && (
          <p role="alert" className="text-sm font-medium text-danger">
            {error}
          </p>
        )}
        <div className="flex flex-col gap-2">
          <Button size="lg" disabled={busy} onClick={() => void confirm()}>
            Remover
          </Button>
          <button
            ref={cancel}
            type="button"
            onClick={close}
            className={buttonClasses("secondary", "lg")}
          >
            Cancelar
          </button>
        </div>
      </div>
    </dialog>
  );
});
