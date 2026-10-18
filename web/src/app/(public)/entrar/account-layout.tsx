import type { ReactNode } from "react";

import { mainContentId } from "@/components/ui/shell";

/** SPEC-0016 — moldura das telas de entrar e da conta: coluna estreita centrada, no padrão do shell. */
export function AccountMain({ children }: { children: ReactNode }) {
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-md flex-1 flex-col gap-6 px-4 py-10"
    >
      {children}
    </main>
  );
}

/** Título das telas de entrar e da conta (Anton em caixa alta, como a agenda). */
export const accountTitle =
  "font-display text-4xl uppercase leading-none tracking-wide text-text focus:outline-none";

/** Texto de apoio das telas de entrar e da conta. */
export const accountText = "text-base text-text-muted";

/** Mensagem de erro das telas de entrar e da conta (vermelho de perigo, contraste AA no fundo escuro). */
export const accountError = "text-sm font-medium text-danger";
