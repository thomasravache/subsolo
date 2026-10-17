import { listCities } from "@/api/cities";
import { AppShell } from "@/components/ui/AppShell";

/**
 * SPEC-0011 — shell das páginas públicas (cabeçalho com o seletor de cidade, rodapé e navegação inferior no
 * celular), com as cidades da API. Sem shell de carregamento: como a agenda, a moldura só sai com a lista pronta
 * (API fora do ar → lista vazia e o cabeçalho sem seletor).
 */
export const instant = false;

export default async function PublicLayout({ children }: LayoutProps<"/">) {
  const cities = await listCities();
  return <AppShell cities={cities}>{children}</AppShell>;
}
