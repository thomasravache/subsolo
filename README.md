# Subsolo

Agenda de shows de metal underground por cidade. Produtores e bandas aprovados cadastram os shows; o fã descobre
o que vai rolar, segue cidades, bandas e casas e é avisado antes do show — em vez de a divulgação se perder em
stories de 24 h, grupos de WhatsApp e posts escondidos pelo algoritmo. Gratuito para fãs, bandas e produtores,
sem anúncios. Visão completa em [SPEC-0002](docs/specs/SPEC-0002-agenda-subsolo-lancamento-mvp.md).

O projeto é desenvolvido com Spec-Driven Development (SDD), por uma pessoa com a ajuda de agentes de IA.

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/` | back-end .NET 10 — monólito modular com vertical slices ([ADR-0001](docs/adr/ADR-0001-monolito-modular-com-vertical-slices-em-net-10.md)) |
| `tests/` | testes .NET (`tests/Subsolo.Tooling.Tests` verifica as ferramentas do próprio repositório) |
| `web/` | front-end Next.js 16 (App Router, TypeScript strict, Tailwind CSS 4) e seus testes |
| `e2e/` | testes ponta a ponta (Playwright) |
| `infra/` | infraestrutura e deploy |
| `docs/` | specs (`docs/specs`), ADRs (`docs/adr`) e runbooks |
| `tools/` | scripts de build e lint e o controle SDD vendorizado (`tools/sdd`) |

Pastas ainda vazias são criadas pela spec que as usa pela primeira vez.

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — a versão é fixada em `global.json` (10.0.4xx ou mais nova da mesma linha).
- [Node.js 24 LTS](https://nodejs.org/) — ver `.nvmrc` (`nvm use`).
- [pnpm](https://pnpm.io/) — a versão é fixada em `web/package.json` (`packageManager`, pnpm 12). Com o Corepack
  (`corepack enable pnpm`) ou com um pnpm 10+ instalado, rodar o pnpm de dentro de `web/` usa a versão fixada;
  `pnpm -C web ...` a partir da raiz usa o pnpm do `PATH`, que precisa ser o 12 (os scripts de `tools/` já entram em `web/`).
- [Docker](https://docs.docker.com/get-docker/) — usado pelo gitleaks no hook de pre-commit (quando o binário `gitleaks` não está no `PATH`) e pelos testes de integração.
- Python 3.8+ — para `tools/sdd/spec_graph.py`.

## Como rodar

Todos os comandos rodam na raiz do repositório.

```bash
./tools/build.sh          # dotnet build -warnaserror + pnpm install --frozen-lockfile + pnpm build
./tools/lint.sh           # dotnet format --verify-no-changes, build com analisadores, eslint, prettier, tsc (e semgrep, se instalado)
dotnet test tests/Subsolo.Tooling.Tests   # testes das ferramentas do repositório (alguns usam Docker)

(cd web && pnpm dev)      # front em modo desenvolvimento (http://localhost:3000)
(cd web && pnpm format)   # formata o front com o Prettier
dotnet format Subsolo.slnx  # formata o C# conforme o .editorconfig
```

O `tools/lint.sh` roda todas as verificações e lista no fim as que falharam, em vez de parar na primeira.

## Convenções

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/pt-br/v1.0.0/) com rodapé
  `Refs: SPEC-NNNN` apontando a spec da mudança. Exemplo:

  ```text
  feat(events): mostra a agenda da cidade

  Refs: SPEC-0008
  ```

  Mudança sem comportamento (typo, formatação) pode usar `[no-spec]` no assunto.
- **Changelog:** [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) em `CHANGELOG.md`.
- **Versões:** [Versionamento Semântico](https://semver.org/lang/pt-BR/).
- **Mudanças:** sempre por pull request na `main`, com o template de `.github/pull_request_template.md` ([ADR-0007](docs/adr/ADR-0007-fluxo-de-mudanca-por-pull-request-no-github.md)).
- **Qualidade:** avisos do compilador e dos analisadores são erros; formatação verificada por `dotnet format` e Prettier ([ADR-0006](docs/adr/ADR-0006-qualidade-de-codigo-automatica.md)).
- **Dependências:** versões NuGet centralizadas em `Directory.Packages.props`, lockfiles versionados e Dependabot semanal ([ADR-0009](docs/adr/ADR-0009-dependencias-e-ciclo-de-vida-da-stack.md)).

## Hooks de git

Os hooks versionados em `.githooks/` validam a mensagem de commit (`commit-msg`), procuram segredos nas mudanças
staged com o gitleaks e validam specs/ADRs alterados (`pre-commit`). Ative-os uma vez por clone:

```bash
git config core.hooksPath .githooks
```

Nunca contorne um hook com `--no-verify`: corrija a causa.

## Spec-Driven Development

- Toda mudança de comportamento começa por uma spec aprovada em `docs/specs/` — índice em [`docs/specs/INDEX.md`](docs/specs/INDEX.md).
- As decisões de arquitetura ficam em `docs/adr/`.
- Comandos dos gates (build, lint, testes) em [`docs/specs/sdd-config.yml`](docs/specs/sdd-config.yml).
- Estado e próximo passo de uma spec: `python3 tools/sdd/spec_graph.py status SPEC-NNNN`.
- Regras para agentes de IA: [`AGENTS.md`](AGENTS.md) (o `CLAUDE.md` o importa).
