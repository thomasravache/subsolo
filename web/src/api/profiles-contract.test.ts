// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0021 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica os endpoints de
 * perfis do §6 — `POST /api/v1/profiles/requests`, `GET /api/v1/profiles/search`, `POST /api/v1/profiles/{id}/claims`,
 * `GET /api/v1/me/profiles`, `GET /api/v1/admin/profile-requests`, `POST …/{id}/approve`, `POST …/{id}/reject` e
 * `POST /api/v1/admin/profiles` — com os corpos, status e códigos de erro; o cliente `src/api/schema.d.ts` foi
 * regenerado com eles e `(cd web && pnpm api:check)` passa. (A assinatura do `IProfilesApi` é conferida nos testes
 * unitários do módulo, CT-01 parte .NET.)
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
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

const requestsPath = "/api/v1/profiles/requests";
const searchPath = "/api/v1/profiles/search";
const claimsPath = "/api/v1/profiles/{id}/claims";
const myProfilesPath = "/api/v1/me/profiles";
const queuePath = "/api/v1/admin/profile-requests";
const approvePath = "/api/v1/admin/profile-requests/{id}/approve";
const rejectPath = "/api/v1/admin/profile-requests/{id}/reject";
const adminProfilesPath = "/api/v1/admin/profiles";

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

const profileBody = ["type", "name", "city", "styles", "links"];
const links = ["instagram", "bandcamp", "metalArchives"];
const created = ["requestId", "status"];

describe("OpenAPI de perfis fiel ao contrato §6", () => {
  it("SPEC-0021:CT-01 POST /api/v1/profiles/requests recebe o pedido com contato e responde 201, 400, 409", () => {
    const body = requestSchema(requestsPath, "post");
    expectKeys(body, [...profileBody, "contact"], `POST ${requestsPath} corpo`);
    expect(types(property(body, "styles") ?? {})).toContain("array");
    expectKeys(
      property(body, "links") ?? {},
      links,
      `POST ${requestsPath} links`,
    );
    expect(types(property(body, "contact") ?? {})).toContain("string");

    expectStrings(
      jsonResponse(requestsPath, "post", "201"),
      created,
      `POST ${requestsPath} 201`,
    );
    expectProblem(requestsPath, "post", "400", [
      "profiles.invalid_request",
      "security.csrf_invalid",
    ]);
    expectProblem(requestsPath, "post", "409", [
      "profiles.request_pending",
      "profiles.name_taken",
    ]);
    expectFanErrors(requestsPath, "post");
  });

  it("SPEC-0021:CT-01 GET /api/v1/profiles/search?q=&type= devolve [{id, type, name, city, styles}]", () => {
    const parameters = (
      operation(searchPath, "get") as Operation & {
        parameters?: { name: string; in: string }[];
      }
    ).parameters;
    expect(
      (parameters ?? []).filter((p) => p.in === "query").map((p) => p.name),
    ).toEqual(expect.arrayContaining(["q", "type"]));

    const list = jsonResponse(searchPath, "get", "200");
    expect(types(list)).toContain("array");
    expectKeys(
      list.items ?? {},
      ["id", "type", "name", "city", "styles"],
      `GET ${searchPath} item`,
    );
    expectProblem(searchPath, "get", "401", ["auth.required"]);
  });

  it("SPEC-0021:CT-01 POST /api/v1/profiles/{id}/claims recebe {proof} e responde 201, 404, 409", () => {
    const body = requestSchema(claimsPath, "post");
    expectStrings(body, ["proof"], `POST ${claimsPath} corpo`);

    expectStrings(
      jsonResponse(claimsPath, "post", "201"),
      created,
      `POST ${claimsPath} 201`,
    );
    expectProblem(claimsPath, "post", "404", ["profiles.not_found"]);
    expectProblem(claimsPath, "post", "409", [
      "profiles.already_owned",
      "profiles.request_pending",
    ]);
    expectFanErrors(claimsPath, "post");
  });

  it("SPEC-0021:CT-01 GET /api/v1/me/profiles devolve {profiles, requests} sem contato", () => {
    const mine = jsonResponse(myProfilesPath, "get", "200");
    expectKeys(mine, ["profiles", "requests"], `GET ${myProfilesPath} 200`);
    const profiles = property(mine, "profiles") ?? {};
    expect(types(profiles)).toContain("array");
    expectKeys(
      profiles.items ?? {},
      ["id", "type", "name", "slug", "city"],
      `GET ${myProfilesPath} profiles[]`,
    );
    const requests = property(mine, "requests") ?? {};
    expect(types(requests)).toContain("array");
    expectKeys(
      requests.items ?? {},
      ["requestId", "kind", "name", "type", "status", "reason", "createdAtUtc"],
      `GET ${myProfilesPath} requests[]`,
    );
    expect(JSON.stringify(mine)).not.toMatch(/contact|proof/i);
    expectProblem(myProfilesPath, "get", "401", ["auth.required"]);
  });

  it("SPEC-0021:CT-01 GET /api/v1/admin/profile-requests devolve a fila com contato e e-mail, com 401 e 403", () => {
    const list = jsonResponse(queuePath, "get", "200");
    expect(types(list)).toContain("array");
    expectKeys(
      list.items ?? {},
      [
        "requestId",
        "kind",
        "type",
        "name",
        "city",
        "styles",
        "links",
        "contactOrProof",
        "requesterEmail",
        "createdAtUtc",
        "profileId",
      ],
      `GET ${queuePath} item`,
    );
    expectAdminErrors(queuePath, "get");
  });

  it("SPEC-0021:CT-01 POST …/{id}/approve responde 200 {profileId}, 404 (pedido ou perfil) e 409 (decidido, com dono, nome tomado — Emenda 3)", () => {
    expectStrings(
      jsonResponse(approvePath, "post", "200"),
      ["profileId"],
      `POST ${approvePath} 200`,
    );
    expectProblem(approvePath, "post", "404", [
      "profiles.request_not_found",
      "profiles.not_found",
    ]);
    expectProblem(approvePath, "post", "409", [
      "profiles.request_decided",
      "profiles.already_owned",
      "profiles.name_taken",
    ]);
    expectAdminErrors(approvePath, "post");
  });

  it("SPEC-0021:CT-01 POST …/{id}/reject recebe {reason} e responde 200, 404 e 409", () => {
    expectStrings(
      requestSchema(rejectPath, "post"),
      ["reason"],
      `POST ${rejectPath} corpo`,
    );
    expect(Object.keys(operation(rejectPath, "post").responses)).toContain(
      "200",
    );
    expectProblem(rejectPath, "post", "404", ["profiles.request_not_found"]);
    expectProblem(rejectPath, "post", "409", ["profiles.request_decided"]);
    expectAdminErrors(rejectPath, "post");
  });

  it("SPEC-0021:CT-01 POST /api/v1/admin/profiles recebe o perfil sem contato e responde 201 {profileId}, 400, 409", () => {
    const body = requestSchema(adminProfilesPath, "post");
    expectKeys(body, profileBody, `POST ${adminProfilesPath} corpo`);

    expectStrings(
      jsonResponse(adminProfilesPath, "post", "201"),
      ["profileId"],
      `POST ${adminProfilesPath} 201`,
    );
    expectProblem(adminProfilesPath, "post", "400", [
      "profiles.invalid_request",
    ]);
    expectProblem(adminProfilesPath, "post", "409", ["profiles.name_taken"]);
    expectAdminErrors(adminProfilesPath, "post");
  });

  it("SPEC-0021:CT-01 o cliente src/api/schema.d.ts foi regenerado com as rotas de perfis", () => {
    const client = readFileSync(clientPath, "utf8");

    for (const path of [
      requestsPath,
      searchPath,
      claimsPath,
      myProfilesPath,
      queuePath,
      approvePath,
      rejectPath,
      adminProfilesPath,
    ]) {
      expect(client).toContain(`"${path}"`);
    }
  });

  it("SPEC-0021:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
