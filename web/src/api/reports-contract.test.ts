// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0033 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica as rotas de
 * denúncia do §6 — `POST /api/v1/shows/{id}/reports`, `GET /api/v1/admin/reports`, `POST …/{showId}/keep` e
 * `POST …/{showId}/hide` — com os corpos, status e códigos de erro; "Meus shows" ganha `hidden` e a edição e a mudança
 * de situação do show declaram 409 `events.show_hidden` (Emenda 1); o cliente `src/api/schema.d.ts` foi regenerado e
 * `(cd web && pnpm api:check)` passa. (O evento `ShowHiddenByModeration` é conferido nos testes unitários do Events, e
 * o OpenAPI gerado pela API no `ModerationOpenApiContractTests`.)
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  enum?: unknown[];
  items?: Schema;
  required?: string[];
  properties?: Record<string, Schema>;
  oneOf?: Schema[];
  anyOf?: Schema[];
  allOf?: Schema[];
};

type Response = {
  description?: string;
  content?: Record<string, { schema?: Schema }>;
};

type Operation = {
  security?: unknown[];
  requestBody?: { content?: Record<string, { schema?: Schema }> };
  responses: Record<string, Response>;
};

type OpenApiDocument = {
  paths: Record<string, Record<string, Operation>>;
  components?: { schemas?: Record<string, Schema> };
};

const webDir = fileURLToPath(new URL("../..", import.meta.url));
const snapshotPath = fileURLToPath(
  new URL("../../../docs/api/openapi.v1.json", import.meta.url),
);
const clientPath = fileURLToPath(new URL("./schema.d.ts", import.meta.url));
const document = JSON.parse(
  readFileSync(snapshotPath, "utf8"),
) as OpenApiDocument;

const reportsPath = "/api/v1/shows/{id}/reports";
const queuePath = "/api/v1/admin/reports";
const keepPath = "/api/v1/admin/reports/{showId}/keep";
const hidePath = "/api/v1/admin/reports/{showId}/hide";
const showPath = "/api/v1/shows/{id}";
const showStatusPath = "/api/v1/shows/{id}/status";
const myShowsPath = "/api/v1/me/shows";

function resolve(schema: Schema | undefined): Schema {
  let current = schema ?? {};
  for (let depth = 0; current.$ref && depth < 10; depth++) {
    const name = current.$ref.replace("#/components/schemas/", "");
    current = document.components?.schemas?.[name] ?? {};
  }
  return current;
}

function property(schema: Schema, name: string): Schema | undefined {
  const resolved = resolve(schema);
  const direct = resolved.properties?.[name];
  if (direct) {
    return resolve(direct);
  }
  for (const part of [
    ...(resolved.allOf ?? []),
    ...(resolved.oneOf ?? []),
    ...(resolved.anyOf ?? []),
  ]) {
    const found = property(part, name);
    if (found) {
      return found;
    }
  }
  return undefined;
}

function types(schema: Schema): string[] {
  const own = schema.type
    ? Array.isArray(schema.type)
      ? schema.type
      : [schema.type]
    : [];
  const alternatives = [...(schema.oneOf ?? []), ...(schema.anyOf ?? [])]
    .map(resolve)
    .flatMap(types);
  return [...own, ...alternatives];
}

function operation(path: string, method: string): Operation {
  const found = document.paths[path]?.[method];
  expect(
    found,
    `${method.toUpperCase()} ${path} ausente no snapshot`,
  ).toBeDefined();
  return found as Operation;
}

function requestSchema(path: string, method: string): Schema {
  const content = operation(path, method).requestBody?.content?.[
    "application/json"
  ];
  expect(
    content,
    `${method.toUpperCase()} ${path} sem corpo application/json`,
  ).toBeDefined();
  return resolve(content?.schema);
}

function jsonResponse(path: string, method: string, status: string): Schema {
  const content = operation(path, method).responses[status]?.content?.[
    "application/json"
  ];
  expect(
    content,
    `${method.toUpperCase()} ${path} ${status} sem application/json`,
  ).toBeDefined();
  return resolve(content?.schema);
}

/** O objeto tem exatamente as propriedades `names`, todas string e obrigatórias. */
function expectStrings(schema: Schema, names: string[], where: string) {
  expect(Object.keys(resolve(schema).properties ?? {}).sort(), where).toEqual(
    [...names].sort(),
  );
  for (const name of names) {
    expect(types(property(schema, name) ?? {}), `${where}.${name}`).toContain(
      "string",
    );
  }
  expect(resolve(schema).required ?? [], where).toEqual(
    expect.arrayContaining(names),
  );
}

/** A resposta de erro é ProblemDetails e cita cada código (`code=…`) na descrição. */
function expectProblem(
  path: string,
  method: string,
  status: string,
  codes: string[],
) {
  const response = operation(path, method).responses[status];
  const where = `${method.toUpperCase()} ${path} ${status}`;
  expect(response, `${where} não declarado`).toBeDefined();
  expect(
    response?.content?.["application/problem+json"],
    `${where} sem application/problem+json`,
  ).toBeDefined();
  for (const code of codes) {
    expect(response?.description ?? "", where).toContain(`code=${code}`);
  }
}

/** O objeto tem exatamente as propriedades `names` (sem exigir o tipo de cada uma). */
function expectKeys(schema: Schema, names: string[], where: string) {
  expect(Object.keys(resolve(schema).properties ?? {}).sort(), where).toEqual(
    [...names].sort(),
  );
}

/** Os erros comuns de toda rota de admin (§6): 401, 403 e, nas mutações, o 400 do anti-forgery. */
function expectAdminErrors(path: string, method: string) {
  expectProblem(path, method, "401", ["auth.required"]);
  expectProblem(path, method, "403", ["auth.forbidden"]);
  if (method !== "get") {
    expectProblem(path, method, "400", ["security.csrf_invalid"]);
  }
}

/** Os erros comuns das rotas de fã (§6): 401, 429 e, nas mutações, o 400 do anti-forgery. */
function expectFanErrors(path: string, method: string) {
  expectProblem(path, method, "401", ["auth.required"]);
  expectProblem(path, method, "429", ["security.rate_limited"]);
  if (method !== "get") {
    expectProblem(path, method, "400", ["security.csrf_invalid"]);
  }
}

const reasons = ["fake", "cancelled_unannounced", "wrong_info", "other"];

describe("OpenAPI de denúncias fiel ao contrato §6", () => {
  it("SPEC-0033:CT-01 POST /api/v1/shows/{id}/reports recebe {reason, details} e responde 201 {id}, 400, 404, 409 e 429", () => {
    const body = requestSchema(reportsPath, "post");
    expectKeys(body, ["reason", "details"], `POST ${reportsPath} corpo`);
    expect(types(property(body, "details") ?? {})).toContain("string");

    expectStrings(
      jsonResponse(reportsPath, "post", "201"),
      ["id"],
      `POST ${reportsPath} 201`,
    );
    expectProblem(reportsPath, "post", "400", [
      "moderation.invalid_report",
      "security.csrf_invalid",
    ]);
    expectProblem(reportsPath, "post", "404", ["events.show_not_found"]);
    expectProblem(reportsPath, "post", "409", ["moderation.already_reported"]);
    expectFanErrors(reportsPath, "post");
  });

  it("SPEC-0033:CT-01 GET /api/v1/admin/reports devolve a fila agrupada por show, sem quem denunciou", () => {
    const list = jsonResponse(queuePath, "get", "200");
    expect(types(list)).toContain("array");
    const item = list.items ?? {};
    expectKeys(
      item,
      [
        "showId",
        "title",
        "citySlug",
        "slug",
        "startsAtUtc",
        "venueName",
        "counts",
        "latestDetails",
        "firstReportedAtUtc",
        // SPEC-0037 (Emenda 1, H33-06): o fuso da cidade, campo aditivo.
        "timeZone",
      ],
      `GET ${queuePath} item`,
    );
    const counts = property(item, "counts") ?? {};
    expectKeys(counts, reasons, `GET ${queuePath} counts`);
    for (const reason of reasons) {
      expect(
        types(property(counts, reason) ?? {}),
        `counts.${reason}`,
      ).toContain("integer");
    }
    const details = property(item, "latestDetails") ?? {};
    expect(types(details)).toContain("array");
    expect(types(resolve(details.items))).toContain("string");
    expect(JSON.stringify(resolve(item))).not.toMatch(/reporter|email|userId/i);
    expectAdminErrors(queuePath, "get");
  });

  it("SPEC-0033:CT-01 POST …/{showId}/keep responde 204 e 404 moderation.no_pending_reports", () => {
    expect(Object.keys(operation(keepPath, "post").responses)).toContain("204");
    expectProblem(keepPath, "post", "404", ["moderation.no_pending_reports"]);
    expectAdminErrors(keepPath, "post");
  });

  it("SPEC-0033:CT-01 POST …/{showId}/hide recebe {note} e responde 204 e 404 moderation.no_pending_reports", () => {
    const body = requestSchema(hidePath, "post");
    expectKeys(body, ["note"], `POST ${hidePath} corpo`);
    expect(types(property(body, "note") ?? {})).toContain("string");
    expect(Object.keys(operation(hidePath, "post").responses)).toContain("204");
    expectProblem(hidePath, "post", "404", ["moderation.no_pending_reports"]);
    expectAdminErrors(hidePath, "post");
  });

  it("SPEC-0033:CT-01 Meus shows traz hidden e editar ou mudar a situação de show oculto declara 409 events.show_hidden", () => {
    const list = jsonResponse(myShowsPath, "get", "200");
    const hidden = property(list.items ?? {}, "hidden");
    expect(hidden, `GET ${myShowsPath} item sem hidden`).toBeDefined();
    expect(types(hidden ?? {})).toContain("boolean");
    expectProblem(showPath, "put", "409", ["events.show_hidden"]);
    expectProblem(showStatusPath, "post", "409", ["events.show_hidden"]);
  });

  it("SPEC-0033:CT-01 o cliente src/api/schema.d.ts foi regenerado com as rotas de denúncia", () => {
    const client = readFileSync(clientPath, "utf8");

    for (const path of [reportsPath, queuePath, keepPath, hidePath]) {
      expect(client).toContain(`"${path}"`);
    }
  });

  it("SPEC-0033:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});

/** Valores de enum de um schema, incluindo as alternativas de oneOf/anyOf/allOf (sem null). */
function enumValues(schema: Schema): unknown[] {
  const alternatives = [
    ...(schema.oneOf ?? []),
    ...(schema.anyOf ?? []),
    ...(schema.allOf ?? []),
  ]
    .map(resolve)
    .flatMap(enumValues);
  return [...(schema.enum ?? []), ...alternatives].filter(
    (value) => value !== null,
  );
}

/**
 * SPEC-0037 CT-01 (H33-04, H33-07) — no snapshot, `ReportShowRequest.reason` é o enum dos quatro motivos do §6 da
 * SPEC-0033 (não `string|null` livre), o 400 do `POST …/{showId}/keep` cita `security.csrf_invalid` uma vez só e
 * (Emenda 1, H33-06) o item de `GET /api/v1/admin/reports` tem `timeZone`, string obrigatório.
 */
describe("OpenAPI da fila de moderação sem as sobras da revisão da SPEC-0033", () => {
  it("SPEC-0037:CT-01 ReportShowRequest.reason é o enum dos quatro motivos", () => {
    const reason = property(requestSchema(reportsPath, "post"), "reason");
    expect(reason, "ReportShowRequest sem reason").toBeDefined();
    expect([...new Set(enumValues(reason ?? {}))].sort()).toEqual(
      [...reasons].sort(),
    );
  });

  it("SPEC-0037:CT-01 o item da fila tem timeZone, string obrigatório", () => {
    const item = jsonResponse(queuePath, "get", "200").items ?? {};
    const timeZone = property(item, "timeZone");
    expect(timeZone, `GET ${queuePath} item sem timeZone`).toBeDefined();
    expect(types(timeZone ?? {})).toEqual(["string"]);
    expect(resolve(item).required ?? []).toContain("timeZone");
  });

  it("SPEC-0037:CT-01 o 400 do keep cita security.csrf_invalid uma vez", () => {
    const description =
      operation(keepPath, "post").responses["400"]?.description ?? "";
    expect(
      description.split("code=security.csrf_invalid").length - 1,
      description,
    ).toBe(1);
  });
});
