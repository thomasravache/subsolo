"use client";

import { useEffect, useRef } from "react";

/**
 * SPEC-0021 — anuncia `message` numa região `role="status"` depois de a página montar (o leitor de tela só lê o que
 * muda numa região já presente), ex.: "Pedido enviado." ao voltar para "Meus perfis".
 */
export function Announcement({ message }: { message: string | null }) {
  const region = useRef<HTMLParagraphElement>(null);
  useEffect(() => {
    if (region.current) {
      region.current.textContent = message ?? "";
    }
  }, [message]);
  return (
    <p ref={region} role="status" className="text-sm text-text empty:hidden" />
  );
}
