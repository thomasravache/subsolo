---
id: SPEC-0014
title: "Módulo Users: conta, sessão e papéis"
tier: full
type: feature
user_facing: false
status: in-progress
created: 2026-10-17
parent: SPEC-0002
depends_on: []
consumes_contract: []
contract_version: 2
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Users/**, tests/Modules/Users/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/Subsolo.Api.csproj, src/Subsolo.Api/packages.lock.json, src/Subsolo.Api/appsettings.json, src/Subsolo.Api/appsettings.Development.json, src/Subsolo.Api/Security/**, src/Common/Subsolo.Common.Presentation/Security/**, tests/Security/**, tests/Subsolo.ArchitectureTests/**, tests/Common/Subsolo.Api.IntegrationTests/**, src/Modules/Events/Subsolo.Modules.Events.Presentation/Features/ListCities/ListCitiesEndpoint.cs, src/Modules/Events/Subsolo.Modules.Events.Presentation/Features/GetCityAgenda/GetCityAgendaEndpoint.cs, src/Subsolo.AppHost/**, Subsolo.slnx, docs/api/**, web/src/api/schema.d.ts, tests/**/packages.lock.json]
adrs: [ADR-0001, ADR-0002, ADR-0003, ADR-0008, ADR-0010]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0014 — Módulo Users: conta, sessão e papéis

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Cria o módulo `Users`, a base de identidade do Subsolo (ADR-0003): a conta do usuário (e-mail, nome opcional, papéis), a **sessão guardada no banco** por trás do cookie `__Host-subsolo` (revogável pelo servidor) e os endpoints `GET /api/v1/me` e `POST /api/v1/auth/logout`. Os métodos de login (link mágico, Google) entram nas specs seguintes e usam o serviço de entrada criado aqui. O administrador inicial vem da configuração.

## 2. Motivação & Escopo
**Motivação:** seguir, "vou", cadastro de show, perfis e o painel do admin dependem de saber quem está logado e com qual papel. A SPEC-0010 já deixou o cookie, as políticas `fan`/`artist`/`admin` e a autorização negada por padrão, mas o cookie ainda não tem conta nem sessão por trás, e o logout não invalida nada no servidor, ao contrário do que pede o ADR-0003.

**Objetivos (dentro do escopo):**
- Módulo `Users` com o padrão do ADR-0001 (Domain, Application, Infrastructure, Presentation, IntegrationEvents) e schema `users` (ADR-0002).
- `User`: id, e-mail normalizado (minúsculas, sem espaços) e único, nome de exibição opcional (até 60 caracteres), papéis (`fan` sempre; `admin` quando o e-mail está em `Users:AdminEmails`), data de criação.
- `Session`: id aleatório de 256 bits (guardado só como hash), usuário, criada em, último uso, expira em (30 dias deslizantes), expira de vez em (90 dias desde a entrada, sem renovação), revogada em, método de entrada (`magic_link` | `google`).
- Serviço interno `ISignInService.SignInAsync(email, method)` — encontra ou cria o usuário, atualiza os papéis de admin, cria a sessão e emite o cookie com o id da sessão. As specs de login só chamam esse serviço.
- Validação da sessão em cada requisição autenticada (`OnValidatePrincipal`): sessão inexistente, revogada ou expirada (30 dias sem uso ou 90 dias desde a entrada) → cookie rejeitado (401); renovação do prazo no máximo uma vez por hora, nunca além do teto de 90 dias. Sem o cookie no navegador não existe sessão: nada de login lembrado por outro meio.
- Respostas de `/me` e `/logout` com `Cache-Control: no-store`, para que nenhum cache guarde dados de quem está logado.
- Rate limit em **todo** endpoint da API: `/me` e `/logout` na política `default`; os endpoints que hoje não declaram limite (`GET /api/v1/cities`, a agenda da cidade e o token anti-forgery) passam a usar `default`; um teste de arquitetura reprova endpoint novo sem política de rate limit (exceto as sondas `/health` e `/alive`, chamadas pelo monitor e pelo orquestrador, e o `/openapi/*` que só existe em desenvolvimento — Emenda v2).
- `GET /api/v1/me` e `POST /api/v1/auth/logout` (revoga a sessão e apaga o cookie).
- Evento de integração `UserRegistered` (id, data) na primeira entrada, pela outbox, para o módulo de avisos consumir depois. Leva só o id: o e-mail não sai do módulo `Users`.

**Não-objetivos (fora do escopo):**
- Telas e métodos de login: link mágico (SPEC-0016), Google, "Minha conta" com exportação e exclusão (specs da onda 3).
- Papel `artist`: concedido pela spec de Perfis.
- Lista de sessões ativas e "sair de todos os aparelhos" (não pedido no PRD).

## 3. Dependências
- **Implementações necessárias:** N/A — a fundação (SPEC-0001) já está `implemented` e é a única base.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** N/A — PostgreSQL local via Aspire.

## 4. Decisão Arquitetural
**Contexto:** padrão do módulo `Events` (SPEC-0008): `src/Modules/Events/**`, slices em `Features/<Caso>/` (consulta + handler + validator + endpoint `IEndpoint`), `ModuleDbContext` com schema próprio, outbox (SPEC-0009) e testes em `tests/Modules/Events/**`. Segurança base em `src/Subsolo.Api/Security/SecurityExtensions.cs` (SPEC-0010).

**Decisão:** novo módulo `Users` copiando a estrutura do `Events`. A configuração do cookie continua no `SecurityExtensions`, que passa a delegar a validação do principal a `ISessionValidator`, exposto pelo `Users` em `Subsolo.Modules.Users.IntegrationEvents` (a superfície pública do módulo pelo ADR-0001). O cookie leva só o token da sessão (256 bits aleatórios, base64url; no banco, só o hash SHA-256) e o id do usuário; os papéis são lidos da sessão validada, então revogar ou trocar o papel vale na hora.

**Justificativa:** o ADR-0003 exige sessão revogável no servidor e permissões num só lugar. Guardar a sessão no banco custa uma consulta por requisição autenticada, irrelevante no volume do lançamento.

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** `ITicketStore` guardando o ticket inteiro no banco (serializa claims e dificulta trocar papéis sem novo login); JWT sem estado (sem revogação, viola o ADR-0003); deixar só o cookie atual (logout não invalida no servidor).

**ADRs:** ADR-0001, ADR-0002, ADR-0003, ADR-0008, ADR-0010

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** validação da sessão em ≤ 5 ms p95 (uma consulta por chave primária) — medida no IT-04 com 200 requisições; renovação gravada no máximo 1 vez por hora por sessão.
- **Segurança:** id da sessão aleatório de 256 bits, guardado só como hash SHA-256 (vazamento do banco não dá sessões válidas); logout revoga no servidor; teto absoluto de 90 dias por sessão (decisão do Thomas em 2026-10-17); `Cache-Control: no-store` nas respostas com dados do usuário; rate limit declarado em todo endpoint (IT-10); `/me` e `/logout` exigem usuário autenticado; logout é mutação e passa pelo anti-forgery; endpoints com política explícita (teste de arquitetura existente) — UT-03, UT-04, IT-02, IT-03, IT-05, IT-10.
- **Privacidade e dados pessoais:** guarda e-mail e nome opcional (base legal: execução do serviço pedido pelo usuário, ADR-0008); o e-mail nunca vai para logs nem traces (mascarado `f***@email.com`) — IT-06; exportação e exclusão em spec própria da onda 3.
- **Disponibilidade e resiliência:** banco fora do ar durante a validação → 503 em ProblemDetails, nunca usuário anônimo silencioso nem 500 com detalhes — IT-07.
- **Acessibilidade (UI):** N/A — sem tela nesta spec.
- **Custo:** zero; tabelas no PostgreSQL existente (ADR-0012).

## 6. Artefato A — Contrato
**Interface:** `GET /api/v1/me` · `POST /api/v1/auth/logout` · `ISignInService` · `ISessionValidator` · evento `UserRegistered`

```yaml
GET /api/v1/me:                       # RequireAuthorization(fan) + RequireRateLimiting(default)
  200: { id: uuid, email: string, displayName: string|null, roles: ("fan"|"artist"|"admin")[] }
  401: ProblemDetails code=auth.required          # sem cookie, sessão revogada ou expirada (30 dias sem uso ou 90 desde a entrada)
  429: ProblemDetails code=security.rate_limited (+ Retry-After)
  # toda resposta: Cache-Control: no-store

POST /api/v1/auth/logout:             # RequireAuthorization(fan) + X-CSRF + RequireRateLimiting(default)
  204: sessão revogada; Set-Cookie __Host-subsolo apagado
  401: ProblemDetails code=auth.required
  400: ProblemDetails code=security.csrf_invalid   # sem X-CSRF válido (SPEC-0010)
  429: ProblemDetails code=security.rate_limited
  # toda resposta: Cache-Control: no-store

GET /api/v1/cities, GET /api/v1/cities/{cidade}/agenda, GET do token anti-forgery:
  passam a declarar RequireRateLimiting(default); 429 ProblemDetails code=security.rate_limited + Retry-After acima de 100/min por IP
```

```csharp
// Subsolo.Modules.Users.Application (interno ao módulo; usado pelos slices de login)
Task<Result<SignInResult>> ISignInService.SignInAsync(string email, SignInMethod method, CancellationToken ct);
//   SignInResult { Guid UserId, bool IsNewUser }  — emite o cookie na resposta atual
//   erro: users.invalid_email (e-mail fora do formato ou > 254 caracteres)

// Subsolo.Modules.Users.IntegrationEvents (público)
Task<SessionPrincipal?> ISessionValidator.ValidateAsync(string sessionToken, CancellationToken ct);   // token de 256 bits do cookie (base64url); Emenda v2
//   null → sessão inexistente, revogada ou expirada
//   SessionPrincipal { Guid UserId, IReadOnlyList<string> Roles }
record UserRegisteredIntegrationEvent(Guid UserId, DateTimeOffset RegisteredAtUtc);
```

```text
Configuração: Users:AdminEmails — lista de e-mails (não é segredo; appsettings/parâmetro do AppHost)
Banco (schema users): users(id, email UNIQUE, display_name, roles, created_at_utc),
                      sessions(id_hash PK, user_id FK, method, created_at_utc, last_seen_at_utc, expires_at_utc, absolute_expires_at_utc, revoked_at_utc)
```

**Design:** N/A — sem interface (as telas Entrar e Confira seu e-mail são da SPEC-0016).

**Arquivos/módulos afetados:** ver `touches` no frontmatter. Novo `src/Modules/Users/` (5 projetos) e `tests/Modules/Users/` (unitários e integração); registro no `Program.cs` e no `Subsolo.slnx`; `SecurityExtensions` ganha a validação da sessão; snapshot do OpenAPI e cliente TS regenerados.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Primeira entrada | e-mail novo | cria `User` com papel `fan`, cria sessão, emite cookie, publica `UserRegistered` | UT-01, IT-01 |
| Entrada de usuário existente | e-mail já cadastrado (com outra caixa ou espaços) | mesmo usuário, nova sessão, sem novo evento | UT-01, IT-01 |
| Admin pela configuração | e-mail em `Users:AdminEmails` | papéis `fan` + `admin`; removido da lista → perde `admin` na próxima entrada | UT-02, IT-01 |
| E-mail inválido | formato inválido ou > 254 caracteres | `users.invalid_email`, nada gravado | UT-02 |
| Quem sou eu | cookie de sessão válida | 200 com id, e-mail, nome e papéis | IT-02 |
| Sem sessão | sem cookie, sessão revogada ou expirada | 401 `auth.required` | UT-03, IT-02, IT-03 |
| Sair | cookie válido + X-CSRF | 204, sessão revogada, cookie apagado; o mesmo cookie depois → 401 | IT-03 |
| Sair sem anti-forgery | sem X-CSRF | 400 `security.csrf_invalid`, sessão continua válida | IT-05 |
| Prazo deslizante | uso depois de 1 h | `expires_at` renovado para +30 dias; uso antes de 1 h não grava | UT-04, IT-04 |
| Teto de 90 dias | sessão usada sempre, 90 dias desde a entrada | renovação nunca passa do teto; depois dele → 401, precisa entrar de novo | UT-03, UT-04, IT-04 |
| Sem cache de dados do usuário | `/me` e `/logout` | `Cache-Control: no-store` | IT-02, IT-03 |
| Rate limit em todo endpoint | endpoint da API sem política de rate limit | teste de arquitetura reprova; `/me` acima de 100/min → 429 | IT-10 |
| Papel admin exigido | usuário só `fan` num endpoint `admin` | 403 `auth.forbidden` | IT-08 |
| E-mail fora dos logs | entrada e `/me` com logs e traces capturados | nenhum e-mail em claro | IT-06 |
| Banco fora do ar | validação da sessão sem banco | 503 ProblemDetails | IT-07 |
| Regras do módulo | arquitetura | `Users` segue o ADR-0001 (aplicado automaticamente aos módulos) | IT-09 |
| Contrato publicado | snapshot OpenAPI | `/me` e `/logout` com esquemas e códigos do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — módulo novo; o comportamento atual do cookie (SPEC-0010) segue coberto pelos testes da `tests/Security/**`.

### 7.2 Testes Unitários
- **UT-01** — Dado um repositório em memória, quando `SignInAsync(" Fulano@Email.COM ", MagicLink)` é chamado duas vezes, então existe um único `User` com e-mail `fulano@email.com`, papel `fan`, duas sessões, e `UserRegistered` foi gerado só na primeira vez.
- **UT-02** — Dado `Users:AdminEmails = [admin@subsolo.test]`, quando esse e-mail entra, então os papéis são `fan` e `admin`; dado o e-mail fora da lista numa entrada seguinte, então o papel `admin` sai; dado `"sem-arroba"` ou 255 caracteres, então retorna `users.invalid_email` e nada é gravado.
- **UT-03** — Dadas sessões revogada, expirada (com `FakeTimeProvider`) e inexistente, quando `ISessionValidator.ValidateAsync` é chamado, então retorna `null`; dada uma sessão usada todo dia mas criada há 90 dias e 1 minuto, também retorna `null`; dada uma sessão válida, retorna o usuário e os papéis atuais.
- **UT-04** — Dada uma sessão usada há 59 min, quando validada, então `expires_at` não muda; usada há 61 min, então passa a `agora + 30 dias`; criada há 75 dias e usada há 61 min, então passa ao teto (`criada + 90 dias`), não a `agora + 30 dias`.

### 7.3 Testes de Integração
- **IT-01** — Dado o PostgreSQL real (Testcontainers) e a API, quando um usuário entra pelo `ISignInService` (host de teste) duas vezes, uma delas com um e-mail de admin, então o banco tem um usuário, duas sessões com id guardado só como hash (o valor do cookie não aparece na tabela), os papéis certos e uma mensagem `UserRegistered` na outbox.
- **IT-02** — Dado um cookie emitido pelo `SignInAsync`, quando `GET /api/v1/me` é chamado, então 200 com o contrato do §6 e `Cache-Control: no-store`; sem cookie, então 401 `auth.required`.
- **IT-03** — Dado um cookie válido e o token X-CSRF, quando `POST /api/v1/auth/logout` é chamado, então 204 com `Cache-Control: no-store`, `Set-Cookie` apaga `__Host-subsolo`, `revoked_at` é gravado e o mesmo cookie em `/me` volta 401.
- **IT-04** — Dado um `FakeTimeProvider` avançado 2 h, quando `/me` é chamado, então `expires_at` é renovado uma vez; com o relógio 90 dias e 1 minuto depois da entrada (usando a sessão a cada 20 dias), `/me` volta 401; 200 chamadas seguidas têm validação com p95 ≤ 5 ms e não gravam de novo.
- **IT-05** — Dado um cookie válido sem X-CSRF, quando `/logout` é chamado, então 400 `security.csrf_invalid` e a sessão segue válida.
- **IT-06** — Dados os logs e spans capturados (coletor em memória do OpenTelemetry) durante a entrada e o `/me`, quando inspecionados, então o e-mail não aparece em claro.
- **IT-07** — Dado o container do PostgreSQL pausado, quando `/me` é chamado com cookie, então 503 em ProblemDetails sem detalhes da exceção.
- **IT-08** — Dado um usuário só `fan`, quando chama um endpoint de teste `RequireAuthorization(admin)`, então 403 `auth.forbidden`; com papel `admin`, 200.
- **IT-09** — Dados os testes de arquitetura (SPEC-0007), quando rodam com o módulo `Users`, então as regras do ADR-0001 valem para ele sem configuração extra e nenhum outro módulo referencia `Users.Domain`, `Users.Application` ou `Users.Infrastructure` (guarda: passa antes da mudança).
- **IT-10** — Dados os endpoints mapeados da API, quando o teste de arquitetura de rate limit (ao lado de `EndpointAuthorizationRules`) os inspeciona, então todos, exceto `/health`, `/alive` e `/openapi/*` (só em desenvolvimento), declaram uma política de rate limit, e um endpoint de exemplo sem política é reprovado; com a política `default` configurada para 3 por minuto no teste, a 4ª chamada a `/me` e a `GET /api/v1/cities` recebe 429 `security.rate_limited` com `Retry-After`.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então `/api/v1/me` e `/api/v1/auth/logout` têm os esquemas e os códigos de erro (`auth.required`, `security.csrf_invalid`) do §6, e `(cd web && pnpm api:check)` passa com o cliente regenerado. O consumidor (SPEC-0016) depende desse contrato.

### 7.5 Testes E2E
- N/A — nenhuma jornada de usuário muda: ainda não há como entrar pela interface. A jornada de entrar e sair é provada pelo E2E da SPEC-0016.

### 7.6 Outros
- Segurança: checklist OWASP de sessão no G4 (fixação de sessão — id novo a cada entrada —, cookie `__Host-`, revogação).
- Desempenho: p95 da validação medido no IT-04.

**Dublês e dados de teste:** `FakeTimeProvider`; repositórios em memória nos unitários; e-mails `@subsolo.test`; host de teste com o endpoint `admin` só no IT-08.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL 18) local e no CI, em série (`--max-parallel-test-modules 1`).

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** migração inicial do schema `users` (tabelas `users` e `sessions`), aditiva. Os endpoints públicos existentes ganham rate limit `default` (100/min por IP), folgado para uso normal.
- **Compatibilidade:** cookies emitidos antes desta spec (só em testes; não há usuários) passam a ser rejeitados por não terem sessão — sem impacto real.
- **Observabilidade:** contador `subsolo.users.sign_ins` (por método) e `subsolo.users.sessions_revoked`; logs de entrada e saída com o id do usuário, nunca o e-mail.
- **Rollback:** reverter o merge commit; a migração é aditiva e pode ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Como o administrador inicial é definido? — Pela configuração `Users:AdminEmails` (e-mail do Thomas no go-live), sem tela; mudar o admin é mudar a configuração (recomendação do Architect dentro do piloto automático, 2026-10-17; confirmada no H1)
- [x] A sessão tem teto absoluto, mesmo com uso contínuo? — Sim, 90 dias desde a entrada; depois disso a pessoa entra de novo pelo e-mail (Thomas, 2026-10-17)
- [x] Sessão some ao apagar os cookies? Rate limit em tudo? — O Thomas pediu (2026-10-17) que a sessão termine de verdade (sem login "infinito") e que exista rate limit; viraram o teto de 90 dias, o `no-store`, o IT-10 aqui e o E2E-03 da SPEC-0016

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Domínio e sessão**
- [ ] Red: UT-01..UT-04 com a tag `SPEC-0014:<ID>`, falhando pelo motivo certo
- [ ] Green: módulo `Users` (5 projetos, padrão do `Events`), `User`, `Session` (hash, 30 dias deslizantes, teto de 90), `ISignInService`, `ISessionValidator`
- [ ] Refactor mantendo tudo verde

**Fase 2: API, persistência e rate limit**
- [ ] Red: IT-01..IT-10 e CT-01 falhando pelo motivo certo (IT-09 é guarda)
- [ ] Green: schema `users` e migração, validação no `OnValidatePrincipal`, `/me` e `/logout` com `no-store`, outbox `UserRegistered`, rate limit `default` nos endpoints existentes e regra de arquitetura, OpenAPI e cliente TS regenerados
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 54ab48c (árvore suja) | 2026-10-17 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 0680945 | 2026-10-17 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0014`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 (escopo) | 2026-10-17 | `touches` ganha `tests/**/packages.lock.json` | Referenciar um módulo novo no `Subsolo.Api` regrava o lock dos projetos de teste que referenciam a API (o CI restaura em modo travado); achado no verify da SPEC-0014 | nenhuma | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
| 2 | 2026-10-17 | `ISessionValidator.ValidateAsync(string sessionToken)` no lugar de `Guid sessionId`: o cookie leva o token de 256 bits em base64url e o banco guarda só o hash; IT-10 isenta `/alive` e `/openapi/*` (só desenvolvimento) além de `/health` | Relatório do Test-writer: Guid tem 128 bits, abaixo dos 256 do §5 e do ADR-0003; `/alive` é sonda do orquestrador e `/openapi` não existe em produção | nenhuma (o único consumidor de `ISessionValidator` é o `SecurityExtensions`, dentro desta spec; `impacted` sem consumidores fixados) | thomas, 2026-10-17 |
