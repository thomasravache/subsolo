import { expect, test } from "../fixtures";
import { appURL } from "./environment";

/**
 * SPEC-0008 E2E-02 — na pilha do Compose gerado pelo `aspire publish` e iniciado por `tools/compose-up.sh`
 * (`BASE_URL`, padrão http://localhost:8080): `/health` responde 200, a agenda da cidade de exemplo carrega e o
 * p95 da duração da navegação em 20 carregamentos fica abaixo de 800 ms (SLO do SSR, ADR-0010).
 */
// SPEC-0010: o Caddy do Compose redireciona HTTP para HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const loads = 20;
const p95BudgetMs = 800;

/** Percentil pelo método do posto mais próximo (nearest-rank). */
function percentile(values: number[], p: number): number {
  const sorted = [...values].sort((a, b) => a - b);
  const rank = Math.ceil((p / 100) * sorted.length);
  return sorted[Math.max(0, rank - 1)] ?? Number.NaN;
}

test.describe("pilha do Compose", () => {
  test("SPEC-0008:E2E-02 /health responde 200 e a agenda carrega com p95 abaixo de 800 ms [jornada: agenda] @smoke", async ({
    page,
    request,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "medição de desempenho num projeto só (mobile-chrome)",
    );

    const health = await request.get("/health");
    expect(health.status()).toBe(200);

    const durations: number[] = [];
    for (let i = 0; i < loads; i++) {
      const response = await page.goto("/cidade-exemplo");
      expect(response?.status()).toBe(200);
      const duration = await page.evaluate(() => {
        const [navigation] = performance.getEntriesByType(
          "navigation",
        ) as PerformanceNavigationTiming[];
        return navigation ? navigation.duration : Number.NaN;
      });
      durations.push(duration);
    }

    await expect(
      page.getByRole("heading", { level: 1, name: "Agenda Cidade Exemplo" }),
    ).toBeVisible();
    const p95 = percentile(durations, 95);
    expect(
      p95,
      `p95 de ${loads} carregamentos (ms): ${durations.map((d) => d.toFixed(0)).join(", ")}`,
    ).toBeLessThan(p95BudgetMs);
  });
});
