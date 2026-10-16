import type { Metadata } from "next";
import { connection } from "next/server";
import "./globals.css";

export const metadata: Metadata = {
  title: "Subsolo",
  description: "Agenda de shows de metal underground por cidade.",
};

// SPEC-0010 (ADR-0017): toda página é renderizada por requisição, sem shell estático, para o Next.js aplicar aos
// scripts o nonce da CSP gerado pelo `proxy` (uma página pré-renderizada sairia sem nonce, com os scripts bloqueados).
export const instant = false;

export default async function RootLayout({ children }: LayoutProps<"/">) {
  await connection();
  return (
    <html lang="pt-BR" className="h-full antialiased">
      <body className="flex min-h-full flex-col">{children}</body>
    </html>
  );
}
