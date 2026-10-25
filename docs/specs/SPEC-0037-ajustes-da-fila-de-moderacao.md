---
id: SPEC-0037
title: Ajustes da fila de moderação
tier: lite
type: fix
user_facing: false
status: implemented
created: 2026-10-24
parent: SPEC-0002
depends_on: [SPEC-0033]
consumes_contract: []
touches: [src/Modules/Moderation/**, tests/Modules/Moderation/**, src/Modules/Events/**, tests/Modules/Events/**, docs/api/**, web/src/api/**, src/Subsolo.Api/OpenApi/**, src/Subsolo.Api/Program.cs, web/src/app/admin/**, web/src/components/admin/**, e2e/tests/moderacao/**]
adrs: [ADR-0001, ADR-0003]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-24
---

# SPEC-0037 — Ajustes da fila de moderação

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados menores da revisão G4 da SPEC-0033 (itens `H33-NN`):
- **H33-01** — sem IT para a corrida no índice único parcial (duas denúncias simultâneas do mesmo fã e show) nem para `/hide` com nota de 201 caracteres (400 `moderation.invalid_note`), comportamentos das Emendas 1 e 4.
- **H33-02** — `ReportsQueue.tsx`: depois de "Manter" ou "Ocultar show" o cartão some com o foco no botão dele e o foco cai no `<body>`.
- **H33-03** — `/admin/fila`: falha em `GET /api/v1/admin/reports` derruba a página inteira, inclusive a aba "Pedidos de perfil" (SPEC-0021).
- **H33-04** — `KeepShowEndpoint.cs:29`: `ProducesProblemCode(400, CsrfInvalid)` repetido; o snapshot mostra o código duas vezes.
- **H33-05** — denúncia aceita entre a decisão de ocultar e o consumidor do `Events` fica `pending` para sempre (some da fila, mas o histórico fica sujo).
- **H33-06** — a fila formata a data do show em `America/Sao_Paulo`, não no fuso da cidade (`ShowHeadline` não traz o fuso).
- **H33-07** — `ReportShowRequest.reason` sai no OpenAPI como `string|null`, sem o enum do §6.

## 2. Causa Raiz
- H33-01: o plano da SPEC-0033 não ganhou IDs para as regras acrescentadas nas Emendas 1 e 4.
- H33-02: `decided()` remove o cartão sem decidir para onde o foco vai.
- H33-03: `web/src/app/admin/fila/page.tsx:49` trata as duas leituras como uma só.
- H33-04: `RequireModerationAdmin(mutation: true)` já declara o código.
- H33-05: `HideShow` resolve só as pendentes existentes no momento da decisão; o `ListReports` descarta shows que o `IShowHeadlines` não devolve.
- H33-06/07: `ShowHeadline` sem `TimeZone`; o esquema do pedido não anota o enum.

## 3. Mudança Proposta
- H33-01: IT com 3 POSTs simultâneos (uma 201, as outras 409, uma linha) e IT de nota com 201 caracteres.
- H33-02: depois da decisão o foco vai para o título do próximo cartão; sem cartões, para a mensagem de fila vazia.
- H33-03: a página renderiza a aba "Pedidos de perfil" e mostra "indisponível" só dentro do painel de denúncias.
- H33-04: remover a declaração repetida e regenerar o snapshot.
- H33-05: a janela é fechada na origem: o `ReportShow` recusa com 404 `events.show_not_found` a denúncia de show que o próprio `Moderation` já decidiu ocultar (há denúncia `hidden` do show), mesmo antes de o consumidor do `Events` gravar `hidden_at_utc`. O `HideShow` continua marcando como `hidden` as pendentes que existirem numa decisão nova (`no_pending_reports` para o show sem pendentes).
- H33-06: `ShowHeadline` ganha `TimeZone` (contrato interno do `Events`); o item da fila `GET /api/v1/admin/reports` ganha o campo aditivo `timeZone` (rota só do admin, compatível com o cliente atual; a lista de chaves do §6 da SPEC-0033 passa a incluí-lo), e a fila formata a data do show nesse fuso.
- H33-07: `ReportShowRequest.Reason` com `[AllowedValues]` (BCL, sem efeito em tempo de execução: a validação continua no validador do módulo) e um transformador genérico de esquema em `src/Subsolo.Api/OpenApi/` que o publica como `enum`, registrado no `Program.cs`; os módulos continuam sem pacote de OpenAPI. Depois, regenerar o snapshot e o cliente.
- **Não muda:** o limite por tentativa (ADR-0003; aceito na revisão), rotas, códigos e textos das telas. A única mudança de contrato é o campo aditivo `timeZone` do H33-06.

**Padrão seguido:** testes e endpoints da SPEC-0033; `MyShowResponse.timeZone` (SPEC-0024, H23-06); o foco após a decisão é novo (o `ProfileRequestsQueue` da SPEC-0021 não move o foco).

**Rollback:** reverter o merge commit.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área coberta pelos testes da SPEC-0033.
- **UT-01** — Dada a fila com dois cartões, quando o admin mantém o primeiro, então o foco vai para o título do segundo; quando decide o último, para a mensagem de fila vazia. (H33-02)
- **UT-02** — Dada a página `/admin/fila` com a leitura de denúncias falhando, quando renderizada, então a aba "Pedidos de perfil" aparece com os pedidos e o painel de denúncias mostra o aviso de indisponível. (H33-03)
- **IT-01** — Dado um fã e um show, quando três denúncias chegam ao mesmo tempo, então uma resposta é 201, as outras 409 `moderation.already_reported`, e há uma linha; e `/hide` com nota de 201 caracteres dá 400 `moderation.invalid_note`. (H33-01)
- **IT-02** — Dado um show oculto com uma denúncia pendente que chegou depois da decisão, quando o admin decide de novo, então a pendente vira `hidden`; e `IShowHeadlines` devolve o fuso da cidade. (H33-05, H33-06)
- **CT-01** — Dado o snapshot OpenAPI, quando lido, então `ReportShowRequest.reason` é o enum dos quatro motivos e o `security.csrf_invalid` do `keep` aparece uma vez; o item da fila tem `timeZone` string obrigatório. (H33-04, H33-06, H33-07)
- **UT-03** — Dada a fila com um show em `America/Manaus` às 23h30 UTC, quando renderizada, então a data e a hora do cartão saem no fuso de Manaus (19h30), não no de São Paulo. (H33-06)
- **IT-03** — Dado um show com denúncia já decidida como `hidden` no `Moderation` e ainda visível no `Events` (consumidor não rodou), quando outro fã denuncia, então 404 `events.show_not_found` e nenhuma linha nova; e o item da fila traz `timeZone` da cidade. (H33-05, H33-06)
- E2E: N/A — sem jornada nova; o E2E da denúncia na SPEC-0033 segue cobrindo a fila.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

H1 delegado: piloto automático do Thomas até o deploy (2026-10-24, "pode seguir no piloto automático com o recomendado").

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
- [x] Red: UT-01, UT-02, IT-01, IT-02 e CT-01 com a tag `SPEC-0037:<ID>`, falhando pelo motivo certo
- [x] Green: testes de corrida e nota, foco, painel isolado, declaração única, pendentes tardias, fuso e enum
- [x] Validar: build + suíte + arquitetura (G2/G3)
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — d74b734 (árvore suja) | 2026-10-24 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 4775103 | 2026-10-24 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 5da13a5 | 2026-10-24 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 11s 472ms) — 5da13a5 | 2026-10-24 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (H37-01 nulo no transformador genérico e H37-02 janela de milissegundos entre ocultar e denunciar vão para a SPEC-0038; H37-03 resolvido com a Emenda 5 da SPEC-0033) — 17bb540 | 2026-10-24 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 17m 52s 965ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 11s 216ms) — 6612ce9 | 2026-10-24 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-25 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live — SPEC-0003 (deploy guiado pelo Thomas) | 2026-10-25 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — acfa2f1 (árvore suja) | 2026-10-25 |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
- Na fila do admin, depois de "Manter" ou "Ocultar show" o foco vai para o próximo cartão (ou para a mensagem de fila vazia).
- Se as denúncias não carregam, a aba "Pedidos de perfil" continua funcionando e só o painel de denúncias mostra o aviso de indisponível.
- O cartão mostra a data e a hora do show no fuso da cidade (`timeZone` aditivo no item de `GET /api/v1/admin/reports`).
- A denúncia de um show que o `Moderation` já decidiu ocultar é recusada com 404 mesmo antes de o `Events` registrar a ocultação.
- OpenAPI: `reason` publicado como enum dos quatro motivos e o código de CSRF do `keep` uma vez só.

### Como foi feito
- TDD com papéis separados: Test-writer (c512ebd scaffold, 92829e1, 01e3e23; Emenda 1: 9e0b93d, 5ebc0d0; fixture 7321541) → Implementer (ae4c89e, 024ce1f, 950b6fb, 92b2e9f, 5da13a5) → Reviewer: APPROVED WITH MINOR ITEMS.
- `IReportRepository.IsHiddenAsync` no `ReportShow`; `ShowHeadlines` com junção em lote nas cidades, no molde do `ShowsReadModel`; data no fuso da cidade com o `cityTimeParts` existente.
- `AllowedValuesSchemaTransformer` genérico em `src/Subsolo.Api/OpenApi/` lendo o `[AllowedValues]` da BCL: os módulos seguem sem pacote de OpenAPI e sem efeito em tempo de execução.
- Emenda 1: H33-05 fechado na origem, `timeZone` aditivo (Emenda 5 da SPEC-0033), `touches` do OpenAPI da API. O primeiro G1 falhou porque a linha "E2E: N/A" citava um ID de teste da SPEC-0033; reescrita e o G1 seguinte passou.

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
- Antes: no G1 a suíte saiu com código 2 — UT-01 (foco em `<body>`), UT-02 (página inteira "Painel indisponível"), UT-03 (sem 19h30), IT-02 (`TimeZone` vazio), IT-03 (201 em vez de 404; item sem `timeZone`) e CT-01 (`reason` `string|null`, CSRF duas vezes). IT-01 e a denúncia tardia do IT-02 já passavam (cobrem comportamento existente). Depois: todos passam em 5da13a5 (G2) e na integração (1fa93c9, G5).

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | depois de "Manter" o foco vai para o título do próximo cartão; ao decidir o último, para a mensagem de fila vazia | PASS | `gate G2 --run` em 5da13a5; `gate G5 --run` na integração (1fa93c9); CI do PR #48 (run 40157140981, checks verdes em acfa2f1) |
| UT-02 | `/admin/fila` com a leitura de denúncias falhando mostra a aba "Pedidos de perfil" e o aviso de indisponível só no painel de denúncias | PASS | `gate G2 --run` em 5da13a5; `gate G5 --run` na integração (1fa93c9); CI do PR #48 (run 40157140981, checks verdes em acfa2f1) |
| UT-03 | show em `America/Manaus` às 23h30 UTC aparece às 19h30 no cartão da fila | PASS | `gate G2 --run` em 5da13a5; `gate G5 --run` na integração (1fa93c9); CI do PR #48 (run 40157140981, checks verdes em acfa2f1) |
| IT-01 | três denúncias simultâneas do mesmo fã: uma 201, duas 409 `moderation.already_reported`, uma linha; `/hide` com nota de 201 caracteres → 400 `moderation.invalid_note` | PASS | `gate G2 --run` em 5da13a5; `gate G5 --run` na integração (1fa93c9); CI do PR #48 (run 40157140981, checks verdes em acfa2f1) |
| IT-02 | denúncia pendente tardia de show oculto vira `hidden` numa decisão nova; `IShowHeadlines` devolve o fuso da cidade | PASS | `gate G2 --run` em 5da13a5; `gate G5 --run` na integração (1fa93c9); CI do PR #48 (run 40157140981, checks verdes em acfa2f1) |
| IT-03 | denúncia de show que o `Moderation` já ocultou, antes do consumidor do `Events`, → 404 `events.show_not_found` sem linha nova; item da fila com `timeZone` | PASS | `gate G2 --run` em 5da13a5; `gate G5 --run` na integração (1fa93c9); CI do PR #48 (run 40157140981, checks verdes em acfa2f1) |
| CT-01 | snapshot OpenAPI: `reason` como enum dos quatro motivos, `security.csrf_invalid` do `keep` uma vez, `timeZone` obrigatório no item da fila | PASS | `gate G2 --run` em 5da13a5; `gate G5 --run` na integração (1fa93c9); CI do PR #48 (run 40157140981, checks verdes em acfa2f1) |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5) — CI verde no PR #48 (run 40157140981)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — G6 N/A até a SPEC-0003
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7) — CHANGELOG atualizado
- [x] Outras ocorrências registradas como novas specs (ou nenhuma) — ver Pendências (SPEC-0039)

### Deploy
Sem ambiente implantado (G6 N/A → SPEC-0003, guiado pelo Thomas). Entrega na `main` pelo PR #48 (https://github.com/thomasravache/subsolo/pull/48), merge commit 0bbf84b em 2026-10-24, com H2 pela política auto-on-green.

### Pendências
- SPEC-0039: H37-01 (nulo preservado no transformador genérico) e H37-02 (bloqueio por show entre denúncia e decisão). H37-03 resolvido na Emenda 5 da SPEC-0033.
## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 | 2026-10-24 | H33-05 fechado na origem (o `ReportShow` recusa show já ocultado pelo `Moderation`); H33-06 com o campo aditivo `timeZone` no item da fila do admin; padrão do foco corrigido; UT-03 e IT-03 novos, CT-01 cobre `timeZone`. | SPEC_DEFECTs 1–3 do Test-writer | SPEC-0033 (lista de chaves do item da fila) | piloto automático do Thomas (2026-10-24) |
| 1 (sem mudança adicional de contrato) | 2026-10-24 | H33-07: `touches` inclui `src/Subsolo.Api/OpenApi/**` e `src/Subsolo.Api/Program.cs` para o transformador genérico de `[AllowedValues]` → `enum` (o pacote de OpenAPI fica só na API). | relatório do Implementer (alternativa no módulo exigiria 13 lock files) | — | piloto automático do Thomas (2026-10-24) |
