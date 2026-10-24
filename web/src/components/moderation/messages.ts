import type { ApiResult } from "@/api/browser";
import type { ReportReason } from "@/api/reports";

/** SPEC-0033 — os motivos da folha "Reportar show" (tela "Denunciar Show - Subsolo" do Stitch), na ordem dela. */
export const reportReasons: {
  value: ReportReason;
  label: string;
  hint: string;
}[] = [
  {
    value: "fake",
    label: "Show falso",
    hint: "O evento não existe ou foi inventado.",
  },
  {
    value: "cancelled_unannounced",
    label: "Cancelado ou adiado e não avisaram",
    hint: "A banda ou casa cancelou e a info continua ativa.",
  },
  {
    value: "wrong_info",
    label: "Informação errada",
    hint: "Data, horário, local ou line-up incorretos.",
  },
  {
    value: "other",
    label: "Outro",
    hint: "Outro problema não listado acima.",
  },
];

/** O rótulo do motivo (como na folha). */
export function reasonLabel(reason: ReportReason): string {
  return reportReasons.find((item) => item.value === reason)?.label ?? reason;
}

/** Tamanho máximo do detalhe (§6). */
export const detailsMaxLength = 500;

/** SPEC-0033 — textos da denúncia e da fila (§6). */
export const reportMessages = {
  intro: "Ajude a manter a agenda do Subsolo correta.",
  introMore: "Selecione o motivo da denúncia para este evento:",
  thanks: "Obrigado. A moderação vai olhar.",
  already: "Você já denunciou este show.",
  chooseReason: "Escolha o motivo da denúncia.",
  detailsRequired: "Conte o que está errado no show.",
  detailsTooLong: `Conte em até ${detailsMaxLength} caracteres.`,
  showGone: "Este show não está mais na agenda.",
  limit: "Você já fez muitas denúncias hoje. Tente de novo amanhã.",
  failed: "Não deu para enviar agora. Tente de novo em instantes.",
  kept: "Show mantido. As denúncias foram arquivadas.",
  hidden: "Show ocultado. Ele saiu da agenda.",
  alreadyDecided: "Este show já foi decidido. Recarregue a página.",
  hideQuestion:
    "O show sai da agenda, da página pública e dos avisos. O dono continua vendo o show em Meus shows, sem poder editar.",
  empty: "Nenhuma denúncia pendente.",
} as const;

/** "1 denúncia" / "{n} denúncias". */
export function reportCount(count: number): string {
  return count === 1 ? "1 denúncia" : `${count} denúncias`;
}

/** A mensagem de uma falha ao denunciar. */
export function reportErrorMessage(
  result: Extract<ApiResult<unknown>, { ok: false }>,
): string {
  switch (result.code) {
    case "moderation.already_reported":
      return reportMessages.already;
    case "events.show_not_found":
      return reportMessages.showGone;
    case "security.rate_limited":
      return reportMessages.limit;
    default:
      return reportMessages.failed;
  }
}
