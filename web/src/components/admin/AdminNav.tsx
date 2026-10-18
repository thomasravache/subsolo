import Link from "next/link";

/** SPEC-0017 — páginas do painel do admin que já existem (o menu só mostra o que existe). */
const pages = [
  { href: "/admin/cidades-e-estilos", label: "Cidades e estilos" },
] as const;

export interface AdminNavProps {
  /** Caminho da página atual (`aria-current="page"`). */
  current: string;
}

/** SPEC-0017 — menu do painel do admin: coluna lateral no desktop, linha no topo no celular. */
export function AdminNav({ current }: AdminNavProps) {
  return (
    <nav aria-label="Painel do admin">
      <ul className="flex gap-2 md:flex-col">
        {pages.map((page) => (
          <li key={page.href}>
            <Link
              href={page.href}
              aria-current={page.href === current ? "page" : undefined}
              className="inline-flex min-h-11 items-center rounded-md px-3 font-label text-xs font-bold uppercase tracking-wider text-text-muted hover:text-text aria-[current=page]:bg-surface aria-[current=page]:text-text"
            >
              {page.label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
