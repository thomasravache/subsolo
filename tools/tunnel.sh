#!/usr/bin/env bash
# Túnel para o ambiente local pelo celular (SPEC-0041): abre o túnel rápido do Cloudflare (sem conta e sem custo) para
# o Caddy do `aspire run` em https://localhost:8443 (SPEC-0040), com o cabeçalho Host e o nome TLS de localhost (o site
# e o certificado `tls internal` do Caddyfile), espera o endereço https://*.trycloudflare.com e mostra o comando do
# AppHost com esse endereço em --Subsolo:PublicUrl (o link mágico e o allowedDevOrigins do `next dev`).
#
#   tools/tunnel.sh          só o túnel: rode o AppHost (pela IDE, para depurar) com o argumento mostrado
#   tools/tunnel.sh --run    o túnel e o AppHost com o endereço (`dotnet run --project src/Subsolo.AppHost`)
#
# Fica em primeiro plano até receber SIGINT/SIGTERM (Ctrl+C) e então encerra o cloudflared (e o AppHost, com --run);
# se um dos dois sair sozinho, encerra o outro. O endereço do túnel é público enquanto ele está aberto. Exige o
# cloudflared (e, com --run, o .NET SDK, Node.js/pnpm e o Docker, como o `aspire run`).
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

readonly ORIGIN="https://localhost:8443"
readonly INSTALL_URL="https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/"
readonly ADDRESS_TIMEOUT=60

run_apphost=false
case "${1:-}" in
  "") ;;
  --run) run_apphost=true ;;
  -h | --help)
    sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
  *)
    echo "tools/tunnel.sh: opção desconhecida '$1' (use --run ou --help)" >&2
    exit 2
    ;;
esac

if ! command -v cloudflared >/dev/null 2>&1; then
  echo "tools/tunnel.sh: cloudflared não encontrado no PATH. Instale o cloudflared: $INSTALL_URL" >&2
  exit 1
fi

# Cada processo filho num grupo próprio (job control): o Ctrl+C do terminal chega só a este script, e o trap encerra
# cada grupo inteiro (o AppHost leva junto os processos que o `dotnet run` e o Aspire sobem).
set -m

log_file=$(mktemp "${TMPDIR:-/tmp}/subsolo-tunnel.XXXXXX")
cloudflared_pid=""
apphost_pid=""

# Manda o sinal ao grupo do processo e espera até $3 segundos; se ainda estiver no ar, SIGKILL.
stop_group() {
  local pid=$1 signal=$2 timeout=$3 i
  [[ -n "$pid" ]] || return 0
  kill "-$signal" -- "-$pid" 2>/dev/null || return 0
  for ((i = 0; i < timeout * 5; i++)); do
    kill -0 "$pid" 2>/dev/null || break
    sleep 0.2
  done
  kill -KILL -- "-$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
}

cleanup() {
  trap - INT TERM EXIT
  # O AppHost primeiro, com SIGINT (como o Ctrl+C do `aspire run`): ele derruba os contêineres antes de sair.
  stop_group "$apphost_pid" INT 60
  stop_group "$cloudflared_pid" TERM 10
  rm -f "$log_file"
}
trap 'cleanup; exit 0' INT TERM
trap cleanup EXIT

# O log do cloudflared (o endereço sai no stderr) vai para o arquivo temporário.
cloudflared tunnel --no-autoupdate --url "$ORIGIN" --no-tls-verify \
  --http-host-header localhost:8443 --origin-server-name localhost >"$log_file" 2>&1 &
cloudflared_pid=$!

url=""
for ((i = 0; i < ADDRESS_TIMEOUT * 5; i++)); do
  url=$(grep -oE 'https://[a-z0-9-]+\.trycloudflare\.com' "$log_file" | grep -v '^https://api\.' | head -n 1 || true)
  [[ -n "$url" ]] && break
  if ! kill -0 "$cloudflared_pid" 2>/dev/null; then
    echo "tools/tunnel.sh: o cloudflared saiu antes de criar o túnel:" >&2
    cat "$log_file" >&2
    exit 1
  fi
  sleep 0.2
done
if [[ -z "$url" ]]; then
  echo "tools/tunnel.sh: o cloudflared não mostrou o endereço do túnel em ${ADDRESS_TIMEOUT}s:" >&2
  cat "$log_file" >&2
  exit 1
fi

apphost_command="dotnet run --project src/Subsolo.AppHost -- --Subsolo:PublicUrl=$url"
echo ">>> Túnel aberto: $url (público enquanto este script estiver no ar; Ctrl+C encerra)"
echo ">>> AppHost com o endereço do túnel (ou o mesmo argumento --Subsolo:PublicUrl=$url na IDE):"
echo "    $apphost_command"

if $run_apphost; then
  echo ">>> $apphost_command"
  MSBUILDDISABLENODEREUSE=1 dotnet run --project src/Subsolo.AppHost -- "--Subsolo:PublicUrl=$url" &
  apphost_pid=$!
  # Primeiro plano até um dos dois sair (ou o sinal de parada); o trap de EXIT encerra o outro.
  wait -n "$cloudflared_pid" "$apphost_pid" || true
  echo "tools/tunnel.sh: o túnel ou o AppHost saiu; encerrando o outro." >&2
  exit 1
fi

# Primeiro plano até o sinal de parada (o trap encerra o cloudflared).
wait "$cloudflared_pid" || true
echo "tools/tunnel.sh: o cloudflared saiu:" >&2
cat "$log_file" >&2
exit 1
