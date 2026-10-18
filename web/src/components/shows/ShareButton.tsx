"use client";

/**
 * SPEC-0024 (andaime) — "Compartilhar" da página do show: Web Share API (título e URL) quando houver; senão copia o
 * link e anuncia "Link copiado." por `aria-live`.
 */
export interface ShareButtonProps {
  /** Título do show (vai no compartilhamento). */
  title: string;
  /** URL canônica da página do show. */
  url: string;
}

export function ShareButton(props: ShareButtonProps): never {
  void props;
  throw new Error("SPEC-0024: ShareButton ainda não implementado");
}
