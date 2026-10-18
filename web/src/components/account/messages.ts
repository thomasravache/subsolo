/** SPEC-0019 — textos da "Minha conta" (§6 e telas "Minha Conta" e "Excluir Conta" do Stitch). */

export const accountMessages = {
  nameSaved: "Nome salvo.",
  invalidName: "Use de 1 a 60 caracteres.",
  emailMismatch: "O e-mail não confere.",
  rateLimited: "Muitas tentativas. Espere um minuto e tente de novo.",
  generic: "Algo deu errado. Tente de novo em instantes.",
  exportFailed:
    "Não conseguimos gerar o arquivo agora. Tente de novo em instantes.",
} as const;

/** Mensagem para um erro da API sem campo próprio (limite de requisições ou falha geral). */
export function accountErrorMessage(status: number): string {
  return status === 429 ? accountMessages.rateLimited : accountMessages.generic;
}

/** Nome do arquivo baixado em "Baixar meus dados" (o mesmo do `Content-Disposition` da API). */
export const exportFileName = "subsolo-meus-dados.json";

/** Para onde a exclusão leva: a página inicial com o aviso "Sua conta foi excluída.". */
export const deletedAccountPath = "/?conta=excluida";
