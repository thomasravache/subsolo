"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";

import { createCity, updateCity, type AdminCity } from "@/api/admin";
import type { ApiResult } from "@/api/browser";
import { Button } from "@/components/ui/Button";

import { Field, fieldAria, fieldControl } from "./Field";
import { cityMessages, fieldMessage, genericMessage } from "./messages";
import { publicDomain, slugFromName } from "./slug";

/** As 27 UFs. */
const states = [
  "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
  "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
] as const; // prettier-ignore

/** Fusos do Brasil (IANA), com America/Sao_Paulo como padrão; a API aceita qualquer fuso IANA válido. */
const timeZones = [
  "America/Sao_Paulo",
  "America/Noronha",
  "America/Belem",
  "America/Fortaleza",
  "America/Recife",
  "America/Araguaina",
  "America/Maceio",
  "America/Bahia",
  "America/Campo_Grande",
  "America/Cuiaba",
  "America/Santarem",
  "America/Porto_Velho",
  "America/Boa_Vista",
  "America/Manaus",
  "America/Eirunepe",
  "America/Rio_Branco",
] as const;

const defaultTimeZone = "America/Sao_Paulo";

const newCityButtonId = "nova-cidade";

type Editing = { mode: "new" } | { mode: "edit"; city: AdminCity } | null;

type FieldName = "name" | "state" | "timeZone" | "slug";

type Errors = Partial<Record<FieldName, string>>;

export interface CitiesPanelProps {
  initialCities: AdminCity[];
  /** Mensagem de sucesso, anunciada na região `aria-live` do painel. */
  onSuccess: (message: string) => void;
}

function byName(a: AdminCity, b: AdminCity): number {
  return a.name.localeCompare(b.name, "pt-BR") || a.slug.localeCompare(b.slug);
}

/**
 * SPEC-0017 — coluna CIDADES do painel: tabela (Nome, UF, Fuso, Endereço `/{slug}`), "Nova cidade" com a prévia do
 * endereço gerado do nome (editável só na criação) e "Editar" em cada linha (endereço só leitura). Os erros da API
 * ficam no campo (`aria-invalid` + `aria-describedby`); o sucesso é anunciado por `onSuccess`.
 */
export function CitiesPanel({ initialCities, onSuccess }: CitiesPanelProps) {
  const [cities, setCities] = useState(initialCities);
  const [editing, setEditing] = useState<Editing>(null);
  const [formKey, setFormKey] = useState(0);

  function open(next: Editing) {
    setEditing(next);
    setFormKey((key) => key + 1);
  }

  function close() {
    setEditing(null);
    document.getElementById(newCityButtonId)?.focus();
  }

  function saved(city: AdminCity, message: string) {
    setCities((current) =>
      [...current.filter((item) => item.slug !== city.slug), city].sort(byName),
    );
    close();
    onSuccess(message);
  }

  return (
    <section aria-labelledby="cidades" className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2
          id="cidades"
          className="font-display text-2xl uppercase tracking-wide text-text"
        >
          Cidades
        </h2>
        <Button id={newCityButtonId} onClick={() => open({ mode: "new" })}>
          Nova cidade
        </Button>
      </div>
      {editing && (
        <CityForm
          key={formKey}
          city={editing.mode === "edit" ? editing.city : undefined}
          onSaved={saved}
          onCancel={close}
        />
      )}
      <div
        role="region"
        aria-labelledby="cidades"
        tabIndex={0}
        className="overflow-x-auto rounded-md border border-border"
      >
        <table className="w-full min-w-[36rem] border-collapse text-left text-sm">
          <thead className="bg-surface font-label text-xs uppercase tracking-wider text-text-muted">
            <tr>
              <th scope="col" className="px-3 py-2">
                Nome
              </th>
              <th scope="col" className="px-3 py-2">
                UF
              </th>
              <th scope="col" className="px-3 py-2">
                Fuso
              </th>
              <th scope="col" className="px-3 py-2">
                Endereço
              </th>
              <th scope="col" className="px-3 py-2">
                <span className="sr-only">Ações</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {cities.map((city) => (
              <tr key={city.slug} className="border-t border-border">
                <td className="px-3 py-2 text-text">{city.name}</td>
                <td className="px-3 py-2 font-label text-text">{city.state}</td>
                <td className="px-3 py-2 font-label text-text-muted">
                  {city.timeZone}
                </td>
                <td className="px-3 py-2 font-label text-text-muted">
                  /{city.slug}
                </td>
                <td className="px-3 py-1 text-right">
                  <Button
                    variant="ghost"
                    aria-label={`Editar ${city.name}`}
                    onClick={() => open({ mode: "edit", city })}
                  >
                    Editar
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

interface CityFormProps {
  /** Cidade em edição; sem ela, o formulário cria uma cidade nova. */
  city?: AdminCity;
  onSaved: (city: AdminCity, message: string) => void;
  onCancel: () => void;
}

/** Erros da API nos campos do formulário (ou, sem campo, num alerta do formulário). */
function errorsOf(result: Extract<ApiResult<AdminCity>, { ok: false }>): {
  fields: Errors;
  form?: string;
} {
  switch (result.code) {
    case "events.city_slug_reserved":
      return { fields: { slug: cityMessages.slugReserved } };
    case "events.city_slug_taken":
      return { fields: { slug: cityMessages.slugTaken } };
    case "events.city_not_found":
      return { fields: {}, form: cityMessages.notFound };
    case "events.invalid_city": {
      const fields: Errors = {};
      for (const name of ["name", "state", "timeZone", "slug"] as const) {
        const message = fieldMessage(result, name);
        if (message) {
          fields[name] = message;
        }
      }
      return { fields };
    }
    default:
      return { fields: {}, form: genericMessage(result.status) };
  }
}

function CityForm({ city, onSaved, onCancel }: CityFormProps) {
  const editing = city !== undefined;
  const [name, setName] = useState(city?.name ?? "");
  const [state, setState] = useState(city?.state ?? "");
  const [timeZone, setTimeZone] = useState(city?.timeZone ?? defaultTimeZone);
  const [slug, setSlug] = useState(city?.slug ?? "");
  const [slugEdited, setSlugEdited] = useState(editing);
  const [errors, setErrors] = useState<Errors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const nameField = useRef<HTMLInputElement>(null);
  const form = useRef<HTMLFormElement>(null);

  useEffect(() => {
    nameField.current?.focus();
  }, []);

  const address = slugEdited ? slug : slugFromName(name);
  const zones: readonly string[] = timeZones.includes(
    timeZone as (typeof timeZones)[number],
  )
    ? timeZones
    : [timeZone, ...timeZones];

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setFormError(null);
    const result = city
      ? await updateCity(city.slug, { name, state, timeZone })
      : await createCity({
          name,
          state,
          timeZone,
          slug: slugEdited && slug ? slug : null,
        });
    setSaving(false);
    if (result.ok) {
      onSaved(result.value, city ? cityMessages.updated : cityMessages.created);
      return;
    }
    const { fields, form: message } = errorsOf(result);
    setErrors(fields);
    setFormError(message ?? null);
    const first = (["name", "state", "timeZone", "slug"] as const).find(
      (field) => fields[field],
    );
    if (first) {
      form.current
        ?.querySelector<HTMLElement>(`#cidade-${first.toLowerCase()}`)
        ?.focus();
    }
  }

  const title = city ? `Editar ${city.name}` : "Nova cidade";
  return (
    <form
      ref={form}
      noValidate
      aria-label={title}
      onSubmit={onSubmit}
      className="flex flex-col gap-4 rounded-md border border-border bg-surface-raised p-4"
    >
      <h3 className="font-label text-sm font-bold uppercase tracking-wider text-text">
        {title}
      </h3>
      <div className="grid gap-4 md:grid-cols-2">
        <Field id="cidade-name" label="Nome" error={errors.name}>
          <input
            ref={nameField}
            {...fieldAria("cidade-name", errors.name)}
            name="name"
            type="text"
            autoComplete="off"
            maxLength={60}
            value={name}
            onChange={(event) => setName(event.target.value)}
            className={fieldControl}
          />
        </Field>
        <Field id="cidade-state" label="UF" error={errors.state}>
          <select
            {...fieldAria("cidade-state", errors.state)}
            name="state"
            value={state}
            onChange={(event) => setState(event.target.value)}
            className={fieldControl}
          >
            <option value="" disabled>
              Escolha a UF
            </option>
            {states.map((uf) => (
              <option key={uf} value={uf}>
                {uf}
              </option>
            ))}
          </select>
        </Field>
        <Field id="cidade-timezone" label="Fuso" error={errors.timeZone}>
          <select
            {...fieldAria("cidade-timezone", errors.timeZone)}
            name="timeZone"
            value={timeZone}
            onChange={(event) => setTimeZone(event.target.value)}
            className={fieldControl}
          >
            {zones.map((zone) => (
              <option key={zone} value={zone}>
                {zone}
              </option>
            ))}
          </select>
        </Field>
        <Field
          id="cidade-slug"
          label="Endereço"
          error={errors.slug}
          hint={
            <>
              <span className="font-label text-text">
                {publicDomain}/{address}
              </span>
              {editing && " · o endereço não muda depois de criado."}
            </>
          }
        >
          <input
            {...fieldAria("cidade-slug", errors.slug, true)}
            name="slug"
            type="text"
            autoComplete="off"
            autoCapitalize="none"
            spellCheck={false}
            maxLength={60}
            readOnly={editing}
            value={address}
            onChange={(event) => {
              setSlugEdited(true);
              setSlug(event.target.value);
            }}
            className={fieldControl}
          />
        </Field>
      </div>
      {formError && (
        <p role="alert" className="text-sm font-medium text-danger">
          {formError}
        </p>
      )}
      <div className="flex flex-wrap gap-3">
        <Button type="submit" disabled={saving}>
          {city ? "Salvar" : "Criar cidade"}
        </Button>
        <Button variant="ghost" onClick={onCancel}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}
