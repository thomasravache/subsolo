import { expect } from "@playwright/test";

import { mailpitURL } from "../auth/environment";

/**
 * SPEC-0031 — apoio da jornada do resumo diário na pilha do Compose: o `tools/compose-up.sh` sobe a API com
 * `Engagement__DigestCron` a cada minuto (o padrão é às 9h de São Paulo), e o e-mail do resumo é lido na API HTTP do
 * Mailpit (`MAILPIT_URL`).
 */
export const digestSubject = "Shows novos de quem você segue";

interface MailpitSummary {
  ID: string;
  Subject: string;
  To: { Address: string }[];
}

/** O texto dos e-mails de resumo mandados para `email` (do mais novo ao mais antigo). */
async function digestTexts(email: string): Promise<string[]> {
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
      m.Subject === digestSubject &&
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

/**
 * Espera o e-mail "Shows novos de quem você segue" para `email` chegar ao Mailpit (até `timeout`, com o resumo a cada
 * minuto) com a linha `line`, e devolve o texto dele.
 */
export async function waitForDigest(
  email: string,
  line: string,
  timeout = 150_000,
): Promise<string> {
  let found = "";
  await expect
    .poll(
      async () => {
        found =
          (await digestTexts(email)).find((text) => text.includes(line)) ?? "";
        return found;
      },
      {
        message: `o resumo diário com "${line}" não chegou ao Mailpit (${mailpitURL}) para ${email}`,
        timeout,
        intervals: [5_000],
      },
    )
    .not.toBe("");
  return found;
}

/** Quantos e-mails de resumo `email` recebeu. */
export async function digestCount(email: string): Promise<number> {
  return (await digestTexts(email)).length;
}

/** O caminho (com a query) do primeiro link do texto que bate com `pattern` (URL absoluta da pilha). */
export function linkPath(text: string, pattern: RegExp): string {
  const match = pattern.exec(text);
  expect(match, `link ${pattern} ausente no e-mail:\n${text}`).not.toBeNull();
  const url = new URL(match?.[0] ?? "");
  return `${url.pathname}${url.search}`;
}

/** "dd/MM" de uma data local `aaaa-mm-dd`. */
export function dayMonth(localDate: string): string {
  const [, month, day] = localDate.split("-");
  return `${day}/${month}`;
}
