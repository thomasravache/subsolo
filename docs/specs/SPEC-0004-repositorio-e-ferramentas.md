---
id: SPEC-0004
title: Repositório e ferramentas
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-10-13
parent: SPEC-0001
depends_on: []
consumes_contract: []
contract_version: 3
touches: [README.md, CLAUDE.md, AGENTS.md, CHANGELOG.md, Subsolo.slnx, global.json, Directory.Build.props, Directory.Packages.props, .editorconfig, .gitignore, .gitattributes, .nvmrc, .semgrep.yml, .gitleaks.toml, .githooks/**, .github/pull_request_template.md, .github/CODEOWNERS, .github/dependabot.yml, tools/**, web/**, tests/Subsolo.Tooling.Tests/**]
adrs: [ADR-0001, ADR-0006, ADR-0007, ADR-0009]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0004 — Repositório e ferramentas

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Cria o repositório do Subsolo no GitHub com a estrutura de pastas, a solution .NET, o projeto Next.js vazio, a formatação e o lint automáticos, as convenções (Conventional Commits, Keep a Changelog, SemVer) e o controle de SDD instalado (`tools/sdd`, hooks, template de PR, bloco nos arquivos de agentes). Ao final, `./tools/build.sh` e `./tools/lint.sh` passam numa máquina limpa (Emenda v2), e qualquer agente que abrir o repositório sabe como trabalhar.

## 2. Motivação & Escopo
**Motivação:** sem estrutura, convenções e verificação automática desde o primeiro commit, cada agente faz de um jeito, e o processo depende da memória do modelo.

**Objetivos (dentro do escopo):**
- Repositório público `thomasravache/subsolo` no GitHub (Emenda v1; transferência para a organização `subsolo-br` no go-live), branch `main`, CODEOWNERS e proteção da `main` (ADR-0007).
- Estrutura: `src/`, `tests/`, `web/`, `infra/`, `docs/` (specs, adr, runbooks), `tools/sdd/`.
- `Subsolo.slnx` com .NET 10 SDK fixado em `global.json`; `Directory.Build.props` (nullable, warnings como erro, analisadores) e `Directory.Packages.props` (Central Package Management + lockfiles) — ADR-0006/ADR-0009.
- `web/` criado com Next.js 16 + TypeScript strict + ESLint + Prettier + Tailwind 4, gerenciado com pnpm e Node 24 LTS (`.nvmrc`, `packageManager`).
- `.editorconfig`, `.gitignore`, `.gitattributes`, `.semgrep.yml`, configuração do gitleaks.
- `spec_graph.py vendor --agents --ci github --pr-template github --hooks --changelog`: `tools/sdd/`, `.githooks/` (commit-msg, pre-commit com gitleaks e validate), template de PR, bloco SDD em `CLAUDE.md`/`AGENTS.md`, `CHANGELOG.md`.
- `README.md` com o propósito, como rodar e as convenções; `sdd-config.yml` com os comandos reais validados (`build`, `lint`).
- `.github/dependabot.yml` (NuGet, npm, Docker e Actions, semanal e agrupado).

**Não-objetivos (fora do escopo):**
- Pipeline de CI (SPEC-0006), harness de testes (SPEC-0005), código de domínio ou do AppHost (SPEC-0007).
- Qualquer serviço pago (ADR-0012).

## 3. Dependências
- **Implementações necessárias:** N/A — primeira spec da fundação.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** `gh` autenticado na conta `thomasravache` (repositório criado pelo Architect com autorização do Thomas); .NET 10 SDK, Node 24 LTS, pnpm e Docker instalados localmente; Python 3.8+ para `tools/sdd`.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; padrão definido nos ADRs de fundação (ADR-0001, ADR-0006, ADR-0007, ADR-0009).

**Decisão:** monorepo único, com back-end e front lado a lado, ferramentas nativas de cada ecossistema e controle de processo fora do modelo (hooks + CI).

**Justificativa:** um repositório para uma pessoa e vários agentes reduz a coordenação; os contratos front↔API ficam no mesmo PR.

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** repositórios separados para front e back (dobra PRs e versionamento de contrato); Nx/Turborepo (complexidade sem ganho com um só app front).

**ADRs:** ADR-0001, ADR-0006, ADR-0007, ADR-0009

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** N/A — infraestrutura de repositório, sem caminho de requisição.
- **Segurança:** gitleaks no pre-commit e configurado para o CI; nenhum segredo versionado; `.gitignore` cobre `.env*` e `appsettings.*.local.json` — verificado pelo IT-02.
- **Privacidade e dados pessoais:** N/A — nenhum dado pessoal é tratado.
- **Disponibilidade e resiliência:** N/A — sem serviço em execução.
- **Acessibilidade (UI):** N/A — sem tela nesta spec.
- **Custo:** zero; repositório no plano gratuito do GitHub (ADR-0012).

## 6. Artefato A — Contrato
**Interface:** `comandos de desenvolvimento na raiz do repositório` (contrato consumido pelas specs SPEC-0005, SPEC-0006 e SPEC-0007)

```text
Comandos (raiz do repositório):
  ./tools/build.sh   → dotnet build Subsolo.slnx -warnaserror ; (cd web && pnpm install --frozen-lockfile && pnpm build)
                       exit 0 numa máquina limpa; é o comando `build` do sdd-config
  ./tools/lint.sh    → dotnet build -warnaserror + dotnet format Subsolo.slnx --verify-no-changes
                       + (cd web && pnpm lint: eslint + prettier --check + tsc --noEmit) + semgrep quando instalado;
                       roda TODAS as verificações e lista todas as falhas; é o comando `lint` do sdd-config
  pnpm é sempre chamado de dentro de web/, para o corepack respeitar o `packageManager` fixado em web/package.json
  (Emenda v2: o `pnpm -C web` a partir da raiz usava o pnpm padrão da máquina e quebrava o lockfile)

Convenções:
  Commit: Conventional Commits + referência a SPEC-NNNN (convenção da ferramenta SDD: preferencialmente no rodapé "Refs:",
          aceita em qualquer parte da mensagem; "[no-spec]" no assunto para mudanças sem comportamento, como typo e formatação);
          hook commit-msg rejeita fora do padrão (exit 1). O CI (pr-check) continua exigindo spec aprovada em todo PR. (Emenda v3)
  Pre-commit: gitleaks protect --staged; spec_graph validate --staged para docs/specs e docs/adr.
  Pastas: src/ (back), tests/ (testes .NET), web/ (front e seus testes), e2e/ (Playwright), infra/, docs/.
Erros:
  formatação divergente → dotnet format/prettier exit ≠ 0 com a lista de arquivos
  segredo em arquivo staged → pre-commit exit 1 com o achado do gitleaks
  mensagem de commit fora do padrão → commit-msg exit 1 com a mensagem esperada
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Build limpo | clone novo + `./tools/build.sh` | build .NET e web com exit 0, com o pnpm fixado | IT-01 |
| Formatação divergente | arquivo C# ou TS fora do padrão | `lint` falha listando o arquivo | IT-03 |
| Segredo commitado | arquivo staged com chave fictícia no formato AWS/Google | pre-commit bloqueia o commit | IT-02 |
| Commit fora do padrão | mensagem "ajustes" sem tipo e sem `Refs:` | commit-msg rejeita | UT-01, IT-04 |
| Commit no padrão | `feat(events): ...` + `Refs: SPEC-0004` | commit aceito | IT-04 |
| Analisador como erro | código C# com aviso de nullable | build falha | IT-05 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dada a regra de validação de mensagem do hook commit-msg, quando avaliada com uma tabela de mensagens (`feat(events): x` + `Refs: SPEC-0004` válida; `ajustes`, `feat: x` sem `Refs`, tipo inexistente `feature: x` inválidas), então aceita apenas as válidas.

### 7.3 Testes de Integração
- **IT-01** — Dado um clone limpo num container com .NET 10 SDK, Node 24 e corepack habilitado (sem pnpm global fixado), quando `./tools/build.sh` é executado, então termina com exit 0 usando a versão do pnpm fixada em `web/package.json` (Emenda v2).
- **IT-02** — Dado um repositório temporário com os hooks instalados, quando um arquivo com uma chave fictícia é commitado, então o pre-commit falha com o achado do gitleaks; sem o segredo, o commit passa.
- **IT-03** — Dado um arquivo C# e um TS fora da formatação, quando `./tools/lint.sh` roda, então termina com exit ≠ 0 e lista os dois arquivos.
- **IT-04** — Dado o hook commit-msg, quando a mensagem não segue Conventional Commits ou não tem `Refs: SPEC-NNNN`, então o commit é rejeitado; com a mensagem correta, é aceito.
- **IT-05** — Dado um projeto de exemplo em `tests/Subsolo.Tooling.Tests/Fixtures` com uma desreferência possivelmente nula, quando compilado com o `Directory.Build.props` do repositório, então a compilação falha com o diagnóstico CS8602 tratado como erro.

### 7.4 Testes de Contrato
- **CT-01** — Os comandos `build` e `lint` gravados no `sdd-config.yml` executam com exit 0 na raiz (contrato consumido pelas SPEC-0005, SPEC-0006 e SPEC-0007).

### 7.5 Testes E2E
- N/A — nenhuma jornada de usuário (UI ou API pública) é afetada; a spec só cria ferramentas de repositório.

### 7.6 Outros
- Segurança: gitleaks sem achados em todo o histórico (`gitleaks detect`) no fechamento.

**Dublês e dados de teste:** repositórios git temporários criados por teste; chaves fictícias que seguem o formato real, mas são inválidas.

**Ambiente de execução:** testes de ferramentas em `tests/Subsolo.Tooling.Tests` (xUnit, rodando scripts via processo) e localmente; no CI a partir da SPEC-0006.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — não há runtime; a entrega é o próprio repositório.
- **Dados/schema:** N/A
- **Compatibilidade:** N/A
- **Observabilidade:** N/A — sem serviço em execução.
- **Rollback:** reverter o merge commit na `main`.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] O repositório no GitHub será público ou privado? — Público: proteção da `main`, CodeQL e GHCR gratuitos; nenhum segredo no código (Thomas, 2026-10-13)
- [x] Onde fica o repositório? — Na organização gratuita `subsolo-br` (o nome `subsolo` já estava ocupado no GitHub em 2026-10-13): `subsolo-br/subsolo` (Thomas, 2026-10-13). **Alterado pela Emenda v1:** repositório público na conta pessoal, `thomasravache/subsolo`; a organização `subsolo-br` é criada e o repositório transferido no go-live (SPEC-0003) (Thomas, 2026-10-13)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Esqueleto do repositório**
- [x] Red: escrever UT-01 e IT-01, IT-05 com a tag `SPEC-0004:<ID>` e confirmar que falham pelo motivo certo (repositório ainda sem solution, sem `Directory.Build.props`)
- [x] Green: `Subsolo.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `.gitattributes`, `web/` (Next.js 16 + TS strict + ESLint + Prettier + Tailwind 4, pnpm, Node 24)
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte (G2)

**Fase 2: Lint, hooks e segurança local**
- [x] Red: IT-02, IT-03, IT-04 e CT-01 falhando
- [x] Green: `tools/lint.sh`, `.semgrep.yml`, `.gitleaks.toml`, `vendor --agents --ci github --pr-template github --hooks --changelog` (hooks usam gitleaks via binário ou imagem Docker), `.github/dependabot.yml`, `CODEOWNERS`, README, comandos `build`/`lint` no sdd-config
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte + lint (G2/G3 — G3 N/A até a SPEC-0007 criar a suíte de arquitetura)

**Fase 3: GitHub**
- [x] Repositório público `thomasravache/subsolo` criado, `main` publicada, proteção da `main` (merge commit, sem force-push; checks obrigatórios adicionados pela SPEC-0006)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003; validação local pelo Compose quando aplicável
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-13 |
| G1 Red | PASS | verify G1: PASS — f15f104 | 2026-10-13 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (duration: 42s 901ms); lint exit 0 ($ next typegen && tsc --noEmit) — c687f4d | 2026-10-13 |
| G3 Arquitetura | N/A | suíte de arquitetura ainda não existe: criada pela SPEC-0005 (projeto) e SPEC-0007 (regras do ADR-0001); SPEC-0004 não tem código de produção | 2026-10-13 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente (2ª rodada): APPROVED em 76e4fbd — 0 blocker/major; minors: .env.example ignorado por .env*, UT-01 sem as linhas da Emenda v3, só chave AWS testada, comentário de scaffold em web/next.config.ts, dependabot/pastas adiadas (relatório) — 76e4fbd | 2026-10-13 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (duration: 45s 527ms) — d5323da | 2026-10-13 |
| H2 Integração aprovada | PASS | aprovado por thomas | 2026-10-13 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live: deploy, staging e produção chegam com SPEC-0003 (decisão de custo do Thomas, ADR-0012); SPEC-0004 entrega só ferramentas de repositório | 2026-10-13 |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-13 | G5 | externo | Organização subsolo-br não existe no GitHub; a API do github.com não permite criá-la (só pela interface web) | gh api orgs/subsolo-br → 404; criação de organização via API só existe no GitHub Enterprise Server | Thomas: criar a organização gratuita subsolo-br em https://github.com/account/organizations/new?plan=free | Emenda v1 — repositório criado em thomasravache/subsolo; organização subsolo-br e transferência movidas para o go-live (SPEC-0003), decisão do Thomas em 2026-10-13 | 2026-10-13 |
| IMP-02 | 2026-10-13 | G2 | spec | Contrato usa 'pnpm -C web ...' a partir da raiz: o corepack ignora o packageManager de web/package.json e usa o pnpm padrão da máquina (10.23 local), que não lê o lockfile do pnpm 12 → comando literal do contrato falha; IT-01 só passa enquanto o pin coincide com o pnpm mais recente do corepack | Implementer: scripts tools/build.sh e tools/lint.sh fazem 'cd web' antes do pnpm (funciona); README documenta corepack install -g pnpm@12.11.2 | Architect + Thomas: Emenda v2 (contrato passa a ser tools/build.sh e tools/lint.sh; IT-01 roda tools/build.sh; .nvmrc entra em touches) | Emenda v2 — contrato de comandos passa a ser tools/build.sh e tools/lint.sh (pnpm sempre de dentro de web/), IT-01 roda tools/build.sh, .nvmrc em touches; SPEC-0005 v2, SPEC-0006 e SPEC-0008 ajustadas; aprovada pelo Thomas em 2026-10-13 | 2026-10-13 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
O repositório público `thomasravache/subsolo` pronto para receber código com qualidade automática:
- Solution .NET 10 (`Subsolo.slnx`, SDK fixado em `global.json` com o runner Microsoft Testing Platform), regras compartilhadas em `Directory.Build.props` (nullable, warnings como erro, analisadores, lockfiles) e Central Package Management em `Directory.Packages.props`.
- App Next.js 16.4 vazio em `web/` (React 19.3, TypeScript 6.0.3 strict, ESLint 9.39.5, Prettier, Tailwind 4.3), com pnpm 12.11.2 fixado e Node 24 (`.nvmrc`).
- `tools/build.sh` e `tools/lint.sh` (contrato v3), gravados no `sdd-config` como `build` e `lint`.
- Hooks em `.githooks/`: `commit-msg` (Conventional Commits + referência à spec, convenção da ferramenta SDD) e `pre-commit` (gitleaks em staged, pelo binário ou pela imagem Docker fixada, + `validate --staged`).
- Ferramenta SDD vendorizada em `tools/sdd/`, bloco SDD em `CLAUDE.md`/`AGENTS.md`, template de PR, `CHANGELOG.md`, `README.md`, `.semgrep.yml`, `.gitleaks.toml`, `.github/dependabot.yml` e `.github/CODEOWNERS`.
- No GitHub: repositório público, somente merge commit (squash e rebase desligados) e proteção da `main` (PR obrigatório, vale para administradores, sem force-push e sem exclusão).

### Como foi feito
- TDD com papéis separados: Test-writer (red f15f104) → Implementer (0f50f38…c3fcac6) → Test-writer (IT-01 da Emenda v2, 4fc3e94) → Reviewer 1 (CHANGES_REQUESTED: projeto de testes fora do CPM) → Implementer (6d77698, 179aab1) e Test-writer (9ff59e2, c687f4d) → Reviewer 2 (APPROVED em 76e4fbd).
- Emendas: v1 (repositório na conta pessoal, organização `subsolo-br` no go-live), v2 (contrato de comandos via `tools/*.sh`, com o pnpm sempre chamado de dentro de `web/`; IMP-02) e v3 (convenção de commit da ferramenta SDD).
- Decisões registradas: ADR-0005 (YamlDotNet, só em teste), ADR-0006 (ESLint 9.39.5 e TS 6.0.3, com o motivo) e ADR-0009 (lista de licenças ampliada).
- Dívidas aceitas: o Dependabot tem os alvos `docker` e `github-actions` sem arquivos até as SPEC-0006/0007 (vai acusar "nada encontrado" até lá); as pastas `src/`, `infra/`, `e2e/` e `docs/runbooks/` nascem com as specs que as usam; o Semgrep só roda localmente se estiver instalado (no CI, a partir da SPEC-0006).

### Prova de Correção
N/A — type foundation.

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | regra do commit-msg aceita só mensagens válidas (4 casos) | PASS | `gate G2 --run` test exit 0 em c687f4d; `gate G5 --run` em d5323da |
| IT-01 | `./tools/build.sh` num clone limpo em container (sdk:10.0.402, Node 24.21.0 com checksum, pnpm fixado) | PASS | G2 em c687f4d; Reviewer 2 executou isoladamente (~25 s) em 76e4fbd |
| IT-02 | pre-commit bloqueia segredo e libera arquivo limpo; `.gitignore` cobre `.env*` e `appsettings.*.local.json` | PASS | G2 em c687f4d; G5 em d5323da |
| IT-03 | `./tools/lint.sh` falha listando o C# e o TS mal formatados | PASS | G2 em c687f4d; G5 em d5323da |
| IT-04 | hook commit-msg rejeita fora do padrão e aceita no padrão | PASS | G2 em c687f4d; G5 em d5323da |
| IT-05 | CS8602 tratado como erro pelo `Directory.Build.props` | PASS | G2 em c687f4d; G5 em d5323da |
| CT-01 | comandos `build` e `lint` do sdd-config com exit 0 na raiz | PASS | G2 em c687f4d (build e lint exit 0); G5 em d5323da |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — suíte verde em d5323da; arquitetura N/A (suíte criada pelas SPEC-0005/0007); CI ainda inexistente (SPEC-0006), por isso G5 local
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — ambiente-alvo desta spec é a `main`: disponível pelo merge do PR #1 (merge commit ef89a00, 2026-10-13); G6 N/A (sem ambiente implantado até a SPEC-0003)
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout
- [x] Documentação raiz e CHANGELOG atualizados (G7) — README e CLAUDE.md/AGENTS.md entregues pela própria spec; entrada no CHANGELOG
- [x] Pendências registradas como novas specs (ou nenhuma)

### Deploy
Sem ambiente implantado (G6 N/A → SPEC-0003). Entrega na `main` pelo PR #1 (https://github.com/thomasravache/subsolo/pull/1), merge commit ef89a00 em 2026-10-13, após o H2 do Thomas (merge executado pelo Architect com a autorização permanente registrada no ADR-0007).

### Pendências
- SPEC-0012 — ajustes menores apontados na 2ª revisão: liberar `.env.example`, casos da Emenda v3 no UT-01, chave falsa no formato Google no pre-commit e comentário de scaffold em `web/next.config.ts`.

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0004`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 | 2026-10-13 | Repositório criado em `thomasravache/subsolo` (conta pessoal) em vez de `subsolo-br/subsolo`; organização e transferência movidas para o go-live (SPEC-0003). Contrato inalterado. | Thomas preferiu não depender da criação da organização agora | SPEC-0003 | thomas (chat, 2026-10-13) |
| 2 | 2026-10-13 | Contrato de comandos passa a ser `./tools/build.sh` e `./tools/lint.sh` (pnpm sempre chamado de dentro de `web/`); IT-01 roda `./tools/build.sh`; `.nvmrc` na raiz entra em `touches`; `tests/Subsolo.Tooling.Tests` usa YamlDotNet (ADR-0005). | IMP-02: `pnpm -C web` da raiz ignorava o pnpm fixado e quebrava o lockfile; IT-01 dependia da versão mais recente do corepack | SPEC-0005, SPEC-0006 (comandos `build`/`lint` do sdd-config) | thomas (chat, 2026-10-13) |
| 3 | 2026-10-13 | Convenção de commit = a da ferramenta SDD: referência `SPEC-NNNN` aceita em qualquer parte da mensagem (preferência pelo rodapé `Refs:`) e `[no-spec]` para mudanças sem comportamento; o `pr-check` do CI segue exigindo spec aprovada. | Achado minor #7 da revisão G4: o hook vendorizado era mais flexível que o texto do contrato | — | thomas (chat, 2026-10-13) |
