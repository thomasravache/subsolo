"use client";

import { ShowUnavailable } from "./show-unavailable";

/** SPEC-0024 — falha inesperada ao carregar a página do show: a mesma mensagem de indisponibilidade. */
export default function ShowError() {
  return <ShowUnavailable />;
}
