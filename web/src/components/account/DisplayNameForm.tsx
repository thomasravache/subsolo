"use client";

import { useState, type FormEvent } from "react";

import { updateDisplayName } from "@/api/account";
import { Field, fieldAria, fieldControl } from "@/components/admin/Field";
import { Button } from "@/components/ui/Button";

import { accountErrorMessage, accountMessages } from "./messages";

const fieldId = "nome-de-exibicao";

export interface DisplayNameFormProps {
  initialName: string | null;
}

/**
 * SPEC-0019 — "Nome de exibição" + "Salvar" (`PUT /api/v1/me`): o sucesso é anunciado ("Nome salvo.", região
 * `role="status"`); o nome fora da regra mostra "Use de 1 a 60 caracteres." ligado ao campo. A regra é conferida pela
 * API (sem espaços nas pontas, até 60 caracteres; vazio remove o nome).
 */
export function DisplayNameForm({ initialName }: DisplayNameFormProps) {
  const [name, setName] = useState(initialName ?? "");
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [status, setStatus] = useState("");
  const [saving, setSaving] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setStatus("");
    setFormError(null);
    const result = await updateDisplayName(name);
    setSaving(false);
    if (result.ok) {
      setError(null);
      setName(result.value ?? "");
      setStatus(accountMessages.nameSaved);
      return;
    }
    if (result.code === "users.invalid_display_name") {
      setError(accountMessages.invalidName);
      document.getElementById(fieldId)?.focus();
      return;
    }
    setFormError(accountErrorMessage(result.status));
  }

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-3">
      <Field id={fieldId} label="Nome de exibição" error={error}>
        <input
          {...fieldAria(fieldId, error)}
          name="displayName"
          type="text"
          autoComplete="nickname"
          value={name}
          onChange={(event) => setName(event.target.value)}
          className={fieldControl}
        />
      </Field>
      <Button type="submit" disabled={saving} className="self-start">
        Salvar
      </Button>
      <p role="status" className="text-sm text-text-muted empty:hidden">
        {status}
      </p>
      {formError && (
        <p role="alert" className="text-sm font-medium text-danger">
          {formError}
        </p>
      )}
    </form>
  );
}
