/**
 * SPEC-0010 — leitura da Content-Security-Policy nos testes de segurança: diretivas → fontes.
 */
export type CspDirectives = Map<string, string[]>;

/** `default-src 'self'; script-src 'self' 'nonce-…'` → { default-src: ['self'], script-src: [...] }. */
export function parseCsp(header: string | undefined): CspDirectives {
  const directives: CspDirectives = new Map();
  for (const part of (header ?? "").split(";")) {
    const [name, ...sources] = part.trim().split(/\s+/);
    if (name) {
      directives.set(name.toLowerCase(), sources);
    }
  }
  return directives;
}

/** O nonce do `script-src` (`'nonce-<valor>'`), ou `undefined` sem nonce. */
export function scriptNonce(directives: CspDirectives): string | undefined {
  const nonce = (directives.get("script-src") ?? [])
    .map((source) => /^'nonce-([A-Za-z0-9+/_=-]+)'$/.exec(source)?.[1])
    .find((value) => value !== undefined);
  return nonce;
}
