import type { Metadata, Viewport } from "next";
import localFont from "next/font/local";
import { connection } from "next/server";

import "./globals.css";

/** SPEC-0011 — fontes self-hosted (SIL OFL 1.1, em public/fonts): títulos, texto e rótulos/dados. */
const anton = localFont({
  src: "../../public/fonts/anton-latin-400-normal.woff2",
  weight: "400",
  display: "swap",
  variable: "--font-anton",
});

const inter = localFont({
  src: "../../public/fonts/inter-latin-wght-normal.woff2",
  weight: "100 900",
  display: "swap",
  variable: "--font-inter",
});

const spaceGrotesk = localFont({
  src: "../../public/fonts/space-grotesk-latin-wght-normal.woff2",
  weight: "300 700",
  display: "swap",
  variable: "--font-space-grotesk",
});

export const metadata: Metadata = {
  title: "Subsolo",
  description: "Agenda de shows de metal underground por cidade.",
};

export const viewport: Viewport = {
  themeColor: "#0b0b0c",
  colorScheme: "dark",
};

// SPEC-0010 (ADR-0017): toda página é renderizada por requisição, sem shell estático, para o Next.js aplicar aos
// scripts o nonce da CSP gerado pelo `proxy` (uma página pré-renderizada sairia sem nonce, com os scripts bloqueados).
export const instant = false;

export default async function RootLayout({ children }: LayoutProps<"/">) {
  await connection();
  return (
    <html
      lang="pt-BR"
      className={`${anton.variable} ${inter.variable} ${spaceGrotesk.variable} h-full antialiased`}
    >
      <body className="flex min-h-full flex-col">{children}</body>
    </html>
  );
}
