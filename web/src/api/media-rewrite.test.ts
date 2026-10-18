// @vitest-environment node
import { afterEach, describe, expect, it } from "vitest";

import nextConfig from "../../next.config";

/**
 * SPEC-0023 H22-06 (revisão da SPEC-0022) — no `next dev` (aspire run) não há Caddy: `/media/*` (as variantes do flyer,
 * servidas pela API em desenvolvimento) chega à API por um rewrite do Next.js para `${API_URL}/media/:path*`. No
 * Compose e em produção o Caddy responde `/media/*` antes do web, então o rewrite não muda nada lá.
 */
type Rewrite = { source: string; destination: string };

type Rewrites =
  | Rewrite[]
  | { beforeFiles?: Rewrite[]; afterFiles?: Rewrite[]; fallback?: Rewrite[] };

const apiUrl = "http://127.0.0.1:5199";
const previous = process.env.API_URL;

afterEach(() => {
  if (previous === undefined) {
    delete process.env.API_URL;
  } else {
    process.env.API_URL = previous;
  }
});

describe("next.config — /media no desenvolvimento", () => {
  it("SPEC-0023:H22-06 /media/:path* é reescrito para a API (API_URL)", async () => {
    process.env.API_URL = apiUrl;

    expect(
      nextConfig.rewrites,
      "next.config sem rewrites para /media",
    ).toBeTypeOf("function");
    const result = (await nextConfig.rewrites!()) as Rewrites;
    const rules = Array.isArray(result)
      ? result
      : [
          ...(result.beforeFiles ?? []),
          ...(result.afterFiles ?? []),
          ...(result.fallback ?? []),
        ];

    expect(rules).toContainEqual(
      expect.objectContaining({
        source: "/media/:path*",
        destination: `${apiUrl}/media/:path*`,
      }),
    );
  });
});
