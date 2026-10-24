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
