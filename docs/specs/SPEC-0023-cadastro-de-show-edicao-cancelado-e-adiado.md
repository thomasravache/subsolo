---
id: SPEC-0023
title: Cadastro de show, edição, cancelado e adiado
tier: full
type: feature
user_facing: true
status: implemented
created: 2026-10-17
parent: SPEC-0002
depends_on: [SPEC-0021, SPEC-0022]
consumes_contract: []
contract_version: 3
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Subsolo.Api/packages.lock.json, src/Modules/Profiles/Subsolo.Modules.Profiles.Infrastructure/**, tests/Modules/Profiles/**, src/Common/Subsolo.Common.Infrastructure/**, src/Common/Subsolo.Common.Application/**, tests/Common/**, infra/caddy/**, src/Subsolo.AppHost/**, web/next.config.ts, src/Modules/Events/**, tests/Modules/Events/**, src/Subsolo.Api/Program.cs, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, docs/api/**, web/src/api/**, web/src/app/(public)/conta/**, web/src/components/shows/**, web/src/components/account/**, web/src/components/agenda/**, e2e/tests/shows/**, docs/design/**]
adrs: [ADR-0001, ADR-0003, ADR-0008, ADR-0013, ADR-0015]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0023 — Cadastro de show, edição, cancelado e adiado

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O produtor, a casa ou a banda aprovados (papel `artist`) cadastram um show em menos de 2 minutos: flyer, nome, data e hora, casa, cidade, line-up, estilos, preço e link do ingresso. O show entra na hora na agenda da cidade. Depois dá para editar, adiar ou cancelar; cancelado e adiado aparecem com selo na agenda. O admin faz o mesmo em qualquer perfil, inclusive nos sem dono. Publicar e mudar a situação de um show geram eventos que os avisos e lembretes vão consumir.

## 2. Motivação & Escopo
**Motivação:** é o coração do MVP: a agenda só existe com shows cadastrados (critério de aceite "Produtor aprovado cadastra um show… em menos de 2 minutos", jornada `cadastro-show`). Decisões do Thomas no épico: admin cadastra em perfis sem dono no começo; cancelado/adiado com selo visível e o show sai dos lembretes.

**Objetivos (dentro do escopo):**
- `Show` no módulo `Events`: perfil organizador (do usuário, ou qualquer um para o admin), título (2–100), slug (`<titulo>-<aaaa-mm-dd>`, único por cidade, imutável), cidade, início (data e hora locais da cidade, guardado em UTC), casa (perfil de casa **ou** nome livre 2–80), line-up (1–15 itens: perfil de banda ou nome livre), estilos (1–5), preço (texto livre até 40, ex.: "R$ 30 / R$ 40 na hora" ou "Gratuito"), link do ingresso (`https`, opcional), flyer (obrigatório, `IFlyerCatalog` da SPEC-0022) e situação (`scheduled` | `cancelled` | `postponed`).
- `POST /api/v1/shows`, `PUT /api/v1/shows/{id}`, `POST /api/v1/shows/{id}/status` e `GET /api/v1/me/shows?period=upcoming|past`.
- Agenda da cidade (`GET /api/v1/cities/{slug}/agenda`, contrato da SPEC-0008) passa a listar os shows dos próximos 90 dias (a partir de 6 h atrás), por data, com selo de situação; os filtros por data e estilo ficam para a spec da agenda.
- Eventos de integração pela outbox: `ShowPublished(ShowId, City, ProfileIds, StartsAtUtc)` na criação e `ShowChanged(ShowId, Status, StartsAtUtc)` na edição ou mudança de situação.
- Telas: "Meus shows" (`/conta/shows`, link em "Meus perfis" e em `/conta` para quem tem `artist`), "Novo show" (`/conta/shows/novo`) e "Editar show" (`/conta/shows/{id}`, mesma tela). Para o admin, as mesmas telas com a escolha de qualquer perfil organizador.

**Não-objetivos (fora do escopo):**
- Página pública do show (spec própria) e filtros da agenda.
- Aviso ativo a quem marcou "vou" quando o show é cancelado (o evento sai daqui; o envio é da spec de avisos).
- Excluir show (cancelar é o caminho; o histórico fica).
- Vários organizadores por show e show em várias cidades.

## 3. Dependências
- **Implementações necessárias:** SPEC-0021 (perfis aprovados e papel `artist`; o `Profiles` implementa o `IShowProfiles` do Emenda v2) e SPEC-0022 (flyer, `IFlyerCatalog`).
- **Contratos consumidos:** N/A — as duas já estarão implementadas.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** módulo `Events` (agenda da SPEC-0008, cidades e estilos da SPEC-0017, `IEventsCatalog` da SPEC-0021, flyers da SPEC-0022); telas de conta com chamadas do navegador à API (SPEC-0016/0019/0021).

**Decisão:** slices `CreateShow`, `UpdateShow`, `ChangeShowStatus`, `ListMyShows` e a evolução de `GetCityAgenda`; a autorização por recurso fica no handler (ADR-0003): o usuário precisa ser dono do perfil organizador (via `IShowProfiles`, interface do contrato do `Events` implementada pelo `Profiles` — Emenda v2), salvo o admin. Perfis de casa e de banda no line-up são validados pelo `IProfilesApi`; nomes livres são aceitos para quem ainda não está no Subsolo.

**Justificativa:** ADR-0001 (`Events` é dono de shows; perfis lidos pela interface pública), ADR-0003 (autorização por recurso no caso de uso), ADR-0013 (agenda SSR já pronta para receber os shows).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** exigir perfil para toda banda do line-up (trava o cadastro em menos de 2 minutos); excluir show (some da história e quebra links compartilhados).

**ADRs:** ADR-0001, ADR-0003, ADR-0008, ADR-0013, ADR-0015

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** a jornada "Novo show" completa (com o envio do flyer) em < 2 minutos para um usuário guiado no E2E, e a API de criação em ≤ 300 ms p95 sem contar o flyer (IT-01); agenda com 300 shows em ≤ 300 ms p95 (IT-05).
- **Segurança:** criar/editar/mudar situação exigem `artist` dono do perfil ou `admin` (anônimo 401, fã 403, artista de outro perfil 403); `X-CSRF`; rate limit `default`; link do ingresso só `https`; textos com tamanho limitado e exibidos escapados (IT-03, UT-02).
- **Privacidade e dados pessoais:** o show não guarda dado pessoal além do perfil organizador (público); export da SPEC-0019 ganha a seção "shows" com os shows criados pelo usuário (IT-07).
- **Disponibilidade e resiliência:** os eventos vão pela outbox na mesma transação (SPEC-0009); falha ao ligar o flyer desfaz a criação.
- **Acessibilidade (UI):** WCAG 2.2 AA nas três telas com axe; data e hora com campos nativos rotulados; line-up com botão "Adicionar outra banda" acessível por teclado; selos com texto; confirmação de cancelar em `dialog` com foco preso.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `POST /api/v1/shows` · `PUT /api/v1/shows/{id}` · `POST /api/v1/shows/{id}/status` · `GET /api/v1/me/shows` · agenda · eventos `ShowPublished` e `ShowChanged` · telas

```yaml
POST /api/v1/shows:                       # artist (dono do organizador) ou admin + X-CSRF + default
  body: { organizerProfileId, title: string(2–100), city: slug, startsLocal: "aaaa-mm-ddThh:mm",
          venue: { profileId } | { name: string(2–80) },
          lineup: [{ profileId } | { name: string(1–80) }] (1–15), styles: slug[] (1–5),
          price: string(1–40), ticketUrl: https|null, flyerId: string(64) }
  201: { id, slug }                       # Location: /api/v1/shows/{id}
  400: events.invalid_show (errors por campo) | events.flyer_not_found
  403: auth.forbidden (não é dono do organizador) ; 409: events.show_slug_taken
PUT /api/v1/shows/{id}:                   # mesmas regras; slug não muda
  200: { id, slug } ; 400 ; 403 ; 404: events.show_not_found
POST /api/v1/shows/{id}/status:           # body { status: scheduled|cancelled|postponed }
  200 ; 400: events.invalid_status ; 403 ; 404
GET /api/v1/shows/{id}:                      # Emenda v3 — dono do organizador ou admin + default ; no-store ; 200 com o show completo (para a tela de edição) ; 403 auth.forbidden ; 404 events.show_not_found
GET /api/v1/me/shows?period=upcoming|past:   # artist ou admin + default ; no-store
  200: [{ id, slug, title, startsAt, city, venueName, status, flyerThumbUrl, organizerProfileId }]
GET /api/v1/cities/{slug}/agenda:         # contrato da SPEC-0008 inalterado; agora com os shows de -6 h a +90 dias
# em todos: 401 auth.required ; 400 security.csrf_invalid ; 429 security.rate_limited
```

```csharp
// Subsolo.Modules.Events.IntegrationEvents (público)
// Emenda v2 — leitura de perfis no contrato do consumidor (Events.IntegrationEvents), implementada por Profiles.Infrastructure (ADR-0001: Profiles → Events, nunca o contrário)
public interface IShowProfiles {
    Task<IReadOnlyList<ShowProfile>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, ShowProfile>> GetManyAsync(IReadOnlyCollection<Guid> profileIds, CancellationToken ct);  // só perfis aprovados
}
public sealed record ShowProfile(Guid Id, string Type, string Name, string Slug, string City, Guid? OwnerUserId);
public sealed record ShowPublishedIntegrationEvent(Guid ShowId, string City, IReadOnlyList<Guid> ProfileIds, DateTimeOffset StartsAtUtc);
public sealed record ShowChangedIntegrationEvent(Guid ShowId, string Status, DateTimeOffset StartsAtUtc);
// ProfileIds = organizador + casa (se perfil) + bandas do line-up com perfil
```

```text
Banco (schema events): shows(id, slug, city, title, starts_at_utc, organizer_profile_id, venue_profile_id null, venue_name,
  lineup jsonb, styles text[], price, ticket_url null, flyer_id FK flyers, status, created_by, created_at_utc, updated_at_utc; UNIQUE(city, slug))
Telas:
/conta/shows: "MEUS SHOWS", "+ NOVO SHOW", abas "PRÓXIMOS" / "PASSADOS", cartões (flyer, data em bloco, nome, casa, selo CANCELADO/ADIADO),
  ações "Editar", "Adiar" (status postponed), "Cancelar show" → dialog "Cancelar este show? Ele continua na agenda com o selo CANCELADO." + "Cancelar show" / "Voltar"
  vazio → "Nenhum show cadastrado ainda."
/conta/shows/novo e /conta/shows/{id}: "CADASTRAR SHOW" / "EDITAR SHOW"; organizador (se tiver mais de um perfil, ou admin); flyer (upload com prévia, SPEC-0022);
  nome; data; hora; casa (busca entre perfis de casa ou nome livre); cidade; line-up ("Adicionar outra banda", como no Stitch); estilos (chips); preço; link do ingresso;
  "Publicar show" / "Salvar" → volta para /conta/shows com "Show publicado." / "Alterações salvas."; erros ligados aos campos
```

**Design:** Stitch, projeto 15194126668426659178: "Cadastrar Show - Subsolo" (0eda9cdf97fa484188933d53f1605783) e "Meus Shows - Produtor - Subsolo" (93c4e1cb1d2a43afad6b9ec0c0ea78c3). Ajustes: sem o selo "produtor verificado" no topo de "Meus shows" (não existe verificação além da aprovação do perfil); o texto da confirmação de cancelar é o do §6 (o show não some, ganha o selo).

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Criar show | artista dono, dados válidos | 201; aparece na agenda da cidade e em "Meus shows"; `ShowPublished` na outbox; flyer ligado | UT-01, IT-01, IT-05, E2E-01 |
| Dados inválidos | título curto, cidade/estilo inexistente, data no passado, line-up vazio ou com 16, link `http`, casa sem perfil nem nome | 400 `invalid_show` com o campo | UT-02, IT-02 |
| Flyer inexistente | `flyerId` desconhecido | 400 `flyer_not_found`; nada gravado | IT-02 |
| Perfil de outro | artista não dono do organizador | 403; nada gravado | UT-03, IT-03 |
| Admin em perfil sem dono | admin | 201 | IT-03 |
| Acesso | anônimo; fã | 401; 403 | IT-03 |
| Slug repetido | mesmo título e data na cidade | 409 `show_slug_taken` | IT-02 |
| Editar | dono muda data e line-up | 200; slug igual; agenda reflete; `ShowChanged` | UT-01, IT-04, E2E-02 |
| Cancelar / adiar | dono muda situação | 200; selo na agenda e em "Meus shows"; `ShowChanged` | IT-04, E2E-02 |
| Agenda | 300 shows, alguns fora da janela | só os de -6 h a +90 dias, por data, em ≤ 300 ms p95 | IT-05 |
| Hora local | show às 22:00 em `America/Sao_Paulo` | `startsAt` em UTC correto e exibido 22:00 | UT-04, IT-05 |
| Meus shows | próximos e passados | listas separadas, só do usuário (admin vê os que criou) | IT-06 |
| Export e exclusão | `GET /me/export`; `UserDeleted` | seção "shows"; shows ficam (são da cena) | IT-07 |
| Contrato publicado | snapshot OpenAPI e eventos | corpos, códigos e eventos do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- **CH-01** — Dada a agenda atual (SPEC-0008), quando `GET /api/v1/cities/cidade-exemplo/agenda` é chamado sem shows, então a resposta segue o contrato da SPEC-0008 com a lista vazia (guarda: passa antes da mudança).

### 7.2 Testes Unitários
- **UT-01** — Dado o handler de `CreateShow` com perfis e catálogo de teste, quando cria "Noite da Peste II" em 2026-11-14 22:00 em Curitiba, então gera o slug `noite-da-peste-ii-2026-11-14`, guarda o início em UTC, liga o flyer e gera `ShowPublished` com organizador, casa com perfil e bandas com perfil; editando, o slug não muda e sai `ShowChanged`.
- **UT-02** — Dado o validador, quando recebe título de 1 e 101 caracteres, data no passado, 0 e 16 itens de line-up, 0 e 6 estilos, preço de 41 caracteres, `ticketUrl` `http://x` e casa sem perfil nem nome, então cada erro vem com o campo em `events.invalid_show`.
- **UT-03** — Dado um artista dono do perfil A, quando tenta criar ou editar show do perfil B, então `auth.forbidden`; o admin pode em qualquer perfil, inclusive sem dono.
- **UT-04** — Dados `2026-11-14T22:00` em `America/Sao_Paulo` e um horário que cai na mudança de fuso de outra cidade, quando convertidos, então o UTC é o certo e a conversão de volta dá a mesma hora local.

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL, um perfil aprovado com dono e um flyer enviado, quando o dono cria o show, então 201, a linha em `events.shows`, o flyer com `attached_at`, `ShowPublished` na outbox, e p95 de 20 criações ≤ 300 ms.
- **IT-02** — Dados os casos inválidos do UT-02 pela API, um `flyerId` inexistente e um título repetido na mesma data e cidade, quando enviados, então 400 com o campo, 400 `flyer_not_found` e 409 `show_slug_taken`, sem gravar nada.
- **IT-03** — Dados anônimo, fã, artista dono de outro perfil, artista dono e admin (em perfil sem dono), quando criam um show, então 401, 403, 403, 201 e 201.
- **IT-04** — Dado um show criado, quando o dono edita data e line-up, adia e depois cancela, então cada resposta é 200, o slug não muda, a agenda mostra a situação certa e a outbox tem um `ShowChanged` por mudança.
- **IT-05** — Dados 300 shows em Curitiba (alguns de 7 h atrás, alguns daqui a 91 dias) e um show às 22:00 locais, quando a agenda é pedida, então vêm só os da janela, ordenados por data, com `startsAt` em UTC correto e situação, em p95 ≤ 300 ms.
- **IT-06** — Dados shows passados e futuros de dois donos, quando cada um chama `GET /me/shows?period=upcoming` e `past`, então vê só os próprios, separados por período, com `no-store`.
- **IT-07** — Dado um dono com shows, quando `GET /me/export` é chamado, então a seção "shows" traz os shows criados por ele; quando o `UserDeleted` dele é processado, os shows continuam na agenda.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json` e os tipos públicos de `Events.IntegrationEvents`, quando lidos, então os endpoints do §6 têm corpos, status e códigos, `ShowPublishedIntegrationEvent` e `ShowChangedIntegrationEvent` têm os campos do §6, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa com um perfil de produtor aprovado para o usuário, nos projetos mobile-chrome e mobile-safari: o produtor entra, vai a "Meus shows", toca em "+ Novo show", envia um flyer, preenche nome, data, hora, casa (nome livre), line-up com duas bandas, estilos, preço e link, e publica; vê "Show publicado." e o show em "Próximos"; a agenda da cidade mostra o card. O tempo do toque em "+ Novo show" até "Show publicado." fica abaixo de 2 minutos. Sem violações do axe. [jornada: cadastro-show]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: o produtor edita a hora do show e salva; adia o show e a agenda mostra "ADIADO"; cancela pelo diálogo e a agenda mostra "CANCELADO"; o foco fica preso no diálogo e "Voltar" devolve o foco. [jornada: cadastro-show]

### 7.6 Outros
- Segurança: G4 confere a autorização por recurso em criar, editar e mudar situação, e o escape dos textos livres.
- Herança da SPEC-0015 (épico, Emenda 1): se esta spec criar a primeira página interna 200 alcançável por `next/link` (ex.: "Meus shows" a partir de `/conta`), ela leva o E2E do título na navegação no cliente.
- Herança da revisão da SPEC-0022 (G4 em d47d28b; cada item com teste próprio com a tag `SPEC-0023:H22-NN`):
  - **H22-01, limpeza × ligar:** a limpeza de flyers apaga primeiro as linhas, com condição (`attached_at_utc IS NULL AND created_at_utc < corte`), e só depois apaga os arquivos dessas linhas. Um `AttachAsync` ou um reenvio no meio não perde o flyer. Reenviar um flyer não ligado renova o `created_at_utc`.
  - **H22-02, Caddy:** o `Cache-Control` imutável vale só para respostas 200; 404 e erros não ficam em cache. `file_server` com `hide .*`, para não servir os temporários.
  - **H22-03, teto do corpo:** `MaxRequestBodySize` = 10 MiB + folga do multipart antes de `ReadFormAsync`, também sem `Content-Length` (corpo chunked) → 400 `events.flyer_too_large`.
  - **H22-04, libvips:** carregadores não confiáveis bloqueados (`BlockUntrusted`; só jpeg, png e webp liberados); JPEG com `FailOn.Truncated` se o IT-05 da SPEC-0022 seguir verde, com teste de um JPEG que só gera aviso benigno (bytes extras antes de um marcador) aceito.
  - **H22-05, fila:** a fila de 1 espera com tempo limite (30 s) e responde 503 `service.unavailable`. `FlyerImages.ContentHash` é conferido com o hash do handler.
  - **H22-06, Compose:** o E2E do cadastro envia o flyer pela pilha do Compose e o lê pelo Caddy, provando o `media-init`, a libvips nativa na imagem e o `/media`. No `next dev`, o `/media` chega à API por rewrite.

**Dublês e dados de teste:** perfis e catálogo de teste no UT; perfis aprovados e flyers criados pela API nos IT e no E2E; `FakeTimeProvider`.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL) e `Media:Root` temporário; E2E contra a pilha do Compose; `--workers=1`.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** tabela nova `events.shows` (aditiva) com FK para `events.flyers`.
- **Compatibilidade:** o contrato da agenda da SPEC-0008 não muda (a lista passa a ter itens).
- **Observabilidade:** contadores `subsolo.shows.created`, `subsolo.shows.status_changed` (por situação); histograma da duração da criação; logs com ids.
- **Rollback:** reverter o merge commit; a tabela pode ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Banda sem perfil pode entrar no line-up? — Sim, por nome livre; o perfil é opcional para não travar o cadastro (recomendação do Architect; piloto automático do Thomas, 2026-10-17)
- [x] Excluir show? — Não; cancelar mantém o histórico e os links (recomendação do Architect; piloto automático do Thomas, 2026-10-17)
- [x] Janela da agenda até os filtros chegarem? — De 6 h atrás até 90 dias à frente (recomendação do Architect; piloto automático do Thomas, 2026-10-17)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"), com as telas do Stitch geradas com ele acordado.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 0: Caracterização**
- [x] CH-01 escrito(s), passando no código atual, commitado(s) antes de qualquer mudança

**Fase 1: Domínio e regras**
- [x] Red: UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0023:<ID>`, falhando pelo motivo certo
- [x] Green: implementar o mínimo seguindo o padrão de referência
- [x] Refactor mantendo tudo verde

**Fase 2: API, persistência e contratos**
- [x] Red: IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, IT-07, CT-01 falhando pelo motivo certo
- [x] Green: endpoints, migração aditiva, OpenAPI e cliente TS regenerados
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 3: Telas e jornada E2E**
- [x] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [x] Green: telas conforme o Stitch e os ajustes do §6; jornada completa passando localmente
- [x] Validar: build + suíte + E2E (G2/G3)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — f1806a4 | 2026-10-17 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 0189fd3 | 2026-10-18 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 28717fe | 2026-10-18 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 4s 909ms) — 28717fe | 2026-10-18 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED em 28717fe — contrato v3 conferido no código e no OpenAPI, autorização por recurso (dono do organizador ou admin) em criar, editar, situação e detalhe, X-CSRF, https no ingresso, textos escapados, sem script inline nem env no navegador; IShowProfiles no contrato do Events implementado pelo Profiles e Events → Users só pelos dados pessoais; H22-01..06 conferidos (JpegHeaderCleaner seguro, matcher de requisição do Caddy aceito); dublê 7d723d0 sem mudar comportamento; telas fiéis ao Stitch com diálogo acessível; sem blocker/major; menores → SPEC-0024 (H23-01..07) e SPEC-0026 (H23-08) — 28717fe | 2026-10-18 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 11m 24s 366ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 6s 332ms) — b0305ce | 2026-10-18 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-18 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live — SPEC-0003 (deploy guiado pelo Thomas) | 2026-10-18 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — f1a6f18 (árvore suja) | 2026-10-18 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-18 | G1 | spec | §4 faz o Events ler perfis pelo IProfilesApi, mas o Profiles já depende do Events (IEventsCatalog): ciclo proibido pelo ADR-0001 e pelo teste NoModuleCycles | Test-writer isolou o dublê TestProfiles em ShowsHarness.cs | Architect | Emenda v2 — IShowProfiles no contrato do Events, implementado pelo Profiles.Infrastructure | 2026-10-18 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
- Cadastro de show por artista dono do perfil organizador, ou pelo admin em qualquer perfil, com flyer, nome, data e hora no fuso da cidade, casa, line-up, estilos, preço e link do ingresso; o show entra na hora na agenda da cidade.
- Editar, adiar e cancelar (com selo e diálogo de confirmação); o endereço do show (slug) nunca muda.
- Telas "Meus shows" (`/conta/shows`, próximos e passados), "Cadastrar show" e "Editar show", como no Stitch; eventos `ShowPublished` e `ShowChanged` pela outbox para os avisos.
- Reforços herdados do flyer (H22): corrida da limpeza, cache do Caddy, teto do corpo, carregadores da libvips, fila com tempo limite e `/media` no `next dev`.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: Test-writer (5e6d6c4 scaffold; 0927362, 717a843, f46eed6, 628902f, a3b4f2d testes) → Implementer (c16033c, 7d723d0 dublê, 9c744e5, ca8e43d, d6dc44a, 66a35d9, 182f07a, 41b79ef) → Reviewer APPROVED em 28717fe.
- Emenda v2 (IMP-01): o `Events` lê perfis pelo `IShowProfiles` do próprio contrato, implementado pelo `Profiles`, sem ciclo; janela padrão da agenda; "Meus shows" por organizador; flyer reaproveitável; lacuna do horário de verão → 400.
- Emenda v3: `GET /api/v1/shows/{id}` para a edição, `events.invalid_period` e `Events → Users` só pelos dados pessoais (ADR-0001 esclarecido).
- `startsAt` da agenda passa a sair com `Z` (UTC), em vez de `+00:00`; os dois são RFC 3339 e o OpenAPI não muda.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| CH-01 | agenda atual (SPEC-0008) sem shows segue o contrato antes da mudança | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| UT-01 | criar "Noite da Peste II": slug com a data, início em UTC, flyer ligado e `ShowPublished` | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| UT-02 | validação de título, data, line-up, estilos, preço, ingresso https e casa | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| UT-03 | artista só no próprio organizador (`auth.forbidden` no de outro); admin em qualquer um | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| UT-04 | hora local ↔ UTC com horário de verão; hora inexistente → `invalid_show` | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| IT-01 | criação pela API: 201, linha, flyer ligado, `ShowPublished` na outbox, p95 de 20 criações ≤ 300 ms | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| IT-02 | casos inválidos, flyer inexistente e título repetido na data e cidade → 400/409 com os códigos do §6 | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| IT-03 | anônimo 401, fã 403, artista de outro perfil 403, dono 201, admin em perfil sem dono 201 | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| IT-04 | editar, adiar e cancelar: 200, slug imutável, agenda com a situação, `ShowChanged` | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| IT-05 | agenda com 300 shows: só a janela de -6 h a +90 dias, ordenada, `startsAt` em UTC, p95 ≤ 300 ms | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| IT-06 | `/me/shows` por período, só os do dono, `no-store`; período inválido → `invalid_period` | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| IT-07 | seção "shows" no export e tratamento no `UserDeleted` | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| CT-01 | OpenAPI (contrato v3, com `GET /shows/{id}`) e eventos públicos de `Events.IntegrationEvents`; cliente TS em dia | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| E2E-01 | jornada cadastro-show (mobile-chrome e mobile-safari): "+ Novo show", flyer, dados e publicação, show na agenda; thumb lido pelo Caddy; axe | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| E2E-02 | editar a hora, adiar ("ADIADO") e cancelar com diálogo de foco preso | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| H22-01 | limpeza apaga primeiro as linhas sem flyer ligado; reenvio renova `created_at_utc` | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| H22-02 | Caddy: cache imutável só nos arquivos servidos; 404 e arquivos com ponto sem cache | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| H22-03 | corpo chunked acima de 10 MiB → 400 `flyer_too_large` | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| H22-04 | carregadores não confiáveis bloqueados; JPEG com aviso benigno aceito pelo `JpegHeaderCleaner` | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| H22-05 | fila ocupada → 503 `service.unavailable` depois do tempo limite; `ContentHash` conferido | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |
| H22-06 | rewrite de `/media` no `next dev` | PASS | `gate G2 --run` em 28717fe; `gate G5 --run` na integração (b0305ce); CI do PR #30 (run 39173686073, 10/10 checks em f1a6f18) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — CI verde no PR #30 (run 39173686073)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — G6 N/A até a SPEC-0003
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout
- [x] Documentação raiz e CHANGELOG atualizados (G7) — CHANGELOG atualizado
- [x] Pendências registradas como novas specs (ou nenhuma) — ver Pendências

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente implantado (G6 N/A → SPEC-0003, guiado pelo Thomas). Entrega na `main` pelo PR #30 (https://github.com/thomasravache/subsolo/pull/30), merge commit add02ff em 2026-10-18, com H2 pela política auto-on-green.

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- Menores da revisão G4: corridas da limpeza com reenvio e cadastro, cópia do `JpegHeaderCleaner`, diretório no Caddy, sobras de código, fuso em "Meus shows" e `aria-describedby` da line-up → SPEC-0024 (§7.6, H23-01..07); busca de qualquer perfil aprovado para casa e organizador → SPEC-0026 (H23-08).
## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0023`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 3 | 2026-10-18 | (a) `GET /api/v1/shows/{id}` (dono ou admin, no-store) para a tela de edição; (b) `period` fora de `upcoming`/`past` → 400 `events.invalid_period`; (c) o `Events` depende do contrato do `Users` só para `UserDeleted` e `IPersonalDataSection` (seção "shows" do export, IT-07) — ADR-0001 esclarecido; (d) `src/Subsolo.Api/packages.lock.json` em `touches` (novas referências entre projetos). | Relatório do Implementer: a tela de edição precisa do show inteiro; export exigido pelo IT-07 | SPEC-0024 (pode reaproveitar o detalhe para o dono) | Architect (piloto automático do Thomas, 2026-10-17) |
| 2 | 2026-10-18 | (a) O `Events` lê perfis por `IShowProfiles` (contrato do `Events`, implementado pelo `Profiles.Infrastructure`), não pelo `IProfilesApi`. (b) A janela de -6 h a +90 dias é o padrão da agenda quando `from` e `to` não vêm; com eles, valem as regras da SPEC-0008. (c) `/me/shows`: para o artista, os shows cujo organizador é um perfil dele; para o admin, os que ele criou (`created_by`). (d) Um flyer pode ser usado por mais de um show (ligar é idempotente). (e) Hora local que não existe (lacuna do horário de verão) → 400 `invalid_show` em `startsLocal`; hora ambígua aceita qualquer um dos instantes, desde que a volta dê a mesma hora local. (f) 503 com o código `service.unavailable`, o que já existe. (g) Botão "Adicionar outra banda", como no Stitch. | Relatório do Test-writer (IMP-01): ciclo Events ↔ Profiles e pontos em aberto | SPEC-0024, SPEC-0025, SPEC-0026 (leem perfis pelo `IShowProfiles` se precisarem no `Events`) | Architect (piloto automático do Thomas, 2026-10-17) |
