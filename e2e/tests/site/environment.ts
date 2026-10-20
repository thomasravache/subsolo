/**
 * SPEC-0028 — a configuração do Pix da pilha de teste (só do servidor do web, nunca `NEXT_PUBLIC_`): o
 * `tools/compose-up.sh` passa ao contêiner do web `Support__PixKey`, `Support__PixName` e `Support__PixCity` com estes
 * valores (os mesmos padrões, sobrescritos por `E2E_PIX_KEY`, `E2E_PIX_NAME` e `E2E_PIX_CITY`). O servidor Next.js sem
 * API do `playwright.config.ts` não recebe nenhum deles: é a aplicação sem chave Pix.
 */
export const testPixKey =
  process.env.E2E_PIX_KEY ?? "chave-teste-e2e@subsolo.test";

export const testPixName = process.env.E2E_PIX_NAME ?? "Subsolo";

export const testPixCity = process.env.E2E_PIX_CITY ?? "Curitiba";

/** Texto alternativo do QR code (§6). */
export const qrCodeName = "QR code do Pix do Subsolo";

export const apoiePath = "/apoie";

export const adminSupportersPath = "/admin/apoiadores";
