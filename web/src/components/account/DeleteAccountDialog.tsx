"use client";

import { useRef, useState, type FormEvent, type KeyboardEvent } from "react";

import { deleteAccount } from "@/api/account";
import { Field, fieldAria, fieldControl } from "@/components/admin/Field";
import { Button } from "@/components/ui/Button";

import {
  accountErrorMessage,
  accountMessages,
  deletedAccountPath,
} from "./messages";

const titleId = "excluir-conta-titulo";
const textId = "excluir-conta-aviso";
const fieldId = "excluir-conta-email";

/** Elementos que recebem o foco pelo Tab dentro do diálogo. */
const focusable =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/** Contorno vermelho do botão que abre a confirmação (texto `--color-danger`, contraste AA no card). */
const openerClasses =
  "inline-flex min-h-11 min-w-11 items-center justify-center gap-2 self-start rounded-md border border-danger px-4 py-2 font-label text-sm font-bold uppercase tracking-wider text-danger transition-colors hover:bg-bg";

/**
 * SPEC-0019 — "Excluir minha conta" e a confirmação "Excluir conta?" (tela "Excluir Conta - Subsolo" do Stitch): um
 * `dialog` modal com rótulo, foco preso (Tab e Shift+Tab giram dentro dele) e o foco de volta no botão ao fechar. O fã
 * digita o próprio e-mail; e-mail diferente → "O e-mail não confere." ligado ao campo. Excluída a conta, a API apaga o
 * cookie e a página vai para `/` com o aviso, trocando a entrada do histórico.
 */
export function DeleteAccountDialog() {
  const dialog = useRef<HTMLDialogElement>(null);
  const opener = useRef<HTMLButtonElement>(null);
  const field = useRef<HTMLInputElement>(null);
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  function open() {
    setEmail("");
    setError(null);
    setFormError(null);
    dialog.current?.showModal();
    field.current?.focus();
  }

  function close() {
    dialog.current?.close();
  }

  /** Mantém o Tab dentro do diálogo: do último volta ao primeiro, e do primeiro (com Shift) vai ao último. */
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

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setDeleting(true);
    setFormError(null);
    const result = await deleteAccount(email);
    if (result.ok) {
      window.location.replace(deletedAccountPath);
      return;
    }
    setDeleting(false);
    if (result.code === "users.delete_confirmation_mismatch") {
      setError(accountMessages.emailMismatch);
      field.current?.focus();
      return;
    }
    setError(null);
    setFormError(accountErrorMessage(result.status));
  }

  return (
    <>
      <button
        ref={opener}
        type="button"
        onClick={open}
        className={openerClasses}
      >
        Excluir minha conta
      </button>
      <dialog
        ref={dialog}
        aria-labelledby={titleId}
        aria-describedby={textId}
        onKeyDown={trapFocus}
        onClose={() => opener.current?.focus()}
        className="m-auto w-[calc(100%-2rem)] max-w-md rounded-md border border-border bg-surface-raised p-6 text-text backdrop:bg-bg/80"
      >
        <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          <h2
            id={titleId}
            className="font-display text-3xl uppercase leading-none tracking-wide text-text"
          >
            Excluir conta?
          </h2>
          <p id={textId} className="text-base text-text-muted">
            Isso apaga sua conta e tudo o que o Subsolo guarda de você. Não dá
            para desfazer.
          </p>
          <Field
            id={fieldId}
            label="Digite seu e-mail para confirmar"
            error={error}
          >
            <input
              ref={field}
              {...fieldAria(fieldId, error)}
              name="confirmEmail"
              type="email"
              inputMode="email"
              autoComplete="off"
              autoCapitalize="none"
              spellCheck={false}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className={fieldControl}
            />
          </Field>
          {formError && (
            <p role="alert" className="text-sm font-medium text-danger">
              {formError}
            </p>
          )}
          <div className="flex flex-col gap-2">
            <Button type="submit" size="lg" disabled={deleting}>
              Excluir para sempre
            </Button>
            <Button variant="secondary" size="lg" onClick={close}>
              Cancelar
            </Button>
          </div>
        </form>
      </dialog>
    </>
  );
}
