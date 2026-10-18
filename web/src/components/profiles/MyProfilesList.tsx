import Link from "next/link";

import type { CitySummary } from "@/api/client";
import type { MyProfiles } from "@/api/profiles";
import { EmptyState } from "@/components/ui/EmptyState";
import { buttonClasses } from "@/components/ui/Button";

import { cityName, profileMessages, profileTypeLabel } from "./messages";
import { requestBadge, StatusBadge } from "./StatusBadge";

export const requestPath = "/conta/perfis/novo";

export const claimPath = "/conta/perfis/reivindicar";

const cardClass =
  "flex flex-col gap-2 rounded-md border border-border bg-surface p-4";

const nameClass =
  "font-display text-2xl uppercase leading-none tracking-wide text-text";

const metaClass = "font-label text-xs uppercase tracking-wider text-text-muted";

export interface MyProfilesListProps {
  data: MyProfiles;
  cities: CitySummary[];
}

/**
 * SPEC-0021 — "Meus perfis" (tela do Stitch, sem "Editar perfil" e "ver painel"): cada perfil aprovado num cartão com
 * o selo "APROVADO" e cada pedido em aberto ou recusado num cartão com "EM ANÁLISE" ou "RECUSADO" (com o motivo e
 * "Pedir novamente"); o pedido aprovado aparece como o perfil. Vazio → o convite para pedir o perfil.
 */
export function MyProfilesList({ data, cities }: MyProfilesListProps) {
  const open = data.requests.filter((request) => request.status !== "approved");
  if (data.profiles.length === 0 && open.length === 0) {
    return (
      <EmptyState
        title={profileMessages.empty}
        hint="Banda, casa ou produtor"
      />
    );
  }

  return (
    <ul className="flex flex-col gap-3">
      {data.profiles.map((profile) => (
        <li key={profile.id}>
          <article
            aria-labelledby={`perfil-${profile.id}`}
            className={cardClass}
          >
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2 id={`perfil-${profile.id}`} className={nameClass}>
                {profile.name}
              </h2>
              <StatusBadge kind="approved" />
            </div>
            <p className={metaClass}>
              {profileTypeLabel(profile.type)} ·{" "}
              {cityName(cities, profile.city)}
            </p>
          </article>
        </li>
      ))}
      {open.map((request) => {
        const rejected = request.status === "rejected";
        const claim = request.kind === "claim";
        return (
          <li key={request.requestId}>
            <article
              aria-labelledby={`pedido-${request.requestId}`}
              className={cardClass}
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h2 id={`pedido-${request.requestId}`} className={nameClass}>
                  {request.name}
                </h2>
                <StatusBadge kind={requestBadge(request.status)} />
              </div>
              <p className={metaClass}>
                {claim ? "Reivindicação" : "Pedido de perfil"} ·{" "}
                {profileTypeLabel(request.type)}
              </p>
              {rejected && request.reason && (
                <p className="text-sm text-text">
                  <span className="text-text-muted">Motivo: </span>
                  {request.reason}
                </p>
              )}
              {rejected && (
                <Link
                  href={claim ? claimPath : requestPath}
                  className={buttonClasses("secondary", "md", "self-start")}
                >
                  {claim ? "Reivindicar de novo" : "Pedir novamente"}
                </Link>
              )}
            </article>
          </li>
        );
      })}
    </ul>
  );
}
