// @vitest-environment node
import { spawnSync } from "node:child_process";
import {
  cpSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  symlinkSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { basename, join } from "node:path";
import { fileURLToPath } from "node:url";

import { afterEach, describe, expect, it } from "vitest";

/**
 * SPEC-0008 CT-01 — `(cd web && pnpm api:check)` confere que o cliente TypeScript versionado em `web/src/api/`
 * é idêntico ao gerado a partir do snapshot `docs/api/openapi.v1.json`: passa no repositório como está e falha
 * quando o OpenAPI muda sem regenerar o cliente.
 *
 * O caso de divergência roda numa cópia de `web/` (com `node_modules` ligado por symlink) ao lado de uma cópia de
 * `docs/api/`, para não tocar nos arquivos versionados.
 */

const webDir = fileURLToPath(new URL("../..", import.meta.url));
const repoRoot = join(webDir, "..");
const snapshotRelative = join("docs", "api", "openapi.v1.json");

/** Pastas de `web/` que não entram na cópia (geradas ou instaladas). */
const notCopied = new Set(["node_modules", ".next", "coverage"]);

const timeoutMs = 180_000;

interface Run {
  status: number | null;
  output: string;
}

function apiCheck(cwd: string): Run {
  const run = spawnSync("pnpm", ["run", "api:check"], {
    cwd,
    encoding: "utf8",
    env: process.env,
    timeout: timeoutMs,
  });
  return {
    status: run.status,
    output: `${run.stdout ?? ""}\n${run.stderr ?? ""}\n${run.error?.message ?? ""}`,
  };
}

let sandbox: string | undefined;

afterEach(() => {
  if (sandbox) {
    rmSync(sandbox, { recursive: true, force: true });
    sandbox = undefined;
  }
});

/** Copia `web/` e `docs/api/openapi.v1.json` para uma pasta temporária com o mesmo layout do repositório. */
function copyRepository(): { web: string; snapshot: string } {
  sandbox = mkdtempSync(join(tmpdir(), "subsolo-ct01-"));
  const web = join(sandbox, "web");
  cpSync(webDir, web, {
    recursive: true,
    filter: (source) => !notCopied.has(basename(source)),
  });
  symlinkSync(join(webDir, "node_modules"), join(web, "node_modules"), "dir");

  const snapshot = join(sandbox, snapshotRelative);
  mkdirSync(join(sandbox, "docs", "api"), { recursive: true });
  cpSync(join(repoRoot, snapshotRelative), snapshot);
  return { web, snapshot };
}

describe("cliente OpenAPI em web/src/api", () => {
  it(
    "SPEC-0008:CT-01 api:check passa com o cliente versionado em dia com o snapshot",
    () => {
      const run = apiCheck(webDir);

      expect(run.status, run.output).toBe(0);
    },
    timeoutMs,
  );

  it(
    "SPEC-0008:CT-01 api:check falha quando o OpenAPI muda sem regenerar o cliente",
    () => {
      const copy = copyRepository();
      const before = apiCheck(copy.web);
      expect(
        before.status,
        `cópia sem mudança deveria passar:\n${before.output}`,
      ).toBe(0);

      const document = JSON.parse(readFileSync(copy.snapshot, "utf8")) as {
        paths?: Record<string, unknown>;
      };
      document.paths = {
        ...document.paths,
        "/api/v1/ct01-probe": {
          get: {
            operationId: "Ct01Probe",
            responses: { "200": { description: "OK" } },
          },
        },
      };
      writeFileSync(copy.snapshot, `${JSON.stringify(document, null, 2)}\n`);

      const after = apiCheck(copy.web);
      expect(after.status, after.output).not.toBe(0);
    },
    timeoutMs * 2,
  );
});
