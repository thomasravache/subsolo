/** SPEC-0030 — textos da parte "Avisos" (§6, Emenda de 2026-10-23) e da página de descadastro. */
export const noticeMessages = {
  title: "Avisos",
  pushTitle: "Avisos no celular (push)",
  pushDescription: "Receba na hora os shows novos de quem você segue.",
  activate: "Ativar avisos",
  activating: "Ativando…",
  active: "Avisos ativos neste aparelho",
  blocked: "Os avisos estão bloqueados no navegador.",
  blockedHint:
    "Para receber, libere as notificações do Subsolo nas configurações do navegador.",
  unsupported: "Este navegador não recebe avisos.",
  installIos: "Adicione o Subsolo à tela de início para receber avisos",
  installIosHint:
    "No Safari, toque em Compartilhar e depois em “Adicionar à Tela de Início”; abra o Subsolo por lá.",
  activateFailed:
    "Não foi possível ativar os avisos agora. Tente de novo em instantes.",
  saveFailed:
    "Não foi possível salvar a preferência agora. Tente de novo em instantes.",
  unavailable:
    "Não foi possível carregar as preferências de aviso agora. Tente de novo em instantes.",
  unsubscribed: {
    digest: "Pronto: você não recebe mais o resumo diário por e-mail.",
    reminder: "Pronto: você não recebe mais o lembrete na véspera por e-mail.",
  },
  unsubscribing: "Descadastrando…",
  invalidLink: "Este link de descadastro é inválido.",
} as const;

/** Os quatro interruptores (§6), na ordem da tela. */
export const switches = [
  { key: "newShowPush", label: "Show novo de quem eu sigo (na hora)" },
  { key: "dailyDigestEmail", label: "Resumo diário por e-mail" },
  { key: "reminderPush", label: "Lembrete na véspera (no celular)" },
  { key: "reminderEmail", label: "Lembrete na véspera (por e-mail)" },
] as const;
