"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";

import { consumeMagicLink } from "@/api/account";
import { Button, buttonClasses } from "@/components/ui/Button";

import {
  AccountMain,
  accountError,
  accountText,
  accountTitle,
} from "../account-layout";
import { genericErrorMessage, rateLimitedMessage } from "../messages";

type State =
  | { step: "ready"; error: string | null }
  | { step: "signing-in" }
  | { step: "invalid" };

export interface ConfirmSignInProps {
  token: string;
}

/**
 * SPEC-0016 — confirmação da entrada: o botão troca o token pela sessão e volta (documento novo, para o shell nascer
 * logado) para a página de onde o fã saiu; link inexistente, expirado ou já usado → "Link inválido - Subsolo".
 */
export function ConfirmSignIn({ token }: ConfirmSignInProps) {
  const [state, setState] = useState<State>(
    token ? { step: "ready", error: null } : { step: "invalid" },
  );

  async function signIn() {
    setState({ step: "signing-in" });
    const result = await consumeMagicLink(token);
    if (result.ok) {
      window.location.assign(result.value);
      return;
    }
    if (result.code === "users.magic_link_invalid") {
      setState({ step: "invalid" });
      return;
    }
    setState({
      step: "ready",
      error: result.status === 429 ? rateLimitedMessage : genericErrorMessage,
    });
  }

  if (state.step === "invalid") {
    return <InvalidLink />;
  }

  return (
    <AccountMain>
      <h1 className={accountTitle}>Quase lá</h1>
      <p className={accountText}>Toque no botão para entrar no Subsolo.</p>
      <Button size="lg" onClick={signIn} disabled={state.step === "signing-in"}>
        Entrar no Subsolo
      </Button>
      {state.step === "ready" && state.error && (
        <p role="alert" className={accountError}>
          {state.error}
        </p>
      )}
    </AccountMain>
  );
}

/** SPEC-0016 — link inexistente, expirado ou já usado (tela "Link inválido - Subsolo"). */
function InvalidLink() {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    heading.current?.focus();
  }, []);

  return (
    <AccountMain>
      <h1 ref={heading} tabIndex={-1} className={accountTitle}>
        Link inválido
      </h1>
      <p role="alert" className="text-base text-text">
        Este link expirou ou já foi usado.
      </p>
      <p className={accountText}>
        Peça outro link: ele chega no seu e-mail em instantes.
      </p>
      <Link href="/entrar" className={buttonClasses("primary", "lg")}>
        Pedir outro link
      </Link>
    </AccountMain>
  );
}
