---
id: SPEC-0020
title: Entrar com Google
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-17
parent: SPEC-0002
depends_on: [SPEC-0016]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Users/**, tests/Modules/Users/**, src/Subsolo.Api/Security/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/Subsolo.Api.csproj, src/Subsolo.Api/packages.lock.json, src/Subsolo.Api/appsettings.json, src/Subsolo.Api/appsettings.Development.json, tests/Security/**, src/Subsolo.AppHost/**, tests/Common/Subsolo.AppHost.IntegrationTests/**, Directory.Packages.props, tests/**/packages.lock.json, docs/api/**, web/src/api/**, web/src/app/(public)/entrar/**, e2e/tests/auth/**, tools/compose-up.sh]
adrs: [ADR-0003, ADR-0008, ADR-0009]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0020 — Entrar com Google

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O fã passa a poder entrar no Subsolo com a conta Google, num toque, além do link por e-mail. O botão "Continuar com Google" da tela `/entrar` (já desenhada no Stitch) leva ao Google e volta logado para onde estava. A conta é ligada pelo e-mail verificado: quem já entrou por link mágico com o mesmo e-mail cai na mesma conta. Localmente e no CI, um provedor OpenID Connect falso em contêiner faz o papel do Google; o cliente real só é criado no go-live (SPEC-0003).

## 2. Motivação & Escopo
**Motivação:** o PRD e o ADR-0003 preveem Google + link mágico; o Google tira o passo de abrir o e-mail, que é o atrito do link mágico no celular. Decisão do Thomas (2026-10-17): cliente OAuth do Google só no go-live, provedor falso no local e no CI.

**Objetivos (dentro do escopo):**
- Login OpenID Connect (fluxo de código com PKCE, `state` e `nonce`) com o handler `Microsoft.AspNetCore.Authentication.OpenIdConnect`, configurado por `Users:Google` (Authority, ClientId, ClientSecret).
- `GET /api/v1/auth/google?returnTo=` inicia o login; a volta acontece no `CallbackPath` do handler (`/api/v1/auth/google/callback`); ao validar o retorno, exige `email_verified = true` e chama `ISignInService.SignInAsync(email, Google)` (SPEC-0014), que cria a sessão e o cookie `__Host-subsolo`.
- `GET /api/v1/auth/providers` → `{ google: bool }`: o botão só aparece quando o Google está configurado.
- Erros voltam para `/entrar?erro=google` (cancelou, falhou ou estado inválido) ou `/entrar?erro=google_email` (e-mail não verificado), com mensagem na tela.
- Provedor OIDC falso (imagem `mock-oauth2-server`, versão fixada pelo ADR-0009) no AppHost em modo `run` e na pilha local do `tools/compose-up.sh`, com o mesmo emissor visto pelo navegador e pela API.

**Não-objetivos (fora do escopo):**
- Cliente OAuth real do Google (go-live, SPEC-0003).
- Selo "Entrou com Google" em "Minha conta" (o `/me` não expõe o método; fica para quando houver pedido).
- Outros provedores (Apple, Facebook) e vínculo de várias contas Google ao mesmo usuário.
- Guardar tokens do Google: o Subsolo usa só o e-mail verificado e descarta o resto.

## 3. Dependências
- **Implementações necessárias:** SPEC-0016 — tela `/entrar`, saneamento do `returnTo` e o `ISignInService` em uso real (e, por ela, SPEC-0014).
- **Contratos consumidos:** N/A — usa o `ISignInService` do mesmo módulo.
- **Pré-requisitos externos:** imagem do provedor OIDC falso (`ghcr.io/navikt/mock-oauth2-server`, licença MIT; versão exata fixada e verificada na implementação, ADR-0009); pacote `Microsoft.AspNetCore.Authentication.OpenIdConnect` da mesma versão do ASP.NET Core em uso (dependência nova prevista no ADR-0003).

## 4. Decisão Arquitetural
**Contexto:** cookie de sessão e políticas no `src/Subsolo.Api/Security/SecurityExtensions.cs` (SPEC-0010/0014); slices do `Users` (SPEC-0014/0016); tela `/entrar` da SPEC-0016; containers locais no AppHost (Mailpit, Seq) e override local do `tools/compose-up.sh`.

**Decisão:** esquema OIDC `google` registrado ao lado do cookie, com um cookie externo temporário (`__Host-subsolo-ext`, 10 minutos, só durante o retorno) como `SignInScheme`; o evento `OnTicketReceived` valida `email_verified`, chama o `ISignInService`, apaga o cookie externo e redireciona para o `returnTo` saneado (mesmo saneador da SPEC-0016, guardado nas `AuthenticationProperties`). Nada do Google é persistido além do e-mail já normalizado.

**Justificativa:** ADR-0003 (Google por OIDC, conta ligada pelo e-mail verificado, autenticação centralizada no `Users`); o handler oficial cuida de PKCE, `state`, `nonce` e validação do `id_token`, em vez de código próprio de segurança.

**Desvio do padrão existente:** Nenhum (o handler OIDC e a dependência estão previstos no ADR-0003).

**Alternativas descartadas:** pacote `Microsoft.AspNetCore.Authentication.Google` (OAuth 2.0 sem OIDC: sem `nonce`/`id_token` e sem apontar para um provedor falso); login pelo front com Auth.js (descartado no ADR-0003); testar contra o Google real no CI (exige conta e segredo no CI, frágil e pago em atenção).

**ADRs:** ADR-0003, ADR-0008, ADR-0009

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** o retorno do provedor até o cookie emitido em ≤ 500 ms p95 com o provedor falso (IT-01).
- **Segurança:** PKCE, `state` e `nonce` do handler; `email_verified` obrigatório; `returnTo` só relativo; cookie externo `__Host-`, HttpOnly, 10 min e apagado no fim; sessão nova a cada entrada; rate limit `auth` no início do login; segredo do cliente só por segredo (ADR-0008) — IT-02, IT-03, IT-05, UT-01.
- **Privacidade e dados pessoais:** guarda só o e-mail (mesma base legal da conta, ADR-0008); `id_token`, nome e foto do Google descartados; e-mail fora dos logs (IT-06).
- **Disponibilidade e resiliência:** provedor fora do ar → volta para `/entrar?erro=google` e o link por e-mail continua funcionando (IT-04).
- **Acessibilidade (UI):** botão com rótulo visível "Continuar com Google" (ícone decorativo com `aria-hidden`), alvo ≥ 44 px, mensagem de erro em `role="alert"`; axe no E2E.
- **Custo:** zero agora; o cliente do Google é gratuito e criado no go-live.

## 6. Artefato A — Contrato
**Interface:** `GET /api/v1/auth/providers` · `GET /api/v1/auth/google` · `/api/v1/auth/google/callback` (handler) · tela `/entrar`

```yaml
GET /api/v1/auth/providers:            # AllowAnonymous + RequireRateLimiting(default)
  200: { google: boolean }             # true só com Users:Google:Authority e ClientId configurados
GET /api/v1/auth/google?returnTo=:     # AllowAnonymous + RequireRateLimiting(auth)
  302: para o authorize do provedor (code + PKCE + state + nonce)
  404: ProblemDetails code=users.provider_disabled          # Google não configurado
  429: ProblemDetails code=security.rate_limited
/api/v1/auth/google/callback:          # tratado pelo handler OIDC
  302 → returnTo saneado (padrão "/"); Set-Cookie __Host-subsolo (sessão nova); cookie externo apagado
  302 → /entrar?erro=google            # usuário cancelou, provedor fora do ar, state/nonce inválidos
  302 → /entrar?erro=google_email      # e-mail ausente ou email_verified ≠ true; nenhuma sessão criada
```

```text
Configuração: Users:Google:Authority, Users:Google:ClientId, Users:Google:ClientSecret (segredo)
  local/CI: provedor falso (AppHost em run; override do compose-up.sh), emissor único para navegador e API
  produção: cliente real do Google (SPEC-0003)
/entrar (SPEC-0016):
  com google=true: botão "Continuar com Google" acima do formulário, separador "ou"
  ?erro=google       → "Não deu para entrar com o Google. Tente de novo ou use o link por e-mail."
  ?erro=google_email → "Sua conta Google não tem e-mail verificado. Use o link por e-mail."
```

**Design:** Stitch, projeto 15194126668426659178, tela "Entrar - Subsolo" (eefd8a676a5d4a8881cec1c1abb29738), com o botão do Google já desenhado; as mensagens de erro usam o mesmo bloco de alerta do formulário.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Entrar com Google | provedor devolve e-mail verificado | sessão nova, cookie `__Host-subsolo`, volta ao `returnTo` | UT-01, IT-01, E2E-01 |
| Conta já existente | mesmo e-mail já entrou por link mágico | mesmo usuário (mesmo id), sessão nova com método `google` | IT-01 |
| E-mail não verificado | `email_verified` falso ou ausente | `/entrar?erro=google_email`; nenhuma sessão nem usuário criados | UT-01, IT-02 |
| Cancelou ou estado inválido | erro do provedor, `state` adulterado | `/entrar?erro=google`; nenhuma sessão | IT-03, E2E-02 |
| Provedor fora do ar | authority inacessível | `/entrar?erro=google`; link por e-mail segue funcionando | IT-04 |
| Google desligado | sem configuração | `providers.google = false`; início → 404 `users.provider_disabled`; botão some | UT-02, IT-04, E2E-02 |
| returnTo seguro | `//evil.com` e afins | vira `/` | UT-01 |
| Rate limit | 6º início no minuto pelo mesmo IP | 429 `security.rate_limited` | IT-05 |
| Dados fora dos logs | fluxo completo com captura | e-mail mascarado; nada do `id_token` | IT-06 |
| Contrato publicado | snapshot OpenAPI | `/auth/providers` e `/auth/google` com respostas e códigos do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — fluxo novo; `/entrar` sem o botão segue coberta pelos E2E da SPEC-0016.

### 7.2 Testes Unitários
- **UT-01** — Dado o tratador do retorno com um `ClaimsPrincipal` de teste, quando o e-mail vem com `email_verified=true`, então chama `SignInAsync(email, Google)` e redireciona para o `returnTo` saneado (`/curitiba` mantido; `//evil.com`, `https://evil.com` → `/`); com `email_verified` falso ou ausente, ou sem e-mail, não chama o serviço e redireciona para `/entrar?erro=google_email`.
- **UT-02** — Dadas as opções `Users:Google` sem Authority ou sem ClientId, quando o módulo é configurado, então o provedor fica desligado (`providers.google=false`) e a API sobe normalmente; com as duas, fica ligado.

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL e o provedor OIDC falso reais (Testcontainers), quando o cliente segue o fluxo (início → authorize do provedor com login automático do e-mail `fa@subsolo.test` verificado → callback), então volta para o `returnTo` com `Set-Cookie __Host-subsolo`, `/me` responde 200 com o e-mail, o cookie externo foi apagado; repetindo depois de criar a conta pelo `SignInAsync(MagicLink)`, o id do usuário é o mesmo; p95 do callback ≤ 500 ms em 10 rodadas.
- **IT-02** — Dado o provedor falso devolvendo `email_verified=false` (e outra rodada sem e-mail), quando o fluxo termina, então 302 para `/entrar?erro=google_email` e nenhuma linha nova em `users` nem `sessions`.
- **IT-03** — Dado um callback com `state` adulterado e outro com `error=access_denied`, quando chegam à API, então 302 para `/entrar?erro=google` e nenhuma sessão criada.
- **IT-04** — Dada a API sem `Users:Google`, quando `GET /auth/providers` e `GET /auth/google` são chamados, então `{ google: false }` e 404 `users.provider_disabled`; dada a authority apontando para um endereço sem servidor, o início devolve 302 para `/entrar?erro=google` em ≤ 11 s.
- **IT-05** — Dado o limite `auth` configurado para 5 por minuto, quando o mesmo IP chama `GET /auth/google` 6 vezes, então a 6ª recebe 429 com `Retry-After`.
- **IT-06** — Dados os logs e spans capturados durante o fluxo completo, quando inspecionados, então o e-mail só aparece mascarado e nenhum `id_token`, `code` ou `state` aparece.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então `/api/v1/auth/providers` e `/api/v1/auth/google` têm as respostas e códigos do §6, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a pilha do Compose com o provedor falso, nos projetos mobile-chrome e mobile-safari: o fã em `/cidade-exemplo` toca em "Conta", vê "Continuar com Google" em `/entrar`, toca, preenche o login do provedor falso com um e-mail verificado e volta para `/cidade-exemplo`; "Conta" leva a `/conta`, que mostra o e-mail. Sem violações do axe em `/entrar`. [jornada: entrar]
- **E2E-02** — Dada a pilha do Compose, no projeto mobile-chrome: cancelando no provedor falso, o fã volta para `/entrar` com "Não deu para entrar com o Google…" anunciado e o formulário do link segue usável; com o Google desligado por configuração de teste, o botão não aparece. [jornada: entrar]

### 7.6 Outros
- Segurança: checklist OWASP de autenticação no G4 (redirecionamento aberto, CSRF do fluxo, fixação de sessão, validação do `id_token`).
- Herança da SPEC-0017 (instabilidade vista no mobile-safari): o E2E-01 da SPEC-0016 preencheu o e-mail antes de a página ficar pronta para digitação (campo vazio e "Digite um e-mail válido."); o helper e o teste esperam o formulário estar interativo (hidratado) antes de digitar.
- Herança da SPEC-0017: o helper `signIn` de `e2e/tests/auth/journey.ts` passa a sempre esperar a navegação depois de entrar (com ou sem `returnTo`), para nenhum teste abrir outra página antes do cookie gravar.
- Herança da revisão da SPEC-0016: a região `aria-live` do reenvio em `/entrar` nasce vazia e é preenchida logo depois de montada, para o início da espera ser anunciado.

**Dublês e dados de teste:** provedor OIDC falso (Testcontainers no IT; contêiner na pilha do Compose no E2E); e-mails `@subsolo.test`; `ClaimsPrincipal` de teste no UT.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL + provedor falso); E2E contra a pilha do Compose (`https://localhost:8443`); `--workers=1`.

## 8. Plano de Rollout
- **Estratégia:** desligado por padrão (sem configuração o botão some); liga no go-live com o cliente real (SPEC-0003).
- **Dados/schema:** nenhum (a sessão já tem o método `google`, SPEC-0014).
- **Compatibilidade:** endpoints novos em `/api/v1`; `/entrar` sem Google continua igual.
- **Observabilidade:** o contador `subsolo.users.sign_ins` já separa por método; logs de falha do retorno com o motivo (`state`, `email_not_verified`, `provider_error`), sem e-mail.
- **Rollback:** tirar a configuração `Users:Google` (o botão some na hora) ou reverter o merge.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Quando criar o cliente OAuth do Google? — No go-live (SPEC-0003); local e CI com provedor OIDC falso em contêiner (Thomas, 2026-10-17)
- [x] Mostrar "Entrou com Google" em Minha conta? — Não nesta spec; o `/me` não expõe o método e não há pedido no PRD (recomendação do Architect; piloto automático do Thomas, 2026-10-17)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"); tela já aprovada na SPEC-0016.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Domínio e regras**
- [ ] Red: UT-01, UT-02 com a tag `SPEC-0020:<ID>`, falhando pelo motivo certo
- [ ] Green: implementar o mínimo seguindo o padrão de referência
- [ ] Refactor mantendo tudo verde

**Fase 2: API, persistência e contratos**
- [ ] Red: IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, CT-01 falhando pelo motivo certo
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
| G0 Spec | PASS | validate: 0 erro(s) — 52c16a4 | 2026-10-17 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0020`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
