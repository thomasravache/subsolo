#!/usr/bin/env bash
# Compose local do Subsolo (SPEC-0008, ADR-0004): a aplicação inteira pelo Docker Compose gerado pelo Aspire, com o
# Caddy na frente em https://localhost:8443 (/api/* e /health → api; o resto → web). SPEC-0010: o certificado é o
# da CA interna do Caddy (`tls internal`, não confiável fora da máquina); http://localhost:8080 só redireciona (308)
# para o HTTPS.
#
#   tools/compose-up.sh          gera o Compose (`aspire publish -o artifacts/compose`), monta as imagens da API e do
#                                web com a tag local-<sha> e sobe tudo
#   tools/compose-up.sh <tag>    sobe imagens já montadas com essa tag, sem montar de novo (rollback local)
#
# Fica em primeiro plano mostrando os logs até receber SIGINT/SIGTERM (Ctrl+C, ou o Playwright ao terminar o E2E) e
# então derruba o projeto Compose com os volumes (`docker compose down --volumes`): cada subida começa com um banco
# novo, migrado e com o seed da cidade de exemplo (a API roda em Development só aqui; no deploy, Production).
# Exige Docker (com Compose v2), o .NET SDK e a Aspire CLI.
#
# SPEC-0009 — só neste Compose local, um override publica o Seq em 127.0.0.1:${SEQ_PORT:-5341} (API e interface),
# para consultar logs e traces (o E2E-01 usa SEQ_URL); o artefato publicado continua sem porta do Seq.
#
# SPEC-0014 (Emenda v3) — só neste Compose local (a pilha do E2E), o rate limit `default` da API sobe para
# ${DEFAULT_RATE_LIMIT:-10000} por minuto: a suíte inteira do Playwright sai de um IP só e estouraria os 100/min de
# produção. O artefato publicado e os padrões da API não mudam.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
root="$PWD"

readonly APPHOST="src/Subsolo.AppHost/Subsolo.AppHost.csproj"
readonly OUT="artifacts/compose"
readonly PROJECT="subsolo-local"
readonly APP_URL="https://localhost:8443"
readonly SEQ_PORT="${SEQ_PORT:-5341}"
if ! [[ "$SEQ_PORT" =~ ^[0-9]+$ ]] || ((10#$SEQ_PORT < 1 || 10#$SEQ_PORT > 65535)); then
  echo "compose-up: SEQ_PORT inválido ('$SEQ_PORT'): use um número de porta entre 1 e 65535" >&2
  exit 2
fi
readonly DEFAULT_RATE_LIMIT="${DEFAULT_RATE_LIMIT:-10000}"
if ! [[ "$DEFAULT_RATE_LIMIT" =~ ^[0-9]+$ ]] || ((10#$DEFAULT_RATE_LIMIT < 1)); then
  echo "compose-up: DEFAULT_RATE_LIMIT inválido ('$DEFAULT_RATE_LIMIT'): use um número inteiro positivo" >&2
  exit 2
fi

for tool in docker dotnet aspire curl; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "compose-up: '$tool' não encontrado no PATH" >&2
    exit 2
  fi
done

echo ">>> aspire publish -o $OUT"
aspire publish --apphost "$APPHOST" -o "$OUT" --non-interactive --nologo

if [[ $# -gt 0 ]]; then
  tag="$1"
  for image in "subsolo-api:$tag" "subsolo-web:$tag"; do
    if ! docker image inspect "$image" >/dev/null 2>&1; then
      echo "compose-up: imagem $image não encontrada (monte-a rodando o script sem tag)" >&2
      exit 2
    fi
  done
else
  tag="local-$(git rev-parse --short HEAD)"
  case "$(uname -m)" in
    aarch64 | arm64) container_rid="linux-arm64" ;;
    *) container_rid="linux-x64" ;;
  esac

  # Contêiner do SDK .NET, como o Aspire monta projetos .NET. A aplicação é publicada sem RID (portável), para o
  # restore continuar batendo com os packages.lock.json; o RID só escolhe a arquitetura da imagem base.
  echo ">>> imagem subsolo-api:$tag"
  dotnet publish src/Subsolo.Api/Subsolo.Api.csproj -c Release -nologo \
    -t:PublishContainer -p:ContainerRepository=subsolo-api -p:ContainerImageTag="$tag" \
    -p:ContainerRuntimeIdentifier="$container_rid"

  # Dockerfile gerado pelo `aspire publish` (Node da web/.nvmrc); o contexto segue a web/.dockerignore.
  echo ">>> imagem subsolo-web:$tag"
  docker build -f "$OUT/web.Dockerfile" -t "subsolo-web:$tag" web
fi

# Valores do .env gerado pelo publish (o Compose local não usa o .env de nenhum ambiente real).
env_file="$OUT/.env.local"
cat >"$env_file" <<ENV
API_ENVIRONMENT=Development
API_IMAGE=subsolo-api:$tag
API_PORT=8080
WEB_IMAGE=subsolo-web:$tag
CADDY_BINDMOUNT_0=$root/infra/caddy/Caddyfile
POSTGRES_PASSWORD=$(od -An -N16 -tx1 /dev/urandom | tr -d ' \n')
ENV

# Override só local: o Seq acessível apenas pelo localhost (SPEC-0009) e o rate limit `default` de teste
# (SPEC-0014, Emenda v3; a janela continua de 1 minuto).
override_file="$OUT/docker-compose.local.yaml"
cat >"$override_file" <<YAML
services:
  seq:
    ports:
      - "127.0.0.1:$SEQ_PORT:80"
  api:
    environment:
      Security__RateLimiting__default__PermitLimit: "$((10#$DEFAULT_RATE_LIMIT))"
      Security__RateLimiting__default__Window: "00:01:00"
YAML

compose() {
  docker compose --project-name "$PROJECT" --file "$OUT/docker-compose.yaml" --file "$override_file" \
    --env-file "$env_file" "$@"
}

stopped=0
logs_pid=""
down() {
  if ((stopped == 0)); then
    stopped=1
    [[ -n "$logs_pid" ]] && kill "$logs_pid" 2>/dev/null || true
    echo ">>> docker compose down --volumes"
    compose down --volumes --remove-orphans --timeout 10 || true
  fi
}
trap 'down; exit 0' INT TERM
trap down EXIT

# Restos de uma execução interrompida à força.
compose down --volumes --remove-orphans --timeout 10 >/dev/null 2>&1 || true

echo ">>> docker compose up ($PROJECT, imagens :$tag)"
compose up --detach

# Pronta = a API (/health) e o web pelo Caddy: `/` com 2xx ou 404, não o 502 do web ainda subindo.
healthy=0
for _ in $(seq 1 120); do
  # -k: o certificado local (`tls internal`) não é confiável fora do contêiner do Caddy.
  if curl -fsSk -o /dev/null "$APP_URL/health" 2>/dev/null; then
    web_status="$(curl -sSk -o /dev/null -w '%{http_code}' "$APP_URL/" 2>/dev/null || true)"
    if [[ "$web_status" == 2?? || "$web_status" == 404 ]]; then
      healthy=1
      break
    fi
  fi
  sleep 1
done
if ((healthy == 0)); then
  echo "compose-up: $APP_URL/health ou o web ($APP_URL/) não responderam em 120 s; logs da pilha:" >&2
  compose ps --all >&2 || true
  compose logs --no-color --tail 200 >&2 || true
  exit 1
fi
echo "compose-up: aplicação no ar em $APP_URL; Seq em http://127.0.0.1:$SEQ_PORT (Ctrl+C para derrubar)"

# Primeiro plano: os logs até o sinal de parada (o trap derruba o Compose).
compose logs --follow --no-color &
logs_pid=$!
wait "$logs_pid"
