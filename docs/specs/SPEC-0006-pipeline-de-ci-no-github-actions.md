---
id: SPEC-0006
title: Pipeline de CI no GitHub Actions
tier: full
type: foundation
user_facing: false
status: approved
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0004]
consumes_contract: [SPEC-0005@2]
contract_version: 1
touches: [.github/workflows/**, tests/Subsolo.Tooling.Tests/CiPolicy/**]
adrs: [ADR-0004, ADR-0005, ADR-0006, ADR-0007, ADR-0009]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0006 — Pipeline de CI no GitHub Actions

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Cria o pipeline de CI no GitHub Actions, que roda em todo PR e em todo push na `main`: build, lint, testes unitários, de integração, de arquitetura e E2E, varredura de segurança (vulnerabilidades, segredos e SAST) e o job `sdd` (validate + pr-check). Os checks tornam-se obrigatórios na proteção da `main`. Nada de deploy: isso fica no épico de go-live (SPEC-0003).

## 2. Motivação & Escopo
**Motivação:** o CI é o portão final que funciona mesmo quando o agente esquece o processo; sem ele, a `main` deixa de ser confiável.

**Objetivos (dentro do escopo):**
- `.github/workflows/ci.yml` com os jobs `build`, `lint`, `test` (unitário + arquitetura), `integration` (Testcontainers no runner Ubuntu), `e2e` (Playwright contra a aplicação subida via Aspire/Compose a partir da SPEC-0008; até lá, contra o exemplo da SPEC-0005), `security` (`dotnet list package --vulnerable --include-transitive`, `pnpm audit --audit-level=high`, gitleaks, Semgrep CE), `codeql` (C# e TypeScript, gratuito no repositório público, ADR-0006) e `sdd` (vendor `--ci github`).
- Cache de NuGet, pnpm, navegadores do Playwright e imagens Docker.
- Artefatos de falha: trace/vídeo do Playwright, relatório de cobertura e TRX.
- `concurrency` para cancelar execuções antigas do mesmo PR.
- Checks obrigatórios configurados na proteção da `main` (ADR-0007).
- Comando `ci` do sdd-config: `gh pr checks --watch`.

**Não-objetivos (fora do escopo):**
- Build e publicação de imagens e deploy (SPEC-0003).
- Varreduras pagas ou serviços externos de qualidade (ADR-0012).

## 3. Dependências
- **Implementações necessárias:** SPEC-0004 — repositório no GitHub, `tools/sdd` e comandos de build/lint.
- **Contratos consumidos:** SPEC-0005@1 — os comandos `test`, `test_integration`, `arch_test`, `test_e2e` e `coverage` do sdd-config.
- **Pré-requisitos externos:** GitHub Actions habilitado no repositório (gratuito; ADR-0004/ADR-0012).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0004 (GitHub Actions), ADR-0005, ADR-0006, ADR-0007 e ADR-0009.

**Decisão:** um workflow `ci.yml` com jobs paralelos que chamam **os mesmos comandos do sdd-config** usados localmente; nenhuma lógica de build existe só no YAML.

**Justificativa:** o que passa localmente passa no CI, e o `gate --run` e o CI usam a mesma fonte.

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** um job sequencial único (lento, feedback ruim); runners self-hosted na VPS (custo e segurança; ADR-0012).

**ADRs:** ADR-0004, ADR-0005, ADR-0006, ADR-0007, ADR-0009

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** feedback completo de um PR em ≤ 15 min (p50) — medido pela duração das execuções no G5; consumo estimado < 1.500 min/mês, dentro do plano gratuito para repositório privado (ilimitado se público).
- **Segurança:** permissões mínimas do `GITHUB_TOKEN` (`contents: read`, `pull-requests: read`; `security-events: write` só no job `codeql`); actions de terceiros fixadas por SHA; nenhum segredo usado no CI de PR — verificado no IT-03.
- **Privacidade e dados pessoais:** N/A — o CI usa somente dados sintéticos.
- **Disponibilidade e resiliência:** jobs com `timeout-minutes`; reexecução manual permitida, mas nunca retry automático de teste (ADR-0005).
- **Acessibilidade (UI):** N/A — sem tela; o axe roda dentro do job `e2e`.
- **Custo:** zero no plano gratuito (ver Desempenho).

## 6. Artefato A — Contrato
**Interface:** `workflow ci.yml: checks obrigatórios do PR`

```text
on: pull_request (main), push (main), workflow_dispatch
jobs (nomes = checks obrigatórios na proteção da main):
  build        → ./tools/build.sh (contrato v2 da SPEC-0004)
  lint         → ./tools/lint.sh
  test         → sdd-config.test + arch_test + coverage (publica o relatório)
  integration  → sdd-config.test_integration
  e2e          → sdd-config.test_e2e (artefato: playwright-report, traces)
  security     → dotnet list package --vulnerable --include-transitive (falha em High/Critical)
                 pnpm audit --audit-level=high ; gitleaks detect ; semgrep ci --config .semgrep.yml
  codeql       → github/codeql-action (csharp, javascript-typescript); alerta High/Critical novo falha o check
  sdd          → python3 tools/sdd/spec_graph.py validate --exclude-drafts && pr-check (PR_TITLE/PR_BODY)
Saídas: status por job; anotações no PR (sdd, semgrep)
Erros: qualquer job com falha → check vermelho → merge bloqueado pela proteção
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| PR saudável | PR com código e testes válidos | todos os jobs verdes | IT-01 |
| Teste falhando | PR com um teste vermelho | job `test` falha e o merge fica bloqueado | IT-02 |
| Segredo no PR | arquivo com chave fictícia | job `security` falha com o achado do gitleaks | IT-03 |
| Dependência vulnerável | pacote com CVE alta | job `security` falha | IT-04 |
| SAST encontra falha | código com injeção de SQL de exemplo | job `codeql` (e Semgrep) falha | IT-06 |
| PR sem spec aprovada | título sem SPEC ou spec em `proposed` | job `sdd` falha com anotação | IT-05 |
| Configuração do workflow | `ci.yml` | jobs, permissões mínimas e actions fixadas por SHA | UT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o `ci.yml`, quando analisado por um teste de política (parse do YAML em `tests/Subsolo.Tooling.Tests`), então contém os 8 jobs nomeados no contrato, `permissions` mínimas, `timeout-minutes` em todo job e todas as actions de terceiros fixadas por SHA de 40 caracteres.

### 7.3 Testes de Integração
- **IT-01** — Dado um PR de verificação com uma mudança válida, quando o CI roda no GitHub, então os 8 checks terminam verdes (evidência: URL da execução).
- **IT-02** — Dado um PR de verificação com um teste propositalmente falhando, quando o CI roda, então o check `test` fica vermelho e o botão de merge fica bloqueado pela proteção.
- **IT-03** — Dado um PR de verificação com uma chave fictícia, quando o CI roda, então o check `security` falha citando o arquivo.
- **IT-04** — Dado um PR de verificação que adiciona um pacote com vulnerabilidade alta conhecida, quando o CI roda, então o check `security` falha.
- **IT-06** — Dado um PR de verificação com uma consulta SQL montada por concatenação de entrada do usuário, quando o CI roda, então o check `codeql` (e o Semgrep no `security`) reporta a falha e fica vermelho.
- **IT-05** — Dado um PR cujo título não cita uma spec aprovada, quando o CI roda, então o check `sdd` falha com uma anotação explicando o motivo.

### 7.4 Testes de Contrato
- **CT-01** — Os jobs `test`, `integration`, `e2e` e o comando `coverage` chamam exatamente os comandos publicados no sdd-config pela SPEC-0005 (o teste de política compara o YAML com o sdd-config).

### 7.5 Testes E2E
- N/A — nenhuma jornada de usuário (UI ou API pública) é afetada; o CI é uma ferramenta interna.

### 7.6 Outros
- N/A

**Dublês e dados de teste:** branches de verificação descartáveis (`ci-check/*`), fechadas sem merge após registrar a evidência.

**Ambiente de execução:** GitHub Actions (`ubuntu-latest`), com Docker disponível para Testcontainers.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — o workflow passa a valer no merge.
- **Dados/schema:** N/A
- **Compatibilidade:** N/A
- **Observabilidade:** duração e taxa de falha dos jobs no histórico do Actions (base das métricas DORA, ADR-0004).
- **Rollback:** reverter o merge commit; a proteção da branch pode ser relaxada temporariamente pelo Thomas, com registro na spec.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Workflow de CI**
- [ ] Red: UT-01 e CT-01 (teste de política do `ci.yml` e comparação com o sdd-config) falhando
- [ ] Green: `.github/workflows/ci.yml` com os 8 jobs do contrato, caches, artefatos, `concurrency`, permissões mínimas, actions fixadas por SHA; job `sdd` via `vendor --ci github`
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte (G2)

**Fase 2: Verificação no GitHub**
- [ ] IT-01..IT-06 com PRs de verificação descartáveis (`ci-check/*`), registrando a URL de cada execução
- [ ] Checks obrigatórios configurados na proteção da `main`; comando `ci` no sdd-config

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
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0006`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 | 2026-10-13 | Job `build` chama `./tools/build.sh`; passa a consumir SPEC-0005@2 (comandos pnpm de dentro de `web/`/`e2e/`). Contrato desta spec inalterado. | Emenda v2 da SPEC-0004 / IMP-02 | — | thomas (chat, 2026-10-13) |
