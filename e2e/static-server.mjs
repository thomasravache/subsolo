// SPEC-0005 — servidor HTTP mínimo (só node:http) das páginas estáticas do harness E2E (e2e/static).
// Usado pelo `webServer` do playwright.config.ts; as jornadas reais rodam contra a aplicação completa.
import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { createServer } from "node:http";
import { extname, join, normalize, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(fileURLToPath(new URL("./static", import.meta.url)));
const port = Number(process.env.E2E_STATIC_PORT ?? 4317);
const types = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".svg": "image/svg+xml",
};

createServer(async (request, response) => {
  let path;
  try {
    path = decodeURIComponent(
      new URL(request.url ?? "/", "http://localhost").pathname,
    );
  } catch {
    // URL malformada (ex.: /%E0%A4%A): 400 em vez de derrubar o servidor (SPEC-0013).
    response.writeHead(400, { "content-type": "text/plain; charset=utf-8" });
    response.end("requisição inválida");
    return;
  }
  const file = normalize(
    join(root, path.endsWith("/") ? `${path}index.html` : path),
  );
  if (file !== root && !file.startsWith(root + sep)) {
    response.writeHead(403).end();
    return;
  }
  try {
    if (!(await stat(file)).isFile()) throw new Error("não é arquivo");
    response.writeHead(200, {
      "content-type": types[extname(file)] ?? "application/octet-stream",
    });
    createReadStream(file).pipe(response);
  } catch {
    response.writeHead(404, { "content-type": "text/plain; charset=utf-8" });
    response.end("não encontrado");
  }
}).listen(port, "127.0.0.1", () => {
  console.log(`e2e/static em http://127.0.0.1:${port}`);
});
