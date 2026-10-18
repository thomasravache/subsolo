"use client";

import { useRouter } from "next/navigation";
import {
  useEffect,
  useId,
  useRef,
  useState,
  type ChangeEvent,
  type FormEvent,
  type KeyboardEvent,
} from "react";

import type { Style } from "@/api/admin";
import type { ApiResult } from "@/api/browser";
import type { CitySummary } from "@/api/client";
import { searchClaimableProfiles, type ClaimableProfile } from "@/api/profiles";
import {
  createShow,
  updateShow,
  uploadFlyer,
  type ShowForEdit,
  type ShowInput,
} from "@/api/shows";
import { Field, fieldAria, fieldControl } from "@/components/admin/Field";
import { fieldMessage } from "@/components/admin/messages";
import { Button } from "@/components/ui/Button";
import { Chip } from "@/components/ui/Chip";

import {
  flyerErrorMessage,
  publishedPath,
  savedPath,
  showErrorMessage,
  showMessages,
} from "./messages";

/** Um perfil que pode organizar o show (do usuário ou, para o admin, qualquer um). */
export interface OrganizerOption {
  id: string;
  name: string;
  city: string;
}

export interface ShowFormProps {
  /** `create`: "Publicar show"; `edit`: "Salvar" (o slug não muda). */
  mode: "create" | "edit";
  cities: CitySummary[];
  styles: Style[];
  /** Os perfis do usuário (para o admin, os dele; os sem dono vêm da busca). */
  organizers: OrganizerOption[];
  isAdmin: boolean;
  /** O show na edição. */
  show?: ShowForEdit;
}

type FieldName =
  | "organizer"
  | "flyer"
  | "title"
  | "date"
  | "venue"
  | "city"
  | "lineup"
  | "styles"
  | "price"
  | "ticketUrl";

/** Campo do formulário → chaves do erro na API (PascalCase, lidas sem diferenciar caixa). */
const apiFields: [FieldName, string][] = [
  ["organizer", "OrganizerProfileId"],
  ["flyer", "FlyerId"],
  ["title", "Title"],
  ["date", "StartsLocal"],
  ["venue", "Venue"],
  ["city", "City"],
  ["lineup", "Lineup"],
  ["styles", "Styles"],
  ["price", "Price"],
  ["ticketUrl", "TicketUrl"],
];

const order: FieldName[] = apiFields.map(([field]) => field);

const legendClass =
  "font-label text-xs font-bold uppercase tracking-wider text-text";

const errorClass = "text-sm font-medium text-danger";

const maxBands = 15;

interface Band {
  key: number;
  profileId: string | null;
  name: string;
}

interface Flyer {
  id: string;
  thumbUrl: string;
}

/** O erro do campo `apiField` ou de um item dele (`Lineup[2]`). */
function apiFieldMessage(
  result: Extract<ApiResult<unknown>, { ok: false }>,
  apiField: string,
): string | undefined {
  const exact = fieldMessage(result, apiField);
  if (exact) {
    return exact;
  }
  const entry = Object.entries(result.fields ?? {}).find(([name]) =>
    name.toLowerCase().startsWith(`${apiField.toLowerCase()}[`),
  );
  return entry?.[1]?.[0];
}

/**
 * SPEC-0023 — "Cadastrar show" / "Editar show" (tela "Cadastrar Show - Subsolo" do Stitch): organizador (com mais de um
 * perfil, ou para o admin, que também busca perfis sem dono), flyer (enviado na hora, com prévia), nome, data e horário
 * (campos nativos), casa (perfil de casa da busca ou nome livre), cidade, line-up ("Adicionar outra banda"), estilos
 * (chips), preço e link do ingresso. "Publicar show" / "Salvar" levam a "Meus shows" com o aviso; os erros da API ficam
 * no campo (`aria-invalid` + `aria-describedby`), e o foco vai ao primeiro.
 */
export function ShowForm({
  mode,
  cities,
  styles,
  organizers,
  isAdmin,
  show,
}: ShowFormProps) {
  const router = useRouter();
  const id = useId();
  // Chave das bandas novas (as iniciais usam a posição: 0, 1, …).
  const bandKey = useRef(Math.max(show?.lineup.length ?? 0, 1));
  const initialOrganizer: OrganizerOption | null = show
    ? {
        id: show.organizerProfileId,
        name:
          show.organizerName ||
          organizers.find((option) => option.id === show.organizerProfileId)
            ?.name ||
          "Perfil organizador",
        city: show.city,
      }
    : (organizers[0] ?? null);
  const [organizerOptions, setOrganizerOptions] = useState<OrganizerOption[]>(
    () =>
      initialOrganizer &&
      !organizers.some((option) => option.id === initialOrganizer.id)
        ? [initialOrganizer, ...organizers]
        : organizers,
  );
  const [organizer, setOrganizer] = useState(initialOrganizer?.id ?? "");
  const [flyer, setFlyer] = useState<Flyer | null>(
    show ? { id: show.flyerId, thumbUrl: show.flyerThumbUrl } : null,
  );
  const [flyerStatus, setFlyerStatus] = useState("");
  const [uploading, setUploading] = useState(false);
  const [title, setTitle] = useState(show?.title ?? "");
  const [date, setDate] = useState(show?.startsLocal.slice(0, 10) ?? "");
  const [time, setTime] = useState(show?.startsLocal.slice(11, 16) ?? "21:00");
  const [venueName, setVenueName] = useState(show?.venue.name ?? "");
  const [venueProfileId, setVenueProfileId] = useState<string | null>(
    show?.venue.profileId ?? null,
  );
  const [venueSuggestions, setVenueSuggestions] = useState<ClaimableProfile[]>(
    [],
  );
  const [city, setCity] = useState(show?.city ?? initialOrganizer?.city ?? "");
  const [bands, setBands] = useState<Band[]>(() =>
    show && show.lineup.length > 0
      ? show.lineup.map((item, index) => ({
          key: index,
          profileId: item.profileId,
          name: item.name,
        }))
      : [{ key: 0, profileId: null, name: "" }],
  );
  const [picked, setPicked] = useState<string[]>(show?.styles ?? []);
  const [price, setPrice] = useState(show?.price ?? "");
  const [ticketUrl, setTicketUrl] = useState(show?.ticketUrl ?? "");
  const [errors, setErrors] = useState<Partial<Record<FieldName, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [organizerQuery, setOrganizerQuery] = useState("");
  const [organizerResults, setOrganizerResults] = useState<ClaimableProfile[]>(
    [],
  );

  const fieldId = (field: FieldName) => `${id}-${field}`;
  const bandId = (index: number) => `${id}-banda-${index}`;
  const showOrganizerPicker = isAdmin || organizerOptions.length > 1;

  // Busca de perfis de casa (sem dono, os que o admin cadastra) para ligar a casa ao perfil.
  useEffect(() => {
    const term = venueName.trim();
    if (venueProfileId || term.length < 2) {
      return;
    }
    const controller = new AbortController();
    const timer = setTimeout(() => {
      void searchClaimableProfiles(term, controller.signal).then((result) => {
        if (result.ok) {
          setVenueSuggestions(
            result.value.filter((profile) => profile.type === "venue"),
          );
        }
      });
    }, 300);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [venueName, venueProfileId]);

  // Admin: busca de perfis sem dono para organizar o show.
  useEffect(() => {
    const term = organizerQuery.trim();
    if (!isAdmin || term.length < 2) {
      return;
    }
    const controller = new AbortController();
    const timer = setTimeout(() => {
      void searchClaimableProfiles(term, controller.signal).then((result) => {
        if (result.ok) {
          setOrganizerResults(result.value);
        }
      });
    }, 300);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [isAdmin, organizerQuery]);

  function chooseOrganizer(option: OrganizerOption) {
    setOrganizerOptions((current) =>
      current.some((item) => item.id === option.id)
        ? current
        : [...current, option],
    );
    setOrganizer(option.id);
    if (!city) {
      setCity(option.city);
    }
    setOrganizerQuery("");
    setOrganizerResults([]);
  }

  function onOrganizerChange(value: string) {
    setOrganizer(value);
    const option = organizerOptions.find((item) => item.id === value);
    if (option && !city) {
      setCity(option.city);
    }
  }

  async function onFlyer(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }
    setUploading(true);
    setFlyerStatus(showMessages.flyerUploading);
    setErrors((current) => ({ ...current, flyer: undefined }));
    const result = await uploadFlyer(file);
    setUploading(false);
    if (result.ok) {
      setFlyer({ id: result.value.flyerId, thumbUrl: result.value.thumbUrl });
      setFlyerStatus(showMessages.flyerSent);
      return;
    }
    setFlyerStatus("");
    setErrors((current) => ({ ...current, flyer: flyerErrorMessage(result) }));
  }

  function onVenueKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Escape") {
      setVenueSuggestions([]);
    }
  }

  function chooseVenue(profile: ClaimableProfile) {
    setVenueProfileId(profile.id);
    setVenueName(profile.name);
    setVenueSuggestions([]);
  }

  function setBandName(key: number, name: string) {
    setBands((current) =>
      current.map((band) =>
        band.key === key ? { ...band, name, profileId: null } : band,
      ),
    );
  }

  function addBand() {
    const key = bandKey.current++;
    setBands((current) => [...current, { key, profileId: null, name: "" }]);
    const index = bands.length;
    requestAnimationFrame(() =>
      document.getElementById(bandId(index))?.focus(),
    );
  }

  function removeBand(key: number) {
    setBands((current) => current.filter((band) => band.key !== key));
  }

  function toggleStyle(slug: string) {
    setPicked((current) =>
      current.includes(slug)
        ? current.filter((item) => item !== slug)
        : [...current, slug],
    );
  }

  function focusFirst(found: Partial<Record<FieldName, string>>) {
    const first = order.find((field) => found[field]);
    if (first) {
      document
        .getElementById(first === "lineup" ? bandId(0) : fieldId(first))
        ?.focus();
    }
  }

  function showErrors(result: Extract<ApiResult<string>, { ok: false }>) {
    if (
      result.code !== "events.invalid_show" &&
      result.code !== "events.flyer_not_found"
    ) {
      setErrors({});
      setFormError(showErrorMessage(result));
      return;
    }
    const found: Partial<Record<FieldName, string>> = {};
    for (const [field, apiField] of apiFields) {
      const message = apiFieldMessage(result, apiField);
      if (message) {
        found[field] = message;
      }
    }
    if (result.code === "events.flyer_not_found") {
      found.flyer = showMessages.flyerNotFound;
    }
    setErrors(found);
    setFormError(
      Object.keys(found).length === 0 ? showErrorMessage(result) : null,
    );
    focusFirst(found);
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!flyer) {
      const found = { flyer: showMessages.flyerMissing };
      setErrors(found);
      focusFirst(found);
      return;
    }
    setSaving(true);
    setFormError(null);
    const venue = venueProfileId
      ? { profileId: venueProfileId, name: null }
      : { profileId: null, name: venueName };
    const input: ShowInput = {
      organizerProfileId: organizer,
      title,
      city,
      startsLocal: date && time ? `${date}T${time}` : "",
      venue,
      lineup: bands
        .filter((band) => band.profileId || band.name.trim() !== "")
        .map((band) =>
          band.profileId
            ? { profileId: band.profileId, name: null }
            : { profileId: null, name: band.name },
        ),
      styles: picked,
      price,
      ticketUrl: ticketUrl.trim() === "" ? null : ticketUrl.trim(),
      flyerId: flyer.id,
    };
    const result = show
      ? await updateShow(show.id, input)
      : await createShow(input);
    if (!result.ok) {
      setSaving(false);
      showErrors(result);
      return;
    }
    setErrors({});
    router.push(show ? savedPath : publishedPath);
    router.refresh();
  }

  const lineupLegendId = `${id}-lineup-legenda`;
  const stylesLegendId = `${id}-estilos-legenda`;
  const venueListId = `${id}-casas`;

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-5">
      {showOrganizerPicker ? (
        <div className="flex flex-col gap-2">
          <Field
            id={fieldId("organizer")}
            label="Perfil organizador"
            error={errors.organizer}
          >
            <select
              {...fieldAria(fieldId("organizer"), errors.organizer)}
              name="organizer"
              value={organizer}
              onChange={(event) => onOrganizerChange(event.target.value)}
              className={fieldControl}
            >
              <option value="">Escolha o perfil</option>
              {organizerOptions.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          </Field>
          {isAdmin && (
            <div className="flex flex-col gap-1.5">
              <label
                htmlFor={`${id}-organizer-search`}
                className="text-sm text-text-muted"
              >
                Buscar perfil sem dono
              </label>
              <input
                id={`${id}-organizer-search`}
                type="search"
                autoComplete="off"
                value={organizerQuery}
                onChange={(event) => setOrganizerQuery(event.target.value)}
                className={fieldControl}
              />
              {organizerResults.length > 0 && (
                <ul className="flex flex-col gap-1">
                  {organizerResults.map((profile) => (
                    <li key={profile.id}>
                      <Button
                        variant="ghost"
                        className="w-full justify-start normal-case"
                        onClick={() =>
                          chooseOrganizer({
                            id: profile.id,
                            name: profile.name,
                            city: profile.city,
                          })
                        }
                      >
                        Usar {profile.name}
                      </Button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>
      ) : (
        organizerOptions[0] && (
          <p className="text-sm text-text-muted">
            Organizador:{" "}
            <span className="text-text">{organizerOptions[0].name}</span>
          </p>
        )
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor={fieldId("flyer")} className={legendClass}>
          Flyer do show (vertical)
        </label>
        <div className="relative flex flex-col items-center gap-2 rounded-md border border-dashed border-border bg-surface p-4 text-center focus-within:border-text-muted">
          {flyer ? (
            // eslint-disable-next-line @next/next/no-img-element -- o next/image injeta style inline, barrado pela CSP (ADR-0017).
            <img
              src={flyer.thumbUrl}
              alt="Prévia do flyer enviado"
              width={400}
              height={500}
              className="h-48 w-auto rounded-xs object-contain"
            />
          ) : null}
          <span aria-hidden="true" className="text-sm text-text">
            {flyer ? "Toque para trocar o flyer" : "Toque para enviar o flyer"}
          </span>
          <span
            id={`${fieldId("flyer")}-dica`}
            className="font-label text-xs uppercase tracking-wider text-text-muted"
          >
            JPEG, PNG ou WebP até 10 MB (recomendado 3:4)
          </span>
          <input
            {...fieldAria(fieldId("flyer"), errors.flyer, true)}
            name="flyer"
            type="file"
            accept="image/jpeg,image/png,image/webp"
            disabled={uploading}
            onChange={onFlyer}
            className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
          />
        </div>
        <p role="status" className="text-sm text-text-muted empty:hidden">
          {flyerStatus}
        </p>
        {errors.flyer && (
          <p id={`${fieldId("flyer")}-erro`} className={errorClass}>
            {errors.flyer}
          </p>
        )}
      </div>

      <Field id={fieldId("title")} label="Nome do evento" error={errors.title}>
        <input
          {...fieldAria(fieldId("title"), errors.title)}
          name="title"
          type="text"
          autoComplete="off"
          maxLength={100}
          placeholder="Ex.: Noite do Caos III"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          className={fieldControl}
        />
      </Field>

      <div className="flex flex-col gap-1.5">
        <div className="grid grid-cols-2 gap-3">
          <Field id={fieldId("date")} label="Data">
            <input
              {...fieldAria(fieldId("date"), errors.date)}
              name="date"
              type="date"
              value={date}
              onChange={(event) => setDate(event.target.value)}
              className={fieldControl}
            />
          </Field>
          <Field id={`${id}-time`} label="Horário">
            <input
              {...fieldAria(`${id}-time`, errors.date)}
              aria-describedby={
                errors.date ? `${fieldId("date")}-erro` : undefined
              }
              name="time"
              type="time"
              value={time}
              onChange={(event) => setTime(event.target.value)}
              className={fieldControl}
            />
          </Field>
        </div>
        {errors.date && (
          <p id={`${fieldId("date")}-erro`} className={errorClass}>
            {errors.date}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <Field
          id={fieldId("venue")}
          label="Casa / Local"
          error={errors.venue}
          hint={
            venueProfileId
              ? "Ligada ao perfil da casa. Escreva outro nome para trocar."
              : "Escreva o nome ou escolha um perfil de casa."
          }
        >
          <input
            {...fieldAria(fieldId("venue"), errors.venue, true)}
            name="venue"
            type="text"
            autoComplete="off"
            maxLength={80}
            placeholder="Bar, inferninho ou centro cultural"
            aria-controls={
              venueSuggestions.length > 0 ? venueListId : undefined
            }
            value={venueName}
            onChange={(event) => {
              setVenueName(event.target.value);
              setVenueProfileId(null);
              if (event.target.value.trim().length < 2) {
                setVenueSuggestions([]);
              }
            }}
            onKeyDown={onVenueKeyDown}
            className={fieldControl}
          />
        </Field>
        {venueSuggestions.length > 0 && (
          <ul
            id={venueListId}
            aria-label="Perfis de casa"
            className="flex flex-col gap-1"
          >
            {venueSuggestions.map((profile) => (
              <li key={profile.id}>
                <Button
                  variant="ghost"
                  className="w-full justify-start normal-case"
                  onClick={() => chooseVenue(profile)}
                >
                  Usar o perfil {profile.name}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <Field id={fieldId("city")} label="Cidade" error={errors.city}>
        <select
          {...fieldAria(fieldId("city"), errors.city)}
          name="city"
          value={city}
          onChange={(event) => setCity(event.target.value)}
          className={fieldControl}
        >
          <option value="">Selecione a cidade</option>
          {cities.map((option) => (
            <option key={option.slug} value={option.slug}>
              {option.name}
            </option>
          ))}
        </select>
      </Field>

      <fieldset
        aria-describedby={errors.lineup ? `${id}-lineup-erro` : undefined}
        className="flex flex-col gap-2"
      >
        <legend id={lineupLegendId} className={`${legendClass} mb-1.5`}>
          Line-up (bandas)
        </legend>
        <ul className="flex flex-col gap-2">
          {bands.map((band, index) => (
            <li key={band.key} className="flex items-center gap-2">
              <label htmlFor={bandId(index)} className="sr-only">
                Banda {index + 1}
              </label>
              <input
                id={bandId(index)}
                name={`band-${index}`}
                type="text"
                autoComplete="off"
                maxLength={80}
                aria-invalid={errors.lineup ? true : undefined}
                placeholder={
                  index === 0
                    ? "Nome da banda 1 (headliner)"
                    : `Nome da banda ${index + 1}`
                }
                value={band.name}
                onChange={(event) => setBandName(band.key, event.target.value)}
                className={fieldControl}
              />
              {bands.length > 1 && (
                <Button
                  variant="ghost"
                  aria-label={`Remover banda ${index + 1}`}
                  onClick={() => removeBand(band.key)}
                >
                  <span aria-hidden="true">×</span>
                </Button>
              )}
            </li>
          ))}
        </ul>
        {bands.length < maxBands && (
          <Button variant="secondary" className="self-start" onClick={addBand}>
            + Adicionar outra banda
          </Button>
        )}
        {errors.lineup && (
          <p id={`${id}-lineup-erro`} className={errorClass}>
            {errors.lineup}
          </p>
        )}
      </fieldset>

      <div
        id={fieldId("styles")}
        role="group"
        tabIndex={-1}
        aria-labelledby={stylesLegendId}
        aria-describedby={
          errors.styles
            ? `${fieldId("styles")}-erro`
            : `${fieldId("styles")}-dica`
        }
        className="flex flex-col gap-1.5"
      >
        <span id={stylesLegendId} className={legendClass}>
          Estilos musicais
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
        <p id={`${fieldId("styles")}-dica`} className="text-sm text-text-muted">
          Escolha de 1 a 5.
        </p>
        {errors.styles && (
          <p id={`${fieldId("styles")}-erro`} className={errorClass}>
            {errors.styles}
          </p>
        )}
      </div>

      <Field
        id={fieldId("price")}
        label="Preço do ingresso"
        error={errors.price}
        hint="Ex.: R$ 30 / R$ 40 na hora, ou Gratuito"
      >
        <input
          {...fieldAria(fieldId("price"), errors.price, true)}
          name="price"
          type="text"
          autoComplete="off"
          maxLength={40}
          value={price}
          onChange={(event) => setPrice(event.target.value)}
          className={fieldControl}
        />
      </Field>

      <Field
        id={fieldId("ticketUrl")}
        label="Link do ingresso"
        error={errors.ticketUrl}
        hint="Opcional. Sympla, direct ou outro, começando com https://"
      >
        <input
          {...fieldAria(fieldId("ticketUrl"), errors.ticketUrl, true)}
          name="ticketUrl"
          type="url"
          inputMode="url"
          placeholder="https://…"
          value={ticketUrl}
          onChange={(event) => setTicketUrl(event.target.value)}
          className={fieldControl}
        />
      </Field>

      <Button type="submit" size="lg" disabled={saving || uploading}>
        {mode === "create" ? "Publicar show" : "Salvar"}
      </Button>
      {formError && (
        <p role="alert" className={errorClass}>
          {formError}
        </p>
      )}
    </form>
  );
}
