import { randomBytes } from "node:crypto";

import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";

/**
 * SPEC-0009 E2E-01 — com a aplicação no ar, o fã abre a agenda da cidade no celular: a página carrega normalmente e
 * o Seq local registra o trace da navegação, consultado pela API do Seq (`GET /api/events?filter=@TraceId = '…'`)
 * pelo trace id do cabeçalho `traceparent` enviado na navegação.
 *
 * - `SEQ_URL`: endereço da API do Seq local (sem porta publicada no Compose — acesso por túnel ou pelo AppHost).
 * - `SEQ_API_KEY` (opcional): enviada em `X-Seq-ApiKey`.
 */
test.use({ baseURL: appURL });

const seqURL = process.env.SEQ_URL;
const seqApiKey = process.env.SEQ_API_KEY;

/** Tempo para o lote de exportação OTLP chegar ao Seq e ser indexado. */
const ingestionTimeout = 60_000;

test.describe("observabilidade da agenda", () => {
  test("SPEC-0009:E2E-01 a navegação do fã pela agenda aparece no Seq pelo trace id do traceparent [jornada: agenda]", async ({
    page,
    request,
  }, testInfo) => {
    test.skip(
      !testInfo.project.name.startsWith("mobile"),
      "jornada do fã no celular (projetos mobile)",
    );

    const traceId = randomBytes(16).toString("hex");
    const parentId = randomBytes(8).toString("hex");
    await page.setExtraHTTPHeaders({
      traceparent: `00-${traceId}-${parentId}-01`,
    });

    const agenda = await page.goto("/cidade-exemplo");
    expect(agenda?.status()).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: "Agenda Cidade Exemplo" }),
    ).toBeVisible();

    expect(
      seqURL,
      "SEQ_URL não definido: informe o endereço da API do Seq local",
    ).toBeTruthy();

    const filter = encodeURIComponent(`@TraceId = '${traceId}'`);
    await expect
      .poll(
        async () => {
          const response = await request.get(
            `${seqURL}/api/events?filter=${filter}&count=100`,
            {
              headers: {
                Accept: "application/json",
                ...(seqApiKey ? { "X-Seq-ApiKey": seqApiKey } : {}),
              },
            },
          );
          if (!response.ok()) {
            return `HTTP ${response.status()}`;
          }
          const events: unknown = await response.json();
          return Array.isArray(events) ? events.length : "resposta inesperada";
        },
        {
          message: `o Seq não registrou eventos do trace ${traceId}`,
          timeout: ingestionTimeout,
          intervals: [1_000, 2_000, 5_000],
        },
      )
      .toBeGreaterThan(0);
  });
});
