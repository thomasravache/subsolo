import type { Metadata, Viewport } from "next";
import localFont from "next/font/local";

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

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="pt-BR"
      className={`${anton.variable} ${inter.variable} ${spaceGrotesk.variable} h-full antialiased`}
    >
      <body className="flex min-h-full flex-col">{children}</body>
    </html>
  );
}
