#!/usr/bin/env bash
# Cobertura do Subsolo (SPEC-0005, ADR-0005) — comando `coverage` do docs/specs/sdd-config.yml.
#
# 1. Compila a solution em Release (saída separada das execuções Debug dos outros comandos de teste).
# 2. Roda cada projeto de teste da solution sob o coverlet (coverlet.console, MIT), com o mesmo filtro do
#    comando `test`, gerando um Cobertura por projeto.
# 3. Junta tudo com o ReportGenerator (Apache-2.0) em coverage/ (Cobertura.xml, Summary.txt e index.html).
# 4. Mostra a porcentagem das linhas ALTERADAS em relação a origin/main (merge-base), por arquivo e no total,
#    e grava o mesmo relatório em coverage/changed-lines.md.
#
# As ferramentas são executadas com `dotnet tool exec` (sem instalação nem manifesto), nas versões fixadas abaixo.
# O script só reporta: o mínimo (coverage_min_changed_lines do sdd-config) é conferido pelo gate G2.
# Variáveis: COVERAGE_BASE (padrão origin/main), COVERAGE_FILTER (padrão: o filtro do comando `test`).
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
root="$PWD"

readonly COVERLET_VERSION="10.1.0"
readonly REPORTGENERATOR_VERSION="5.5.11"
readonly CONFIGURATION="Release"
base_ref="${COVERAGE_BASE:-origin/main}"
filter="${COVERAGE_FILTER:-Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract}"
out="$root/coverage"

# Base conferida antes de compilar e rodar a suíte: sem ela o relatório das linhas alteradas não sai (SPEC-0013).
if ! base="$(git merge-base "$base_ref" HEAD 2>/dev/null)"; then
  echo "coverage: base $base_ref não encontrada (git fetch origin main?)" >&2
  exit 2
fi

solution="$(find . -maxdepth 1 -name '*.slnx' -print -quit)"
if [[ -z "$solution" ]]; then
  echo "coverage: nenhuma solution (*.slnx) na raiz" >&2
  exit 2
fi

rm -rf "$out"
mkdir -p "$out/raw"

echo ">>> dotnet build $solution -c $CONFIGURATION"
dotnet build "$solution" -c "$CONFIGURATION" -nologo -warnaserror

# Leitura em laço (bash 3.2 do macOS não tem mapfile); o array vazio é expandido com guarda por causa do `set -u`.
projects=()
while IFS= read -r project; do
  projects+=("$project")
done < <(dotnet sln "$solution" list | grep -E '\.csproj$')

ran=0
for project in ${projects[@]+"${projects[@]}"}; do
  props="$(dotnet msbuild "$project" -nologo -p:Configuration="$CONFIGURATION" \
    -getProperty:IsTestProject -getProperty:TargetPath)"
  [[ "$(grep -o '"IsTestProject": *"[^"]*"' <<<"$props" | sed 's/.*"\([^"]*\)"$/\1/')" == "true" ]] || continue
  target="$(grep -o '"TargetPath": *"[^"]*"' <<<"$props" | sed 's/.*"\([^"]*\)"$/\1/')"
  name="$(basename "$project" .csproj)"

  echo
  echo ">>> coverlet: $name"
  dotnet tool exec "coverlet.console@$COVERLET_VERSION" --yes -- "$target" \
    --target dotnet \
    --targetargs "test --project \"$project\" -c $CONFIGURATION --no-build --filter \"$filter\"" \
    --include '[Subsolo.*]*' \
    --exclude-by-attribute GeneratedCodeAttribute \
    --exclude-by-attribute CompilerGeneratedAttribute \
    --exclude-by-attribute ExcludeFromCodeCoverageAttribute \
    --format cobertura \
    --output "$out/raw/$name.cobertura.xml"
  ran=$((ran + 1))
done

if ((ran == 0)); then
  echo "coverage: nenhum projeto de teste (IsTestProject=true) em $solution" >&2
  exit 2
fi

echo
echo ">>> ReportGenerator"
dotnet tool exec "dotnet-reportgenerator-globaltool@$REPORTGENERATOR_VERSION" --yes -- \
  "-reports:$out/raw/*.cobertura.xml" \
  "-targetdir:$out" \
  "-reporttypes:Cobertura;TextSummary;Html" \
  "-verbosity:Warning"

# Linhas alteradas: diff (sem contexto) entre o merge-base com a base (conferida no início) e a árvore de trabalho.

echo
git diff --unified=0 --no-color --no-ext-diff --diff-filter=AMR "$base" -- '*.cs' >"$out/changed.diff"
python3 -I - "$out/Cobertura.xml" "$out/changed.diff" "$root" "$base_ref" "$base" "$out/changed-lines.md" <<'PY'
import os
import re
import sys
import xml.etree.ElementTree as ET

cobertura, diff_path, root, base_ref, base, report_path = sys.argv[1:7]

# Linhas executáveis e se foram executadas, por arquivo (caminho relativo à raiz).
tree = ET.parse(cobertura)
sources = [s.text or "" for s in tree.getroot().iter("source")]
hits = {}
for cls in tree.getroot().iter("class"):
    filename = cls.get("filename", "")
    candidates = [filename] if os.path.isabs(filename) else [os.path.join(s, filename) for s in sources] or [filename]
    path = next((c for c in candidates if os.path.exists(c)), candidates[0])
    rel = os.path.relpath(os.path.realpath(path), os.path.realpath(root))
    lines = hits.setdefault(rel, {})
    for line in cls.iter("line"):
        number = int(line.get("number"))
        lines[number] = lines.get(number, False) or int(line.get("hits", "0")) > 0

# Linhas acrescentadas/alteradas por arquivo, a partir do diff unificado sem contexto.
changed = {}
current = None
with open(diff_path, encoding="utf-8", errors="replace") as diff:
    for raw in diff:
        if raw.startswith("+++ "):
            target = raw[4:].strip()
            current = target[2:] if target.startswith("b/") else None
        elif raw.startswith("@@") and current:
            match = re.match(r"@@ -\S+ \+(\d+)(?:,(\d+))? @@", raw)
            start, count = int(match.group(1)), int(match.group(2) or "1")
            changed.setdefault(current, set()).update(range(start, start + count))

def percent(covered, total):
    value = round(100 * covered / total, 1)
    return f"{value:g}%"

rows = []
for rel in sorted(changed):
    executable = {n: hit for n, hit in hits.get(rel, {}).items() if n in changed[rel]}
    if executable:
        covered = sum(executable.values())
        rows.append((rel, covered, len(executable)))

lines = [f"Cobertura das linhas alteradas (vs {base_ref}, merge-base {base[:12]})", ""]
if rows:
    width = max(len(rel) for rel, _, _ in rows)
    for rel, covered, total in rows:
        lines.append(f"{rel.ljust(width)}  {covered}/{total}  {percent(covered, total)}")
    covered = sum(r[1] for r in rows)
    total = sum(r[2] for r in rows)
    lines += ["", f"Total: {covered}/{total} linhas alteradas cobertas — {percent(covered, total)}"]
else:
    lines.append("Nenhuma linha executável alterada em código instrumentado.")

text = "\n".join(lines)
print(text)
with open(report_path, "w", encoding="utf-8") as report:
    report.write(text + "\n")
PY

echo
echo "coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.md)"
