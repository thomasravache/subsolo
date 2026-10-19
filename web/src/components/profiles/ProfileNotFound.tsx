"use client";

import Link from "next/link";

import { mainContentId } from "@/components/ui/shell";

/**
 * SPEC-0026 — perfil inexistente, pendente, recusado ou de outro tipo (status 404 pelo `proxy`): "Perfil não
 * encontrado" e o caminho de volta para o início. Componente cliente, como o `ShowNotFound`: o texto do 404 não aparece
 * na resposta de outra situação (ex.: o 503 de "Perfil indisponível").
 */
export function ProfileNotFound() {
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-4 px-4 py-8"
    >
      <h1 className="font-display text-3xl uppercase leading-none tracking-wide text-text md:text-5xl">
        Perfil não encontrado
      </h1>
      <p className="text-lg text-text-muted">
        Esse perfil não está no Subsolo. O link pode estar errado ou o perfil
        ainda não foi aprovado.
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
