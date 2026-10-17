import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  // SPEC-0015: metadados (o `<title>`) bloqueantes para todos os agentes; o padrão faz streaming do título para
  // navegadores comuns e o documento fica sem `<title>` durante a navegação no cliente.
  htmlLimitedBots: /.*/,
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
