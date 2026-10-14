#!/usr/bin/env bash
# Lint do Subsolo (SPEC-0004, ADR-0006) — comando `lint` do docs/specs/sdd-config.yml.
# Roda TODAS as verificações, mesmo depois de uma falha, e termina com exit ≠ 0 se alguma falhou,
# listando no fim quais falharam. Executar a partir de qualquer pasta: o script vai para a raiz do repositório.
set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

failed=()

step() {
  local name="$1"
  shift
  echo
  echo ">>> ${name}: $*"
  if "$@"; then
    echo "<<< ${name}: ok"
  else
    echo "<<< ${name}: FALHOU" >&2
    failed+=("$name")
    return 1
  fi
}

# .NET: formatação/estilo conforme .editorconfig e build com analisadores e avisos como erro.
step "dotnet-format" dotnet format Subsolo.slnx --verify-no-changes --verbosity minimal
step "dotnet-build" dotnet build Subsolo.slnx -warnaserror -nologo --verbosity quiet

# Front: dependências do lockfile e lint (eslint + prettier --check + tsc --noEmit).
# O pnpm roda de dentro de web/ para respeitar o `packageManager` de web/package.json (o pnpm instalado
# troca para a versão fixada; com `-C web` a partir da raiz ele não faz essa troca).
web() { (cd web && pnpm "$@"); }
if step "web-install" web install --frozen-lockfile --reporter=silent; then
  step "web-lint" web lint
else
  failed+=("web-lint (não executado: dependências não instaladas)")
fi

# SAST (ADR-0006): Semgrep CE com .semgrep.yml quando instalado localmente; no CI sempre roda (SPEC-0006).
if command -v semgrep >/dev/null 2>&1; then
  step "semgrep" semgrep scan --config p/csharp --config p/typescript --config p/secrets --config .semgrep.yml \
    --error --metrics off --quiet
else
  echo
  echo ">>> semgrep: não instalado localmente — pulado (roda no CI)"
fi

echo
if ((${#failed[@]})); then
  echo "lint: FALHOU — ${failed[*]}" >&2
  exit 1
fi
echo "lint: ok"
