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
#
# SPEC-0016 — também só neste Compose local: o limite `auth` sobe para ${AUTH_RATE_LIMIT:-10000} por minuto (as jornadas
# de entrada pedem vários links do mesmo IP); o Mailpit publica a API HTTP em 127.0.0.1:${MAILPIT_PORT:-8025} (o E2E lê
# o link mágico por MAILPIT_URL); a API fala com o Mailpit sem TLS (SMTP_SECURITY=None, SPEC-0018 Emenda 2) e monta o
# link com App__PublicUrl=https://localhost:8443; o contêiner do web recebe duas variáveis sentinela
# (SUBSOLO_E2E_SENTINEL e SUBSOLO_E2E_SECRET_KEY, valores de E2E_PLAIN_SENTINEL e E2E_SECRET_SENTINEL) para o E2E-04
# provar que nenhuma variável de ambiente chega ao navegador.
#
# SPEC-0024 (Emenda v2, a) — o web recebe SUBSOLO_PUBLIC_URL com o mesmo valor do App__PublicUrl da API (o endereço da
# pilha): as URLs absolutas do Open Graph e o canonical da página do show. Só do servidor (sem NEXT_PUBLIC_).
#
# SPEC-0021 — também só neste Compose local: o limite `report` (pedido e reivindicação de perfil) sobe para
# ${REPORT_RATE_LIMIT:-10000} por hora (a jornada de perfis repete os pedidos do mesmo IP a cada execução), e o limite de
# links mágicos por e-mail sobe para ${MAGIC_LINK_EMAIL_LIMIT:-50} a cada 15 min (as jornadas de perfis e do admin entram
# várias vezes com `admin@subsolo.test` na mesma subida).
#
# SPEC-0028 — também só neste Compose local: o web recebe a chave Pix de teste e o recebedor (Support__PixKey,
# Support__PixName e Support__PixCity, de E2E_PIX_KEY, E2E_PIX_NAME e E2E_PIX_CITY, com os padrões de
# e2e/tests/site/environment.ts) e o e-mail de contato de teste (Site__ContactEmail, de E2E_CONTACT_EMAIL). Só do servidor
# (sem NEXT_PUBLIC_); os valores reais entram no go-live.
#
# SPEC-0030 — também só neste Compose local: a API recebe a chave do token de descadastro dos e-mails de aviso
# (Notifications__Unsubscribe__SigningKey, de E2E_UNSUBSCRIBE_KEY, com o padrão de e2e/tests/avisos/avisos.ts: o E2E gera
# links válidos com ela) e um par de chaves VAPID de teste gerado a cada subida com o openssl
# (Notifications__WebPush__PublicKey/PrivateKey/Subject, pelo .env local). Nenhuma chave real; as de produção entram no
# go-live.
#
# SPEC-0017 — também só neste Compose local: `admin@subsolo.test` tem o papel admin (Users__AdminEmails__0), para o E2E
# entrar no painel do admin pelo link mágico. O artefato publicado e os padrões da API não mudam.
#
# SPEC-0020 — também só neste Compose local: o provedor OpenID Connect falso (mock-oauth2-server, login interativo) faz o
# papel do Google. Ele roda na rede do contêiner da API (`network_mode: service:api`) e escuta em ${OIDC_PORT:-8090},
# publicado em 127.0.0.1 com o mesmo número: o emissor `http://127.0.0.1:${OIDC_PORT:-8090}/default` é o mesmo para o
# navegador (porta publicada) e para a API (o próprio loopback do contêiner). A API recebe Users__Google__Authority,
# Users__Google__ClientId e um segredo de cliente aleatório por subida (Users__Google__ClientSecret, pelo .env local).
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
readonly OIDC_PORT="${OIDC_PORT:-8090}"
if ! [[ "$OIDC_PORT" =~ ^[0-9]+$ ]] || ((10#$OIDC_PORT < 1 || 10#$OIDC_PORT > 65535)); then
  echo "compose-up: OIDC_PORT inválido ('$OIDC_PORT'): use um número de porta entre 1 e 65535" >&2
  exit 2
fi
readonly OIDC_IMAGE="ghcr.io/navikt/mock-oauth2-server:6.0.4"
# 127.0.0.1, não `localhost`: o WebKit grava o HSTS do Caddy para `localhost` (qualquer porta) e trocaria o http:// do
# provedor por https://; o HSTS não vale para endereço IP.
readonly OIDC_ISSUER="http://127.0.0.1:$((10#$OIDC_PORT))/default"
readonly MAILPIT_PORT="${MAILPIT_PORT:-8025}"
if ! [[ "$MAILPIT_PORT" =~ ^[0-9]+$ ]] || ((10#$MAILPIT_PORT < 1 || 10#$MAILPIT_PORT > 65535)); then
  echo "compose-up: MAILPIT_PORT inválido ('$MAILPIT_PORT'): use um número de porta entre 1 e 65535" >&2
  exit 2
fi
# Limite de rate limit: inteiro de 1 a 2147483647 (o PermitLimit da API é um int de 32 bits; SPEC-0016, §7.6 h). O
# tamanho é conferido antes da conta, para um número enorme não estourar a aritmética do bash.
check_rate_limit() {
  local name=$1 value=$2
  if ! [[ "$value" =~ ^[0-9]{1,10}$ ]] || ((10#$value < 1 || 10#$value > 2147483647)); then
    echo "compose-up: $name inválido ('$value'): use um número inteiro de 1 a 2147483647" >&2
    exit 2
  fi
}
readonly AUTH_RATE_LIMIT="${AUTH_RATE_LIMIT:-10000}"
check_rate_limit AUTH_RATE_LIMIT "$AUTH_RATE_LIMIT"
readonly E2E_PLAIN_SENTINEL="${E2E_PLAIN_SENTINEL:-subsolo-sentinela-comum-e2e04}"
readonly E2E_SECRET_SENTINEL="${E2E_SECRET_SENTINEL:-subsolo-sentinela-segredo-e2e04}"
for sentinel in "$E2E_PLAIN_SENTINEL" "$E2E_SECRET_SENTINEL"; do
  if ! [[ "$sentinel" =~ ^[A-Za-z0-9._-]+$ ]]; then
    echo "compose-up: sentinela inválida ('$sentinel'): use só letras, números, ponto, hífen e sublinhado" >&2
    exit 2
  fi
done
readonly E2E_PIX_KEY="${E2E_PIX_KEY:-chave-teste-e2e@subsolo.test}"
readonly E2E_PIX_NAME="${E2E_PIX_NAME:-Subsolo}"
readonly E2E_PIX_CITY="${E2E_PIX_CITY:-Curitiba}"
readonly E2E_CONTACT_EMAIL="${E2E_CONTACT_EMAIL:-contato-teste@subsolo.test}"
for setting in "$E2E_PIX_KEY" "$E2E_PIX_NAME" "$E2E_PIX_CITY" "$E2E_CONTACT_EMAIL"; do
  if ! [[ "$setting" =~ ^[A-Za-z0-9@.+\ _-]+$ ]]; then
    echo "compose-up: configuração de teste do Pix ou do contato inválida ('$setting'): use letras, números, espaço, @ . + _ -" >&2
    exit 2
  fi
done
readonly E2E_UNSUBSCRIBE_KEY="${E2E_UNSUBSCRIBE_KEY:-chave-descadastro-e2e-subsolo-local}"
if ! [[ "$E2E_UNSUBSCRIBE_KEY" =~ ^[A-Za-z0-9._-]{16,}$ ]]; then
  echo "compose-up: E2E_UNSUBSCRIBE_KEY inválida: use 16 ou mais letras, números, ponto, hífen ou sublinhado" >&2
  exit 2
fi
readonly DEFAULT_RATE_LIMIT="${DEFAULT_RATE_LIMIT:-10000}"
check_rate_limit DEFAULT_RATE_LIMIT "$DEFAULT_RATE_LIMIT"
readonly REPORT_RATE_LIMIT="${REPORT_RATE_LIMIT:-10000}"
check_rate_limit REPORT_RATE_LIMIT "$REPORT_RATE_LIMIT"
readonly MAGIC_LINK_EMAIL_LIMIT="${MAGIC_LINK_EMAIL_LIMIT:-50}"
check_rate_limit MAGIC_LINK_EMAIL_LIMIT "$MAGIC_LINK_EMAIL_LIMIT"

for tool in docker dotnet aspire curl openssl; do
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

# SPEC-0030 — par VAPID (P-256) de teste: a chave SEC1 em DER tem a privada (32 bytes) a partir do 8º byte e a pública
# não comprimida (65 bytes) no fim; as duas em base64url sem preenchimento.
base64url() { base64 | tr -d '\n=' | tr '+/' '-_'; }
vapid_der="$(mktemp)"
openssl ecparam -name prime256v1 -genkey -noout -outform DER -out "$vapid_der"
VAPID_PRIVATE_KEY="$(tail -c +8 "$vapid_der" | head -c 32 | base64url)"
VAPID_PUBLIC_KEY="$(tail -c 65 "$vapid_der" | base64url)"
rm -f "$vapid_der"

# Valores do .env gerado pelo publish (o Compose local não usa o .env de nenhum ambiente real).
env_file="$OUT/.env.local"
cat >"$env_file" <<ENV
API_ENVIRONMENT=Development
API_IMAGE=subsolo-api:$tag
API_PORT=8080
WEB_IMAGE=subsolo-web:$tag
CADDY_BINDMOUNT_0=$root/infra/caddy/Caddyfile
POSTGRES_PASSWORD=$(od -An -N16 -tx1 /dev/urandom | tr -d ' \n')
SMTP_SECURITY=None
GOOGLE_CLIENT_SECRET=$(od -An -N16 -tx1 /dev/urandom | tr -d ' \n')
VAPID_PUBLIC_KEY=$VAPID_PUBLIC_KEY
VAPID_PRIVATE_KEY=$VAPID_PRIVATE_KEY
UNSUBSCRIBE_SIGNING_KEY=$E2E_UNSUBSCRIBE_KEY
ENV

# Override só local: o Seq acessível apenas pelo localhost (SPEC-0009), o rate limit `default` de teste
# (SPEC-0014, Emenda v3; a janela continua de 1 minuto) e, da SPEC-0016, o limite `auth` de teste, o Mailpit no
# localhost, o endereço público do link mágico e as sentinelas do web; da SPEC-0020, o provedor OIDC falso.
override_file="$OUT/docker-compose.local.yaml"
cat >"$override_file" <<YAML
services:
  seq:
    ports:
      - "127.0.0.1:$SEQ_PORT:80"
  mailpit:
    ports:
      - "127.0.0.1:$MAILPIT_PORT:8025"
  api:
    environment:
      Security__RateLimiting__default__PermitLimit: "$((10#$DEFAULT_RATE_LIMIT))"
      Security__RateLimiting__default__Window: "00:01:00"
      Security__RateLimiting__auth__PermitLimit: "$((10#$AUTH_RATE_LIMIT))"
      Security__RateLimiting__auth__Window: "00:01:00"
      Security__RateLimiting__report__PermitLimit: "$((10#$REPORT_RATE_LIMIT))"
      Security__RateLimiting__report__Window: "01:00:00"
      App__PublicUrl: "$APP_URL"
      Users__AdminEmails__0: "admin@subsolo.test"
      Users__MagicLinkEmailLimit: "$((10#$MAGIC_LINK_EMAIL_LIMIT))"
      Users__Google__Authority: "$OIDC_ISSUER"
      Users__Google__ClientId: "subsolo-local"
      Users__Google__ClientSecret: "\${GOOGLE_CLIENT_SECRET}"
      Notifications__WebPush__PublicKey: "\${VAPID_PUBLIC_KEY}"
      Notifications__WebPush__PrivateKey: "\${VAPID_PRIVATE_KEY}"
      Notifications__WebPush__Subject: "mailto:nao-responda@subsolo.test"
      Notifications__Unsubscribe__SigningKey: "\${UNSUBSCRIBE_SIGNING_KEY}"
    ports:
      - "127.0.0.1:$((10#$OIDC_PORT)):$((10#$OIDC_PORT))"
  oidc:
    image: "$OIDC_IMAGE"
    network_mode: "service:api"
    depends_on:
      - api
    environment:
      SERVER_PORT: "$((10#$OIDC_PORT))"
      JAVA_TOOL_OPTIONS: "-Xmx128m"
      JSON_CONFIG: '{"interactiveLogin":true,"httpServer":"NettyWrapper"}'
  web:
    environment:
      SUBSOLO_PUBLIC_URL: "$APP_URL"
      SUBSOLO_E2E_SENTINEL: "$E2E_PLAIN_SENTINEL"
      SUBSOLO_E2E_SECRET_KEY: "$E2E_SECRET_SENTINEL"
      Support__PixKey: "$E2E_PIX_KEY"
      Support__PixName: "$E2E_PIX_NAME"
      Support__PixCity: "$E2E_PIX_CITY"
      Site__ContactEmail: "$E2E_CONTACT_EMAIL"
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

# Pronta = a API (/health) e o web pelo Caddy: `/` com 2xx ou 404, não o 502 do web ainda subindo; e o provedor OIDC
# falso (SPEC-0020) respondendo /isalive.
healthy=0
for _ in $(seq 1 120); do
  # -k: o certificado local (`tls internal`) não é confiável fora do contêiner do Caddy.
  if curl -fsSk -o /dev/null "$APP_URL/health" 2>/dev/null; then
    web_status="$(curl -sSk -o /dev/null -w '%{http_code}' "$APP_URL/" 2>/dev/null || true)"
    if [[ "$web_status" == 2?? || "$web_status" == 404 ]] \
      && curl -fsS -o /dev/null "http://127.0.0.1:$((10#$OIDC_PORT))/isalive" 2>/dev/null; then
      healthy=1
      break
    fi
  fi
  sleep 1
done
if ((healthy == 0)); then
  echo "compose-up: $APP_URL/health, o web ($APP_URL/) ou o provedor OIDC falso não responderam em 120 s; logs da pilha:" >&2
  compose ps --all >&2 || true
  compose logs --no-color --tail 200 >&2 || true
  exit 1
fi
echo "compose-up: aplicação no ar em $APP_URL; Seq em http://127.0.0.1:$SEQ_PORT; Mailpit em http://127.0.0.1:$MAILPIT_PORT; provedor OIDC falso em $OIDC_ISSUER (Ctrl+C para derrubar)"

# Primeiro plano: os logs até o sinal de parada (o trap derruba o Compose).
compose logs --follow --no-color &
logs_pid=$!
wait "$logs_pid"
