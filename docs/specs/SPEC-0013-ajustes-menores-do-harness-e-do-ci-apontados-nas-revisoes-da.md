---
id: SPEC-0013
title: Ajustes menores do harness e do CI apontados nas revisões das SPEC-0005 e SPEC-0006
tier: lite
type: fix
user_facing: false
status: approved
created: 2026-10-14
parent: SPEC-0001
depends_on: []
consumes_contract: []
touches: [tests/README.md, CODEOWNERS, .github/CODEOWNERS, tools/coverage.sh, tools/lint.sh, e2e/static-server.mjs, e2e/package.json, e2e/pnpm-lock.yaml, e2e/tsconfig.json, .github/workflows/ci.yml, tests/Subsolo.Tooling.Tests/**, e2e/tests/**]
adrs: [ADR-0005, ADR-0006, ADR-0009]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-16
approved_via: "delegado: autorização de piloto automático (2026-10-16, seguir as recomendações)"
---

# SPEC-0013 — Ajustes menores do harness e do CI apontados nas revisões das SPEC-0005 e SPEC-0006

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
As revisões independentes (G4) das SPEC-0005 (harness de testes) e SPEC-0006 (CI) aprovaram as entregas e deixaram estes achados minor:
1. `tools/coverage.sh` usa `mapfile` (só existe no bash 4+; o macOS vem com bash 3.2) e expande um array possivelmente vazio sob `set -u`.
2. `tools/coverage.sh` só confere se a base (`origin/main`) existe no fim, depois de rodar toda a suíte (minutos perdidos).
3. `e2e/static-server.mjs` chama `decodeURIComponent` fora de um `try`: uma URL malformada (ex.: `/%E0%A4%A`) derruba o servidor dos testes E2E em vez de responder 400.
4. A pasta `e2e/` (TypeScript do Playwright) fica fora do `tools/lint.sh`: erro de tipo ali só aparece ao rodar os testes.
5. CI (`ci.yml:25-27`): no push na `main`, o grupo de concorrência por `github.ref` deixa o GitHub cancelar um run que está esperando; com merges próximos, alguns commits da `main` ficam sem CI.
6. CI (`ci.yml`, jobs web e e2e): o cache do pnpm usa a mesma chave para `web/` e `e2e/`, que guardam pacotes diferentes; um sempre perde o cache.
7. IT-05 da SPEC-0006 (`GitHubCiVerificationTests.cs:156`) aceita a anotação genérica do próprio workflow ("SDD pr-check"), então passa mesmo se o `pr-check` não explicar o motivo da falha.
8. A supressão de falso positivo do CodeQL por comentário no código nunca foi confirmada para C# e TypeScript com a suíte padrão.

Da revisão das Emendas v6 (SPEC-0005) e v4 (SPEC-0006), em 2026-10-16:
9. `tests/README.md` mostra `test_commands_contract` sem `--max-parallel-test-modules 1` (o sdd-config tem).
10. Nada impede por teste que `test`, `test_integration` ou o filtro padrão do `coverage.sh` voltem a selecionar `Kind=CommandContract` (recursão) nem que o CI rode `test_commands_contract`.
11. O UT-02 da SPEC-0006 não trata `test_commands_contract` como dependente de Node/pnpm.
12. Com `tools/sdd/**` isento de escopo, um PR pode alterar o próprio verificador do gate sem sinal; falta CODEOWNERS ou checagem de que a cópia bate com a versão vendorizada.
13. O comentário de `CiWorkflowCommandsContractTests.cs` ainda cita o contrato SPEC-0005@2 (o atual é @4).
14. A chave do cache do pnpm agora é compartilhada por quatro jobs (agrava o item 6).

## 2. Causa Raiz
Itens 1–4: escritos e validados só no Linux/bash 5 e no caminho feliz. Itens 5–6: padrões de concorrência e cache copiados para todos os jobs sem considerar push na `main` nem os dois lockfiles. Item 7: asserção fraca demais. Item 8: verificação ficou para depois da primeira execução no GitHub.

## 3. Mudança Proposta
- `tools/coverage.sh`: trocar `mapfile` por leitura em laço compatível com bash 3.2, proteger a expansão de arrays vazios e conferir a base logo no início (falha rápida com a mesma mensagem de hoje).
- `e2e/static-server.mjs`: `decodeURIComponent` dentro de `try`; URL malformada responde 400.
- `e2e/`: `typescript` (mesma versão do `web/`, ADR-0006) como devDependency, script `lint` (`tsc --noEmit`) e `tsconfig.json`; `tools/lint.sh` ganha o passo `e2e-lint`.
- `ci.yml`: grupo de concorrência `ci-<número do PR ou github.sha>` (único por commit no push); chaves de cache `pnpm-store-web-…` e `pnpm-store-e2e-…`, cada uma com o hash do próprio lockfile.
- IT-05 da SPEC-0006: ignorar a anotação "SDD pr-check" e exigir uma anotação que cite a spec ou a regra violada.
- CodeQL: confirmar a supressão por comentário; se não funcionar, documentar no `ci.yml` o caminho real (`query-filters` num config do CodeQL, com o motivo).
- `tests/README.md`: comando `test_commands_contract` literal; comentário do contrato atualizado para SPEC-0005@4.
- Teste de política: filtros de `test`, `test_integration` e o padrão do `coverage.sh` não selecionam `Kind=CommandContract`; nenhum job do CI roda `test_commands_contract`; o UT-02 da SPEC-0006 passa a incluir essa chave.
- `CODEOWNERS` com `tools/sdd/**`, `docs/specs/sdd-config.yml` e `.github/workflows/**` exigindo o dono do repositório.
- Não muda: os contratos das SPEC-0004/0005/0006 (comandos do `sdd-config` iguais), os jobs do CI e a política de auditoria.

**Padrão seguido:** `tests/Subsolo.Tooling.Tests` (CiPolicy e testes de ferramentas) e os scripts existentes em `tools/`.

**Rollback:** revert do merge commit.

**Outras ocorrências:** `ArchitectureRules.OnlyReferences` sem teste próprio vai para a SPEC-0007 (que cria as regras do monólito sobre ele). O tempo do comando `test` (~4 min por causa dos testes de contrato aninhados) fica como questão para decidir na SPEC-0007, quando a suíte crescer.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área já coberta pelos testes das SPEC-0004/0005/0006.
- **UT-01** — Dado o `ci.yml`, quando a política de CI é avaliada, então o grupo de concorrência usa `github.sha` fora de PR e os caches do pnpm de `web/` e `e2e/` têm chaves distintas, cada uma com o hash do próprio lockfile (falha antes da correção).
- **IT-01** — Dado o servidor estático do E2E rodando, quando recebe `GET /%E0%A4%A`, então responde 400 e continua respondendo 200 em `/` (falha antes: o processo cai).
- **IT-02** — Dado um repositório sem a base `origin/main`, quando `tools/coverage.sh` roda, então termina com exit ≠ 0 e a mensagem da base em poucos segundos, sem rodar a suíte (falha antes da correção).
- **IT-03** — Dado o `tools/coverage.sh`, quando analisado, então não usa `mapfile`/`readarray` e roda num shell bash 3.2 compatível (checagem estática; falha antes da correção).
- **IT-04** — Dado um arquivo `.ts` em `e2e/` com erro de tipo, quando `tools/lint.sh` roda, então o passo `e2e-lint` falha (falha antes da correção).
- **UT-02** — Dados o sdd-config, o `coverage.sh` e o `ci.yml`, quando a política é avaliada, então nenhum filtro de `test`, `test_integration` ou do coverage seleciona `Kind=CommandContract` e nenhum job roda `test_commands_contract` (guarda: passa hoje, falha se alguém reintroduzir).
- **UT-03** — Dado o repositório, quando o `CODEOWNERS` é lido, então `tools/sdd/**`, `docs/specs/sdd-config.yml` e `.github/workflows/**` têm dono (falha antes da correção).
- IT-05 da SPEC-0006 fortalecido (anotação genérica não basta) e a confirmação do CodeQL rodam sob demanda com `SDD_GITHUB_VERIFY=1`, como as demais verificações no GitHub.

## 5. Questões em Aberto
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1ed3cf7 | 2026-10-14 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
