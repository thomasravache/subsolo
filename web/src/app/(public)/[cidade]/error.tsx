"use client";

import { AgendaUnavailable } from "./agenda-unavailable";

/** SPEC-0008 — falha inesperada ao carregar a agenda: a mesma mensagem de indisponibilidade. */
export default function AgendaError() {
  return <AgendaUnavailable />;
}
