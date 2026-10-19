---
id: SPEC-0031
title: Aviso de show novo (push imediato e resumo diário por e-mail)
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0023, SPEC-0027, SPEC-0030]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Engagement/**, tests/Modules/Engagement/**, src/Modules/Events/**, tests/Modules/Events/**, src/Subsolo.Api/appsettings.json, src/Subsolo.Api/appsettings.Development.json, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, tools/compose-up.sh, src/Subsolo.AppHost/**, e2e/tests/avisos/**, e2e/tests/fixtures.ts, web/src/components/profiles/**]
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
| Limpeza | `published_shows` com mais de 7 dias | apagadas | IT-04 |
| Arquitetura | dependências | `Engagement` só nas direções do ADR-0001; `Notifications` sem módulos de domínio | IT-06 |
| Contrato | `IEventsCatalog` | assinatura do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — consumidor, job e consulta novos; a criação do show segue coberta pela SPEC-0023.

### 7.2 Testes Unitários
- **UT-01** — Dado um `ShowSummary` de "Noite da Peste II" com Lineup ["Cova Rasa", "Peste Negra"], Casa "Basement", início 2026-11-15T01:00Z e fuso America/Sao_Paulo, quando o compositor monta o aviso, então título "Show novo em Curitiba", corpo "Cova Rasa + Peste Negra · sáb 14/11 22h00 · Basement" e url "/curitiba/noite-da-peste-ii".
- **UT-02** — Dados follows de quatro fãs e cinco shows publicados (um cancelado, um passado, 22 de uma cidade só), quando o montador do resumo roda, então cada fã recebe só os shows dele, sem repetição, até 20 com "e mais {n} na agenda", e quem não tem show novo fica de fora.

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL e 10.000 seguidores de Curitiba e 300 de uma banda (parte em comum), quando um artista cria um show pela API, então o evento é consumido e o `notifications.deliveries` recebe uma entrega push por fã com push ativo e o aviso ligado, em ≤ 5 s do evento.
- **IT-02** — Dado o mesmo `ShowPublished` entregue duas vezes à inbox, quando processado, então não há entregas duplicadas.
- **IT-03** — Dados um evento de show com início no passado e um de show cancelado antes do processamento, quando consumidos, então nenhuma entrega é criada.
- **IT-04** — Dados 10.000 fãs com o resumo ligado que seguem cidades com shows novos e linhas antigas em `published_shows`, quando o job roda, então as entregas `digest` são criadas em ≤ 60 s e as linhas com mais de 7 dias somem.
- **IT-05** — Dados dois fãs com o resumo ligado e o Mailpit real, quando o job roda duas vezes no mesmo dia, então cada um recebe um e-mail com os seus shows, o link da agenda e o link de descadastro, e não recebe um segundo.
- **IT-06** — Dados os testes de arquitetura, quando rodam, então o `Engagement` só depende de `Users`, `Events`, `Profiles` e `Notifications` pelos contratos, e o `Notifications` não referencia o `Engagement`.

### 7.4 Testes de Contrato
- **CT-01** — Dado o tipo público `IEventsCatalog`, quando lido por reflexão, então `GetShowSummaryAsync` e `ShowSummary` têm a forma do §6; o snapshot OpenAPI não muda.

### 7.5 Testes E2E
- **E2E-01** — Dada a pilha do Compose com `Engagement:DigestCron` a cada minuto: o fã logado segue Curitiba e liga "Resumo diário por e-mail"; um artista publica um show em Curitiba; em até 2 minutos chega ao Mailpit o e-mail "Shows novos de quem você segue" com o show; o link do show abre a página dele e o de descadastro confirma. [jornada: seguir-e-avisar]

### 7.6 Outros
- **Itens herdados da revisão da SPEC-0026 (G4):**
  - H26-01 — `ListProfileShowsEndpoint.cs`: remover o `.Cast<ProfileShowResponse>()`; tipar `ProfileShowsResponse.Shows` e `IShowsReadModel.ListByProfileAsync` como `ProfileShowResponse`.
  - H26-02 — `IFlyerLocks` obrigatório no construtor de `UploadFlyerCommandHandler` e `CleanupFlyersCommandHandler` (fake no-op nos testes unitários).
  - H26-03b — `profile-metadata.ts` `ogImageOf` (web): montar a URL do og a partir do id do flyer, não por regex sobre `flyerThumbUrl`; exige expor o id do flyer nos itens de show (contrato do `Events`).
  - H26-07 — upload de flyer: não manter o advisory lock e a transação abertos durante a escrita dos três arquivos (avaliar antes do go-live).
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0031`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
