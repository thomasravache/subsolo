import type { ReactNode } from "react";

/** Classes dos campos do painel do admin (o contorno fica vermelho com `aria-invalid`). */
export const fieldControl =
  "min-h-11 w-full rounded-md border border-border bg-surface px-3 text-base text-text placeholder:text-text-muted aria-[invalid=true]:border-danger read-only:text-text-muted";

const labelClass =
  "font-label text-xs font-bold uppercase tracking-wider text-text";

export interface FieldProps {
  id: string;
  label: string;
  /** Erro do campo, ligado ao controle por `aria-describedby` (o controle recebe `aria-invalid`). */
  error?: string | null;
  /** Dica sob o campo, também ligada por `aria-describedby`. */
  hint?: ReactNode;
  /** O controle, que recebe `id`, `aria-invalid` e `aria-describedby` de {@link fieldAria}. */
  children: ReactNode;
}

/** Ids da dica e do erro de um campo. */
export function fieldIds(id: string) {
  return { hint: `${id}-dica`, error: `${id}-erro` };
}

/** Atributos de acessibilidade do controle: inválido com erro e descrito pelo erro e pela dica. */
export function fieldAria(id: string, error?: string | null, hint = false) {
  const ids = fieldIds(id);
  const describedBy = [error ? ids.error : null, hint ? ids.hint : null]
    .filter(Boolean)
    .join(" ");
  return {
    id,
    "aria-invalid": error ? true : undefined,
    "aria-describedby": describedBy || undefined,
  } as const;
}

/** SPEC-0017 — campo de formulário do painel: rótulo visível, controle, dica e erro (WCAG 2.2: 1.3.1, 3.3.1). */
export function Field({ id, label, error, hint, children }: FieldProps) {
  const ids = fieldIds(id);
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className={labelClass}>
        {label}
      </label>
      {children}
      {hint && (
        <p id={ids.hint} className="text-sm text-text-muted">
          {hint}
        </p>
      )}
      {error && (
        <p id={ids.error} className="text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </div>
  );
}
