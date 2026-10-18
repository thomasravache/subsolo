import type { Metadata } from "next";

import type { ShowDetail } from "@/api/show-detail";

/**
 * SPEC-0024 (andaime) — os metadados da página `/{cidade}/{show}` (§6): `<title>`, `description`, Open Graph com a
 * variante `og` absoluta por `publicUrl` (o `App:PublicUrl`), `twitter:card` e `canonical`.
 */
export function showMetadata(show: ShowDetail, publicUrl: string): Metadata {
  void show;
  void publicUrl;
  throw new Error("SPEC-0024: showMetadata ainda não implementado");
}
