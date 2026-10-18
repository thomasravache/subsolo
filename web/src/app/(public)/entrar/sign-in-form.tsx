"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";

import { requestMagicLink } from "@/api/account";
import { Button } from "@/components/ui/Button";

import {
  AccountMain,
  accountError,
  accountText,
  accountTitle,
} from "./account-layout";
import { requestErrorMessage } from "./messages";

/** Segundos até o "Reenviar link" voltar a funcionar. */
const resendSeconds = 60;

/** Contador no começo da espera (de 0:59 a 0:00, 60 s no total). */
const startCountdown = `0:${resendSeconds - 1}`;

/** Formato do e-mail conferido antes de chamar a API (a API valida de novo). */
const emailFormat = /^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$/;

const invalidEmail = "Digite um e-mail válido.";

const fieldId = "email";
const errorId = "email-erro";
const hintId = "email-dica";

type State = { step: "form" } | { step: "sent"; email: string };

export interface SignInFormProps {
  returnTo: string | null;
}

/**
 * SPEC-0016 — formulário de `/entrar` e o estado "Confira seu e-mail" (tela do Stitch): reenviar depois de 60 s (a
 * espera é anunciada por `aria-live="polite"` só no começo e no fim) ou usar outro e-mail. O erro do campo fica ligado
 * a ele por `aria-describedby` e leva o foco até o campo; os erros da API (limite, SMTP fora do ar) aparecem num alerta.
 */
export function SignInForm({ returnTo }: SignInFormProps) {
  const [state, setState] = useState<State>({ step: "form" });
  const [email, setEmail] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const field = useRef<HTMLInputElement>(null);

  /** Mostra o erro de formato no campo e leva o foco até ele (§7.6 l). */
  function rejectEmail() {
    setFieldError(invalidEmail);
    field.current?.focus();
  }

  async function send(address: string): Promise<boolean> {
    setSending(true);
    setFormError(null);
    const result = await requestMagicLink(address, returnTo);
    setSending(false);
    if (result.ok) {
      return true;
    }
    if (result.code === "users.invalid_email") {
      rejectEmail();
    } else {
      setFormError(requestErrorMessage(result.code, result.status));
    }
    return false;
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const address = email.trim();
    if (!emailFormat.test(address) || address.length > 254) {
      rejectEmail();
      return;
    }
    setFieldError(null);
    if (await send(address)) {
      setState({ step: "sent", email: address });
    }
  }

  if (state.step === "sent") {
    return (
      <CheckYourEmail
        email={state.email}
        error={formError}
        onResend={() => send(state.email)}
        onOtherEmail={() => {
          setFormError(null);
          setState({ step: "form" });
        }}
      />
    );
  }

  const describedBy = [fieldError ? errorId : null, hintId]
    .filter(Boolean)
    .join(" ");
  return (
    <AccountMain>
      <h1 className={accountTitle}>Entrar</h1>
      <p className={accountText}>
        Digite seu e-mail e mandamos um link para você entrar no Subsolo.
      </p>
      <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <label
            htmlFor={fieldId}
            className="font-label text-xs font-bold uppercase tracking-wider text-text"
          >
            Seu e-mail
          </label>
          <input
            ref={field}
            id={fieldId}
            name="email"
            type="email"
            inputMode="email"
            autoComplete="email"
            autoCapitalize="none"
            spellCheck={false}
            required
            maxLength={254}
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            aria-invalid={fieldError ? true : undefined}
            aria-describedby={describedBy}
            className="min-h-12 rounded-md border border-border bg-surface px-4 text-base text-text placeholder:text-text-muted aria-[invalid=true]:border-danger"
            placeholder="voce@email.com"
          />
          {fieldError && (
            <p id={errorId} className={accountError}>
              {fieldError}
            </p>
          )}
        </div>
        <Button type="submit" size="lg" disabled={sending}>
          Mandar link de acesso
        </Button>
        <p id={hintId} className="text-sm text-text-muted">
          Sem senha: o link vale por 15 minutos.
        </p>
        {formError && (
          <p role="alert" className={accountError}>
            {formError}
          </p>
        )}
      </form>
      <p className="text-xs text-text-muted">
        Ao entrar, você concorda com os{" "}
        <Link href="/termos" className="underline underline-offset-4">
          Termos
        </Link>{" "}
        e a{" "}
        <Link href="/privacidade" className="underline underline-offset-4">
          Privacidade
        </Link>
        .
      </p>
    </AccountMain>
  );
}

interface CheckYourEmailProps {
  email: string;
  error: string | null;
  onResend: () => Promise<boolean>;
  onOtherEmail: () => void;
}

/** SPEC-0016 — estado "Confira seu e-mail" (tela do Stitch), com o contador de reenvio. */
function CheckYourEmail({
  email,
  error,
  onResend,
  onOtherEmail,
}: CheckYourEmailProps) {
  const heading = useRef<HTMLHeadingElement>(null);
  const [sentAt, setSentAt] = useState(() => Date.now());
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    heading.current?.focus();
  }, []);

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1_000);
    return () => window.clearInterval(timer);
  }, [sentAt]);

  const elapsed = Math.floor((now - sentAt) / 1_000);
  const remaining = Math.max(0, resendSeconds - 1 - elapsed);
  const waiting = elapsed < resendSeconds;
  const countdown = `0:${String(remaining).padStart(2, "0")}`;

  async function resend() {
    if (await onResend()) {
      const at = Date.now();
      setSentAt(at);
      setNow(at);
    }
  }

  return (
    <AccountMain>
      <h1 ref={heading} tabIndex={-1} className={accountTitle}>
        Confira seu e-mail
      </h1>
      <p className="text-base text-text">
        Mandamos o link de acesso para{" "}
        <strong className="break-all font-semibold">{email}</strong>. Ele vale
        por 15 minutos e só funciona uma vez.
      </p>
      <p className={accountText}>Não chegou? Olhe a caixa de spam.</p>
      <div className="flex flex-col gap-3">
        <Button
          variant="secondary"
          size="lg"
          disabled={waiting}
          onClick={resend}
        >
          {waiting ? `Reenviar em ${countdown}` : "Reenviar link"}
        </Button>
        {/* Anunciado só no começo e no fim da espera, não a cada segundo (§7.6 l). */}
        <p aria-live="polite" className="sr-only">
          {waiting
            ? `Você poderá reenviar o link em ${startCountdown}.`
            : "Você já pode reenviar o link."}
        </p>
        <Button variant="ghost" size="lg" onClick={onOtherEmail}>
          Usar outro e-mail
        </Button>
      </div>
      {error && (
        <p role="alert" className={accountError}>
          {error}
        </p>
      )}
    </AccountMain>
  );
}
