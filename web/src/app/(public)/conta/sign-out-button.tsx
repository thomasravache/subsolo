"use client";

import { useState } from "react";

import { signOut } from "@/api/account";
import { Button } from "@/components/ui/Button";

import { accountError } from "../entrar/account-layout";

/**
 * SPEC-0016 — "Sair": revoga a sessão na API e vai para `/entrar` trocando a entrada do histórico (voltar no
 * navegador não reabre a conta). Sessão já inexistente (401) também sai.
 */
export function SignOutButton() {
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);

  async function onClick() {
    setBusy(true);
    setFailed(false);
    const result = await signOut();
    if (result.ok || result.status === 401) {
      window.location.replace("/entrar");
      return;
    }
    setBusy(false);
    setFailed(true);
  }

  return (
    <div className="flex flex-col gap-2">
      <Button variant="secondary" size="lg" onClick={onClick} disabled={busy}>
        Sair
      </Button>
      {failed && (
        <p role="alert" className={accountError}>
          Não conseguimos sair agora. Tente de novo em instantes.
        </p>
      )}
    </div>
  );
}
