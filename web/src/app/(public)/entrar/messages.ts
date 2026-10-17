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
