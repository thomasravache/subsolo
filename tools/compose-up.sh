#!/usr/bin/env bash
# Compose local do Subsolo (SPEC-0008, ADR-0004): a aplicação inteira pelo Docker Compose gerado pelo Aspire, com o
# Caddy na frente em http://localhost:8080 (/api/* e /health → api; o resto → web).
#
#   tools/compose-up.sh          gera o Compose (`aspire publish -o artifacts/compose`), monta as imagens da API e do
#                                web com a tag local-<sha> e sobe tudo
#   tools/compose-up.sh <tag>    sobe imagens já montadas com essa tag, sem montar de novo (rollback local)
#
# Fica em primeiro plano mostrando os logs até receber SIGINT/SIGTERM (Ctrl+C, ou o Playwright ao terminar o E2E) e
# então derruba o projeto Compose com os volumes (`docker compose down --volumes`): cada subida começa com um banco
# novo, migrado e com o seed da cidade de exemplo (a API roda em Development só aqui; no deploy, Production).
# Exige Docker (com Compose v2), o .NET SDK e a Aspire CLI.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
root="$PWD"

readonly APPHOST="src/Subsolo.AppHost/Subsolo.AppHost.csproj"
readonly OUT="artifacts/compose"
readonly PROJECT="subsolo-local"
readonly APP_URL="http://localhost:8080"

for tool in docker dotnet aspire; do
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

compose() {
  docker compose --project-name "$PROJECT" --file "$OUT/docker-compose.yaml" --env-file "$env_file" "$@"
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

for _ in $(seq 1 120); do
  if curl -fsS -o /dev/null "$APP_URL/health" 2>/dev/null; then
    echo "compose-up: aplicação no ar em $APP_URL (Ctrl+C para derrubar)"
    break
  fi
  sleep 1
done

# Primeiro plano: os logs até o sinal de parada (o trap derruba o Compose).
compose logs --follow --no-color &
logs_pid=$!
wait "$logs_pid"
