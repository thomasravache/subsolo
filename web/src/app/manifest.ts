import type { MetadataRoute } from "next";

/**
 * SPEC-0030 — o manifest do PWA (`/manifest.webmanifest`, ADR-0013): o Subsolo instalável na tela de início (no iPhone,
 * é o que libera os avisos por push), em tela cheia (`standalone`), com as cores do design system (fundo `#0B0B0C`).
 * O Next.js põe o `<link rel="manifest">` em toda página.
 */
export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Subsolo",
    short_name: "Subsolo",
    description: "Agenda de shows de metal underground por cidade.",
    lang: "pt-BR",
    start_url: "/",
    scope: "/",
    display: "standalone",
    background_color: "#0b0b0c",
    theme_color: "#0b0b0c",
    icons: [
      {
        src: "/icons/icon-192.png",
        sizes: "192x192",
        type: "image/png",
        purpose: "any",
      },
      {
        src: "/icons/icon-512.png",
        sizes: "512x512",
        type: "image/png",
        purpose: "any",
      },
    ],
  };
}
