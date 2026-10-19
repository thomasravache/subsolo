"use client";

import { useState } from "react";

import { Button } from "@/components/ui/Button";

import { ShareIcon } from "./show-icons";

/**
 * SPEC-0024 — "Compartilhar" da página do show: Web Share API (título e URL) quando houver, como no celular; senão copia
 * o link e anuncia "Link copiado." numa região `aria-live` (sempre no DOM, para o leitor de tela ouvir a mudança).
 * SPEC-0026 (H24-03): se o compartilhamento falhar por outro motivo que não o cancelamento, também copia o link.
 */
export interface ShareButtonProps {
  /** Título do show (vai no compartilhamento). */
  title: string;
  /** URL canônica da página do show. */
  url: string;
}

export function ShareButton({ title, url }: ShareButtonProps) {
  const [message, setMessage] = useState("");

  async function share() {
    setMessage("");
    if (typeof navigator.share === "function") {
      try {
        await navigator.share({ title, url });
        return;
      } catch (error) {
        // SPEC-0026 (H24-03): só o cancelamento por quem tocou fica em silêncio; outro erro (compartilhamento negado,
        // dados recusados) cai na cópia do link.
        if (error instanceof DOMException && error.name === "AbortError") {
          return;
        }
      }
    }
    try {
      await navigator.clipboard.writeText(url);
      setMessage("Link copiado.");
    } catch {
      setMessage(`Não deu para copiar. O link é ${url}`);
    }
  }

  return (
    <div className="flex flex-col gap-1">
      <Button variant="secondary" size="lg" onClick={() => void share()}>
        <ShareIcon />
        Compartilhar
      </Button>
      <p
        aria-live="polite"
        role="status"
        className="min-h-5 break-all font-label text-xs text-text-muted"
      >
        {message}
      </p>
    </div>
  );
}
