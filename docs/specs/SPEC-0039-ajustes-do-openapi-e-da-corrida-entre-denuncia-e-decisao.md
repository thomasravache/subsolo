---
id: SPEC-0039
title: Ajustes do OpenAPI e da corrida entre denúncia e decisão
tier: lite
type: fix
user_facing: false
status: implemented
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
- [x] Red: UT-01 e IT-01 com a tag `SPEC-0039:<ID>`, falhando pelo motivo certo
- [x] Green: nulo preservado no transformador; bloqueio por show na denúncia e na decisão
- [x] Validar: build + suíte + arquitetura (G2/G3)
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 7ed7690 (árvore suja) | 2026-10-24 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — d68d8f3 | 2026-10-25 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 8776b06 | 2026-10-25 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 11s 064ms) — 8776b06 | 2026-10-25 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (H39-01 reason omitido e H39-02 espera no bloqueio provada por pg_stat_activity, testes em af102af; H39-03 tag Kind=Unit mantida pela convenção do plano) — af102af | 2026-10-25 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 17m 09s 252ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 11s 968ms) — 2ef2dd1 | 2026-10-25 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-25 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live — SPEC-0003 (deploy guiado pelo Thomas) | 2026-10-25 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 59090f2 (árvore suja) | 2026-10-25 |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
- O transformador genérico do OpenAPI mantém o `null` de propriedades anuláveis; o documento publicado não muda.
- Denúncia e decisão de ocultar se serializam por show: uma denúncia que chega durante a ocultação espera e recebe 404, sem ficar `pending` para trás.

### Como foi feito
- TDD com papéis separados: Test-writer (bd95871, d68d8f3; itens da revisão H39-01/02 em af102af) → Implementer (b4de042, 8776b06) → Reviewer: APPROVED WITH MINOR ITEMS.
- Anulabilidade lida do `JsonPropertyInfo` (`IsGetNullable`/`IsSetNullable`); `ReportShowRequest.Reason` passa a `string` não anulável (Emenda 1), sem efeito em tempo de execução.
- `IShowLocks`/`ShowLocks` no molde do `FlyerLocks` do `Events`: transação explícita com `pg_advisory_xact_lock(hashtextextended('moderation.show:' || show_id::text, 0))` antes de qualquer leitura, cobrindo o `SaveChanges` e a outbox.

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
- Antes: no G1 o UT-01 falhou (tipo `string` em vez de `["null","string"]`) e o IT-01 respondeu 201 sem esperar a decisão em curso. Depois: passam em 8776b06 (G2) e na integração (15ffdd5, G5).

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | propriedade `string?` com `[AllowedValues("a","b")]` sai `string \| null` com `null` no enum; `string` sai só `string`; `reason` da denúncia segue `string` com os quatro valores, sem nulo | PASS | `gate G2 --run` em 8776b06; `gate G5 --run` na integração (15ffdd5); CI do PR #50 (run 40176707506, checks verdes em 59090f2) |
| IT-01 | denúncia durante uma ocultação em curso espera o bloqueio do show (visto em `pg_stat_activity`) e, depois do commit, é 404 `events.show_not_found` sem linha nova; `reason` omitido → 400 `moderation.invalid_report` | PASS | `gate G2 --run` em 8776b06; `gate G5 --run` na integração (15ffdd5); CI do PR #50 (run 40176707506, checks verdes em 59090f2) |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5) — CI verde no PR #50 (run 40176707506)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — G6 N/A até a SPEC-0003
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7) — CHANGELOG atualizado
- [x] Outras ocorrências registradas como novas specs (ou nenhuma) — nenhuma

### Deploy
Sem ambiente implantado (G6 N/A → SPEC-0003, guiado pelo Thomas). Entrega na `main` pelo PR #50 (https://github.com/thomasravache/subsolo/pull/50), merge commit 8a3d9d4 em 2026-10-25, com H2 pela política auto-on-green.

### Pendências
- Nenhuma (H39-01 e H39-02 resolvidos nesta spec; H39-03 mantido pela convenção dos IDs do plano).
## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (sem mudança de contrato público) | 2026-10-25 | H37-01: `ReportShowRequest.Reason` vira `string` não anulável (o transformador usa a anulabilidade do tipo); H37-02: fórmula da chave do bloqueio fixada. | SPEC_DEFECT do Test-writer (nulo do `reason` × nulo preservado) | — | piloto automático do Thomas (2026-10-25) |
