import type { NextConfig } from "next";

const nextConfig: NextConfig = {
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
