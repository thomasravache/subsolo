// @vitest-environment node
import { NextRequest } from "next/server";
import { describe, expect, it } from "vitest";

import { proxy } from "@/proxy";

/**
 * SPEC-0010 UT-02 — o proxy (ADR-0016) gera a CSP de cada requisição: o `script-src` traz `'self'` e um nonce
 * novo (diferente a cada requisição) e a política tem `frame-ancestors 'none'`; o mesmo nonce vai para a
 * requisição repassada ao Next.js (cabeçalho `Content-Security-Policy` da requisição), que o aplica nos scripts.
 */
function directives(header: string | null): Map<string, string[]> {
  const map = new Map<string, string[]>();
  for (const part of (header ?? "").split(";")) {
    const [name, ...sources] = part.trim().split(/\s+/);
    if (name) {
      map.set(name.toLowerCase(), sources);
    }
  }
  return map;
}

function nonceOf(header: string | null): string | undefined {
  return (directives(header).get("script-src") ?? [])
    .map((source) => /^'nonce-([A-Za-z0-9+/_=-]+)'$/.exec(source)?.[1])
    .find((value) => value !== undefined);
}

async function cspFor(path: string): Promise<string | null> {
  const response = await proxy(new NextRequest(`http://localhost${path}`));
  return response.headers.get("content-security-policy");
}

describe("CSP do proxy", () => {
  it("SPEC-0010:UT-02 script-src com 'self' e nonce, e frame-ancestors 'none'", async () => {
    const csp = await cspFor("/");

    const parsed = directives(csp);
    expect(parsed.get("script-src")).toContain("'self'");
    expect(nonceOf(csp)).toBeTruthy();
    expect(parsed.get("frame-ancestors")).toEqual(["'none'"]);
  });

  it("SPEC-0010:UT-02 o nonce é novo a cada requisição", async () => {
    const nonces = new Set<string | undefined>();
    for (let i = 0; i < 5; i++) {
      nonces.add(nonceOf(await cspFor("/")));
    }

    expect(nonces.has(undefined)).toBe(false);
    expect(nonces.size).toBe(5);
  });
});
