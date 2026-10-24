"use client";

import type { ReportQueueItem } from "@/api/reports";

export interface ReportsQueueProps {
  initialItems: ReportQueueItem[];
}

/**
 * SPEC-0033 — scaffold da aba "DENÚNCIAS (n)" da "Fila do admin" (tela "Fila do Admin - Subsolo" do Stitch): um cartão
 * por show denunciado, com "Manter" e "Ocultar show" (com confirmação). Implementação pendente.
 */
export function ReportsQueue(props: ReportsQueueProps) {
  void props;
  return null;
}
