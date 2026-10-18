"use client";

import { useId, useState, type FormEvent } from "react";

import type { CitySummary } from "@/api/client";
import {
  approveProfileRequest,
  rejectProfileRequest,
  type QueueItem,
} from "@/api/profiles";
import {
  cityName,
  profileErrorMessage,
  profileMessages,
  profileTypeLabel,
} from "@/components/profiles/messages";
import { Button } from "@/components/ui/Button";

import { Field, fieldAria, fieldControl } from "./Field";
import { fieldMessage } from "./messages";

export interface ProfileRequestsQueueProps {
  initialItems: QueueItem[];
  cities: CitySummary[];
}

const dateFormat = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "short",
  year: "numeric",
  timeZone: "America/Sao_Paulo",
});

/** Os links informados, com o nome de cada serviço. */
function linksOf(item: QueueItem): { label: string; href: string }[] {
  return [
    { label: "Instagram", href: item.links.instagram },
    { label: "Bandcamp", href: item.links.bandcamp },
    { label: "Metal Archives", href: item.links.metalArchives },
  ].filter((entry): entry is { label: string; href: string } =>
    Boolean(entry.href),
  );
}

/**
 * SPEC-0021 — aba "PEDIDOS DE PERFIL (n)" da "Fila do admin" (tela do Stitch, só com a aba de pedidos): cada pedido
 * pendente num cartão, mais antigo primeiro, com os dados do perfil, o contato ou a prova e o e-mail de quem pediu;
 * "Aprovar" decide na hora e "Recusar" abre o campo "Motivo" e "Confirmar recusa". O cartão decidido sai da fila e a
 * decisão é anunciada.
 */
export function ProfileRequestsQueue({
  initialItems,
  cities,
}: ProfileRequestsQueueProps) {
  const id = useId();
  const [items, setItems] = useState(initialItems);
  const [rejecting, setRejecting] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState<string | null>(null);
  const [cardError, setCardError] = useState<{
    requestId: string;
    message: string;
  } | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [status, setStatus] = useState("");

  const tabId = `${id}-aba`;
  const panelId = `${id}-painel`;
  const reasonId = (requestId: string) => `${id}-motivo-${requestId}`;

  function decided(requestId: string, message: string) {
    setItems((current) =>
      current.filter((item) => item.requestId !== requestId),
    );
    setRejecting(null);
    setReason("");
    setReasonError(null);
    setCardError(null);
    setStatus(message);
  }

  async function onApprove(requestId: string) {
    setBusy(requestId);
    setStatus("");
    const result = await approveProfileRequest(requestId);
    setBusy(null);
    if (result.ok) {
      decided(requestId, profileMessages.approved);
      return;
    }
    setCardError({ requestId, message: profileErrorMessage(result) });
  }

  async function onReject(
    event: FormEvent<HTMLFormElement>,
    requestId: string,
  ) {
    event.preventDefault();
    setBusy(requestId);
    setStatus("");
    const result = await rejectProfileRequest(requestId, reason);
    setBusy(null);
    if (result.ok) {
      decided(requestId, profileMessages.rejected);
      return;
    }
    const message =
      result.code === "profiles.invalid_request"
        ? fieldMessage(result, "Reason")
        : undefined;
    if (message) {
      setReasonError(message);
      document.getElementById(reasonId(requestId))?.focus();
      return;
    }
    setCardError({ requestId, message: profileErrorMessage(result) });
  }

  function openReject(requestId: string) {
    setRejecting(requestId);
    setReason("");
    setReasonError(null);
    setCardError(null);
  }

  return (
    <div className="flex flex-col gap-4">
      <div
        role="tablist"
        aria-label="Fila do admin"
        className="flex gap-2 border-b border-border"
      >
        <button
          id={tabId}
          type="button"
          role="tab"
          aria-selected="true"
          aria-controls={panelId}
          className="inline-flex min-h-11 items-center border-b-2 border-accent px-3 font-label text-xs font-bold uppercase tracking-wider text-text"
        >
          Pedidos de perfil ({items.length})
        </button>
      </div>
      <p role="status" className="text-sm text-text empty:hidden">
        {status}
      </p>
      <div
        id={panelId}
        role="tabpanel"
        aria-labelledby={tabId}
        className="flex flex-col gap-3"
      >
        {items.length === 0 ? (
          <p className="rounded-md border border-dashed border-border px-4 py-10 text-center text-base text-text-muted">
            Nenhum pedido de perfil em análise.
          </p>
        ) : (
          <ul className="grid gap-3 lg:grid-cols-2">
            {items.map((item) => {
              const claim = item.kind === "claim";
              const open = rejecting === item.requestId;
              const titleId = `${id}-pedido-${item.requestId}`;
              return (
                <li key={item.requestId}>
                  <article
                    aria-labelledby={titleId}
                    className="flex h-full flex-col gap-3 rounded-md border border-border bg-surface p-4"
                  >
                    <div className="flex flex-col gap-1">
                      <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
                        {claim ? "Reivindicação" : "Perfil novo"} ·{" "}
                        {profileTypeLabel(item.type)}
                      </p>
                      <h2
                        id={titleId}
                        className="font-display text-2xl uppercase leading-none tracking-wide text-text"
                      >
                        {item.name}
                      </h2>
                    </div>
                    <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
                      <dt className="text-text-muted">Cidade</dt>
                      <dd className="text-text">
                        {cityName(cities, item.city)}
                      </dd>
                      {item.styles.length > 0 && (
                        <>
                          <dt className="text-text-muted">Estilos</dt>
                          <dd className="text-text">
                            {item.styles.join(", ")}
                          </dd>
                        </>
                      )}
                      <dt className="text-text-muted">
                        {claim ? "Prova" : "Contato"}
                      </dt>
                      <dd className="break-words text-text">
                        {item.contactOrProof}
                      </dd>
                      <dt className="text-text-muted">E-mail</dt>
                      <dd className="break-all text-text">
                        {item.requesterEmail ?? "—"}
                      </dd>
                      <dt className="text-text-muted">Pedido em</dt>
                      <dd className="text-text">
                        {dateFormat.format(new Date(item.createdAtUtc))}
                      </dd>
                    </dl>
                    {linksOf(item).length > 0 && (
                      <ul className="flex flex-wrap gap-3 text-sm">
                        {linksOf(item).map((entry) => (
                          <li key={entry.label}>
                            <a
                              href={entry.href}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="text-text underline decoration-accent underline-offset-4"
                            >
                              {entry.label}
                            </a>
                          </li>
                        ))}
                      </ul>
                    )}
                    {open ? (
                      <form
                        noValidate
                        onSubmit={(event) => onReject(event, item.requestId)}
                        className="flex flex-col gap-3"
                      >
                        <Field
                          id={reasonId(item.requestId)}
                          label="Motivo"
                          error={reasonError}
                        >
                          <textarea
                            {...fieldAria(
                              reasonId(item.requestId),
                              reasonError,
                            )}
                            rows={3}
                            maxLength={200}
                            value={reason}
                            onChange={(event) => setReason(event.target.value)}
                            className={`${fieldControl} py-2`}
                          />
                        </Field>
                        <div className="flex flex-wrap gap-2">
                          <Button
                            variant="ghost"
                            onClick={() => setRejecting(null)}
                          >
                            Cancelar
                          </Button>
                          <Button
                            type="submit"
                            disabled={busy === item.requestId}
                          >
                            Confirmar recusa
                          </Button>
                        </div>
                      </form>
                    ) : (
                      <div className="flex flex-wrap gap-2">
                        <Button
                          disabled={busy === item.requestId}
                          onClick={() => onApprove(item.requestId)}
                        >
                          Aprovar
                        </Button>
                        <Button
                          variant="secondary"
                          onClick={() => openReject(item.requestId)}
                        >
                          Recusar
                        </Button>
                      </div>
                    )}
                    {cardError?.requestId === item.requestId && (
                      <p
                        role="alert"
                        className="text-sm font-medium text-danger"
                      >
                        {cardError.message}
                      </p>
                    )}
                  </article>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}
