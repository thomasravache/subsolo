"use client";

export interface ReportShowSheetProps {
  /** O show (id da API). */
  showId: string;
  /** O título do show. */
  title: string;
  /** A folha está aberta. */
  open: boolean;
  /** Fechar ("Cancelar", `Esc` ou depois do envio). */
  onClose: () => void;
}

/**
 * SPEC-0033 — scaffold da folha "Reportar show" (tela "Denunciar Show - Subsolo" do Stitch), aberta pelo link
 * "Reportar" da página do show. Implementação pendente.
 */
export function ReportShowSheet(props: ReportShowSheetProps) {
  void props;
  return null;
}
