/** SPEC-0016 — mensagens das telas de entrar para os erros da API (§6). */

export const rateLimitedMessage =
  "Muitas tentativas. Espere um minuto e tente de novo.";

export const emailUnavailableMessage =
  "Não conseguimos mandar o e-mail agora. Tente de novo em alguns minutos.";

export const genericErrorMessage =
  "Algo deu errado. Tente de novo em instantes.";

/** Mensagem do pedido de link para o código (ou o status) do erro da API. */
export function requestErrorMessage(code: string, status: number): string {
  if (status === 429 || code === "security.rate_limited") {
    return rateLimitedMessage;
  }
  if (status === 503 || code === "notifications.email_unavailable") {
    return emailUnavailableMessage;
  }
  return genericErrorMessage;
}

/** SPEC-0020 — o login com Google falhou (cancelou, provedor fora do ar, `state` inválido): `/entrar?erro=google`. */
export const googleErrorMessage =
  "Não deu para entrar com o Google. Tente de novo ou use o link por e-mail.";

/** SPEC-0020 — a conta Google não tem e-mail verificado: `/entrar?erro=google_email`. */
export const googleEmailErrorMessage =
  "Sua conta Google não tem e-mail verificado. Use o link por e-mail.";

/** SPEC-0020 — mensagem do `?erro=` da volta do Google (outros valores são ignorados). */
export function signInErrorMessage(error: string | null): string | null {
  if (error === "google") {
    return googleErrorMessage;
  }
  if (error === "google_email") {
    return googleEmailErrorMessage;
  }
  return null;
}
