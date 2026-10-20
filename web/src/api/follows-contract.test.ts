// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0027 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica os endpoints de
 * seguir do §6:
 * - `PUT`/`DELETE /api/v1/me/follows/cities/{slug}` e `PUT`/`DELETE /api/v1/me/follows/profiles/{profileId}` (uuid):
 *   204 sem corpo; o `PUT` declara 404 `engagement.target_not_found`; todos declaram 401 `auth.required`, 400
 *   `security.csrf_invalid` e 429 `security.rate_limited`;
 * - `GET /api/v1/me/follows`: 200 `{cities: [{slug, name}], profiles: [{id, type, name, slug}]}`, 401 e 429;
 * - `GET /api/v1/profiles/{profileId}/followers` e `GET /api/v1/cities/{slug}/followers`: públicos, 200 `{count}`
 *   (inteiro, só o número) e 404 `engagement.target_not_found`;
 * - o cliente `src/api/schema.d.ts` foi regenerado com as rotas e `(cd web && pnpm api:check)` passa. (A assinatura do
 *   `IEngagementApi` é conferida nos testes unitários do módulo, CT-01 parte .NET.)
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  format?: string;
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

type Parameter = { name: string; in: string; schema?: Schema };

type Operation = {
  security?: unknown[];
  parameters?: Parameter[];
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

const cityFollowPath = "/api/v1/me/follows/cities/{slug}";
const profileFollowPath = "/api/v1/me/follows/profiles/{profileId}";
const myFollowsPath = "/api/v1/me/follows";
const profileFollowersPath = "/api/v1/profiles/{profileId}/followers";
const cityFollowersPath = "/api/v1/cities/{slug}/followers";

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

/** Os erros comuns das rotas de fã (§6): 401, 429 e, nas mutações, o 400 do anti-forgery. */
function expectFanErrors(path: string, method: string) {
  expectProblem(path, method, "401", ["auth.required"]);
  expectProblem(path, method, "429", ["security.rate_limited"]);
  if (method !== "get") {
    expectProblem(path, method, "400", ["security.csrf_invalid"]);
  }
}

function pathParameters(path: string, method: string): Parameter[] {
  return (operation(path, method).parameters ?? []).filter(
    (parameter) => parameter.in === "path",
  );
}

/** Os erros comuns das mutações de fã (§6): 401, 429 e o 400 do anti-forgery. */
function expectMutationErrors(path: string, method: string) {
  expectFanErrors(path, method);
  expectProblem(path, method, "400", ["security.csrf_invalid"]);
}

/** 204 sem corpo. */
function expectNoContent(path: string, method: string) {
  const response = operation(path, method).responses["204"];
  expect(
    response,
    `${method.toUpperCase()} ${path} 204 não declarado`,
  ).toBeDefined();
  expect(
    Object.keys(response?.content ?? {}),
    `${method.toUpperCase()} ${path} 204 com corpo`,
  ).toEqual([]);
}

/** `{count}` com um inteiro, e nada mais. */
function expectCount(path: string) {
  const body = jsonResponse(path, "get", "200");
  expectKeys(body, ["count"], `GET ${path} 200`);
  expect(types(property(body, "count") ?? {}), `GET ${path} count`).toContain(
    "integer",
  );
}

describe("OpenAPI de seguir fiel ao contrato §6", () => {
  it.each([
    ["put", cityFollowPath],
    ["delete", cityFollowPath],
    ["put", profileFollowPath],
    ["delete", profileFollowPath],
  ])(
    "SPEC-0027:CT-01 %s %s responde 204 sem corpo, com 401, 400 do anti-forgery e 429",
    (method, path) => {
      expectNoContent(path, method);
      expectMutationErrors(path, method);
    },
  );

  it.each([
    ["put", cityFollowPath],
    ["put", profileFollowPath],
  ])(
    "SPEC-0027:CT-01 %s %s declara 404 com engagement.target_not_found",
    (method, path) => {
      expectProblem(path, method, "404", ["engagement.target_not_found"]);
    },
  );

  it("SPEC-0027:CT-01 a cidade vai pelo slug e o perfil pelo id (uuid) no caminho", () => {
    for (const method of ["put", "delete"]) {
      expect(
        pathParameters(cityFollowPath, method).map(
          (parameter) => parameter.name,
        ),
      ).toEqual(["slug"]);
      const profile = pathParameters(profileFollowPath, method);
      expect(profile.map((parameter) => parameter.name)).toEqual(["profileId"]);
      expect(resolve(profile[0]?.schema).format).toBe("uuid");
    }
  });

  it("SPEC-0027:CT-01 GET /api/v1/me/follows devolve {cities: [{slug, name}], profiles: [{id, type, name, slug}]}", () => {
    const body = jsonResponse(myFollowsPath, "get", "200");
    expectKeys(body, ["cities", "profiles"], "GET /me/follows 200");
    const cities = property(body, "cities") ?? {};
    expect(types(cities)).toContain("array");
    expectStrings(cities.items ?? {}, ["slug", "name"], "cities[]");
    const profiles = property(body, "profiles") ?? {};
    expect(types(profiles)).toContain("array");
    expectStrings(
      profiles.items ?? {},
      ["id", "type", "name", "slug"],
      "profiles[]",
    );
    expectFanErrors(myFollowsPath, "get");
  });

  it.each([profileFollowersPath, cityFollowersPath])(
    "SPEC-0027:CT-01 GET %s é público e devolve só {count}, com 404 engagement.target_not_found",
    (path) => {
      expectCount(path);
      expectProblem(path, "get", "404", ["engagement.target_not_found"]);
      expect(
        operation(path, "get").responses["401"],
        `GET ${path} é público (sem 401)`,
      ).toBeUndefined();
    },
  );

  it("SPEC-0027:CT-01 a contagem do perfil recebe o id (uuid) e a da cidade o slug", () => {
    const profile = pathParameters(profileFollowersPath, "get");
    expect(profile.map((parameter) => parameter.name)).toEqual(["profileId"]);
    expect(resolve(profile[0]?.schema).format).toBe("uuid");
    expect(
      pathParameters(cityFollowersPath, "get").map(
        (parameter) => parameter.name,
      ),
    ).toEqual(["slug"]);
  });

  it("SPEC-0027:CT-01 o cliente src/api/schema.d.ts foi regenerado com as rotas de seguir", () => {
    const client = readFileSync(clientPath, "utf8");
    for (const path of [
      cityFollowPath,
      profileFollowPath,
      myFollowsPath,
      profileFollowersPath,
      cityFollowersPath,
    ]) {
      expect(client, `${path} ausente no cliente`).toContain(`"${path}": {`);
    }
  });

  it("SPEC-0027:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
