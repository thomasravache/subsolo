---
id: SPEC-0031
title: Aviso de show novo (push imediato e resumo diário por e-mail)
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0023, SPEC-0027, SPEC-0030]
consumes_contract: []
contract_version: 3
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Engagement/**, tests/Modules/Engagement/**, src/Modules/Events/**, tests/Modules/Events/**, src/Subsolo.Api/appsettings.json, src/Subsolo.Api/appsettings.Development.json, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, tools/compose-up.sh, src/Subsolo.AppHost/**, e2e/tests/avisos/**, e2e/tests/fixtures.ts, web/src/components/profiles/**, src/Modules/Notifications/**, tests/Modules/Notifications/**, infra/seq/alerts.json, docs/runbooks/**, tests/Observability/Subsolo.Observability.UnitTests/**, web/src/components/notifications/**, docs/api/openapi.v1.json, web/src/api/schema.d.ts, src/**/packages.lock.json, tests/Modules/Profiles/Subsolo.Profiles.UnitTests/Infrastructure/ProfilesHarness.cs, web/src/api/public-profile.ts, web/src/api/public-profile-contract.test.ts, tests/Security/**, src/Common/Subsolo.Common.Presentation/Security/AntiforgeryFilter.cs, tests/Modules/Profiles/**]
adrs: [ADR-0001, ADR-0008, ADR-0014]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0031 — Aviso de show novo (push imediato e resumo diário por e-mail)

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Quando um show novo é publicado, quem segue a cidade, a banda ou a casa recebe um push na hora ("Show novo em Curitiba") e, se pediu, um resumo diário por e-mail às 9h com os shows novos das últimas 24 horas. O `Engagement` decide quem recebe (seguidores) e monta os avisos; o `Notifications` entrega pelos canais e respeita as preferências (SPEC-0030).

## 2. Motivação & Escopo
**Motivação:** é o motivo de seguir (SPEC-0027) e o principal retorno para o fã voltar à agenda; ADR-0014 (show novo: push imediato, e-mail em resumo diário, no máximo um por dia).

**Objetivos (dentro do escopo):**
- O `Engagement` consome `ShowPublishedIntegrationEvent` (SPEC-0023) pela inbox (idempotente), busca o resumo do show (`IEventsCatalog.GetShowSummaryAsync`, novo) e os seguidores da cidade e dos perfis da line-up (`GetFollowersAsync`, SPEC-0027), e chama `INotificationsApi.EnqueueAsync` com `Notice(new_show, "new_show:{showId}", "Show novo em {Cidade}", "{atrações} · {dia e hora no fuso da cidade} · {casa}", "/{cidade}/{slug}")`.
- Não avisa show já começado nem show que, no processamento, já não está `scheduled`.
- Registro curto `engagement.published_shows(show_id, city_slug, profile_ids, published_at_utc)` para o resumo; linhas com mais de 7 dias são apagadas pelo job.
- Job do Quartz `DailyDigest` às 9h (America/Sao_Paulo; `Engagement:DigestCron` configurável): para cada seguidor dos shows publicados nas últimas 24 h ainda `scheduled` e futuros, um `Notice(digest, "digest:{data}", "Shows novos de quem você segue", lista de até 20 shows + "e mais {n} na agenda", "/")`. O `Notifications` só manda por e-mail a quem ligou o resumo.

**Não-objetivos (fora do escopo):**
- Inscrição, preferências, canais e descadastro: SPEC-0030.
- Aviso de mudança de data ou cancelamento para quem segue (só o lembrete trata isso, spec seguinte).
- Horário de silêncio para o push.

## 3. Dependências
- **Implementações necessárias:** SPEC-0023 (`ShowPublished`), SPEC-0027 (follows) e SPEC-0030 (`EnqueueAsync`, preferências e canais).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** ADR-0014 com o esclarecimento de 2026-10-18 (o `Engagement` decide quem recebe; o `Notifications` entrega); ADR-0001 (inbox/outbox; `Engagement → Users, Events, Profiles, Notifications`); Quartz no `Common.Infrastructure`.

**Decisão:** slice `NotifyNewShow` (consumidor do evento) e job `DailyDigest` no `Engagement`; `IEventsCatalog` ganha `GetShowSummaryAsync`. O texto do aviso é montado no `Engagement` com o fuso da cidade; o `Notifications` não conhece shows.

**Justificativa:** ADR-0014 (imediato por push, e-mail em resumo, idempotência por usuário + chave), ADR-0001 (sem ciclos; consulta pública no contrato do `Events`).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** o `Notifications` consultar seguidores (ciclo, ADR-0001); um e-mail por show (lota a caixa, ADR-0014); guardar a lista do resumo por usuário (dado pessoal sem necessidade; o resumo é recalculado a partir dos follows).

**ADRs:** ADR-0001, ADR-0008, ADR-0014

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** do evento ao `EnqueueAsync` para 10.000 seguidores em ≤ 5 s (IT-01); resumo para 10.000 seguidores em ≤ 60 s (IT-04).
- **Segurança:** nenhuma entrada pública nova; o texto do aviso usa só dados públicos do show.
- **Privacidade e dados pessoais:** `published_shows` não guarda dados de usuários; o aviso não cita outros fãs (ADR-0008).
- **Disponibilidade e resiliência:** inbox idempotente; o job pode rodar de novo no mesmo dia sem duplicar (chave `digest:{data}`); falha no meio retoma na próxima execução.
- **Acessibilidade (UI):** e-mail do resumo com texto alternativo em texto puro e link claro (o modelo é o da SPEC-0030).
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** consumidor de `ShowPublishedIntegrationEvent` · job `DailyDigest` · `IEventsCatalog.GetShowSummaryAsync` · avisos `new_show` e `digest`

```csharp
// Subsolo.Modules.Events.IntegrationEvents — IEventsCatalog ganha:
Task<ShowSummary?> GetShowSummaryAsync(Guid showId, CancellationToken ct);
public sealed record ShowSummary(Guid ShowId, string Slug, string CitySlug, string CityName, string CityTimeZone,
    IReadOnlyList<string> Lineup, string VenueName, DateTimeOffset StartsAtUtc, string Status);
```

```text
Aviso new_show (push): título "Show novo em {CityName}" ; corpo "{Lineup separado por ' + '} · {ddd dd/MM HH'h'mm no fuso} · {VenueName}" ; url "/{CitySlug}/{Slug}" ; chave "new_show:{ShowId}"
Aviso digest (e-mail): título "Shows novos de quem você segue" ; corpo uma linha por show "{dd/MM} · {Lineup} · {VenueName}, {CityName}" (até 20, ordenados pela data) + "e mais {n} na agenda" ; url "/" ; chave "digest:{yyyy-MM-dd em America/Sao_Paulo}"
Banco (schema engagement): published_shows(show_id PK, city_slug, profile_ids uuid[], published_at_utc) ; índice por published_at_utc
Config: Engagement:DigestCron (padrão "0 0 9 * * ?", fuso America/Sao_Paulo)
```

**Ajustes de contrato herdados (Emenda 1, contrato v2):**

```text
PUT /api/v1/me/notification-preferences:   # SPEC-0030; os quatro booleanos passam a ser obrigatórios
  400: notifications.invalid_preferences    # campo ausente ou não booleano
POST /api/v1/notifications/unsubscribe/one-click?token=…:   # RFC 8058; público, sem X-CSRF (o token assinado é a autorização), rate limit default, no-store
  corpo application/x-www-form-urlencoded "List-Unsubscribe=One-Click" ; 204 ; 400 notifications.invalid_token
E-mail de aviso (resumo e lembrete): cabeçalhos
  List-Unsubscribe: <{base}/api/v1/notifications/unsubscribe/one-click?token={token}>
  List-Unsubscribe-Post: List-Unsubscribe=One-Click
GET /api/v1/shows?profileId=…&period=…:   # ListProfileShows (SPEC-0026); cada item ganha flyerId (string | null, o mesmo id de 64 caracteres do upload), aditivo
Notice.Url: caminho do site começando com "/" (como "/curitiba/noite-da-peste-ii"); o Notifications o guarda assim, o push o abre na origem do site e o e-mail o resolve contra App:PublicUrl
deliveries.status ganha skipped: entrega sem canal do destinatário (sem aparelho, usuário excluído, sem e-mail), com o evento NoticeDeliverySkipped
Banco (schema notifications): job NoticesRetention diário apaga deliveries concluídas (sent, failed, skipped) e notices sem entrega pendente com mais de 90 dias e registra NoticesRetentionCompleted {Deliveries} {Notices}
Falha do job DailyDigest: evento DailyDigestRunFailed, incluído no alerta "Falha de job" do Seq e num alerta próprio "Resumo diário falhou" (event.name = 'DailyDigestRunFailed', 1 h, count >= 1, runbook docs/runbooks/outbox-parada.md), porque o job roda uma vez por dia
One-click: o endpoint sai do filtro de X-CSRF do grupo por uma marcação explícita com motivo no Common.Presentation (AntiforgeryFilter)
```

**Design:** N/A — push do sistema e e-mail no modelo da SPEC-0030; sem tela nova.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Show novo | `ShowPublished` de um show futuro em Curitiba com as bandas A e B | `EnqueueAsync` com os seguidores únicos da cidade, de A e de B, aviso `new_show` com o texto do §6 | UT-01, IT-01 |
| Evento repetido | o mesmo evento entregue duas vezes | um aviso por usuário | IT-02 |
| Show já começado ou fora de `scheduled` | início no passado; cancelado antes do processamento | nenhum aviso | IT-03 |
| Sem seguidores | ninguém segue | nada enfileirado | UT-02 |
| Resumo diário | shows publicados nas últimas 24 h | um e-mail por fã que ligou o resumo, com os shows dele | UT-02, IT-04, IT-05, E2E-01 |
| Resumo repetido | job roda duas vezes no dia | um e-mail só | IT-05 |
| Resumo sem nada | nenhum show novo para o fã | nenhum e-mail | UT-02 |
| Falha do resumo | job `DailyDigest` falha (ex.: banco fora) | `DailyDigestRunFailed` no log e alerta "Falha de job" | IT-07 |
| Limpeza | `published_shows` com mais de 7 dias | apagadas | IT-04 |
| Arquitetura | dependências | `Engagement` só nas direções do ADR-0001; `Notifications` sem módulos de domínio | IT-06 |
| Contrato | `IEventsCatalog` | assinatura do §6 | CT-01 |
| Preferências incompletas | `PUT` sem um dos quatro campos | 400 `notifications.invalid_preferences` | H30-03, CT-01 |
| Descadastro em um clique | `POST …/unsubscribe/one-click` do provedor de e-mail | 204 e o tipo desligado; token adulterado → 400 | H30-04, CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — consumidor, job e consulta novos; a criação do show segue coberta pela SPEC-0023.

### 7.2 Testes Unitários
- **UT-01** — Dado um `ShowSummary` de "Noite da Peste II" com Lineup ["Cova Rasa", "Peste Negra"], Casa "Basement", início 2026-11-15T01:00Z e fuso America/Sao_Paulo, quando o compositor monta o aviso, então título "Show novo em Curitiba", corpo "Cova Rasa + Peste Negra · sáb 14/11 22h00 · Basement" e url "/curitiba/noite-da-peste-ii".
- **UT-02** — Dados follows de quatro fãs e shows publicados (dois em Curitiba, um cancelado, um já começado e 22 em São Paulo; linha do resumo com a line-up unida por " + ", uma linha por show separada por quebra de linha e "e mais {n} na agenda" como última linha), quando o montador do resumo roda, então cada fã recebe só os shows dele, sem repetição, até 20 com "e mais {n} na agenda", e quem não tem show novo fica de fora.

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL e 10.000 seguidores de Curitiba e 300 de uma banda (parte em comum), quando um artista cria um show pela API, então o evento é consumido e o `notifications.deliveries` recebe uma entrega push por fã com push ativo e o aviso ligado, em ≤ 5 s do evento.
- **IT-02** — Dado o mesmo `ShowPublished` entregue duas vezes à inbox, quando processado, então não há entregas duplicadas.
- **IT-03** — Dados um evento de show com início no passado e um de show cancelado antes do processamento, quando consumidos, então nenhuma entrega é criada.
- **IT-04** — Dados 10.000 fãs com o resumo ligado que seguem cidades com shows novos e linhas antigas em `published_shows`, quando o job roda, então as entregas `digest` são criadas em ≤ 60 s e as linhas com mais de 7 dias somem.
- **IT-05** — Dados dois fãs com o resumo ligado e o Mailpit real, quando o job roda duas vezes no mesmo dia, então cada um recebe um e-mail com os seus shows, o link da agenda e o link de descadastro, e não recebe um segundo.
- **IT-07** — Dado o job `DailyDigest` com o banco fora do ar, quando roda, então registra `DailyDigestRunFailed`, e o alerta "Falha de job" de `infra/seq/alerts.json` inclui esse evento, além do alerta próprio "Resumo diário falhou" (`count >= 1` em 1 h).
- **IT-06** — Dados os testes de arquitetura, quando rodam, então o `Engagement` só depende de `Users`, `Events`, `Profiles` e `Notifications` pelos contratos, e o `Notifications` não referencia o `Engagement`.

### 7.4 Testes de Contrato
- **CT-01** — Dado o tipo público `IEventsCatalog`, quando lido por reflexão, então `GetShowSummaryAsync` e `ShowSummary` têm a forma do §6; o snapshot OpenAPI muda só pelos ajustes herdados do §6 (400 `notifications.invalid_preferences` no PUT de preferências, `POST /api/v1/notifications/unsubscribe/one-click` e `flyerId` nos shows do perfil), e `pnpm api:check` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a pilha do Compose com `Engagement:DigestCron` a cada minuto: o fã logado segue Curitiba e liga "Resumo diário por e-mail"; um artista publica um show em Curitiba; em até 2 minutos chega ao Mailpit o e-mail "Shows novos de quem você segue" com o show; o link do e-mail abre a agenda do site e o de descadastro confirma. [jornada: seguir-e-avisar]

### 7.6 Outros
- **Itens herdados da revisão da SPEC-0030 (G4, tag `SPEC-0031:H30-NN`):**
  - H30-01 — Dado um endpoint `https://wns2-bl2p.notify.windows.com/w/?token=…` com chaves válidas, quando o `PushSubscriptionValidator` valida, então aceita (WNS do Edge no Windows, mesma regra de sufixo de host exato `.notify.windows.com`); `https://notify.windows.com.evil.example/…` e `http://…notify.windows.com/…` continuam recusados.
  - H30-02 — Dadas entregas `sent`, `failed` e `skipped` e avisos sem entrega pendente com mais de 90 dias, além de uma entrega `pending` antiga e linhas recentes, quando o job `NoticesRetention` roda, então só as concluídas antigas e os avisos antigos sem pendência somem, e o log registra `NoticesRetentionCompleted` com as contagens de entregas e de avisos apagados.
  - H30-03 — Dado `PUT /api/v1/me/notification-preferences` com um dos quatro campos ausente ou não booleano, quando enviado, então 400 `notifications.invalid_preferences` e nada muda; o painel "Avisos" do web continua mandando os quatro campos.
  - H30-04 — Dado um e-mail de aviso (resumo ou lembrete), quando enviado ao Mailpit, então tem `List-Unsubscribe` com a URL `…/api/v1/notifications/unsubscribe/one-click?token=…` e `List-Unsubscribe-Post: List-Unsubscribe=One-Click`; um `POST` nessa URL com `List-Unsubscribe=One-Click`, sem X-CSRF, desliga aquele tipo (204); token adulterado → 400 `notifications.invalid_token`. A página `/avisos/descadastrar` não muda.
  - H30-05 — Dado `infra/seq/alerts.json`, quando o teste lê o alerta "Falha de job", então o filtro inclui `NoticeDeliveriesRunFailed` junto de `OutboxProcessingFailed`.
  - H30-06 — Dada uma entrega sem canal do destinatário (sem aparelho, usuário excluído ou sem e-mail), quando o `DeliverySender` processa, então ela termina com status `skipped` e o evento `NoticeDeliverySkipped` (não `NoticeDeliveryFailed`) e fica fora do alerta "Falha no envio de aviso".
  - H30-07 — Dado um fã com linha de preferências e `newShowPush` desligado cujo aparelho o servidor perdeu (410), quando o painel reenvia a inscrição ao abrir, então `newShowPush` continua desligado; na primeira inscrição de quem não tem linha, ele liga.
- **Itens herdados da revisão da SPEC-0026 (G4):**
  - H26-01 — Dado `ListProfileShowsEndpoint`, quando o teste de arquitetura e a suíte rodam, então não há `.Cast<ProfileShowResponse>()`: `ProfileShowsResponse.Shows` e `IShowsReadModel.ListByProfileAsync` já são tipados como `ProfileShowResponse`, e as respostas da API não mudam.
  - H26-02 — Dados `UploadFlyerCommandHandler` e `CleanupFlyersCommandHandler`, quando construídos, então `IFlyerLocks` é obrigatório no construtor (sem padrão nulo); os testes unitários usam um fake no-op.
  - H26-03b — Dado um show do perfil com flyer, quando a API devolve os shows do perfil, então o item traz `flyerId`, e `ogImageOf` em `profile-metadata.ts` monta `{base}/media/flyers/{flyerId}-og.jpg` a partir dele (sem regex sobre `flyerThumbUrl`); show sem flyer → sem imagem.
  - H26-07 — Avaliado pelo Architect (Emenda 1): fica como está. O upload é raro, os três arquivos são pequenos (≤ 2 MB no total) e o lock é por show, então não há disputa no volume do MVP; volta a ser avaliado se o painel do Seq mostrar upload lento.
- N/A — sem superfície pública nova.

**Dublês e dados de teste:** nenhum dublê de módulo; seguidores e shows criados no banco do teste; `TimeProvider` fixo nos UT e IT.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL, Mailpit); E2E contra a pilha do Compose; Playwright com um worker.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** tabela nova `engagement.published_shows` (aditivo).
- **Compatibilidade:** `IEventsCatalog` ganha um método; consumidores atuais não mudam.
- **Observabilidade:** contadores `subsolo.engagement.new_show_notices` e `subsolo.engagement.digest_recipients`; falha do job vira alerta no Seq (ADR-0010).
- **Rollback:** reverter o merge commit; a tabela pode ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Horário do resumo? — 9h de Brasília, configurável (recomendação do Architect; piloto automático do Thomas, 2026-10-18)
- [x] Horário de silêncio no push? — Não no MVP; o fã desliga o aviso se quiser (recomendação do Architect; piloto automático do Thomas, 2026-10-18)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado").

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Regras**
- [ ] Red: UT-01, UT-02 com a tag `SPEC-0031:<ID>`, falhando pelo motivo certo
- [ ] Green e Refactor

**Fase 2: Consumidor, job e contrato**
- [ ] Red: IT-01..IT-06, CT-01 falhando pelo motivo certo
- [ ] Green: `GetShowSummaryAsync`, consumidor `NotifyNewShow`, `published_shows`, job `DailyDigest`
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Jornada E2E**
- [ ] Red: E2E-01 falhando pelo motivo certo
- [ ] Green: cron do resumo configurável na pilha do E2E

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 6855548 | 2026-10-18 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 080b13f | 2026-10-24 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — e72721d | 2026-10-24 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 11s 324ms) — e72721d | 2026-10-24 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (0 blocker/major; 5 minor e 3 nits levados à SPEC-0032 §7.6 como H31-01..08; DKIM cobrindo List-Unsubscribe na SPEC-0003) — e72721d | 2026-10-24 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0031`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 | 2026-10-23 | Itens herdados com decisão concreta e teste: H30-01 (WNS aceito), H30-02 (retenção de 90 dias, job `NoticesRetention`), H30-03 (campos obrigatórios no PUT, 400 `notifications.invalid_preferences`), H30-04 (cabeçalhos RFC 8058 e `POST …/unsubscribe/one-click`), H30-05..07 como escritos, H26-01/02/03b como escritos (com `flyerId` nos shows do perfil) e H26-07 avaliado e mantido. CT-01 passa a esperar essas mudanças no OpenAPI. `touches` ganha `docs/api/openapi.v1.json`, `web/src/api/schema.d.ts`. | itens herdados das revisões das SPEC-0026 e SPEC-0030 sem decisão fechada antes do G1 | SPEC-0030 (contrato do PUT e do descadastro) | piloto automático do Thomas (2026-10-23) |
| 3 | 2026-10-24 | SPEC_DEFECTs do Test-writer no G1: `Notice.Url` é caminho do site (`/…`), resolvido contra `App:PublicUrl` no e-mail; `deliveries.status` ganha `skipped` (H30-06; H30-02 apaga `sent`, `failed` e `skipped`); log `NoticesRetentionCompleted {Deliveries} {Notices}`; novo IT-07 (`DailyDigestRunFailed` no alerta "Falha de job"); `flyerId` é `string \| null` e a rota real é `GET /api/v1/shows?profileId=…`; formatos da linha do resumo no UT-02; E2E-01 abre a agenda pelo link do e-mail (o corpo do §6 não tem link por show); `touches` ganha `src/**/packages.lock.json`, o `ProfilesHarness.cs` (novo membro de `IEventsCatalog` implementado normalmente), `web/src/api/public-profile.ts` e `tests/Security/**` (one-click sem X-CSRF). | SPEC_DEFECTs 1–10 do Test-writer (G1) | SPEC-0030 (`Notice.Url` relativo, status `skipped`) | piloto automático do Thomas (2026-10-24) |
| 3 (sem mudança de contrato) | 2026-10-24 | Achados do Implementer: `touches` ganha `AntiforgeryFilter.cs` (exceção explícita de X-CSRF para o one-click), `tests/Modules/Profiles/**`; alerta próprio "Resumo diário falhou" (o "Falha de job" pede 3 eventos em 10 min e o resumo roda uma vez por dia), coberto pelo IT-07; aceitas as escolhas do Implementer: destinatários do `new_show` = seguidores da cidade e de todos os perfis do show, entrega com todos os aparelhos em 410 fica `failed`, line-up limitada a 200 caracteres e corpo do resumo dentro de 1.000. | relatório do Implementer (G2) | — | piloto automático do Thomas (2026-10-24) |
| 3 (sem mudança de contrato) | 2026-10-24 | `touches` ganha `web/src/api/public-profile-contract.test.ts`: o CT-01 da SPEC-0026 compara o item de show do perfil com o resumo da agenda e passa a esperar o `flyerId` aditivo (H26-03b). | G2 FAIL em 0d72369 | SPEC-0026 | piloto automático do Thomas (2026-10-24) |
