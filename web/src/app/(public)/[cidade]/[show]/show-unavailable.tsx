import { mainContentId } from "@/components/ui/shell";

/** SPEC-0024 (Emenda v2, d) — a página do show com a API fora do ar (status 503 pelo `proxy`, ou a página de erro). */
export function ShowUnavailable() {
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-4 px-4 py-8"
    >
      <h1 className="font-display text-3xl uppercase leading-none tracking-wide text-text md:text-5xl">
        Show indisponível
      </h1>
      <p role="alert" className="text-lg text-text-muted">
        A página do show está fora do ar por alguns instantes. Tente de novo.
      </p>
    </main>
  );
}
