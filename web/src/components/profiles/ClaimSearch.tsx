"use client";

import { useRouter } from "next/navigation";
import { useEffect, useId, useState, type FormEvent } from "react";

import type { CitySummary } from "@/api/client";
import {
  claimProfile,
  searchClaimableProfiles,
  type ClaimableProfile,
} from "@/api/profiles";
import { Field, fieldAria, fieldControl } from "@/components/admin/Field";
import { fieldMessage } from "@/components/admin/messages";
import { Button } from "@/components/ui/Button";

import {
  cityName,
  profileErrorMessage,
  profileMessages,
  profileTypeLabel,
  requestSentPath,
} from "./messages";
import { StatusBadge } from "./StatusBadge";

/** Tamanho mínimo da busca (o mesmo da API). */
const minTerm = 2;

/** Espera depois da última tecla antes de buscar. */
const debounceMs = 250;

export interface ClaimSearchProps {
  cities: CitySummary[];
}

/**
 * SPEC-0021 — "Reivindicar perfil" (tela do Stitch): busca pelos perfis aprovados sem dono (selo "SEM DONO"),
 * "Reivindicar" num resultado abre o campo "Prova" e "Enviar pedido" manda a reivindicação, que leva a "Meus perfis"
 * com "Pedido enviado.".
 */
export function ClaimSearch({ cities }: ClaimSearchProps) {
  const router = useRouter();
  const id = useId();
  const [term, setTerm] = useState("");
  const [results, setResults] = useState<ClaimableProfile[] | null>(null);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [selected, setSelected] = useState<ClaimableProfile | null>(null);
  const [proof, setProof] = useState("");
  const [proofError, setProofError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const searchId = `${id}-busca`;
  const proofId = `${id}-prova`;

  useEffect(() => {
    const q = term.trim();
    if (q.length < minTerm) {
      return;
    }
    const controller = new AbortController();
    const timer = setTimeout(async () => {
      const result = await searchClaimableProfiles(q, controller.signal);
      if (controller.signal.aborted) {
        return;
      }
      if (result.ok) {
        setResults(result.value);
        setSearchError(null);
      } else {
        setSearchError(profileMessages.searchUnavailable);
      }
    }, debounceMs);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [term]);

  const shown = term.trim().length < minTerm ? null : results;

  async function onClaim(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) {
      return;
    }
    setSending(true);
    setFormError(null);
    const result = await claimProfile(selected.id, proof);
    setSending(false);
    if (result.ok) {
      router.push(requestSentPath);
      return;
    }
    const message =
      result.code === "profiles.invalid_request"
        ? fieldMessage(result, "Proof")
        : undefined;
    if (message) {
      setProofError(message);
      document.getElementById(proofId)?.focus();
      return;
    }
    setProofError(null);
    setFormError(profileErrorMessage(result));
  }

  return (
    <div className="flex flex-col gap-5">
      <div role="search" className="flex flex-col gap-1.5">
        <Field
          id={searchId}
          label="Buscar perfil"
          hint="Nome da banda, da casa ou do produtor."
        >
          <input
            {...fieldAria(searchId, null, true)}
            type="search"
            autoComplete="off"
            value={term}
            onChange={(event) => setTerm(event.target.value)}
            className={fieldControl}
          />
        </Field>
      </div>

      {searchError && (
        <p role="alert" className="text-sm font-medium text-danger">
          {searchError}
        </p>
      )}

      <section
        aria-label="Resultados"
        aria-live="polite"
        className="flex flex-col gap-3"
      >
        {shown && shown.length === 0 && (
          <p className="text-base text-text-muted">
            {profileMessages.noResults}
          </p>
        )}
        {shown && shown.length > 0 && (
          <ul className="flex flex-col gap-3">
            {shown.map((profile) => (
              <li key={profile.id}>
                <article
                  aria-labelledby={`resultado-${profile.id}`}
                  className={`flex flex-col gap-2 rounded-md border bg-surface p-4 ${selected?.id === profile.id ? "border-text-muted" : "border-border"}`}
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h2
                      id={`resultado-${profile.id}`}
                      className="font-display text-2xl uppercase leading-none tracking-wide text-text"
                    >
                      {profile.name}
                    </h2>
                    <StatusBadge kind="ownerless" />
                  </div>
                  <p className="font-label text-xs uppercase tracking-wider text-text-muted">
                    {profileTypeLabel(profile.type)} ·{" "}
                    {cityName(cities, profile.city)}
                  </p>
                  <Button
                    variant="secondary"
                    className="self-start"
                    aria-pressed={selected?.id === profile.id}
                    onClick={() => {
                      setSelected(profile);
                      setFormError(null);
                    }}
                  >
                    Reivindicar
                  </Button>
                </article>
              </li>
            ))}
          </ul>
        )}
      </section>

      {selected && (
        <form
          noValidate
          onSubmit={onClaim}
          className="flex flex-col gap-3 rounded-md border border-border p-4"
        >
          <p className="text-base text-text">
            Reivindicando <strong>{selected.name}</strong>
          </p>
          <Field
            id={proofId}
            label="Prova"
            error={proofError}
            hint="Conte como o admin pode confirmar que o perfil é seu (ex.: o Instagram da banda)."
          >
            <textarea
              {...fieldAria(proofId, proofError, true)}
              rows={3}
              maxLength={300}
              value={proof}
              onChange={(event) => setProof(event.target.value)}
              className={`${fieldControl} py-2`}
            />
          </Field>
          <Button type="submit" size="lg" disabled={sending}>
            Enviar pedido
          </Button>
          {formError && (
            <p role="alert" className="text-sm font-medium text-danger">
              {formError}
            </p>
          )}
        </form>
      )}
    </div>
  );
}
