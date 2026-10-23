---
id: SPEC-0034
title: Atualizar o Next.js para corrigir avisos de segurança
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-23
parent: SPEC-0002
depends_on: []
consumes_contract: []
touches: [web/package.json, web/pnpm-lock.yaml, web/src/next-version.test.ts]
adrs: [ADR-0009]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-23
---

# SPEC-0034 — Atualizar o Next.js para corrigir avisos de segurança

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
O web usa `next@16.4.0` (e `eslint-config-next@16.4.0`). Em 2026-10-23, `pnpm audit --prod` aponta três avisos de segurança nessa versão, corrigidos a partir da 16.4.1:
- GHSA-2464-fhr4-w9xw (crítico): execução remota de código sem autenticação na API de otimização de imagens com SVG;
- GHSA-673q-mjm6-r5xr (crítico): execução remota de código sem autenticação em funções `use cache` que devolvem typed arrays;
- GHSA-p3m5-59mp-jv5r (alto): negação de serviço no App Router com Server Actions.

**Impacto:** o job "Vulnerabilidades do front — produção High/Critical (bloqueante)" do CI (`pnpm audit --prod --audit-level=high`, ADR-0009) passa a reprovar todo PR, e o `next build`/`next dev` interrompe a primeira execução de cada comando com o aviso de segurança.

**Esperado:** o web numa versão do Next sem esses avisos e o CI verde.

## 2. Causa Raiz
Versão fixada `16.4.0` em `web/package.json` (ADR-0009: versões exatas), publicada antes dos avisos. Não há defeito no código do Subsolo; a correção é a atualização de patch.

## 3. Mudança Proposta
- `next` e `eslint-config-next` de `16.4.0` para `16.4.2` (exatos, ADR-0009), com o `web/pnpm-lock.yaml` regenerado. A 16.4.2 (2026-10-15) é o patch mais recente com pelo menos uma semana de publicação; a 16.4.3 saiu no próprio dia 2026-10-23 e fica para o Dependabot.
- **Não muda:** React, demais dependências, configuração do Next, código das páginas.

**Padrão seguido:** dependências fixadas em versão exata no `web/package.json` (ADR-0009); teste de configuração do web em `web/src/next-config.test.ts` (SPEC-0015).

**Rollback:** revert do commit.

**Outras ocorrências:** nenhuma — `pnpm audit --prod` não aponta outros pacotes de produção.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — a suíte do web, a do .NET e os E2E existentes cobrem o comportamento e rodam no G2/G5.
- **UT-01** — Dado o pacote `next` instalado no web, quando `web/src/next-version.test.ts` lê a versão de `next/package.json` e a de `eslint-config-next`, então a versão do `next` é pelo menos 16.4.1 (primeira com a correção dos três avisos) e o `eslint-config-next` tem a mesma versão do `next`.
- Integração/E2E: N/A — sem mudança de comportamento; a suíte E2E completa roda no G5 e o job de vulnerabilidades do CI confere os avisos.

## 5. Questões em Aberto
- [x] Qual versão adotar? — 16.4.2, o patch com a correção e pelo menos uma semana de publicação (recomendação do Architect; piloto automático do Thomas, 2026-10-23)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

H1 delegado: piloto automático do Thomas até o deploy (2026-10-23, "pode continuar de onde parou no modo automático com o recomendado até chegarmos na parte do deploy"); correção de segurança achada durante a SPEC-0030.

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Atualização do Next**
- [ ] Red: UT-01 com a tag `SPEC-0034:UT-01`, falhando pelo motivo certo
- [ ] Green: `next` e `eslint-config-next` 16.4.2 e lock regenerado
- [ ] Validar: build + suíte + arquitetura (G2/G3) e `pnpm audit --prod --audit-level=high` sem achados

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 7a0a6ef | 2026-10-23 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 61db63c | 2026-10-23 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — e60dd7b | 2026-10-23 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 7s 959ms) — e60dd7b | 2026-10-23 |
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
