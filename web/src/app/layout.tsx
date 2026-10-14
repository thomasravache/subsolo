import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Subsolo",
  description: "Agenda de shows de metal underground por cidade.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="pt-BR" className="h-full antialiased">
      <body className="flex min-h-full flex-col">{children}</body>
    </html>
  );
}
