#!/usr/bin/env bash
# Build do Subsolo (SPEC-0004) — comando `build` do docs/specs/sdd-config.yml.
# Executa os comandos de build do contrato, na ordem, e para no primeiro erro.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

echo '>>> dotnet build Subsolo.slnx -warnaserror'
dotnet build Subsolo.slnx -warnaserror -nologo
echo '>>> pnpm -C web install --frozen-lockfile'
pnpm -C web install --frozen-lockfile
echo '>>> pnpm -C web build'
pnpm -C web build
