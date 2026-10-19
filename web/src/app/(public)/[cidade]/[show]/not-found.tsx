import type { Metadata } from "next";

import { ShowNotFound } from "./show-not-found";

/**
 * SPEC-0024 — com o status 404 (definido pelo `proxy`), o Next.js monta os metadados com os do `not-found` mais próximo,
 * não com os da página: sem este arquivo, o show inexistente sairia com o título da cidade inexistente. A página
 * renderiza o aviso ela mesma (com o link para a agenda da cidade); este componente só vale para um `notFound()`.
 */
export const metadata: Metadata = { title: "Show não encontrado · Subsolo" };

export default function ShowNotFoundBoundary() {
  return <ShowNotFound city={undefined} />;
}
