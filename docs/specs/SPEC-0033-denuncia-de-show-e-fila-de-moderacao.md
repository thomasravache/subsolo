---
id: SPEC-0033
title: Denúncia de show e fila de moderação
tier: full
type: feature
user_facing: true
status: implemented
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0019, SPEC-0021, SPEC-0024, SPEC-0031]
consumes_contract: []
contract_version: 4
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Moderation/**, tests/Modules/Moderation/**, src/Modules/Events/**, tests/Modules/Events/**, src/Modules/Users/**, tests/Modules/Users/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/Subsolo.Api.csproj, src/Subsolo.Api/packages.lock.json, src/Subsolo.Api/appsettings.json, Subsolo.slnx, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, tests/Common/Subsolo.Api.IntegrationTests/**, docs/api/**, web/src/api/**, web/src/app/(public)/[cidade]/**, web/src/components/shows/**, web/src/components/moderation/**, web/src/app/admin/**, web/src/components/admin/**, web/src/app/(public)/conta/**, e2e/tests/moderacao/**, e2e/tests/fixtures.ts, docs/design/**, .github/workflows/ci.yml]
adrs: [ADR-0001, ADR-0003, ADR-0008, ADR-0016]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0033 — Denúncia de show e fila de moderação

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O fã logado denuncia um show pela página dele ("Reportar show": show falso, cancelado ou adiado sem aviso, informação errada ou outro, com detalhe opcional). O admin vê as denúncias na aba "Denúncias" da "Fila do admin", agrupadas por show, e decide: **Manter** (arquiva as denúncias) ou **Ocultar show** (some da agenda, das páginas públicas e dos avisos). Novo módulo `Moderation`.

## 2. Motivação & Escopo
**Motivação:** a agenda é aberta a bandas, casas e produtores aprovados; o fã é quem percebe show falso ou cancelado sem aviso. ADR-0001 já prevê o módulo `Moderation` (denúncias e fila de decisão do admin).

**Objetivos (dentro do escopo):**
- `POST /api/v1/shows/{id}/reports` (fã + X-CSRF): motivo `fake | cancelled_unannounced | wrong_info | other` e `details` opcional (até 500 caracteres; obrigatório em `other`); uma denúncia pendente por fã e show (repetir dá 409); limite de 10 denúncias por fã por dia.
- Fila: `GET /api/v1/admin/reports` (shows com denúncias pendentes, mais antigos primeiro: show, data, casa, quantidade por motivo, detalhes mais recentes) e `POST /api/v1/admin/reports/{showId}/keep` e `/hide` (com motivo interno opcional); a decisão resolve todas as pendentes do show.
- Ocultar: o `Moderation` publica `ShowHiddenByModeration(ShowId, HiddenAtUtc)`, evento definido no contrato do `Events` (padrão do consumidor, ADR-0001); o `Events` marca o show como oculto: sai da agenda e dos cards, a página pública responde 404 (ADR-0016) e o `IEventsCatalog` deixa de devolvê-lo (sem aviso nem lembrete); o dono vê em "Meus shows" o selo "Oculto pela moderação" e não pode editar.
- Tela "Reportar show" (folha aberta pelo link "Reportar" na página do show) e aba "DENÚNCIAS (n)" da "Fila do admin" (`/admin/fila`).
- Conta: seção "reports" no export (só as denúncias do próprio fã) e, no `UserDeleted`, as denúncias ficam sem autor (o histórico de moderação continua).

**Não-objetivos (fora do escopo):**
- Mostrar de novo um show oculto (vira spec se for preciso) e denúncia de perfil.
- Avisar o dono ou quem denunciou da decisão.

## 3. Dependências
- **Implementações necessárias:** SPEC-0019 (`UserDeleted` e export), SPEC-0021 (tela `/admin/fila`) e SPEC-0024 (página pública do show).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** ADR-0001 (módulo `Moderation`; evento de propriedade do consumidor; direção das dependências); `Profiles` (SPEC-0021) como implementação de referência de módulo novo com fila do admin; ADR-0016 (status pelo proxy); ADR-0003 (rate limit).

**Decisão:** módulo `Moderation` com slices `ReportShow`, `ListReports`, `KeepShow`, `HideShow`, `ExportReports`; valida o show por `IEventsCatalog.GetShowSummaryAsync` (SPEC-0031); `Moderation → Users, Events`. O `Events` ganha a coluna `hidden_at_utc` e o consumidor de `ShowHiddenByModeration`.

**Justificativa:** ADR-0001 (sem ciclos: o `Events` não depende do `Moderation`; o evento mora no contrato do `Events`), ADR-0008 (dados mínimos de quem denuncia), ADR-0003 (limite por fã).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** ocultar automaticamente depois de N denúncias (abre espaço para abuso contra uma banda); apagar o show (perde o histórico e quebra links sem explicação ao dono); denúncia anônima (abuso sem controle).

**ADRs:** ADR-0001, ADR-0003, ADR-0008, ADR-0016

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** enviar denúncia ≤ 200 ms p95; fila com 500 shows denunciados em ≤ 300 ms (IT-04).
- **Segurança:** denúncia exige `fan` + X-CSRF; fila e decisões exigem `admin` (anônimo 401, fã 403) + X-CSRF; `details` guardado como texto e mostrado escapado; limite por fã (ADR-0003).
- **Privacidade e dados pessoais:** a fila não mostra quem denunciou (só contagem e texto); export e anonimização na exclusão (IT-05).
- **Disponibilidade e resiliência:** decisão idempotente (decidir de novo um show sem pendentes dá 404); o evento de ocultar vai pela outbox.
- **Acessibilidade (UI):** a folha "Reportar show" é um diálogo com foco preso e `Esc`; motivos como grupo de rádio com rótulo; mensagens em `aria-live`; axe no E2E.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `/api/v1/shows/{id}/reports` · `/api/v1/admin/reports*` · evento `ShowHiddenByModeration` · telas "Reportar show" e aba "Denúncias"

```yaml
POST /api/v1/shows/{id}/reports:            # fan + X-CSRF + limite moderation-report (10/dia por fã)
  body: { reason: fake|cancelled_unannounced|wrong_info|other, details: string(0–500)|null }
  201: { id } ; 400 moderation.invalid_report ; 404 events.show_not_found ; 409 moderation.already_reported ; 429
GET /api/v1/admin/reports:                  # admin + default ; no-store
  200: [{ showId, title, citySlug, slug, startsAtUtc, venueName, counts: { fake, cancelled_unannounced, wrong_info, other }, latestDetails: [string] (até 3), firstReportedAtUtc }]
POST /api/v1/admin/reports/{showId}/keep:   # admin + X-CSRF ; 204 ; 404 moderation.no_pending_reports
POST /api/v1/admin/reports/{showId}/hide:   # admin + X-CSRF ; body { note: string(0–200)|null } (corpo obrigatório) ; 204 ; 400 moderation.invalid_note ; 404 moderation.no_pending_reports
```

```csharp
// Subsolo.Modules.Events.IntegrationEvents (público; publicado pelo Moderation, consumido pelo Events)
public sealed record ShowHiddenByModerationIntegrationEvent(Guid ShowId, DateTimeOffset HiddenAtUtc);
```

```text
Banco (schema moderation): reports(id, show_id, reporter_user_id NULL, reason, details, status pending|kept|hidden, created_at_utc, decided_at_utc, decided_by)
  índice único parcial (show_id, reporter_user_id) WHERE status='pending'
Banco (schema events): shows.hidden_at_utc NULL (oculto → fora da agenda, da página e do IEventsCatalog)
Página do show: link "Reportar" (logado) → folha "Reportar show"; anônimo → /entrar?returnTo=
Folha "Reportar show": "Ajude a manter a agenda do Subsolo correta." ; rádios "Show falso" / "Cancelado ou adiado e não avisaram" / "Informação errada" / "Outro" ;
  "Conte mais (opcional)" ; "Cancelar" e "Enviar denúncia" → "Obrigado. A moderação vai olhar."; repetida → "Você já denunciou este show."
/admin/fila: aba "DENÚNCIAS (n)": cartão por show (título, data • casa, "{n} denúncias" com os motivos, detalhes) e "Manter" / "Ocultar show" (confirmação)
"Meus shows": selo "Oculto pela moderação", sem "Editar"
```

**Ajustes de contrato (Emenda 1, contrato v2):**
```text
Show oculto (hidden_at_utc não nulo) é tratado como inexistente em toda leitura pública do Events: agenda da cidade, página do show (404 events.show_not_found),
  listas de shows dos perfis, imagem/metadados do show, e no IEventsCatalog (GetShowSummaryAsync → null; GetShowStateAsync/GetShowStatesAsync sem o id;
  ListScheduledShowsStartingAsync sem o show) — por isso resumo diário, aviso de show novo e lembrete da véspera também o ignoram, sem mudar o Engagement.
Só "Meus shows" do dono (GET autenticado do próprio dono) devolve o show, com hidden: true; PUT/cancelar/flyer de show oculto → 409 events.show_hidden.
Consumidor ShowHiddenByModeration no Events: idempotente (ocultar de novo mantém o primeiro hidden_at_utc).
Limite moderation-report: política nomeada no padrão do ADR-0003, 10 por fã por janela fixa de 24 h, chave = id do usuário.
Export: seção "reports" com { showId, reason, details, status, createdAtUtc } das denúncias do próprio fã.
Fila sem pendentes: 200 []. Contagem da aba "DENÚNCIAS (n)" = número de shows na fila.
```

**Ajustes de contrato (Emenda 2, contrato v3):**
```csharp
// Subsolo.Modules.Events.IntegrationEvents — interface própria (como IEventsCatalogNames), leitura em lote para a fila; ignora shows ocultos
public interface IShowHeadlines { Task<IReadOnlyDictionary<Guid, ShowHeadline>> GetAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct); }
public sealed record ShowHeadline(Guid ShowId, string Title, string CitySlug, string Slug, DateTimeOffset StartsAtUtc, string VenueName);
```
```text
moderation.reports ganha decision_note varchar(200) NULL (nota interna do /hide; nunca sai pela API nem pelo export).
GET /api/v1/shows/{id} do dono (formulário de edição) devolve o show oculto com hidden: true.
Export: sections.reports = [{ showId, reason, details, status, createdAtUtc }].
Testes de componente web (vitest) levam o ID do comportamento que sustentam: UT-01 (validação de "Outro" na folha), IT-01 (mensagens de 201/409), UT-02 (cartões agrupados), IT-03 (Manter/Ocultar e selo).
```

**Design:** Stitch, projeto 15194126668426659178: "Denunciar Show - Subsolo" (9d6f88a63299478789478394c15bdd56) e "Fila do Admin - Subsolo" (600bfe18de6e44a995790033ab0f817e, aba "Denúncias"). Ajustes: o cartão agrupa as denúncias do show (contagem por motivo) em vez de um cartão por denúncia; "Ocultar show" pede confirmação; a fila não mostra quem denunciou.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Denunciar | fã logado, motivo válido | 201; aparece na fila | UT-01, IT-01, E2E-01 |
| Denúncia inválida | motivo desconhecido; `other` sem detalhe; detalhe > 500 | 400 `invalid_report` | UT-01, IT-01 |
| Repetida | mesma pessoa, mesmo show pendente | 409 `already_reported` | IT-01, E2E-01 |
| Show inexistente ou oculto | id desconhecido | 404 `events.show_not_found` | IT-01 |
| Limite | 11ª denúncia no dia | 429 | IT-02 |
| Acesso | anônimo denuncia; fã abre a fila | 401; 403 | IT-02 |
| Fila | vários shows denunciados | agrupado por show, contagens, sem autor | UT-02, IT-03, IT-04 |
| Manter | admin mantém | denúncias `kept`; show continua; sai da fila | IT-03, E2E-01 |
| Ocultar | admin oculta | denúncias `hidden`; show fora da agenda, página 404, fora do `IEventsCatalog`; selo em "Meus shows" | IT-03, E2E-01 |
| Decidir sem pendentes | já decidido | 404 `no_pending_reports` | IT-03 |
| Conta | export; exclusão | seção "reports" só do fã; autor vira nulo | IT-05 |
| Arquitetura | dependências | `Moderation → Users, Events`; `Events` não referencia `Moderation` | IT-06 |
| Contrato | OpenAPI e evento | corpos, códigos e tipos do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — módulo novo; agenda e página do show seguem cobertas pelas SPEC-0008 e 0024, e o filtro de ocultos entra com testes próprios (IT-03).

### 7.2 Testes Unitários
- **UT-01** — Dado o validador da denúncia, quando recebe cada motivo, `other` sem detalhe, detalhe com 500 e 501 caracteres e motivo desconhecido, então só os válidos passam, com o erro do campo.
- **UT-02** — Dadas denúncias de três shows com motivos variados, quando o agrupador da fila roda, então devolve um item por show com as contagens por motivo, até três detalhes mais recentes e a ordem pela primeira denúncia.

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL e um show publicado, quando o fã denuncia, denuncia de novo, manda motivo inválido e denuncia um id inexistente, então 201, 409, 400 e 404.
- **IT-02** — Dado um fã, quando envia 11 denúncias de shows diferentes no mesmo dia, então a 11ª dá 429; anônimo dá 401 e fã na fila dá 403.
- **IT-03** — Dados dois shows denunciados, quando o admin mantém um e oculta o outro, então as denúncias mudam de status, o mantido segue na agenda, o oculto some da agenda e de `GET /api/v1/cities/{city}/shows/{slug}` (404) e de `IEventsCatalog.GetShowSummaryAsync`, "Meus shows" do dono traz `hidden: true`, editar o oculto dá 409 `events.show_hidden`, o `IEventsCatalog` não o devolve em `GetShowStatesAsync` nem em `ListScheduledShowsStartingAsync`, e decidir de novo dá 404.
- **IT-04** — Dados 500 shows com denúncias pendentes, quando o admin lista a fila, então responde em ≤ 300 ms p95 sem identificar quem denunciou.
- **IT-05** — Dado um fã com denúncias, quando o `Users` exporta e depois exclui a conta, então o export traz a seção "reports" só com as dele e, depois da exclusão, as denúncias continuam com `reporter_user_id` nulo.
- **IT-06** — Dados os testes de arquitetura, quando rodam com o `Moderation`, então as regras do ADR-0001 valem, o `Moderation` só depende de `Users` e `Events` pelos contratos e o `Events` não referencia o `Moderation`.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json` e os tipos públicos, quando lidos, então os endpoints e o evento do §6 batem, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa, no projeto mobile-chrome: o fã logado abre um show, toca em "Reportar", escolhe "Cancelado ou adiado e não avisaram", envia e vê "Obrigado. A moderação vai olhar."; tenta de novo e vê "Você já denunciou este show."; o admin abre `/admin/fila`, aba "Denúncias", vê o show com "1 denúncia", toca em "Ocultar show" e confirma; a página do show passa a mostrar a página de não encontrado e o show some da agenda. Sem violações do axe na folha e na fila. [jornada: moderacao]

### 7.6 Outros
- Segurança: G4 confere o escape do `details`, as políticas `fan`/`admin` e o limite.

**Dublês e dados de teste:** nenhum dublê de módulo; show e perfis criados pelos helpers do E2E e no banco dos IT.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL); E2E contra a pilha do Compose; Playwright com um worker.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** schema novo `moderation` e coluna `events.shows.hidden_at_utc` nula (aditivo).
- **Compatibilidade:** shows existentes ficam visíveis (coluna nula); OpenAPI só ganha endpoints.
- **Observabilidade:** contadores `subsolo.moderation.reports` (por motivo) e `subsolo.moderation.decisions` (por decisão).
- **Rollback:** reverter o merge commit; schema e coluna podem ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Ocultar automaticamente com muitas denúncias? — Não; só o admin decide (recomendação do Architect; piloto automático do Thomas, 2026-10-18)
- [x] A fila mostra quem denunciou? — Não; só contagem e texto (ADR-0008; recomendação do Architect; piloto automático do Thomas, 2026-10-18)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado").

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Regras**
- [x] Red: UT-01, UT-02 com a tag `SPEC-0033:<ID>`, falhando pelo motivo certo
- [x] Green e Refactor

**Fase 2: Módulo, ocultar e contrato**
- [x] Red: IT-01..IT-06, CT-01 falhando pelo motivo certo
- [x] Green: módulo `Moderation`, endpoints, limite, evento `ShowHiddenByModeration`, filtro de ocultos no `Events`, export e anonimização, OpenAPI e cliente TS
- [x] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Telas e jornada E2E**
- [x] Red: E2E-01 falhando pelo motivo certo
- [x] Green: folha "Reportar show", aba "Denúncias" e selo em "Meus shows"

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — e6fa00a (árvore suja) | 2026-10-18 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 7dab32e | 2026-10-24 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 70ec8c4 | 2026-10-24 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 10s 820ms) — 0712bdf | 2026-10-24 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (0 blocker/major; 3 minor e 5 nits; aviso dos lock files aceito — só referências de projeto do Moderation; H33-01..07 levados à SPEC-0037) — cadda8e | 2026-10-24 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 16m 50s 507ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 11s 905ms) — d0189b9 | 2026-10-24 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-24 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live — SPEC-0003 (deploy guiado pelo Thomas) | 2026-10-24 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 3d16fa1 (árvore suja) | 2026-10-24 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
- O fã logado denuncia um show pela página dele ("Reportar show falso ou cancelado"): show falso, cancelado ou adiado sem aviso, informação errada ou outro, com detalhe opcional; uma denúncia pendente por show e até 10 por dia.
- A "Fila do admin" ganha a aba "DENÚNCIAS (n)": um cartão por show com as contagens por motivo e os detalhes, sem mostrar quem denunciou; "Manter" arquiva as denúncias e "Ocultar show" (com confirmação e nota interna opcional) tira o show da agenda, das páginas públicas, dos avisos e do lembrete.
- O dono vê "Oculto pela moderação" em "Meus shows" e não pode editar o show oculto.
- Conta: a exportação traz as denúncias do próprio fã e, na exclusão, as denúncias ficam sem autor.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: Test-writer (fdcdaff scaffold, 2341cb2, 26eeced, 13d5fac, da85cbf, 1aa241c, 3922e9a, db73c9b; Emenda 2: b5b8d35 scaffold, e6fb9ea, 7dab32e; contrato v3 nos testes antigos: e277f7f) → Implementer (5ac0823, 16f7e3b, 35552f1, 7f9783e; lock files 0712bdf) → Reviewer: APPROVED WITH MINOR ITEMS. O primeiro G5 (b6b1a42) falhou no axe do E2E-01 por contraste das etiquetas de motivo da fila; corrigido em 772bd82 (texto sobre o par "chip ativo" do DESIGN.md, 4,96:1) e o G5 seguinte passou. O job `test` do CI passou a levar ~51 min e o limite subiu de 50 para 75 min (221ff85, Emenda sem mudança de contrato).
- Módulo novo `Moderation` (schema `moderation`, slices `ReportShow`, `ListReports`, `KeepShow`, `HideShow`, export e `UserDeleted`) seguindo o `Profiles` (SPEC-0021); evento `ShowHiddenByModerationIntegrationEvent` no contrato do `Events` (consumidor dono do contrato, ADR-0001), pela outbox, com consumidor idempotente e coluna `events.shows.hidden_at_utc`.
- `IShowHeadlines` em lote no contrato do `Events` para a fila; limite `moderation-report` com a chave do usuário lida do ticket do cookie de sessão (o limitador roda antes da autenticação).
- Telas pelo Stitch ("Denunciar Show" e "Fila do Admin"), com os ajustes do §6; desvios aceitos na revisão: texto "Reportar…", estado de resultado na folha, abas sublinhadas como as existentes (`AdminQueueTabs`) e selo reaproveitando `Badge`.
- Emendas 1–4 (contrato v4): show oculto como inexistente nas leituras públicas e no catálogo, respostas aos SPEC_DEFECTs do Test-writer e decisões do Implementer (400 `moderation.invalid_note`).

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | validador da denúncia: os quatro motivos, `other` sem detalhe (inclusive só espaços), detalhe com 500 e 501 caracteres e motivo desconhecido | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |
| UT-02 | agrupador da fila: um item por show, contagem por motivo, até três detalhes mais recentes, ordem pela primeira denúncia | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |
| IT-01 | denunciar, repetir, motivo inválido e id inexistente: 201, 409 `moderation.already_reported`, 400 `moderation.invalid_report` e 404 | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |
| IT-02 | 11ª denúncia do dia → 429 com a chave do usuário; anônimo 401; fã na fila 403; mutação sem X-CSRF recusada | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |
| IT-03 | manter e ocultar: status das denúncias, show oculto fora da agenda, da página (404), das listas dos perfis, do `IEventsCatalog` e do `IShowHeadlines`; `hidden` em "Meus shows" e no formulário; edição → 409 `events.show_hidden`; nota interna guardada e não exposta; decidir de novo → 404; consumidor idempotente | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |
| IT-04 | 500 shows denunciados: fila em ≤ 300 ms p95 sem identificar quem denunciou | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |
| IT-05 | export com a seção `reports` só do fã, no formato do §6; depois da exclusão as denúncias ficam com `reporter_user_id` nulo | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |
| IT-06 | `Moderation` só depende de `Users` e `Events` pelos contratos; `Events` não referencia `Moderation`; regras do ADR-0001 | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |
| CT-01 | endpoints e evento `ShowHiddenByModerationIntegrationEvent` e `IShowHeadlines` como no §6; snapshot OpenAPI e `pnpm api:check` | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |
| E2E-01 | fã denuncia "Cancelado ou adiado e não avisaram", vê o agradecimento e o aviso de repetida; admin oculta pela aba "Denúncias"; página do show vira não encontrado e o show some da agenda; axe sem violações (mobile-chrome) | PASS | `gate G2 --run` em 70ec8c4; `gate G5 --run` na integração (8d9be27); CI do PR #46 (run 40123772828, checks verdes em 3d16fa1) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — CI verde no PR #46 (run 40123772828)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — G6 N/A até a SPEC-0003
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout
- [x] Documentação raiz e CHANGELOG atualizados (G7) — CHANGELOG atualizado
- [x] Pendências registradas como novas specs (ou nenhuma) — ver Pendências

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente implantado (G6 N/A → SPEC-0003, guiado pelo Thomas). Entrega na `main` pelo PR #46 (https://github.com/thomasravache/subsolo/pull/46), merge commit cd65d7d em 2026-10-24, com H2 pela política auto-on-green.

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- SPEC-0037: H33-01 (IT da corrida e da nota longa), H33-02 (foco depois da decisão), H33-03 (falha da fila de denúncias isolada da aba de perfis), H33-04 (código de CSRF repetido no OpenAPI), H33-05 (denúncia tardia de show já oculto), H33-06 (fuso da cidade na fila), H33-07 (enum do motivo no OpenAPI).
## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0033`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 | 2026-10-24 | Show oculto tratado como inexistente em toda leitura pública do `Events` e em todos os métodos do `IEventsCatalog` (resumo, aviso e lembrete o ignoram); `hidden: true` só em "Meus shows"; edição de show oculto → 409 `events.show_hidden`; consumidor idempotente; forma do limite, do export e da fila vazia. | despacho do Test-writer: pontos sem decisão concreta | — | piloto automático do Thomas (2026-10-24) |
| 3 | 2026-10-24 | Retorno do Test-writer: `IShowHeadlines` em lote no contrato do `Events` para os títulos da fila; coluna `decision_note`; rota real da página no IT-03; `GET /api/v1/shows/{id}` do dono com `hidden: true`; forma da seção `reports` do export; IDs dos testes de componente. | SPEC_DEFECTs 1–6 do Test-writer | — | piloto automático do Thomas (2026-10-24) |
| 4 | 2026-10-24 | Retorno do Implementer: `/hide` com corpo obrigatório e 400 `moderation.invalid_note` (nota > 200); `hidden` obrigatório em "Meus shows" e opcional no esquema do formulário de edição; limite `moderation-report` com a chave do usuário lida do ticket do cookie de sessão (o rate limiter roda antes da autenticação); testes `SPEC-0024:H23-06` e `EveryModuleAssemblyBesideTheTestsIsEvaluated` acompanham o contrato. | relatório do Implementer | — | piloto automático do Thomas (2026-10-24) |
| 4 (sem mudança de contrato) | 2026-10-24 | Achados menores da revisão G4 (H33-01..07) vão para a SPEC-0037. | revisão G4 | SPEC-0037 | piloto automático do Thomas (2026-10-24) |
| 4 (sem mudança de contrato) | 2026-10-24 | `touches` inclui `.github/workflows/ci.yml`: os testes desta spec levaram o job `test` a ~51 min e o limite de 50 min cancelou o CI do PR #46; o limite sobe para 75 min (commit 221ff85). | CI do PR #46 (run 40113362896) | — | piloto automático do Thomas (2026-10-24) |
