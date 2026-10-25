import type { NextConfig } from "next";

// SPEC-0041: no `next dev` (aspire run) aberto pelo túnel (tools/tunnel.sh, para o celular), os scripts de
// desenvolvimento (`/_next/*`) só saem para as origens de localhost e as de `allowedDevOrigins`. O AppHost define
// SUBSOLO_DEV_ORIGINS (hosts separados por vírgula; só do servidor, nunca chega ao navegador) quando recebe
// Subsolo:PublicUrl. Vazia ou ausente (o build e o `next start` de produção), a chave não é declarada.
const devOrigins = (process.env.SUBSOLO_DEV_ORIGINS ?? "")
  .split(",")
  .map((origin) => origin.trim())
  .filter((origin) => origin.length > 0);

const nextConfig: NextConfig = {
  ...(devOrigins.length > 0 ? { allowedDevOrigins: devOrigins } : {}),
  cacheComponents: true,
  partialPrefetching: true,
  // SPEC-0015: metadados (o `<title>`) bloqueantes para todos os agentes; o padrão faz streaming do título para
  // navegadores comuns e o documento fica sem `<title>` durante a navegação no cliente.
  htmlLimitedBots: /.*/,
  // SPEC-0023 (H22-06): no `next dev` (aspire run) não há Caddy, e as variantes do flyer (`/media/*`) saem da API. No
  // Compose e em produção o Caddy responde `/media/*` antes do web, então o rewrite não chega a valer lá. Sem `API_URL`
  // (ex.: no build da imagem), nenhum rewrite.
  async rewrites() {
    const api = process.env.API_URL;
    return api
      ? [{ source: "/media/:path*", destination: `${api}/media/:path*` }]
      : [];
  },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
