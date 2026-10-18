export type ProfileBadgeKind =
  "pending" | "approved" | "rejected" | "ownerless";

const kinds: Record<ProfileBadgeKind, { label: string; className: string }> = {
  pending: {
    label: "Em análise",
    className: "border-warning/40 bg-warning/10 text-warning",
  },
  approved: {
    label: "Aprovado",
    className: "border-border bg-surface-raised text-text",
  },
  rejected: {
    label: "Recusado",
    className: "border-danger/40 bg-danger/10 text-danger",
  },
  ownerless: {
    label: "Sem dono",
    className: "border-border text-text-muted",
  },
};

/** A situação de um pedido (`pending`/`approved`/`rejected`) como selo. */
export function requestBadge(status: string): ProfileBadgeKind {
  return status === "approved" || status === "rejected" ? status : "pending";
}

/**
 * SPEC-0021 — selo de situação do perfil ou do pedido ("EM ANÁLISE", "APROVADO", "RECUSADO", "SEM DONO"): o texto diz a
 * situação (a cor nunca é a única pista).
 */
export function StatusBadge({ kind }: { kind: ProfileBadgeKind }) {
  const { label, className } = kinds[kind];
  return (
    <span
      className={`inline-flex items-center rounded-xs border px-1.5 py-0.5 font-label text-[11px] font-bold uppercase tracking-wider ${className}`}
    >
      {label}
    </span>
  );
}
