---
id: SPEC-0029
title: Vou com contagem
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0027]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Engagement/**, tests/Modules/Engagement/**, src/Modules/Events/Subsolo.Modules.Events.IntegrationEvents/**, src/Modules/Events/Subsolo.Modules.Events.Infrastructure/**, tests/Modules/Events/**, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, docs/api/**, web/src/api/**, web/src/app/(public)/[cidade]/**, web/src/app/(public)/banda/**, web/src/app/(public)/casa/**, web/src/app/(public)/produtor/**, web/src/components/engagement/**, web/src/components/agenda/**, web/src/components/shows/**, web/src/components/profiles/**, e2e/tests/vou/**, docs/design/**, web/src/components/ui/Header.tsx, web/src/components/ui/AppShell.tsx, web/src/components/ui/BottomNav.tsx, web/src/components/ui/__tests__/**, src/Modules/Profiles/Subsolo.Modules.Profiles.IntegrationEvents/**, src/Modules/Profiles/Subsolo.Modules.Profiles.Application/**, src/Modules/Profiles/Subsolo.Modules.Profiles.Infrastructure/**, tests/Modules/Profiles/**, web/src/components/ui/Card.tsx, e2e/tests/seguir/**, e2e/tests/shows/show-page.spec.ts]
adrs: [ADR-0001, ADR-0008]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0029 — Vou com contagem

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O fã logado marca "Vou" num show (na página do show e nos cards da agenda e dos perfis) e pode desmarcar. A contagem "X vão" aparece para todos; quem vai não aparece para ninguém (só a contagem é pública, ADR-0008). O "vou" é a base do lembrete na véspera.

## 2. Motivação & Escopo
**Motivação:** objetivo do MVP "Botão vou com contagem" e regra do ADR-0008 "a lista de quem marcou vou não é pública: só a contagem aparece".

**Objetivos (dentro do escopo):**
- `Attendance` no módulo `Engagement` (usuário, show, criado em; único por usuário e show).
- `PUT /api/v1/me/attendances/{showId}` e `DELETE` (idempotentes, 204); `GET /api/v1/me/attendances?period=upcoming` (shows que o fã marcou).
- A contagem `AttendingCount` do resumo de show (contrato da SPEC-0008, hoje sempre 0) passa a vir do `Engagement` por uma interface pública `IAttendanceApi.CountAsync(showIds)`; a agenda, a página do show e o perfil público mostram "X vão".
- Show cancelado: o "vou" fica registrado, mas o botão some e a contagem continua visível; show passado: botão some.
- `IAttendanceApi.GetAttendeesAsync(showId)` (ids) para a spec do lembrete, que mora no próprio `Engagement` e entrega pelo `INotificationsApi.EnqueueAsync` (ADR-0014 e ADR-0001 esclarecidos em 2026-10-18); seção "attendances" no export e remoção no `UserDeleted`.
- Botão "Vou" / "Vou ✓" (`aria-pressed`) com contagem; anônimo → `/entrar?returnTo=…`.

**Não-objetivos (fora do escopo):**
- Lista de quem vai (nunca pública).
- Aviso a quem marcou "vou" quando o show muda (fora do MVP, épico "se der tempo").
- Convite para instalar o PWA depois do primeiro "vou" (spec de push).

## 3. Dependências
- **Implementações necessárias:** SPEC-0027 (módulo `Engagement`) e SPEC-0024/0026 (página do show e perfil público onde o botão aparece).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** `Engagement` (SPEC-0027); resumo de show da SPEC-0008 já tem `AttendingCount`; regra de sem ciclos do ADR-0001.

**Decisão:** o `Events` precisa da contagem para montar a agenda, mas o `Engagement` já depende do `Events` (valida cidade pelo `IEventsCatalog`). Para não criar ciclo, a contagem não entra pelo `Events`: o web pede a contagem ao `Engagement` (`GET /api/v1/attendances/counts?showIds=…`) e junta com a agenda no servidor. O `Engagement` valida o show por uma leitura pública do `Events` (`IEventsCatalog.GetShowStateAsync`, situação e data).

**Justificativa:** ADR-0001 (sem ciclos; leitura por interface pública), ADR-0008 (só a contagem é pública).

**Desvio do padrão existente:** Nenhum (o `AttendingCount` do contrato da agenda continua existindo e vem 0 do `Events`; o web usa a contagem do `Engagement` — documentado no §6).

**Alternativas descartadas:** `Events` consultar o `Engagement` (ciclo); contagem denormalizada no `Events` por evento (mais peças para o MVP; reavaliar se a agenda ficar lenta).

**ADRs:** ADR-0001, ADR-0008

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** contagem de 50 shows em ≤ 50 ms p95 (IT-03); marcar/desmarcar ≤ 100 ms p95.
- **Segurança:** marcar exige `fan` + `X-CSRF`; rate limit `default`; contagens públicas só com números.
- **Privacidade e dados pessoais:** quem vai é pessoal: não aparece para ninguém; export e exclusão (IT-04).
- **Disponibilidade e resiliência:** contagem indisponível → página sem a contagem, nunca erro (IT-05); corrida de dois PUT → um registro.
- **Acessibilidade (UI):** `aria-pressed`, nome "Vou ao show {título}", mudança e nova contagem anunciadas; axe no E2E.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `/api/v1/me/attendances*` · `GET /api/v1/attendances/counts` · `IAttendanceApi` · botões

```yaml
PUT    /api/v1/me/attendances/{showId}:   # fan + X-CSRF + default ; 204 (idempotente)
  404: engagement.show_not_found ; 409: engagement.show_not_attendable (cancelado, adiado ou já passou)
DELETE /api/v1/me/attendances/{showId}:   # fan + X-CSRF + default ; 204 (idempotente)
GET    /api/v1/me/attendances?period=upcoming:   # fan + default ; no-store ; 200 { showIds: uuid[] }
GET    /api/v1/attendances/counts?showIds=a,b,c: # AllowAnonymous + default ; até 100 ids ; 200 { counts: { "<showId>": n } } ; 400 engagement.invalid_query
```

```csharp
// Subsolo.Modules.Engagement.IntegrationEvents (público)
public interface IAttendanceApi {
    Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct);
    Task<IReadOnlyCollection<Guid>> GetAttendeesAsync(Guid showId, CancellationToken ct);
}
// Subsolo.Modules.Events.IntegrationEvents — IEventsCatalog ganha:
Task<ShowState?> GetShowStateAsync(Guid showId, CancellationToken ct);   // record ShowState(string Status, DateTimeOffset StartsAtUtc)
```

```text
Banco (schema engagement): attendances(user_id, show_id, created_at_utc; PK(user_id, show_id)); índice por show_id
Página do show: botão "VOU" / "VOU ✓" + "{n} vão" ; passado → só "{n} foram" sem botão; cancelado ou adiado → só "{n} vão" sem botão
Cards (agenda e perfil): "{n} vão" com o ícone do Stitch ; o web busca as contagens em lote
Anônimo tocando "Vou" → /entrar?returnTo=/{cidade}/{show}
```

**Design:** Stitch, projeto 15194126668426659178: "Página do Show - Noite da Peste II" (98cc07285ec5439197d3273da0f7a11d, botão "Vou" e contagem) e "Agenda Curitiba - Subsolo" (5d1946463ad9484b83c67cc2e6d78fd3, "47 vão" nos cards). Ajustes: sem "Quem vai" com nomes ou fotos (só a contagem, ADR-0008).

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Marcar vou | fã, show futuro agendado | 204; contagem +1; aparece em `/me/attendances` | UT-01, IT-01, E2E-01 |
| Desmarcar | DELETE | 204; contagem −1 | IT-01, E2E-01 |
| Repetir ou corrida | dois PUT iguais | 204 e um registro só | IT-02 |
| Show não marcável | cancelado; já passou; inexistente | 409 `show_not_attendable` / 404 `show_not_found` | UT-01, IT-01 |
| Contagens em lote | até 100 ids | números certos; >100 ou id inválido → 400 | UT-02, IT-03 |
| Anônimo | toca "Vou" | `/entrar?returnTo=…`; API 401 | IT-02, E2E-02 |
| Privacidade | outros usuários | nunca veem quem vai; export e exclusão | IT-04 |
| Contagem indisponível | `Engagement` fora | página e agenda sem a contagem, sem erro | IT-05 |
| Lembrete | `GetAttendeesAsync` | ids de quem marcou | UT-02, IT-03 |
| Contrato | OpenAPI e interfaces | corpos, códigos e assinaturas do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- **CH-01** — Dada a agenda atual, quando lida, então `attendingCount` vem 0 em todos os shows (guarda do contrato da SPEC-0008: passa antes da mudança e continua 0 vindo do `Events`).

### 7.2 Testes Unitários
- **UT-01** — Dado um catálogo de teste com shows agendado, cancelado e passado, quando o fã marca, então só o agendado futuro aceita; repetir não duplica; cancelado e passado dão `show_not_attendable`; inexistente `show_not_found`.
- **UT-02** — Dados registros de vários usuários, quando `CountAsync` e `GetAttendeesAsync` são chamados, então devolvem as contagens e os ids certos, sem repetição.

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL e shows reais, quando o fã marca, lista e desmarca, então 204, a lista e as contagens refletem cada passo; cancelado e passado dão 409; inexistente 404.
- **IT-02** — Dados quatro PUT iguais em paralelo e chamadas anônimas ou sem `X-CSRF`, quando processados, então um registro só, 401 e 400.
- **IT-03** — Dados 50 shows com contagens variadas, quando `GET /attendances/counts` é chamado, então números certos em ≤ 50 ms p95; 101 ids → 400.
- **IT-04** — Dado um fã com "vou" marcados, quando outro usuário consulta qualquer rota pública, então não há ids nem nomes; `GET /me/export` traz a seção "attendances"; o `UserDeleted` apaga os registros e a contagem cai.
- **IT-05** — Dada a contagem indisponível (simulada), quando a página do show e a agenda são pedidas, então 200 sem a contagem.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json` e os tipos públicos, quando lidos, então os endpoints e as interfaces têm o §6, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa com um show futuro, nos projetos mobile-chrome e mobile-safari: o fã logado abre a página do show, toca em "Vou" (vira "Vou ✓", contagem +1 anunciada), volta à agenda e vê a contagem no card; desmarca e a contagem volta. Sem violações do axe. [jornada: seguir-e-avisar]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: anônimo tocando em "Vou" vai para `/entrar?returnTo=…`; um show cancelado não mostra o botão. [jornada: seguir-e-avisar]

### 7.6 Outros
- **Itens herdados da revisão da SPEC-0027 (G4, tag `SPEC-0029:H27-NN`):**
  - H27-01 — `FollowButton.tsx`: dependências do efeito por valores primitivos (`target.kind` e `target.slug ?? target.id`), sem refazer `GET /me/follows` a cada re-render da agenda.
  - H27-02 — botões de seguir e "Deixar de seguir": `aria-disabled` e cliques ignorados durante a requisição, em vez de `disabled` (não perder o foco do teclado).
  - H27-03 — `FollowButton`: a contagem acompanha a prop `followers` quando ela muda.
  - H27-04 — nome acessível do botão de seguir (WCAG 2.5.3, rótulo no nome): nome constante "Seguir {nome}" com `aria-pressed`, texto visível "Seguindo" quando seguido; vale também para o botão "Vou" desta spec.
  - H27-05 — link "Seguindo" no cabeçalho do desktop (`Header.tsx`) e a aba ativa calculada em `AppShell.currentDestination`, não no `BottomNav`.
  - H27-06 — `ProfileReader`/`IProfilesApi`: busca em lote (`GetManyAsync`) para a lista "Seguindo" e a exportação, sem uma chamada por perfil.
  - H27-07 — teste do H26-04 também ao meio-dia UTC (sonda de no máximo um dia; `from` final na data da cidade).
  - H27-08 — captura da agenda no `docs/design/stitch/` atualizada com o botão "SEGUIR CURITIBA". Movido para a SPEC-0003 §8 (Emenda de 2026-10-20): a tela do Stitch ainda não tem o botão e só é editada com o Thomas.
  - H27-09 — follows de perfil que deixa de ser aprovado ou é excluído: não contam em `GetFollowersAsync` (ou são removidos), com teste; hoje ficam ocultos só na lista.
- Privacidade: G4 confere que nenhuma resposta pública expõe quem vai.

**Dublês e dados de teste:** catálogo de teste no UT; shows criados pela API nos IT e no E2E.

**Ambiente de execução:** IT com Testcontainers; E2E contra a pilha do Compose; Playwright com um worker.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** tabela nova `engagement.attendances` (aditiva).
- **Compatibilidade:** `attendingCount` do contrato da agenda continua 0 no `Events`; o web usa a contagem do `Engagement` (documentado).
- **Observabilidade:** contadores `subsolo.engagement.attendances` (marcar/desmarcar); logs com ids.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] De onde vem a contagem da agenda sem criar ciclo? — O web pede ao `Engagement` em lote e junta com a agenda no servidor (recomendação do Architect; piloto automático do Thomas, 2026-10-18)
- [x] "Vou" em show cancelado? — O registro fica, o botão some e a contagem continua (recomendação do Architect; piloto automático do Thomas, 2026-10-18)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"); telas do Stitch já existentes.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 0: Caracterização**
- [ ] CH-01 escrito, passando no código atual, commitado antes de qualquer mudança

**Fase 1: Domínio**
- [ ] Red: UT-01, UT-02 com a tag `SPEC-0029:<ID>`, falhando pelo motivo certo
- [ ] Green e Refactor

**Fase 2: API e contrato**
- [ ] Red: IT-01..IT-05, CT-01 falhando pelo motivo certo
- [ ] Green: tabela, endpoints, `IAttendanceApi`, leitura do estado do show, export e exclusão, OpenAPI e cliente TS
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Botões e jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [ ] Green: botão na página do show, contagem nos cards

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 7cf550a | 2026-10-18 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 025f357 | 2026-10-20 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 241087e | 2026-10-20 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 8s 932ms) — 241087e | 2026-10-20 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (0 bloqueadores/majors; decisões 1–11 e o aviso do verify aceitos; H29-01..03 levados à SPEC-0032 §7.6; IT-05 por testes web com a API simulada fora do ar, registrado na Verificação) — f617ab8 | 2026-10-20 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0029`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (sem mudança de contrato) | 2026-10-20 | `touches` ganha `web/src/components/ui/Card.tsx` (card aceita contagem ausente, IT-05) e `e2e/tests/seguir/**` (nome acessível constante "Seguir {nome}" do H27-04 muda o E2E da SPEC-0027); §4 cita `GetShowStateAsync`, como no §6; show adiado também dá 409 `show_not_attendable` (só o agendado futuro aceita, como no UT-01), e o "vou" já marcado continua contando; rótulo "{n} foram" no show passado e "{n} vão" no cancelado ou adiado. | SPEC_DEFECTs do Test-writer no G1 | — | piloto automático do Thomas (2026-10-17) |
| 1 (sem mudança de contrato) | 2026-10-20 | `touches` ganha `e2e/tests/shows/show-page.spec.ts` (o E2E da página do show da SPEC-0024 afirma que não há botão "Vou", o que esta spec passa a ter); H27-08 sai desta spec e vai para a SPEC-0003 §8, pois a tela "Agenda Curitiba - Subsolo" do Stitch ainda não tem o botão "SEGUIR CURITIBA" e o Stitch só é editado com o Thomas. | TEST_DEFECT e lacuna do Implementer | SPEC-0003 | piloto automático do Thomas (2026-10-17) |
