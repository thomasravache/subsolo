"use client";

import { useRef, useState, type FormEvent } from "react";

import {
  addSupporter,
  removeSupporter,
  type AdminSupporter,
} from "@/api/admin";
import type { ApiResult } from "@/api/browser";
import { Button } from "@/components/ui/Button";

import { Field, fieldAria, fieldControl } from "./Field";
import { fieldMessage, genericMessage, supporterMessages } from "./messages";
import {
  RemoveSupporterDialog,
  type RemoveSupporterDialogHandle,
} from "./RemoveSupporterDialog";

export interface SupportersPanelProps {
  initialSupporters: AdminSupporter[];
}

interface FormErrors {
  name?: string;
  url?: string;
  form?: string;
}

/** Erros do cadastro por campo (`site.invalid_supporter` cita `Name` e/ou `Url`). */
function addErrors(
  result: Extract<ApiResult<string>, { ok: false }>,
): FormErrors {
  if (result.code === "site.invalid_supporter") {
    const name = fieldMessage(result, "name");
    const url = fieldMessage(result, "url");
    return name || url
      ? {
          name: name ? supporterMessages.invalidName : undefined,
          url: url ? supporterMessages.invalidUrl : undefined,
        }
      : { name: supporterMessages.invalidName };
  }
  return { form: genericMessage(result.status) };
}

/**
 * SPEC-0028 — conteúdo de `/admin/apoiadores` (tela "Admin - Apoiadores" do Stitch, na ordem de cadastro em vez da
 * alfabética): "NOVO APOIADOR" (Nome e Link opcional, "Adicionar") e a lista com "Remover" (com confirmação). O sucesso
 * é anunciado na região `aria-live`; os erros ficam nos campos (`aria-invalid` + `aria-describedby`).
 */
export function SupportersPanel({ initialSupporters }: SupportersPanelProps) {
  const [supporters, setSupporters] = useState(initialSupporters);
  const [name, setName] = useState("");
  const [url, setUrl] = useState("");
  const [errors, setErrors] = useState<FormErrors>({});
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [removing, setRemoving] = useState<AdminSupporter | null>(null);
  const [removeError, setRemoveError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const nameField = useRef<HTMLInputElement>(null);
  const urlField = useRef<HTMLInputElement>(null);
  const listTitle = useRef<HTMLHeadingElement>(null);
  const dialog = useRef<RemoveSupporterDialogHandle>(null);

  async function onAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    const trimmedName = name.trim();
    const trimmedUrl = url.trim() || null;
    const result = await addSupporter(trimmedName, trimmedUrl);
    setSaving(false);
    if (result.ok) {
      setErrors({});
      setName("");
      setUrl("");
      setSupporters((current) => [
        ...current,
        { id: result.value, name: trimmedName, url: trimmedUrl },
      ]);
      setMessage(supporterMessages.added);
      return;
    }
    const found = addErrors(result);
    setErrors(found);
    (found.url && !found.name ? urlField : nameField).current?.focus();
  }

  function askRemove(supporter: AdminSupporter, opener: HTMLElement) {
    setRemoving(supporter);
    setRemoveError(null);
    dialog.current?.open(opener);
  }

  async function confirmRemove(): Promise<boolean> {
    if (!removing) {
      return false;
    }
    setBusy(true);
    const result = await removeSupporter(removing.id);
    setBusy(false);
    if (result.ok || result.code === "site.supporter_not_found") {
      const id = removing.id;
      setSupporters((current) => current.filter((item) => item.id !== id));
      setMessage(
        result.ok ? supporterMessages.removed : supporterMessages.notFound,
      );
      return true;
    }
    setRemoveError(
      result.status === 401 || result.status === 403
        ? genericMessage(result.status)
        : supporterMessages.removeFailed,
    );
    return false;
  }

  return (
    <div className="flex flex-col gap-8">
      <p
        role="status"
        aria-live="polite"
        className="min-h-6 font-label text-sm font-bold uppercase tracking-wider text-text"
      >
        {message}
      </p>

      <section
        aria-labelledby="novo-apoiador"
        className="flex flex-col gap-4 rounded-md border border-border bg-surface p-4"
      >
        <h2
          id="novo-apoiador"
          className="font-display text-2xl uppercase tracking-wide text-text"
        >
          Novo apoiador
        </h2>
        <form noValidate onSubmit={onAdd} className="flex flex-col gap-4">
          <Field
            id="apoiador-nome"
            label="Nome do apoiador"
            error={errors.name}
          >
            <input
              ref={nameField}
              {...fieldAria("apoiador-nome", errors.name)}
              name="name"
              type="text"
              autoComplete="off"
              maxLength={60}
              required
              value={name}
              onChange={(event) => setName(event.target.value)}
              className={fieldControl}
            />
          </Field>
          <Field
            id="apoiador-link"
            label="Link (opcional)"
            error={errors.url}
            hint="Começa com https://."
          >
            <input
              ref={urlField}
              {...fieldAria("apoiador-link", errors.url, true)}
              name="url"
              type="url"
              inputMode="url"
              autoComplete="off"
              maxLength={300}
              placeholder="https://"
              value={url}
              onChange={(event) => setUrl(event.target.value)}
              className={fieldControl}
            />
          </Field>
          {errors.form && (
            <p role="alert" className="text-sm font-medium text-danger">
              {errors.form}
            </p>
          )}
          <Button type="submit" disabled={saving} className="self-start">
            Adicionar
          </Button>
        </form>
      </section>

      <section aria-labelledby="cadastrados" className="flex flex-col gap-4">
        <h2
          ref={listTitle}
          id="cadastrados"
          tabIndex={-1}
          className="font-display text-2xl uppercase tracking-wide text-text"
        >
          Cadastrados ({supporters.length})
        </h2>
        {supporters.length === 0 ? (
          <p className="text-base text-text-muted">
            Nenhum apoiador cadastrado.
          </p>
        ) : (
          <ul className="flex flex-col gap-2">
            {supporters.map((supporter) => (
              <li
                key={supporter.id}
                className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4 sm:flex-row sm:items-center sm:justify-between"
              >
                <div className="flex min-w-0 flex-col gap-1">
                  <span className="font-label text-base font-bold text-text">
                    {supporter.name}
                  </span>
                  {supporter.url ? (
                    <a
                      href={supporter.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="break-all text-sm text-text-muted underline underline-offset-4 hover:text-text"
                    >
                      {supporter.url}
                      <span className="sr-only"> (abre em nova aba)</span>
                    </a>
                  ) : (
                    <span className="text-sm text-text-muted">Sem link</span>
                  )}
                </div>
                <Button
                  variant="secondary"
                  className="self-start sm:self-auto"
                  aria-label={`Remover ${supporter.name}`}
                  onClick={(event) => askRemove(supporter, event.currentTarget)}
                >
                  Remover
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <RemoveSupporterDialog
        ref={dialog}
        name={removing?.name ?? ""}
        busy={busy}
        error={removeError}
        onConfirm={confirmRemove}
        onRemoved={() => listTitle.current?.focus()}
      />
    </div>
  );
}
