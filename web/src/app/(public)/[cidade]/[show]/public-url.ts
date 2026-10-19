/**
 * SPEC-0024 (Emenda v2, a) — o endereço público do site (o mesmo `App:PublicUrl` da API), para as URLs absolutas do
 * Open Graph e o canonical. Vem de `SUBSOLO_PUBLIC_URL`, variável só do servidor (sem o prefixo público do Next.js: nunca vai para o
 * navegador), definida pelo AppHost e pelo `tools/compose-up.sh`.
 */
export function publicUrl(): string {
  const url = process.env.SUBSOLO_PUBLIC_URL;
  if (!url) {
    throw new Error(
      "SUBSOLO_PUBLIC_URL não configurada: o web precisa do endereço público do site.",
    );
  }
  return url.replace(/\/+$/, "");
}
