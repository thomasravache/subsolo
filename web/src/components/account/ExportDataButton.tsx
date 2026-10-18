"use client";

import { useState } from "react";

import { exportPersonalData } from "@/api/account";
import { Button } from "@/components/ui/Button";

import {
  accountErrorMessage,
  accountMessages,
  exportFileName,
} from "./messages";

/** Espera antes de liberar a URL do arquivo baixado. */
const revokeAfterMs = 10_000;

/**
 * SPEC-0019 — "Baixar meus dados": pede o arquivo à API (`GET /api/v1/me/export`, com o cookie de sessão) e o entrega
 * ao navegador como download `subsolo-meus-dados.json`.
 */
export function ExportDataButton() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onClick() {
    setBusy(true);
    setError(null);
    const result = await exportPersonalData();
    setBusy(false);
    if (!result.ok) {
      setError(
        result.status === 429
          ? accountErrorMessage(result.status)
          : accountMessages.exportFailed,
      );
      return;
    }
    const url = URL.createObjectURL(result.value);
    const link = document.createElement("a");
    link.href = url;
    link.download = exportFileName;
    document.body.append(link);
    link.click();
    link.remove();
    // A URL fica viva o bastante para o navegador terminar de ler o arquivo.
    setTimeout(() => URL.revokeObjectURL(url), revokeAfterMs);
  }

  return (
    <div className="flex flex-col gap-2">
      <Button
        variant="secondary"
        onClick={onClick}
        disabled={busy}
        className="self-start"
      >
        Baixar meus dados
      </Button>
      {error && (
        <p role="alert" className="text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </div>
  );
}
