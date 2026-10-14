#!/usr/bin/env bash
# Build do Subsolo (SPEC-0004) — comando `build` do docs/specs/sdd-config.yml.
# Executa os comandos de build do contrato, na ordem, e para no primeiro erro.
# O pnpm roda de dentro de web/ para respeitar o `packageManager` de web/package.json (o pnpm instalado
# troca para a versão fixada; com `-C web` a partir da raiz ele não faz essa troca).
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

echo '>>> dotnet build Subsolo.slnx -warnaserror -nologo'
dotnet build Subsolo.slnx -warnaserror -nologo
echo '>>> cd web'
cd web
echo '>>> pnpm install --frozen-lockfile'
pnpm install --frozen-lockfile
echo '>>> pnpm build'
pnpm build
