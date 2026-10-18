---
id: SPEC-0021
title: "Perfis: pedido, aprovação pelo admin, perfil sem dono e reivindicação"
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-17
parent: SPEC-0002
depends_on: [SPEC-0017, SPEC-0019]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Profiles/**, tests/Modules/Profiles/**, src/Modules/Events/Subsolo.Modules.Events.IntegrationEvents/**, src/Modules/Events/Subsolo.Modules.Events.Infrastructure/**, src/Modules/Users/**, tests/Modules/Users/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/Subsolo.Api.csproj, src/Subsolo.Api/packages.lock.json, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, Subsolo.slnx, docs/api/**, web/src/api/**, web/src/app/(public)/conta/**, web/src/components/account/**, web/src/components/profiles/**, web/src/app/admin/**, web/src/components/admin/**, e2e/tests/perfis/**, docs/design/**]
adrs: [ADR-0001, ADR-0003, ADR-0008]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0021 — Perfis: pedido, aprovação pelo admin, perfil sem dono e reivindicação

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Cria o módulo `Profiles`: perfis de banda, casa e produtor. Quem toca, tem uma casa ou produz shows pede o perfil pela conta; o admin aprova ou recusa numa fila. O admin também cria perfis "sem dono" para cadastrar shows enquanto a banda ou o produtor ainda não está no Subsolo; quando o dono aparece, ele reivindica o perfil e, aprovado, assume com todo o histórico. Aprovar um dono dá ao usuário o papel `artist`, que a spec de cadastro de show usa.

## 2. Motivação & Escopo
**Motivação:** o PRD exige aprovação manual de todo novo produtor e banda (contra show falso e bandas NSBM, riscos do épico), e o Thomas decidiu que no começo o admin cadastra shows em perfis sem dono, que o produtor assume depois (épico, 2026-10-13).

**Objetivos (dentro do escopo):**
- Módulo `Profiles` (ADR-0001, schema `profiles`): `Profile` com tipo (`band` | `venue` | `producer`), nome (2–80), slug (único por tipo, gerado do nome, imutável), cidade (slug existente), estilos (só banda, slugs existentes), links (Instagram, Bandcamp, Metal Archives — só URLs `https` desses domínios), situação (`pending` | `approved` | `rejected`, com motivo) e dono (usuário ou nenhum).
- Pedido de perfil pelo fã logado (`POST /api/v1/profiles/requests`), com contato para a aprovação (guardado só para o admin) e no máximo 1 pedido em análise por usuário.
- Reivindicação de perfil aprovado sem dono (`GET /api/v1/profiles/search`, `POST /api/v1/profiles/{id}/claims`).
- Fila do admin (`GET /api/v1/admin/profile-requests`, aprovar e recusar com motivo) e criação de perfil sem dono pelo admin (`POST /api/v1/admin/profiles`).
- "Meus perfis" (`GET /api/v1/me/profiles`) com a situação de cada pedido e o motivo da recusa.
- Evento `ProfileOwnerAssignedIntegrationEvent(ProfileId, UserId)` pela outbox; o `Users` consome (inbox) e dá o papel `artist`.
- Interface pública `IProfilesApi` (em `Profiles.IntegrationEvents`) com `GetOwnedProfilesAsync(userId)` e `GetAsync(profileId)` para o cadastro de show; `IPersonalDataSection` "profiles" no export (SPEC-0019); no `UserDeleted`, os perfis do usuário ficam sem dono (o histórico de shows é da cena) e os pedidos dele são apagados.
- Leitura de cidades e estilos para validar: interface pública `IEventsCatalog` em `Events.IntegrationEvents` (`CityExistsAsync`, `StylesExistAsync`).
- Telas: "Meus perfis" (`/conta/perfis`, com link a partir de `/conta`), "Pedir perfil" (`/conta/perfis/novo`), "Reivindicar perfil" (`/conta/perfis/reivindicar`), aba "Pedidos de perfil" da "Fila do admin" (`/admin/fila`) e criação de perfil sem dono (`/admin/perfis/novo`, mesmo formulário do pedido sem o contato).

**Não-objetivos (fora do escopo):**
- Página pública do perfil (spec "Perfil público").
- Editar perfil aprovado e foto do perfil.
- Aviso por e-mail ou push da aprovação (a situação aparece em "Meus perfis").
- Aba "Denúncias" da fila (spec de denúncia).
- Vários donos por perfil.

## 3. Dependências
- **Implementações necessárias:** SPEC-0017 (cidades e estilos editáveis, área `/admin`) e SPEC-0019 (`/conta` completa, `IPersonalDataSection`, `UserDeleted`); por elas, SPEC-0014 e SPEC-0016.
- **Contratos consumidos:** N/A — o `IEventsCatalog` é criado aqui; o evento `UserDeleted` é da SPEC-0019, já implementada quando esta começar.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** módulos `Users` e `Notifications` (SPEC-0014/0018) como referência de módulo novo; outbox/inbox da SPEC-0009; área `/admin` e páginas que chamam a API pelo navegador com `X-CSRF` (SPEC-0016/0017).

**Decisão:** novo módulo `Profiles` com slices `RequestProfile`, `SearchClaimableProfiles`, `ClaimProfile`, `ListMyProfiles`, `ListProfileRequests`, `ApproveProfileRequest`, `RejectProfileRequest`, `CreateOwnerlessProfile`. Pedido e reivindicação são a mesma entidade `ProfileRequest` (kind `new` | `claim`). Aprovar grava o dono e publica `ProfileOwnerAssigned`; o `Users` ganha um consumidor que adiciona o papel `artist` (idempotente). A validação de cidade e estilo passa pelo `IEventsCatalog`, nunca pelo banco do `Events`.

**Justificativa:** ADR-0001 (módulo por contexto, conversa por evento e por interface pública; o próprio ADR cita `IProfilesApi`), ADR-0003 (papel `artist` concedido quando um perfil é aprovado e vinculado), ADR-0008 (contato do pedido visível só ao admin, apagado com o pedido).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** perfis dentro do `Events` (mistura identidade pública da cena com a agenda); papel `artist` gravado pelo `Profiles` direto na tabela do `Users` (fere o ADR-0001); apagar os perfis quando o dono exclui a conta (some o histórico de shows da cena).

**ADRs:** ADR-0001, ADR-0003, ADR-0008

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** busca de perfis para reivindicar em ≤ 200 ms p95 com 1.000 perfis (IT-03); fila do admin em ≤ 300 ms p95 com 200 pedidos (IT-04).
- **Segurança:** pedir, reivindicar e "meus perfis" exigem `fan`; fila, aprovar, recusar e criar sem dono exigem `admin` (anônimo 401, fã 403); mutações com `X-CSRF`; rate limit `default` em tudo e `report` (10/h) no pedido e na reivindicação; links só `https` dos domínios permitidos (UT-02); o contato do pedido nunca aparece fora da fila do admin (IT-04, IT-06).
- **Privacidade e dados pessoais:** contato do pedido com finalidade só de aprovação (ADR-0008), apagado junto com o pedido 90 dias depois da decisão (IT-07); export inclui os perfis e pedidos do usuário; e-mail e contato fora dos logs (IT-06).
- **Disponibilidade e resiliência:** o papel `artist` chega pelo evento; se o `Users` estiver parado, o inbox aplica quando voltar (idempotente, IT-05).
- **Acessibilidade (UI):** WCAG 2.2 AA nas cinco telas, com axe no E2E; selos de situação com texto, não só cor; seletor de tipo como grupo de rádio com rótulo.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `/api/v1/profiles/*` · `/api/v1/me/profiles` · `/api/v1/admin/profile-requests` · `/api/v1/admin/profiles` · `IProfilesApi` · `IEventsCatalog` · evento `ProfileOwnerAssigned` · telas

```yaml
POST /api/v1/profiles/requests:          # fan + X-CSRF + RequireRateLimiting(report)
  body: { type: band|venue|producer, name: string(2–80), city: slug, styles: slug[] (só band, 1–5),
          links: { instagram?, bandcamp?, metalArchives? } (https dos domínios oficiais), contact: string(3–200) }
  201: { requestId, status: "pending" }
  400: profiles.invalid_request (errors por campo) ; 409: profiles.request_pending | profiles.name_taken
GET /api/v1/profiles/search?q=&type=:    # fan + default ; só perfis aprovados SEM dono ; q ≥ 2 caracteres ; até 20
  200: [{ id, type, name, city, styles }]
POST /api/v1/profiles/{id}/claims:       # fan + X-CSRF + report
  body: { proof: string(3–300) }
  201: { requestId, status: "pending" } ; 404: profiles.not_found ; 409: profiles.already_owned | profiles.request_pending
GET /api/v1/me/profiles:                 # fan + default ; no-store
  200: { profiles: [{ id, type, name, slug, city }], requests: [{ requestId, kind: new|claim, name, type, status, reason?, createdAtUtc }] }
GET /api/v1/admin/profile-requests:      # admin + default ; no-store ; pendentes, mais antigos primeiro
  200: [{ requestId, kind, type, name, city, styles, links, contactOrProof, requesterEmail, createdAtUtc, profileId? }]
POST /api/v1/admin/profile-requests/{id}/approve:   # admin + X-CSRF
  200: { profileId } ; 404: profiles.request_not_found ; 409: profiles.request_decided | profiles.already_owned
POST /api/v1/admin/profile-requests/{id}/reject:    # admin + X-CSRF ; body { reason: string(3–200) }
  200 ; 404: profiles.request_not_found ; 409: profiles.request_decided
POST /api/v1/admin/profiles:             # admin + X-CSRF ; body como o pedido, sem contact
  201: { profileId } (aprovado, sem dono) ; 400: profiles.invalid_request ; 409: profiles.name_taken
# em todos: 401 auth.required ; 403 auth.forbidden ; 400 security.csrf_invalid ; 429 security.rate_limited
```

```csharp
// Subsolo.Modules.Profiles.IntegrationEvents (público)
public interface IProfilesApi {
    Task<IReadOnlyList<ProfileSummary>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct);
    Task<ProfileSummary?> GetAsync(Guid profileId, CancellationToken ct);
}
public sealed record ProfileSummary(Guid Id, string Type, string Name, string Slug, string City, Guid? OwnerUserId);
public sealed record ProfileOwnerAssignedIntegrationEvent(Guid ProfileId, Guid UserId);
// Subsolo.Modules.Events.IntegrationEvents (público, novo)
public interface IEventsCatalog {
    Task<bool> CityExistsAsync(string slug, CancellationToken ct);
    Task<bool> StylesExistAsync(IReadOnlyCollection<string> slugs, CancellationToken ct);
}
```

```text
Banco (schema profiles): profiles(id, type, name, slug, city, styles text[], links jsonb, owner_user_id null, created_at_utc; UNIQUE(type, slug))
  profile_requests(id, kind, requester_user_id, profile_id null, payload jsonb, contact_or_proof, status, reason, created_at_utc, decided_at_utc)
Limpeza: pedidos decididos há > 90 dias apagados (tarefa periódica)
Telas:
/conta → seção "PERFIS" com link "Meus perfis"
/conta/perfis: selos "EM ANÁLISE" / "APROVADO" / "RECUSADO" (+ motivo e "Pedir novamente"); "Pedir novo perfil"; "Reivindicar perfil existente";
  vazio → "Você ainda não tem perfil. Toca uma banda, tem uma casa ou produz shows? Peça o seu."
/conta/perfis/novo: tipo (BANDA | CASA / LOCAL | PRODUTOR), nome, cidade, estilos (só banda), links, contato,
  aviso "Seu pedido passa por aprovação do admin da cena", "Enviar pedido" → "Pedido enviado." e volta a /conta/perfis
/conta/perfis/reivindicar: busca, resultados com selo "SEM DONO", campo de prova, "Enviar pedido"
/admin/fila: aba "PEDIDOS DE PERFIL (n)" com cartões e "Aprovar" / "Recusar" (recusar abre campo de motivo)
/admin/perfis/novo: formulário do pedido sem contato → "Perfil criado."
```

**Design:** Stitch, projeto 15194126668426659178: "Meus perfis - Subsolo" (90a9b7cb839a4992a9500362c48ec9e1), "Pedir Perfil - Subsolo" (af62730f71414b6082259d068b025b00), "Reivindicar Perfil - Subsolo" (75b72cdf03284a5998d07d0fd8ae6543) e "Fila do Admin - Subsolo" (600bfe18de6e44a995790033ab0f817e). Ajustes: em "Meus perfis", sem "Editar perfil" e "ver painel" (fora do escopo); na fila, só a aba de pedidos (a de denúncias entra com a spec dela); "Bandcamp / Soundcloud" vira só Bandcamp; `/admin/perfis/novo` reaproveita o formulário de "Pedir perfil" sem o contato.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Pedir perfil | fã, dados válidos | 201 pendente; aparece em "Meus perfis" e na fila | UT-01, IT-01, E2E-01 |
| Dados inválidos | nome curto, cidade ou estilo inexistente, link de outro domínio ou `http` | 400 com o campo | UT-02, IT-01 |
| Pedido duplicado | já há pedido em análise; nome já usado no tipo | 409 `request_pending` / `name_taken` | UT-01, IT-01 |
| Aprovar pedido novo | admin | perfil aprovado com dono; evento; o `Users` dá `artist` | UT-03, IT-02, IT-05, E2E-01 |
| Recusar | admin com motivo | pedido recusado; motivo em "Meus perfis"; pode pedir de novo | UT-03, IT-02, E2E-01 |
| Perfil sem dono | admin cria | aprovado sem dono; aparece na busca de reivindicação | IT-03, E2E-02 |
| Reivindicar | fã, perfil sem dono | pedido `claim`; aprovado → vira dono e ganha `artist` | UT-03, IT-03, E2E-02 |
| Reivindicar com dono | perfil já tem dono | 409 `already_owned`; não aparece na busca | IT-03 |
| Acesso | anônimo / fã nas rotas de admin | 401 / 403 | IT-04 |
| Contato privado | "Meus perfis", busca, `IProfilesApi` | contato e prova nunca aparecem fora da fila do admin | IT-04 |
| Papel artist idempotente | evento entregue duas vezes; `Users` parado e depois volta | papel uma vez só, aplicado quando volta | IT-05 |
| Exclusão de conta | `UserDeleted` do dono | perfis ficam sem dono; pedidos do usuário apagados | IT-07 |
| Export | `GET /me/export` | seção "profiles" com perfis e pedidos do usuário | IT-07 |
| Limpeza | pedidos decididos há > 90 dias | apagados | IT-07 |
| Dados fora dos logs | fluxos com captura | e-mail e contato ausentes ou mascarados | IT-06 |
| Arquitetura | módulo novo | regras do ADR-0001 valem; `Profiles` não lê o banco do `Events` nem do `Users` | IT-08 |
| Contrato publicado | snapshot OpenAPI e `IProfilesApi` | corpos, códigos e assinatura do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — módulo novo; `/conta` segue coberta pelos testes da SPEC-0019.

### 7.2 Testes Unitários
- **UT-01** — Dado o handler de `RequestProfile` com catálogo de teste, quando um fã pede "Podridão" (banda, `curitiba`, `death`), então grava pedido pendente com slug `podridao`; com outro pedido dele em análise, retorna `profiles.request_pending`; com banda `podridao` já existente, `profiles.name_taken`.
- **UT-02** — Dado o validador do pedido, quando recebe nome de 1 e 81 caracteres, cidade ou estilo que o catálogo nega, estilos em perfil de casa, 0 ou 6 estilos, links `http://instagram.com/x`, `https://evil.com/x` e `https://www.instagram.com/podridao`, então só o último link passa e cada erro vem com o campo em `profiles.invalid_request`.
- **UT-03** — Dados pedidos `new` e `claim` pendentes, quando aprovados, então o perfil fica aprovado com o dono e é gerado `ProfileOwnerAssigned`; aprovar de novo dá `request_decided`; `claim` de perfil que ganhou dono no meio dá `already_owned`; recusar sem motivo é erro de validação.

### 7.3 Testes de Integração
- **IT-01** — Dados a API e o PostgreSQL reais com as cidades e estilos semeados, quando um fã faz `POST /profiles/requests` válido, inválido, repetido e com nome já usado, então 201, 400 com o campo, 409 `request_pending` e 409 `name_taken`; `GET /me/profiles` mostra o pendente.
- **IT-02** — Dado um pedido pendente, quando o admin aprova, então 200 com `profileId`, o perfil tem dono e a outbox tem `ProfileOwnerAssigned`; quando o admin recusa outro com motivo, `GET /me/profiles` do autor mostra "rejected" com o motivo e um novo pedido é aceito.
- **IT-03** — Dado um perfil criado pelo admin sem dono e 1.000 perfis semeados, quando um fã busca "pod" e reivindica, então o perfil aparece com p95 ≤ 200 ms; a reivindicação aprovada torna o fã dono; um segundo fã reivindicando recebe 409 `already_owned`, e o perfil some da busca.
- **IT-04** — Dados anônimo, fã e admin, quando chamam as rotas de admin, então 401, 403 e 200; o contato e a prova aparecem só na resposta da fila do admin (não em `/me/profiles`, na busca nem no `IProfilesApi`); fila com 200 pedidos em p95 ≤ 300 ms.
- **IT-05** — Dado `ProfileOwnerAssigned` publicado duas vezes e o processamento do `Users` parado e retomado, quando o inbox processa, então o usuário tem o papel `artist` uma vez só e `/me` mostra `artist`.
- **IT-06** — Dados os logs e spans capturados em pedido, reivindicação, aprovação e recusa, quando inspecionados, então e-mail e contato não aparecem em claro.
- **IT-07** — Dado um dono com perfil aprovado e um pedido pendente, quando `GET /me/export` é chamado, então a seção "profiles" traz os dois; quando o `UserDeleted` dele é processado, então o perfil fica sem dono e o pedido é apagado; e pedidos decididos há 91 dias são apagados pela limpeza (`FakeTimeProvider`).
- **IT-08** — Dados os testes de arquitetura, quando rodam com o módulo `Profiles`, então as regras do ADR-0001 valem para ele, `IEventsCatalog` e `IProfilesApi` estão em `IntegrationEvents` e nenhum módulo referencia `Profiles.Domain`, `.Application` ou `.Infrastructure` (guarda: passa antes da mudança para os módulos existentes).

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então os endpoints do §6 têm corpos, status e códigos de erro, e `(cd web && pnpm api:check)` passa; e o `IProfilesApi` tem a assinatura do §6 (consumido pelo cadastro de show).

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa, nos projetos mobile-chrome e mobile-safari: um fã logado vai de `/conta` a "Meus perfis", pede o perfil de banda "Podridão" e vê "EM ANÁLISE"; o admin (outro contexto do navegador) abre `/admin/fila` e aprova; o fã recarrega e vê "APROVADO"; um segundo pedido recusado com motivo mostra "RECUSADO" e o motivo. Sem violações do axe nas telas. [jornada: perfis]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: o admin cria o perfil sem dono "Porão do Rock" (casa); um fã o encontra em "Reivindicar perfil" e envia a prova; o admin aprova; o perfil aparece em "Meus perfis" do fã como "APROVADO". [jornada: perfis]

### 7.6 Outros
- Segurança: G4 confere a autorização em cada rota de admin e o vazamento do contato.
- Herança da revisão da SPEC-0019 (primeira consumidora do export e do `UserDeleted`): teste unitário de `PublishUserDeleted` (mesmo id, `DeletedAtUtc`, só ids), e nomes de `IPersonalDataSection` repetidos falham na inicialização.
- Herança da revisão da SPEC-0017: `ModuleOf` nas regras de arquitetura (`tests/Subsolo.ArchitectureTests/ModularMonolithRules.cs`) passa a tolerar dependências que o ArchUnitNET não resolve (`type?.Namespace?.FullName`), para expressões de coleção não derrubarem a regra.

**Dublês e dados de teste:** catálogo de teste no UT; cidades e estilos semeados pelas migrações; e-mails `@subsolo.test`; admin pela configuração `Users:AdminEmails` nos hosts de teste e na pilha do E2E.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL); E2E contra a pilha do Compose com o Mailpit; `--workers=1`.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** schema novo `profiles` (aditivo); nada muda nos outros schemas.
- **Compatibilidade:** endpoints novos; o papel `artist` já existe nas políticas (SPEC-0010).
- **Observabilidade:** contadores `subsolo.profiles.requests` (por tipo e kind) e `subsolo.profiles.decisions` (aprovado/recusado); logs com ids, nunca contato ou e-mail.
- **Rollback:** reverter o merge commit; o schema novo pode ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Quando o dono exclui a conta, o perfil some? — Não: fica sem dono, porque o histórico de shows é da cena; os pedidos e o contato dele são apagados (recomendação do Architect; piloto automático do Thomas, 2026-10-17)
- [x] Editar perfil aprovado entra agora? — Não; fica para quando houver pedido (recomendação do Architect; piloto automático do Thomas, 2026-10-17)
- [x] Telas "Meus perfis" e "Reivindicar perfil" geradas no Stitch? — Sim, em 2026-10-17 com o Thomas acordado (ids no §6)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"), com as telas do Stitch geradas com ele acordado.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Domínio e regras**
- [ ] Red: UT-01, UT-02, UT-03 com a tag `SPEC-0021:<ID>`, falhando pelo motivo certo
- [ ] Green: implementar o mínimo seguindo o padrão de referência
- [ ] Refactor mantendo tudo verde

**Fase 2: API, persistência e contratos**
- [ ] Red: IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, IT-07, IT-08, CT-01 falhando pelo motivo certo
- [ ] Green: endpoints, migração aditiva, OpenAPI e cliente TS regenerados
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 3: Telas e jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [ ] Green: telas conforme o Stitch e os ajustes do §6; jornada completa passando localmente
- [ ] Validar: build + suíte + E2E (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — a1ca145 (árvore suja) | 2026-10-17 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0021`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
