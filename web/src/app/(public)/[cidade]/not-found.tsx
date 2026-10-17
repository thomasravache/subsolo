import Link from "next/link";

import { mainContentId } from "@/components/ui/shell";

/** SPEC-0008 — cidade sem agenda no Subsolo (status 404). */
export default function CityNotFound() {
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-4 px-4 py-8"
    >
      <h1 className="font-display text-3xl uppercase leading-none tracking-wide text-text md:text-5xl">
        Cidade não encontrada
      </h1>
      <p className="text-lg text-text-muted">
        Ainda não temos agenda para essa cidade.
      </p>
      <Link
        href="/"
        className="inline-flex min-h-11 items-center text-lg underline decoration-accent decoration-2 underline-offset-4"
      >
        Voltar para o início
      </Link>
    </main>
  );
}
