---
id: SPEC-0005
title: Harness de testes
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0004]
consumes_contract: []
contract_version: 3
touches: [tests/Subsolo.UnitTests.Common/**, tests/Subsolo.IntegrationTests.Common/**, tests/Subsolo.ArchitectureTests/**, tests/Examples/**, tests/README.md, web/vitest.config.ts, web/src/test/**, web/package.json, web/pnpm-lock.yaml, e2e/**, tools/coverage.sh, Directory.Packages.props, Subsolo.slnx, tests/Subsolo.Tooling.Tests/SddConfigCommandsContractTests.cs, tests/Subsolo.Tooling.Tests/packages.lock.json]
adrs: [ADR-0005, ADR-0002]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0005 — Harness de testes

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Monta a infraestrutura de testes de todos os níveis do ADR-0005, com um exemplo funcional de cada: unitário .NET (xUnit v3 + Shouldly), integração com PostgreSQL 18 real (Testcontainers), arquitetura (ArchUnitNET), unitário do front (Vitest + Testing Library) e E2E (Playwright, com viewport de celular e axe). Ao final, `test`, `test_integration`, `arch_test` e `test_e2e` do sdd-config rodam localmente com um comando cada.

## 2. Motivação & Escopo
**Motivação:** o TDD das specs seguintes exige que escrever um teste em qualquer nível seja trivial; sem harness, cada agente monta o seu e os testes divergem.

**Objetivos (dentro do escopo):**
- Projetos `tests/Subsolo.UnitTests.Common` (base) e `tests/Subsolo.IntegrationTests.Common` com `PostgresFixture` (Testcontainers.PostgreSql, imagem `postgres:18.6`, container compartilhado por coleção, limpeza por `TRUNCATE` por schema) e `MailpitFixture` (container SMTP de teste).
- `FakeTimeProvider` padronizado para testes com tempo (Microsoft.Extensions.TimeProvider.Testing).
- Projeto `tests/Subsolo.ArchitectureTests` com ArchUnitNET e um helper para declarar regras por módulo (consumido pela SPEC-0007).
- Front: Vitest 5 + Testing Library + jsdom em `web/`, com um teste de exemplo.
- E2E: projeto Playwright em `e2e/` (TypeScript), com os projetos `mobile` (Pixel 7 e iPhone 14) e `desktop`, o fixture `axe` e um teste de exemplo contra uma página estática.
- Convenção de tags de rastreabilidade (`[Trait("Spec", "SPEC-NNNN:UT-01")]` e `test("SPEC-NNNN:E2E-01 …")`) documentada no README de testes.
- Comandos do sdd-config: `test`, `test_integration`, `arch_test`, `test_e2e`, `coverage` (coverlet + ReportGenerator, com relatório das linhas alteradas).

**Não-objetivos (fora do escopo):**
- Regras de arquitetura dos módulos reais (SPEC-0007), E2E das jornadas reais (SPEC-0008 em diante) e execução no CI (SPEC-0006).

## 3. Dependências
- **Implementações necessárias:** SPEC-0004 — solution, Central Package Management e `web/` precisam existir.
- **Contratos consumidos:** N/A — os comandos `build`/`lint` vêm da implementação de SPEC-0004, já em `depends_on`.
- **Pré-requisitos externos:** Docker local (Testcontainers); navegadores do Playwright instalados (`pnpm exec playwright install --with-deps`).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ferramentas definidas no ADR-0005 (tabela Ferramenta por nível).

**Decisão:** projetos de teste compartilhados por nível, com fixtures reutilizáveis; os testes de cada módulo ficam em `tests/Modules/<M>/` e herdam as fixtures.

**Justificativa:** um container de banco por coleção mantém os testes de integração rápidos e com dependência real, como exige o ADR-0005.

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** banco por teste (lento demais); SQLite ou EF InMemory (descartados no ADR-0005).

**ADRs:** ADR-0005, ADR-0002

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** suíte de exemplo completa (UT + IT + arquitetura) em < 90 s localmente com a imagem do Postgres em cache — medido pelo tempo do `dotnet test` registrado no G2.
- **Segurança:** N/A — sem código de produção nem dados reais; containers efêmeros sem portas fixas.
- **Privacidade e dados pessoais:** os dados de teste são sintéticos (builders); nenhum dado real.
- **Disponibilidade e resiliência:** testes determinísticos: tempo via `FakeTimeProvider` e nenhuma dependência da ordem — verificado pelo IT-03.
- **Acessibilidade (UI):** o fixture axe falha diante de violações sérias ou críticas — provado pelo E2E-01.
- **Custo:** zero; tudo roda local (ADR-0012).

## 6. Artefato A — Contrato
**Interface:** `fixtures e comandos de teste` (consumidos por todas as specs seguintes)

```text
.NET
  [Collection(nameof(PostgresCollection))]
  class XTests(PostgresFixture db)       // db.ConnectionString; await db.ResetAsync(params string[] schemas)
  MailpitFixture                         // SmtpHost/SmtpPort; await mail.GetMessagesAsync(to) ; await mail.ClearAsync()
  FakeTimeProvider (Microsoft.Extensions.Time.Testing)
  ArchitectureRules.ForModule("Events")  // usado pela SPEC-0007: .DomainMustNotDependOn(...), .OnlyReferences(...)
  Tag: [Trait("Spec", "SPEC-NNNN:<ID>")]

TypeScript
  web:  vitest + @testing-library/react; arquivo *.test.ts(x) ao lado do código
  e2e:  import { test, expect } from "./fixtures"  // fixture `axe`: await axe.check(page) → falha com violações serious/critical
        projetos: mobile-chrome (Pixel 7), mobile-safari (iPhone 14), desktop-chrome
  Tag: test("SPEC-NNNN:E2E-01 descrição", ...)

Comandos (sdd-config)
  test             = dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification" && (cd web && pnpm test)
                     # Emenda v3: testes Kind=GitHubVerification (SPEC-0006) rodam só sob demanda, com SDD_GITHUB_VERIFY=1
  test_integration = dotnet test Subsolo.slnx --filter "Kind=Integration"
  arch_test        = dotnet test tests/Subsolo.ArchitectureTests
  test_e2e         = (cd e2e && pnpm test)   # e2e/package.json fixa o mesmo pnpm de web/
  coverage         = ./tools/coverage.sh   (coverlet + ReportGenerator; % das linhas alteradas vs origin/main)
Erros
  Docker indisponível → IT falha logo, com a mensagem "Docker não encontrado: inicie o Docker para rodar testes de integração"
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Unitário .NET de exemplo | `dotnet test` | teste passa e aparece com a tag | UT-01 |
| Tempo controlado | `FakeTimeProvider` avançado 1 dia | o código sob teste enxerga a nova data | UT-02 |
| Integração com Postgres real | fixture sobe o container | consulta `select version()` retorna 18.x | IT-01 |
| Isolamento entre testes | dois testes gravam na mesma tabela após `ResetAsync` | cada um vê só os próprios dados | IT-03 |
| E-mail de teste | envio SMTP para o Mailpit | mensagem recuperável por destinatário | IT-02 |
| Docker ausente | Docker parado | falha imediata com mensagem clara | IT-04 |
| Regra de arquitetura viola | assembly de exemplo depende do proibido | teste de arquitetura falha | IT-05 |
| Unitário do front | `(cd web && pnpm test)` | componente de exemplo renderiza e passa | UT-03 |
| E2E com axe | página de exemplo com e sem violação | sem violação passa; com violação falha | E2E-01 |
| Cobertura | `./tools/coverage.sh` | relatório com % das linhas alteradas | IT-06 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o projeto de testes unitários de exemplo, quando `dotnet test` roda, então o teste com `[Trait("Spec","SPEC-0005:UT-01")]` passa e é listado com a tag.
- **UT-02** — Dado um serviço de exemplo que usa `TimeProvider`, quando o `FakeTimeProvider` avança 1 dia, então o serviço retorna a data seguinte.
- **UT-03** — Dado um componente React de exemplo, quando renderizado com Testing Library no Vitest, então o texto esperado aparece.

### 7.3 Testes de Integração
- **IT-01** — Dado o `PostgresFixture`, quando o teste consulta `select version()`, então a versão começa com "PostgreSQL 18".
- **IT-02** — Dado o `MailpitFixture`, quando um e-mail é enviado por SMTP, então `GetMessagesAsync(destinatário)` retorna a mensagem com o assunto enviado.
- **IT-03** — Dados dois testes na mesma coleção que inserem na mesma tabela após `ResetAsync`, quando rodam em qualquer ordem, então cada um enxerga exatamente uma linha.
- **IT-04** — Dado o Docker indisponível (variável `DOCKER_HOST` apontando para um socket inexistente), quando o fixture inicializa, então falha com a mensagem "Docker não encontrado".
- **IT-05** — Dado um assembly de exemplo cujo "Domain" referencia o "Infrastructure", quando a regra `ArchitectureRules.ForModule` é avaliada, então o teste de arquitetura falha nomeando a dependência.
- **IT-06** — Dado um commit que altera um arquivo coberto pela metade, quando `./tools/coverage.sh` roda, então o relatório mostra a porcentagem das linhas alteradas daquele arquivo.

### 7.4 Testes de Contrato
- **CT-01** — Os comandos `test`, `test_integration`, `arch_test`, `test_e2e` e `coverage` gravados no sdd-config executam com exit 0 na raiz (contrato consumido pela SPEC-0006 e pelas specs de módulo).

### 7.5 Testes E2E
- **E2E-01** — Dadas duas páginas estáticas de exemplo (uma com contraste insuficiente), quando o Playwright as abre nos projetos mobile e desktop e roda `axe.check`, então a página correta passa e a página com violação falha, provando o harness E2E e de acessibilidade.

### 7.6 Outros
- N/A

**Dublês e dados de teste:** assemblies de exemplo para as regras de arquitetura; páginas HTML estáticas servidas pelo `webServer` do Playwright; nenhum dado real.

**Ambiente de execução:** local, com Docker; no CI a partir da SPEC-0006.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — apenas código de teste.
- **Dados/schema:** N/A
- **Compatibilidade:** N/A
- **Observabilidade:** N/A — sem serviço em execução.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Harness .NET**
- [ ] Red: UT-01, UT-02, IT-01..IT-06 com a tag `SPEC-0005:<ID>`, falhando pelo motivo certo (fixtures e helpers ainda inexistentes — scaffold `chore` com NotImplemented)
- [ ] Green: projetos `Subsolo.UnitTests.Common`, `Subsolo.IntegrationTests.Common` (PostgresFixture, MailpitFixture), `Subsolo.ArchitectureTests` (ArchUnitNET + `ArchitectureRules`), `tools/coverage.sh`
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa (G2)

**Fase 2: Harness do front e E2E**
- [ ] Red: UT-03, E2E-01 e CT-01 falhando
- [ ] Green: Vitest + Testing Library em `web/`; projeto Playwright em `e2e/` (mobile + desktop, fixture axe); comandos `test`, `test_integration`, `arch_test`, `test_e2e`, `coverage` no sdd-config
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + E2E (G2)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003; validação local pelo Compose quando aplicável
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-13 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — cc247d8 | 2026-10-13 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ next typegen && tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 535aa1b | 2026-10-13 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 555ms) — 535aa1b | 2026-10-13 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED após Emenda v5 — único blocker do 1º review (web/pnpm-lock.yaml fora de touches) resolvido só por escopo, sem mudança de código; WARN de testes fora de commits red justificado no 1º review (infra do harness na fase green: fixtures, configs, exemplos); minors viram spec de pendências — 434de58 | 2026-10-14 |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-13 | G1 | spec | Contrato v2: 'test' roda toda a solution; os testes de verificação no GitHub da SPEC-0006 (Kind=GitHubVerification) falham de propósito sem SDD_GITHUB_VERIFY=1, deixando a suíte padrão e o job test do CI sempre vermelhos | Test-writer da SPEC-0006 confirmou que '--filter "Kind!=GitHubVerification"' funciona com o runner MTP (20 → 14 testes no CiPolicy) | Architect + Thomas: Emenda v3 da SPEC-0005 — test = dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification" && (cd web && pnpm test) | Emenda v3 — test exclui Kind=GitHubVerification; SPEC-0006 consome SPEC-0005@3; aprovada pelo Thomas em 2026-10-13 | 2026-10-13 |
| IMP-02 | 2026-10-13 | G2 | spec | CT-01 da SPEC-0005 roda a suíte inteira aninhada; ela inclui o CT-01 da SPEC-0004 (tests/Subsolo.Tooling.Tests/SddConfigCommandsContractTests.cs), que roda ./tools/build.sh (next build) ao mesmo tempo que a execução externa → 'Another next build process is already running' e test/test_integration com exit 2 (falha estável em 2 execuções) | Cada comando isolado passa; test_integration com SUBSOLO_SPEC0005_CT01_NESTED=1 passa (23 ok, 5 skipped); descartadas gambiarras de espera/ordem e lock no next build | Architect + Thomas: Emenda v4 — incluir tests/Subsolo.Tooling.Tests/SddConfigCommandsContractTests.cs nos touches da SPEC-0005 para o Test-writer pular o CT-01 da SPEC-0004 quando SUBSOLO_SPEC0005_CT01_NESTED=1 | Emenda v4 (escopo) — CT-01 da SPEC-0004 pula quando SUBSOLO_SPEC0005_CT01_NESTED=1; aprovada pelo Thomas em 2026-10-13 | 2026-10-13 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Todos os testes do plano passando e listados na Verificação
- [ ] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [ ] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [ ] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [ ] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6)
- [ ] Observabilidade e rollback prontos conforme o Plano de Rollout
- [ ] Documentação raiz e CHANGELOG atualizados (G7)
- [ ] Pendências registradas como novas specs (ou nenhuma)

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0005`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 3 (escopo, contrato inalterado) | 2026-10-14 | Emenda v5: `web/pnpm-lock.yaml` entra nos `touches` (regenerado pelo pnpm ao adicionar as devDependencies de teste em `web/package.json`, já no escopo) | G4 FAIL: verify apontou o lockfile fora dos `touches`; sem mudança de código | Nenhuma | thomas (chat, 2026-10-14) |
| 3 (escopo, contrato inalterado) | 2026-10-13 | Emenda v4: `tests/Subsolo.Tooling.Tests/SddConfigCommandsContractTests.cs` (CT-01 da SPEC-0004) entra nos `touches` para pular quando `SUBSOLO_SPEC0005_CT01_NESTED=1` (só na execução aninhada; a execução normal continua rodando-o), e `tests/Subsolo.Tooling.Tests/packages.lock.json` (regenerado pelo CPM ao adicionar pacotes) | IMP-02: o CT-01 aninhado da SPEC-0005 disparava um segundo `next build` simultâneo pelo CT-01 da SPEC-0004 | SPEC-0004 (teste de ferramentas, comportamento inalterado fora da execução aninhada) | thomas (chat, 2026-10-13) |
| 3 | 2026-10-13 | Comando `test` passa a excluir `Kind=GitHubVerification` (`--filter "Category!=Architecture&Kind!=GitHubVerification"`); essas verificações rodam sob demanda com `SDD_GITHUB_VERIFY=1` | IMP-01: as verificações reais no GitHub da SPEC-0006 falham de propósito sem a variável e deixariam a suíte padrão e o CI vermelhos | SPEC-0006 | thomas (chat, 2026-10-13) |
| 2 | 2026-10-13 | Comandos pnpm chamados de dentro de `web/` e `e2e/` em vez de `pnpm -C` (mesma regra da Emenda v2 da SPEC-0004) | `pnpm -C` a partir da raiz ignora o pnpm fixado (IMP-02 da SPEC-0004) | SPEC-0006 | thomas (chat, 2026-10-13) |
