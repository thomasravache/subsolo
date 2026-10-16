import Link from "next/link";

/** SPEC-0008 — moldura mínima das páginas públicas (tema escuro provisório; o visual final vem com o design system). */
export default function PublicLayout({ children }: LayoutProps<"/">) {
  return (
    <>
      <header className="border-b border-foreground/15">
        <div className="mx-auto w-full max-w-2xl px-4 py-3">
          <Link href="/" className="text-lg font-semibold tracking-tight">
            Subsolo
          </Link>
        </div>
      </header>
      {children}
    </>
  );
}
