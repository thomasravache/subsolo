"use client";

import { useState } from "react";

import { Button } from "@/components/ui/Button";

import { supportMessages } from "./messages";

/** Copia pelo seletor antigo (`execCommand`), para navegadores sem a API de área de transferência. */
function copyWithSelection(text: string): boolean {
  const field = document.createElement("textarea");
  field.value = text;
  field.setAttribute("readonly", "");
  field.className = "fixed -left-[9999px] top-0";
  document.body.append(field);
  field.select();
  try {
    return document.execCommand("copy");
  } catch {
    return false;
  } finally {
    field.remove();
  }
}

async function copy(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    // Sem permissão: tenta pela seleção.
  }
  return copyWithSelection(text);
}

/**
 * SPEC-0028 — a chave Pix em texto e "Copiar chave", que copia a chave e anuncia "Chave copiada." numa região
 * `role="status"` (sem copiar, avisa para selecionar a chave).
 */
export function CopyPixKey({ pixKey }: { pixKey: string }) {
  const [message, setMessage] = useState("");

  async function onCopy() {
    setMessage("");
    setMessage(
      (await copy(pixKey))
        ? supportMessages.copied
        : supportMessages.copyFailed,
    );
  }

  return (
    <div className="flex w-full flex-col gap-2">
      <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
        Chave
      </p>
      <div className="flex flex-col gap-3 rounded-md border border-border bg-bg p-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="break-all font-label text-base text-text select-all">
          {pixKey}
        </p>
        <Button onClick={() => void onCopy()} className="shrink-0">
          Copiar chave
        </Button>
      </div>
      <p
        role="status"
        aria-live="polite"
        className="min-h-6 font-label text-sm font-bold uppercase tracking-wider text-text"
      >
        {message}
      </p>
    </div>
  );
}
