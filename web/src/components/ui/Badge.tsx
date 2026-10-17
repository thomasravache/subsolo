import type { ReactNode } from "react";

export type BadgeKind = "style" | "cancelled" | "postponed";

const kinds: Record<BadgeKind, string> = {
  style: "border-border text-text-muted",
  cancelled: "border-danger/40 bg-danger/10 text-danger",
  postponed: "border-warning/40 bg-warning/10 text-warning",
};

const situationLabel: Partial<Record<BadgeKind, string>> = {
  cancelled: "Cancelado",
  postponed: "Adiado",
};

export interface BadgeProps {
  kind: BadgeKind;
  /** Texto do selo; para `cancelled`/`postponed`, o padrão é "Cancelado"/"Adiado". */
  children?: ReactNode;
}

/**
 * SPEC-0011 — selo: estilo musical (`style`) ou situação do show (`cancelled`/`postponed`, com papel de status para
 * leitores de tela). A cor nunca é a única pista: o texto diz a situação.
 */
export function Badge({ kind, children }: BadgeProps) {
  const situation = kind !== "style";
  return (
    <span
      role={situation ? "status" : undefined}
      className={`inline-flex items-center rounded-xs border px-1.5 py-0.5 font-label text-[11px] font-bold uppercase tracking-wider ${kinds[kind]}`}
    >
      {children ?? situationLabel[kind]}
    </span>
  );
}
