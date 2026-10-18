"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";

import { createStyle, renameStyle, type Style } from "@/api/admin";
import type { ApiResult } from "@/api/browser";
import { Button } from "@/components/ui/Button";

import { Field, fieldAria, fieldControl } from "./Field";
import { genericMessage, styleMessages } from "./messages";

export interface StylesPanelProps {
  initialStyles: Style[];
  /** Mensagem de sucesso, anunciada na região `aria-live` do painel. */
  onSuccess: (message: string) => void;
}

/** Id do botão "Renomear" de um estilo (alvo do foco ao fechar a renomeação). */
function renameButtonId(slug: string): string {
  return `renomear-botao-${slug}`;
}

function byName(a: Style, b: Style): number {
  return a.name.localeCompare(b.name, "pt-BR") || a.slug.localeCompare(b.slug);
}

/** Mensagem do erro da API para o campo do nome do estilo. */
function styleError(result: Extract<ApiResult<Style>, { ok: false }>): string {
  switch (result.code) {
    case "events.style_taken":
      return styleMessages.taken;
    case "events.invalid_style":
      return styleMessages.invalid;
    case "events.style_not_found":
      return styleMessages.notFound;
    default:
      return genericMessage(result.status);
  }
}

/**
 * SPEC-0017 — coluna ESTILOS do painel: os estilos em chips, "Novo estilo" + "Adicionar" e "Renomear" em cada chip
 * (o slug não muda). Erros no campo (`aria-invalid` + `aria-describedby`); o sucesso é anunciado por `onSuccess`.
 */
export function StylesPanel({ initialStyles, onSuccess }: StylesPanelProps) {
  const [styles, setStyles] = useState(initialStyles);
  const [renaming, setRenaming] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  /** Estilo cujo botão "Renomear" recebe o foco depois de salvar ou cancelar a renomeação. */
  const [focusSlug, setFocusSlug] = useState<string | null>(null);
  const field = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (focusSlug) {
      document.getElementById(renameButtonId(focusSlug))?.focus();
      setFocusSlug(null);
    }
  }, [focusSlug]);

  function closeRename(slug: string) {
    setRenaming(null);
    setFocusSlug(slug);
  }

  function saved(style: Style, message: string) {
    setStyles((current) =>
      [...current.filter((item) => item.slug !== style.slug), style].sort(
        byName,
      ),
    );
    onSuccess(message);
  }

  async function onAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    const result = await createStyle(name);
    setSaving(false);
    if (result.ok) {
      setError(null);
      setName("");
      saved(result.value, styleMessages.added);
      return;
    }
    setError(styleError(result));
    field.current?.focus();
  }

  return (
    <section aria-labelledby="estilos" className="flex flex-col gap-4">
      <h2
        id="estilos"
        className="font-display text-2xl uppercase tracking-wide text-text"
      >
        Estilos
      </h2>
      <ul className="flex flex-wrap gap-2">
        {styles.map((style) => (
          <li key={style.slug}>
            {renaming === style.slug ? (
              <RenameStyleForm
                style={style}
                onSaved={(renamed) => {
                  closeRename(renamed.slug);
                  saved(renamed, styleMessages.renamed);
                }}
                onCancel={() => closeRename(style.slug)}
              />
            ) : (
              <div className="inline-flex min-h-11 items-center gap-1 rounded-md border border-border bg-surface pl-3.5">
                <span className="font-label text-xs font-bold uppercase tracking-wider text-text">
                  {style.name}
                </span>
                <Button
                  id={renameButtonId(style.slug)}
                  variant="ghost"
                  className="px-2 text-xs normal-case text-text-muted"
                  aria-label={`Renomear ${style.name}`}
                  onClick={() => setRenaming(style.slug)}
                >
                  Renomear
                </Button>
              </div>
            )}
          </li>
        ))}
      </ul>
      <form noValidate onSubmit={onAdd} className="flex flex-col gap-3">
        <Field id="novo-estilo" label="Novo estilo" error={error}>
          <input
            ref={field}
            {...fieldAria("novo-estilo", error)}
            name="name"
            type="text"
            autoComplete="off"
            maxLength={30}
            value={name}
            onChange={(event) => setName(event.target.value)}
            className={fieldControl}
          />
        </Field>
        <Button type="submit" disabled={saving} className="self-start">
          Adicionar
        </Button>
      </form>
    </section>
  );
}

interface RenameStyleFormProps {
  style: Style;
  onSaved: (style: Style) => void;
  onCancel: () => void;
}

/** Renomear no próprio chip: campo com o nome atual, "Salvar" e "Cancelar". */
function RenameStyleForm({ style, onSaved, onCancel }: RenameStyleFormProps) {
  const [name, setName] = useState(style.name);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const field = useRef<HTMLInputElement>(null);
  const id = `renomear-${style.slug}`;

  useEffect(() => {
    field.current?.focus();
  }, []);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    const result = await renameStyle(style.slug, name);
    setSaving(false);
    if (result.ok) {
      onSaved(result.value);
      return;
    }
    setError(styleError(result));
    field.current?.focus();
  }

  return (
    <form
      noValidate
      onSubmit={onSubmit}
      className="flex flex-col gap-2 rounded-md border border-border bg-surface-raised p-3"
    >
      <Field id={id} label={`Renomear ${style.name}`} error={error}>
        <input
          ref={field}
          {...fieldAria(id, error)}
          name="name"
          type="text"
          autoComplete="off"
          maxLength={30}
          value={name}
          onChange={(event) => setName(event.target.value)}
          className={fieldControl}
        />
      </Field>
      <div className="flex gap-2">
        <Button type="submit" disabled={saving}>
          Salvar
        </Button>
        <Button variant="ghost" onClick={onCancel}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}
