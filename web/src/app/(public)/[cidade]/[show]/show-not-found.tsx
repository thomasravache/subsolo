import Link from "next/link";

import type { CitySummary } from "@/api/client";
import { mainContentId } from "@/components/ui/shell";

/**
 * SPEC-0024 — show inexistente (status 404 pelo `proxy`): "Show não encontrado" e o caminho de volta, para a agenda da
 * cidade quando ela existe ("Ver a agenda de {cidade}") ou para o início.
 */
export function ShowNotFound({ city }: { city: CitySummary | undefined }) {
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-4 px-4 py-8"
    >
      <h1 className="font-display text-3xl uppercase leading-none tracking-wide text-text md:text-5xl">
        Show não encontrado
      </h1>
      <p className="text-lg text-text-muted">
        Esse show não está na agenda. O link pode estar errado ou o show pode
        ter saído do ar.
      </p>
      <Link
        href={city ? `/${city.slug}` : "/"}
        className="inline-flex min-h-11 items-center text-lg underline decoration-accent decoration-2 underline-offset-4"
      >
        {city ? `Ver a agenda de ${city.name}` : "Voltar para o início"}
      </Link>
    </main>
  );
}
