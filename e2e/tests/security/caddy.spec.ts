import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";

/**
 * SPEC-0010 IT-06 — o Caddy do Compose local (ADR-0004) atende em HTTPS com o certificado local (`tls internal`):
 * `http://localhost:8080/` responde 308 para o endereço HTTPS, e a resposta HTTPS traz
 * `Strict-Transport-Security: max-age=31536000`.
 */
test.use({ ignoreHTTPSErrors: true });

/** O endereço HTTP da aplicação (`BASE_URL` em HTTPS vira o mesmo host em HTTP, porta padrão). */
function httpURL(): string {
  const url = new URL(appURL);
  if (url.protocol === "https:") {
    url.protocol = "http:";
    url.port = "";
  }
  return url.origin;
}

test.describe("Caddy com HTTPS", () => {
  test("SPEC-0010:IT-06 HTTP responde 308 para HTTPS e o HTTPS traz HSTS @smoke", async ({
    request,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );

    const plain = await request.get(`${httpURL()}/`, { maxRedirects: 0 });

    expect(plain.status()).toBe(308);
    const location = plain.headers()["location"] ?? "";
    expect(location, "308 sem Location para HTTPS").toMatch(/^https:\/\//);

    const secure = await request.get(new URL("/", location).toString(), {
      maxRedirects: 0,
    });

    expect(secure.status()).toBe(200);
    expect(secure.headers()["strict-transport-security"] ?? "").toMatch(
      /(^|;)\s*max-age=31536000\b/,
    );
  });
});
