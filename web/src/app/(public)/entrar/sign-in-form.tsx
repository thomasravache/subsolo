"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";

import { requestMagicLink } from "@/api/account";
import { googleSignInHref } from "@/api/providers";
import { Button, buttonClasses } from "@/components/ui/Button";

import {
  AccountMain,
  accountError,
  accountText,
  accountTitle,
} from "./account-layout";
import { requestErrorMessage, signInErrorMessage } from "./messages";

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
  /** SPEC-0020 — o Google está ligado na API: mostra "Continuar com Google" acima do formulário. */
  google?: boolean;
  /** SPEC-0020 — o `?erro=` da volta do Google (`google` ou `google_email`). */
  error?: string | null;
}

/**
 * SPEC-0016 — formulário de `/entrar` e o estado "Confira seu e-mail" (tela do Stitch): reenviar depois de 60 s (a
 * espera é anunciada por `aria-live="polite"` só no começo e no fim) ou usar outro e-mail. O erro do campo fica ligado
 * a ele por `aria-describedby` e leva o foco até o campo; os erros da API (limite, SMTP fora do ar) aparecem num alerta.
 * SPEC-0020 — com o Google ligado, "Continuar com Google" acima do formulário, separado por "ou"; o erro da volta do
 * Google aparece num alerta.
 */
export function SignInForm({
  returnTo,
  google = false,
  error = null,
}: SignInFormProps) {
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

  const googleError = signInErrorMessage(error);
  const describedBy = [fieldError ? errorId : null, hintId]
    .filter(Boolean)
    .join(" ");
  return (
    <AccountMain>
      <h1 className={accountTitle}>Entrar</h1>
      <p className={accountText}>
        Digite seu e-mail e mandamos um link para você entrar no Subsolo.
      </p>
      {googleError && (
        <p role="alert" className={accountError}>
          {googleError}
        </p>
      )}
      {google && (
        <>
          <a
            href={googleSignInHref(returnTo)}
            className={buttonClasses("secondary", "lg", "w-full")}
          >
            <GoogleIcon />
            Continuar com Google
          </a>
          <div className="flex items-center gap-3">
            <span aria-hidden="true" className="h-px flex-1 bg-border" />
            <span className="font-label text-xs uppercase tracking-wider text-text-muted">
              ou
            </span>
            <span aria-hidden="true" className="h-px flex-1 bg-border" />
          </div>
        </>
      )}
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
  const live = useRef<HTMLParagraphElement>(null);
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
  const announcement = waiting
    ? `Você poderá reenviar o link em ${startCountdown}.`
    : "Você já pode reenviar o link.";

  // SPEC-0020 (§7.6) — a região viva nasce vazia e recebe o texto depois de montada: o leitor de tela só anuncia
  // mudanças numa região que já existe. O texto muda só no começo e no fim da espera, não a cada segundo.
  useEffect(() => {
    if (live.current) {
      live.current.textContent = announcement;
    }
  }, [announcement]);

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
        <p ref={live} aria-live="polite" className="sr-only" />
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

/** SPEC-0020 — o "G" do Google (decorativo: o texto do botão dá o nome). */
function GoogleIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      width="20"
      height="20"
      aria-hidden="true"
      focusable="false"
    >
      <path
        fill="#4285F4"
        d="M23.5 12.3c0-.8-.1-1.6-.2-2.3H12v4.4h6.5a5.5 5.5 0 0 1-2.4 3.6v3h3.9c2.2-2.1 3.5-5.1 3.5-8.7Z"
      />
      <path
        fill="#34A853"
        d="M12 24c3.2 0 6-1.1 8-2.9l-3.9-3c-1.1.7-2.5 1.2-4.1 1.2-3.1 0-5.8-2.1-6.7-5H1.3v3.1A12 12 0 0 0 12 24Z"
      />
      <path
        fill="#FBBC05"
        d="M5.3 14.3a7.2 7.2 0 0 1 0-4.6V6.6h-4a12 12 0 0 0 0 10.8l4-3.1Z"
      />
      <path
        fill="#EA4335"
        d="M12 4.8c1.8 0 3.3.6 4.6 1.8l3.4-3.4A12 12 0 0 0 1.3 6.6l4 3.1c.9-2.9 3.6-4.9 6.7-4.9Z"
      />
    </svg>
  );
}
