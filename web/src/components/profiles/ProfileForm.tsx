"use client";

import { useRouter } from "next/navigation";
import { useId, useState, type FormEvent } from "react";

import type { Style } from "@/api/admin";
import type { ApiResult } from "@/api/browser";
import type { CitySummary } from "@/api/client";
import {
  createOwnerlessProfile,
  requestProfile,
  type ProfileInput,
  type ProfileType,
} from "@/api/profiles";
import { Field, fieldAria, fieldControl } from "@/components/admin/Field";
import { fieldMessage } from "@/components/admin/messages";
import { Button } from "@/components/ui/Button";
import { Chip } from "@/components/ui/Chip";

import {
  profileErrorMessage,
  profileMessages,
  profileTypeLabels,
  requestSentPath,
} from "./messages";

export interface ProfileFormProps {
  /** `request`: pedido do fã (com o contato); `ownerless`: perfil sem dono criado pelo admin (sem o contato). */
  mode: "request" | "ownerless";
  cities: CitySummary[];
  styles: Style[];
}

type FieldName =
  | "type"
  | "name"
  | "city"
  | "styles"
  | "instagram"
  | "bandcamp"
  | "metalArchives"
  | "contact";

/** Campo do formulário → chave do erro na API (PascalCase, lida sem diferenciar caixa). */
const apiFields: Record<FieldName, string> = {
  type: "Type",
  name: "Name",
  city: "City",
  styles: "Styles",
  instagram: "Links.Instagram",
  bandcamp: "Links.Bandcamp",
  metalArchives: "Links.MetalArchives",
  contact: "Contact",
};

const types: ProfileType[] = ["band", "venue", "producer"];

const legendClass =
  "font-label text-xs font-bold uppercase tracking-wider text-text";

const emptyLinks = { instagram: "", bandcamp: "", metalArchives: "" };

/** Vazio → `null` (link não informado). */
function link(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

/**
 * SPEC-0021 — formulário de perfil (tela "Pedir Perfil" do Stitch, com "Bandcamp / Soundcloud" virando só Bandcamp):
 * tipo (grupo de rádio "Tipo": Banda | Casa / Local | Produtor), nome, cidade, estilos (só banda, chips), links e, no
 * pedido, o contato, com o aviso de aprovação. O pedido enviado leva a "Meus perfis" com "Pedido enviado."; o perfil
 * sem dono criado pelo admin anuncia "Perfil criado." e limpa o formulário. Erros no campo (`aria-invalid` +
 * `aria-describedby`).
 */
export function ProfileForm({ mode, cities, styles }: ProfileFormProps) {
  const router = useRouter();
  const id = useId();
  const [type, setType] = useState<ProfileType | null>(null);
  const [name, setName] = useState("");
  const [city, setCity] = useState("");
  const [picked, setPicked] = useState<string[]>([]);
  const [links, setLinks] = useState(emptyLinks);
  const [contact, setContact] = useState("");
  const [errors, setErrors] = useState<Partial<Record<FieldName, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [status, setStatus] = useState("");
  const [saving, setSaving] = useState(false);

  const fieldId = (field: FieldName) => `${id}-${field}`;
  const isBand = type === "band";

  function toggleStyle(slug: string) {
    setPicked((current) =>
      current.includes(slug)
        ? current.filter((item) => item !== slug)
        : [...current, slug],
    );
  }

  function showErrors(result: Extract<ApiResult<string>, { ok: false }>) {
    if (result.code !== "profiles.invalid_request") {
      setErrors({});
      setFormError(profileErrorMessage(result));
      return;
    }
    const found: Partial<Record<FieldName, string>> = {};
    for (const [field, apiField] of Object.entries(apiFields) as [
      FieldName,
      string,
    ][]) {
      const message = fieldMessage(result, apiField);
      if (message) {
        found[field] = message;
      }
    }
    setErrors(found);
    setFormError(
      Object.keys(found).length === 0 ? profileErrorMessage(result) : null,
    );
    const first = (Object.keys(apiFields) as FieldName[]).find(
      (field) => found[field],
    );
    if (first) {
      document.getElementById(fieldId(first))?.focus();
    }
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setStatus("");
    setFormError(null);
    const input: ProfileInput = {
      type,
      name,
      city,
      styles: isBand ? picked : [],
      links: {
        instagram: link(links.instagram),
        bandcamp: link(links.bandcamp),
        metalArchives: link(links.metalArchives),
      },
    };
    const result =
      mode === "request"
        ? await requestProfile(input, contact)
        : await createOwnerlessProfile(input);
    setSaving(false);
    if (!result.ok) {
      showErrors(result);
      return;
    }
    setErrors({});
    if (mode === "request") {
      router.push(requestSentPath);
      return;
    }
    setType(null);
    setName("");
    setCity("");
    setPicked([]);
    setLinks(emptyLinks);
    setStatus(profileMessages.profileCreated);
  }

  const typeLabelId = `${id}-tipo`;
  const typeError = errors.type;
  const stylesLabelId = `${id}-estilos`;

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-5">
      <div className="flex flex-col gap-1.5">
        <span id={typeLabelId} className={legendClass}>
          Tipo
        </span>
        <div
          role="radiogroup"
          aria-labelledby={typeLabelId}
          aria-describedby={typeError ? `${fieldId("type")}-erro` : undefined}
          className="flex flex-wrap gap-2"
        >
          {types.map((option) => (
            <label
              key={option}
              className="inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-md border border-border bg-surface px-3.5 font-label text-xs font-bold uppercase tracking-wider text-text has-[:checked]:border-accent has-[:checked]:bg-accent has-[:checked]:text-text-on-accent"
            >
              <input
                id={option === types[0] ? fieldId("type") : undefined}
                type="radio"
                name={`${id}-type`}
                value={option}
                checked={type === option}
                onChange={() => setType(option)}
                className="h-4 w-4 accent-accent"
              />
              {profileTypeLabels[option]}
            </label>
          ))}
        </div>
        {typeError && (
          <p
            id={`${fieldId("type")}-erro`}
            className="text-sm font-medium text-danger"
          >
            {typeError}
          </p>
        )}
      </div>

      <Field id={fieldId("name")} label="Nome" error={errors.name}>
        <input
          {...fieldAria(fieldId("name"), errors.name)}
          name="name"
          type="text"
          autoComplete="off"
          maxLength={80}
          value={name}
          onChange={(event) => setName(event.target.value)}
          className={fieldControl}
        />
      </Field>

      <Field id={fieldId("city")} label="Cidade" error={errors.city}>
        <select
          {...fieldAria(fieldId("city"), errors.city)}
          name="city"
          value={city}
          onChange={(event) => setCity(event.target.value)}
          className={fieldControl}
        >
          <option value="">Escolha a cidade</option>
          {cities.map((option) => (
            <option key={option.slug} value={option.slug}>
              {option.name}
            </option>
          ))}
        </select>
      </Field>

      {isBand && (
        <div
          role="group"
          aria-labelledby={stylesLabelId}
          aria-describedby={
            errors.styles
              ? `${fieldId("styles")}-erro`
              : `${fieldId("styles")}-dica`
          }
          className="flex flex-col gap-1.5"
        >
          <span id={stylesLabelId} className={legendClass}>
            Estilos
          </span>
          <ul className="flex flex-wrap gap-2">
            {styles.map((style) => (
              <li key={style.slug}>
                <Chip
                  selected={picked.includes(style.slug)}
                  onToggle={() => toggleStyle(style.slug)}
                >
                  {style.name}
                </Chip>
              </li>
            ))}
          </ul>
          <p
            id={`${fieldId("styles")}-dica`}
            className="text-sm text-text-muted"
          >
            Escolha de 1 a 5.
          </p>
          {errors.styles && (
            <p
              id={`${fieldId("styles")}-erro`}
              className="text-sm font-medium text-danger"
            >
              {errors.styles}
            </p>
          )}
        </div>
      )}

      <Field
        id={fieldId("instagram")}
        label="Instagram"
        error={errors.instagram}
      >
        <input
          {...fieldAria(fieldId("instagram"), errors.instagram)}
          name="instagram"
          type="url"
          inputMode="url"
          placeholder="https://www.instagram.com/…"
          value={links.instagram}
          onChange={(event) =>
            setLinks({ ...links, instagram: event.target.value })
          }
          className={fieldControl}
        />
      </Field>
      <Field id={fieldId("bandcamp")} label="Bandcamp" error={errors.bandcamp}>
        <input
          {...fieldAria(fieldId("bandcamp"), errors.bandcamp)}
          name="bandcamp"
          type="url"
          inputMode="url"
          placeholder="https://seu-nome.bandcamp.com"
          value={links.bandcamp}
          onChange={(event) =>
            setLinks({ ...links, bandcamp: event.target.value })
          }
          className={fieldControl}
        />
      </Field>
      <Field
        id={fieldId("metalArchives")}
        label="Metal Archives"
        error={errors.metalArchives}
      >
        <input
          {...fieldAria(fieldId("metalArchives"), errors.metalArchives)}
          name="metalArchives"
          type="url"
          inputMode="url"
          placeholder="https://www.metal-archives.com/…"
          value={links.metalArchives}
          onChange={(event) =>
            setLinks({ ...links, metalArchives: event.target.value })
          }
          className={fieldControl}
        />
      </Field>

      {mode === "request" && (
        <>
          <Field
            id={fieldId("contact")}
            label="Contato"
            error={errors.contact}
            hint="Telefone, e-mail ou @ para o admin confirmar o pedido. Só o admin vê."
          >
            <input
              {...fieldAria(fieldId("contact"), errors.contact, true)}
              name="contact"
              type="text"
              autoComplete="off"
              maxLength={200}
              value={contact}
              onChange={(event) => setContact(event.target.value)}
              className={fieldControl}
            />
          </Field>
          <p className="rounded-md border border-border bg-surface p-3 text-sm text-text-muted">
            {profileMessages.approvalNotice}.
          </p>
        </>
      )}

      <Button type="submit" size="lg" disabled={saving}>
        {mode === "request" ? "Enviar pedido" : "Criar perfil"}
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
