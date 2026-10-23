"use client";

import type { NotificationPreferences } from "@/api/notifications";

/**
 * SPEC-0030 — a parte "Avisos" da tela "Avisos e Seguindo" (em `/seguindo`). Esqueleto do G1: ainda não renderiza
 * nada; o G2 entrega o botão "Ativar avisos" (só pede permissão depois do toque, ADR-0013), a situação e os quatro
 * interruptores.
 */
export function NoticesPanel({
  initial,
}: {
  initial: NotificationPreferences;
}) {
  void initial;
  return null;
}
