---
id: SPEC-0039
title: Ajustes do OpenAPI e da corrida entre denúncia e decisão
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-24
parent: SPEC-0002
depends_on: [SPEC-0037]
consumes_contract: []
touches: [src/Modules/Moderation/**, tests/Modules/Moderation/**, src/Subsolo.Api/OpenApi/**, tests/Common/Subsolo.Api.IntegrationTests/**]
adrs: [ADR-0001, ADR-0003]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-24
---

# SPEC-0039 — Ajustes do OpenAPI e da corrida entre denúncia e decisão

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados menores da revisão G4 da SPEC-0037 (itens `H37-NN`):
- **H37-01** — o `AllowedValuesSchemaTransformer` troca o tipo por `string` e perde o `null` de uma propriedade `string?` de verdade. Hoje só o `reason` o usa, e ele não aceita nulo, mas o transformador é genérico.
- **H37-02** — entre o `IsHiddenAsync` do `ReportShow` e o commit do `HideShow` ainda há uma janela de milissegundos (READ COMMITTED): uma denúncia pode entrar `pending` depois que a decisão listou as pendentes.

## 2. Causa Raiz
- H37-01: `src/Subsolo.Api/OpenApi/AllowedValuesSchemaTransformer.cs:27` sobrescreve o tipo inteiro.
- H37-02: `ReportShowCommandHandler.cs:27` e o `HideShow` não se serializam por show.

## 3. Mudança Proposta
- H37-01: o transformador mantém o `null` quando a propriedade é anulável (tipo `string | null` e `null` no `enum`); o `reason` do `ReportShowRequest` passa a ser `string` não anulável em C# (o contrato honesto: o validador já recusa nulo), então continua `string` sem nulo no documento e o snapshot não muda; `reason` ausente ou nulo continua dando 400 `moderation.invalid_report` com o campo.
- H37-02: `ReportShow` e `HideShow` tomam `pg_advisory_xact_lock(hashtextextended('moderation.show:' || show_id::text, 0))` na mesma transação, antes das leituras; a denúncia espera a decisão em curso e vê o show já oculto (404 `events.show_not_found`).
- **Não muda:** rotas, códigos, textos, o limite por tentativa (ADR-0003) e o documento OpenAPI atual.

**Padrão seguido:** `ProblemDetailsCodeSchemaTransformer` e seus testes; `QueueAdjustmentsApiTests` e `ModerationDatabase` (SPEC-0037).

**Rollback:** reverter o merge commit.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0039:<ID>`. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — área coberta pelos testes das SPEC-0033 e 0037.
- **UT-01** — Dado um request de teste com propriedade `string?` anotada com `[AllowedValues("a", "b")]`, quando o esquema é transformado, então o tipo é `string | null` e o `enum` é `a`, `b` e `null`; com `string` não anulável, o tipo é só `string` e o `enum` só `a`, `b`. (H37-01)
- **IT-01** — Dado um show com denúncia pendente e uma transação aberta que segura o bloqueio do show e marca as denúncias como `hidden`, quando outro fã denuncia ao mesmo tempo, então a resposta só sai depois do commit e é 404 `events.show_not_found`, sem linha nova `pending`. (H37-02)
- E2E: N/A — sem jornada nova.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

H1 delegado: piloto automático do Thomas até o deploy (2026-10-23, "pode continuar de onde parou no modo automático com o recomendado até chegarmos na parte do deploy").

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
- [ ] Red: UT-01 e IT-01 com a tag `SPEC-0039:<ID>`, falhando pelo motivo certo
- [ ] Green: nulo preservado no transformador; bloqueio por show na denúncia e na decisão
- [ ] Validar: build + suíte + arquitetura (G2/G3)
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 7ed7690 (árvore suja) | 2026-10-24 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — d68d8f3 | 2026-10-25 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 8776b06 | 2026-10-25 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 11s 064ms) — 8776b06 | 2026-10-25 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (H39-01 reason omitido e H39-02 espera no bloqueio provada por pg_stat_activity, testes em af102af; H39-03 tag Kind=Unit mantida pela convenção do plano) — af102af | 2026-10-25 |
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
| 1 (sem mudança de contrato público) | 2026-10-25 | H37-01: `ReportShowRequest.Reason` vira `string` não anulável (o transformador usa a anulabilidade do tipo); H37-02: fórmula da chave do bloqueio fixada. | SPEC_DEFECT do Test-writer (nulo do `reason` × nulo preservado) | — | piloto automático do Thomas (2026-10-25) |
