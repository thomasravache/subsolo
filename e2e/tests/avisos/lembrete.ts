import { expect } from "@playwright/test";

import { mailpitURL } from "../auth/environment";

/**
 * SPEC-0032 — apoio da jornada do lembrete na véspera na pilha do Compose: o `tools/compose-up.sh` sobe a API com
 * `Engagement__ReminderCron` a cada minuto e `Engagement__ReminderHour` = 0 (os padrões são de hora em hora e 10h no
 * fuso da cidade), e o e-mail do lembrete ("Amanhã: {atrações}") é lido na API HTTP do Mailpit (`MAILPIT_URL`).
 */
interface MailpitSummary {
  ID: string;
  Subject: string;
  To: { Address: string }[];
}

/** O texto dos e-mails com o assunto `subject` mandados para `email`. */
async function mailTexts(email: string, subject: string): Promise<string[]> {
  const query = encodeURIComponent(`to:"${email}"`);
  const search = await fetch(
    `${mailpitURL}/api/v1/search?query=${query}&limit=50`,
  );
  if (!search.ok) {
    return [];
  }
  const { messages } = (await search.json()) as { messages: MailpitSummary[] };
  const texts: string[] = [];
  for (const message of messages.filter(
    (m) =>
      m.Subject === subject &&
      m.To.some((to) => to.Address.toLowerCase() === email.toLowerCase()),
  )) {
    const detail = await fetch(
      `${mailpitURL}/api/v1/message/${encodeURIComponent(message.ID)}`,
    );
    const { Text } = (await detail.json()) as { Text?: string };
    texts.push(Text ?? "");
  }
  return texts;
}

/** O assunto do lembrete (o título do aviso, §6): "Amanhã: {atrações}". */
export function reminderSubject(lineup: string[]): string {
  return `Amanhã: ${lineup.join(" + ")}`;
}

/**
 * Espera o lembrete `subject` para `email` chegar ao Mailpit (até `timeout`, com o job a cada minuto) e devolve o texto
 * dele.
 */
export async function waitForReminder(
  email: string,
  subject: string,
  timeout = 150_000,
): Promise<string> {
  let found = "";
  await expect
    .poll(
      async () => {
        found = (await mailTexts(email, subject))[0] ?? "";
        return found;
      },
      {
        message: `o lembrete "${subject}" não chegou ao Mailpit (${mailpitURL}) para ${email}`,
        timeout,
        intervals: [5_000],
      },
    )
    .not.toBe("");
  return found;
}

/** Quantos lembretes `subject` o `email` recebeu. */
export async function reminderCount(
  email: string,
  subject: string,
): Promise<number> {
  return (await mailTexts(email, subject)).length;
}

/**
 * SPEC-0036:H32-05 — quanto falta (ms) para a próxima meia-noite de São Paulo a partir de `now`. O fuso de São Paulo não
 * tem horário de verão desde 2019 (UTC-3 fixo).
 */
export function untilSaoPauloMidnight(now = Date.now()): number {
  const offset = 3 * 60 * 60 * 1000;
  const day = 24 * 60 * 60 * 1000;
  const local = now - offset;
  return day - (((local % day) + day) % day);
}

/**
 * SPEC-0036:H32-05 — se faltar menos de `margin` (3 min) para a meia-noite de São Paulo, espera passar dela (com 5 s de
 * folga), para o show "de amanhã" não virar "de hoje" no meio da jornada. Devolve quanto esperou (ms).
 */
export async function waitPastSaoPauloMidnight(
  margin = 180_000,
): Promise<number> {
  const remaining = untilSaoPauloMidnight();
  if (remaining >= margin) {
    return 0;
  }
  const wait = remaining + 5_000;
  await new Promise((resolve) => setTimeout(resolve, wait));
  return wait;
}
