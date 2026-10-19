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
  `pnpm -C web ...` a partir da raiz usa o pnpm padrão do `PATH`, que precisa ser o mesmo (os scripts de `tools/` já
  entram em `web/`). Com o Corepack, alinhe o padrão global uma vez: `corepack install -g pnpm@12.11.2`.
- [Docker](https://docs.docker.com/get-docker/) — usado pelo gitleaks no hook de pre-commit (quando o binário `gitleaks` não está no `PATH`) e pelos testes de integração.
- [Aspire CLI](https://aspire.dev) 13.6 — `dotnet tool install -g Aspire.Cli --version 13.6.1` (ambiente local com `aspire run`).
- Python 3.8+ — para `tools/sdd/spec_graph.py`.

## Como rodar

Todos os comandos rodam na raiz do repositório.

```bash
./tools/build.sh          # dotnet build -warnaserror + pnpm install --frozen-lockfile + pnpm build
./tools/lint.sh           # dotnet format --verify-no-changes, build com analisadores, eslint, prettier, tsc (e semgrep, se instalado)
dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)   # suíte padrão (`test` do sdd-config)
dotnet test Subsolo.slnx --filter "Kind=Integration" --max-parallel-test-modules 1   # integração com PostgreSQL e Mailpit reais (precisa do Docker)
dotnet test tests/Subsolo.ArchitectureTests            # regras de arquitetura
(cd e2e && pnpm install && pnpm exec playwright install && pnpm test)   # E2E com Playwright (mobile e desktop)
./tools/coverage.sh       # cobertura das linhas alteradas em relação à main

aspire run                # sobe PostgreSQL 18, Seq, Mailpit, API e front (dashboard do Aspire com logs e traces)
./tools/compose-up.sh     # sobe a pilha de produção local (Compose gerado pelo Aspire + Caddy) em https://localhost:8443 — a agenda de exemplo fica em /cidade-exemplo
(cd web && pnpm dev)      # front em modo desenvolvimento (http://localhost:3000)
(cd web && pnpm format)   # formata o front com o Prettier
dotnet format Subsolo.slnx  # formata o C# conforme o .editorconfig
```

O `tools/lint.sh` roda todas as verificações e lista no fim as que falharam, em vez de parar na primeira.

Os módulos de teste rodam um de cada vez (`--max-parallel-test-modules 1`): o Next.js 16 aceita um só `next dev` por pasta (os testes do AppHost sobem o front) e isso mantém a suíte dentro de 8 GB de RAM.

Na pilha local do `compose-up.sh` o certificado HTTPS é o da CA interna do Caddy (`tls internal`), que o navegador acusa como não confiável; `http://localhost:8080` só redireciona para o HTTPS. Logs e traces da API e do front vão para o Seq; no `compose-up.sh` ele fica em http://127.0.0.1:5341 (`SEQ_PORT` muda a porta). Os runbooks dos alertas ficam em `docs/runbooks/`, e o checklist de segurança (OWASP Top 10) em `docs/runbooks/checklist-seguranca.md`. O design system está em [`docs/design/DESIGN.md`](docs/design/DESIGN.md).

Contas (SPEC-0014): o admin inicial vem da configuração `Users:AdminEmails` (ex.: variável `Users__AdminEmails__0`); a sessão é guardada no banco, vale 30 dias sem uso e no máximo 90 dias, e o rate limit da API conta por visitante — na pilha do `compose-up.sh` o limite `default` sobe para 10000/min (`DEFAULT_RATE_LIMIT`), porque toda a E2E sai de um IP só.

E-mail (SPEC-0018): só o módulo `Notifications` envia, por SMTP; no `aspire run` os e-mails caem no Mailpit (dashboard do Aspire), e fora de desenvolvimento o TLS é obrigatório (`Notifications:Smtp:Security`).

Login (SPEC-0016): para testar entrar e sair, use a pilha do `./tools/compose-up.sh` (https://localhost:8443) — o link chega no Mailpit em http://127.0.0.1:8025. Pelo `aspire run` o login não funciona (o cookie `__Host-` exige HTTPS e o `/api` passa pelo Caddy).

Admin (SPEC-0017): na pilha do `compose-up.sh`, `admin@subsolo.test` é admin; depois de entrar com ele, o painel fica em https://localhost:8443/admin/cidades-e-estilos.

Google (SPEC-0020): na pilha do `compose-up.sh` e no `aspire run` o "Continuar com Google" usa um provedor falso em http://127.0.0.1:8090 (digite o e-mail e, nas claims, `{"email": "...", "email_verified": true}`).

Perfis (SPEC-0021): o fã pede o perfil em https://localhost:8443/conta/perfis; com `admin@subsolo.test`, a fila fica em https://localhost:8443/admin/fila e o perfil sem dono em https://localhost:8443/admin/perfis/novo.

Flyers (SPEC-0022): no `aspire run` os flyers ficam em `artifacts/media` e são servidos pela API em `/media/flyers/*`; na pilha do `compose-up.sh` ficam no volume `media` e o Caddy os serve em https://localhost:8443/media/flyers/.

Shows (SPEC-0023): quem tem perfil aprovado cadastra shows em https://localhost:8443/conta/shows (o admin também, em qualquer perfil).

Os comandos oficiais ficam em `docs/specs/sdd-config.yml`; o guia de testes (traits, fixtures, como escrever cada tipo de teste) está em [`tests/README.md`](tests/README.md). Todo PR roda o CI do GitHub Actions (`.github/workflows/ci.yml`), cujos jobs são checks obrigatórios para o merge na `main`.

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
