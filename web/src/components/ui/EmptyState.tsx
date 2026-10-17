import type { ReactNode } from "react";

export interface EmptyStateProps {
  title: string;
  hint: string;
  action?: ReactNode;
}

/** SPEC-0011 — estado vazio (ex.: agenda sem shows): mensagem, dica e uma ação opcional. */
export function EmptyState({ title, hint, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-md border border-dashed border-border px-4 py-10 text-center">
      <p className="text-lg text-text">{title}</p>
      <p className="font-label text-xs uppercase tracking-wider text-text-muted">
        {hint}
      </p>
      {action}
    </div>
  );
}
