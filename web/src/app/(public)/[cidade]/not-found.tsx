import Link from "next/link";

/** SPEC-0008 — cidade sem agenda no Subsolo (status 404). */
export default function CityNotFound() {
  return (
    <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-4 px-4 py-10">
      <h1 className="text-3xl font-semibold tracking-tight">
        Cidade não encontrada
      </h1>
      <p className="text-lg text-foreground/80">
        Ainda não temos agenda para essa cidade.
      </p>
      <Link href="/" className="text-lg underline underline-offset-4">
        Voltar para o início
      </Link>
    </main>
  );
}
