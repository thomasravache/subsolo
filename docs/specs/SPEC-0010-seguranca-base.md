---
id: SPEC-0010
title: Segurança base
tier: full
type: foundation
user_facing: true
status: implemented
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0008]
consumes_contract: []
contract_version: 1
touches: [Subsolo.slnx, web/src/proxy.test.ts, web/src/app/layout.tsx, web/src/app/page.tsx, web/src/app/not-found.tsx, src/Subsolo.AppHost/Program.cs, tests/Common/Subsolo.AppHost.IntegrationTests/**, src/Subsolo.ServiceDefaults/Extensions.cs, e2e/tests/agenda/**, e2e/playwright.config.ts, tools/compose-up.sh, tests/Common/Subsolo.Api.IntegrationTests/ThrowingEndpoint.cs, src/Common/Subsolo.Common.Presentation/Security/**, src/Subsolo.Api/Security/**, src/Subsolo.Api/Program.cs, web/src/proxy.ts, web/next.config.ts, infra/caddy/**, docs/runbooks/checklist-seguranca.md, tests/Security/**, tests/Subsolo.ArchitectureTests/Security/**, e2e/tests/security/**]
adrs: [ADR-0003, ADR-0008, ADR-0004, ADR-0017]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0010 — Segurança base

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Aplica a segurança base dos ADR-0003 e ADR-0008 antes da primeira feature com usuário: autorização negada por padrão (todo endpoint declara a política, verificado por teste de arquitetura), cabeçalhos de segurança (CSP, HSTS, frame-ancestors etc.) no Next.js e na API, rate limiting configurável, proteção CSRF para mutações, e Caddy com HTTPS e HSTS no Compose. O login em si (módulo Users) vem no épico do produto.

## 2. Motivação & Escopo
**Motivação:** brechas de autorização e de cabeçalhos são baratas de prevenir no início e caras de achar depois; as features de cadastro e "vou" já nascem protegidas.

**Objetivos (dentro do escopo):**
- Política de fallback do ASP.NET Core = usuário autenticado; teste de arquitetura: todo endpoint tem `RequireAuthorization(<política>)` ou `AllowAnonymous()` explícito.
- Políticas nomeadas e preparadas (sem login ainda): `fan`, `artist`, `admin`, com o esquema de autenticação por cookie configurado (`__Host-subsolo`, HttpOnly, Secure, SameSite=Lax).
- Anti-forgery: mutações (POST/PUT/PATCH/DELETE) exigem o cabeçalho `X-CSRF` com o token emitido em `GET /api/v1/antiforgery`.
- Rate limiting (`Microsoft.AspNetCore.RateLimiting`): políticas `auth` (5/min por IP), `report` (10/h por IP) e `default` (100/min por IP), aplicadas por atributo.
- Cabeçalhos no Next.js (`next.config.ts` + middleware com nonce): CSP com nonce, `X-Content-Type-Options`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`, `frame-ancestors 'none'`.
- Caddy (Compose): HTTPS (certificado local `tls internal` no Compose local), HSTS, compressão; a API só é alcançável pelo Caddy.
- Checklist OWASP Top 10 do G4 em `docs/runbooks/checklist-seguranca.md`.

**Não-objetivos (fora do escopo):**
- Login com link mágico e Google, sessões e papéis concretos (épico SPEC-0002, módulo Users).
- TLS com certificado público e domínio real (SPEC-0003).

## 3. Dependências
- **Implementações necessárias:** SPEC-0008 — a página e o endpoint públicos da agenda precisam existir para verificar cabeçalhos e `AllowAnonymous`.
- **Contratos consumidos:** N/A — o contrato v1 da SPEC-0008 vem de uma implementação já em `depends_on`; o CT-01 garante que ele não muda.
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0003 (identidade própria, nega por padrão) e ADR-0008.

**Decisão:** segurança como configuração central (`Common.Presentation` + `web/src/proxy.ts`), com teste de arquitetura impedindo endpoint sem política.

**Justificativa:** com agentes escrevendo endpoints, a regra precisa falhar no CI, não depender da revisão.

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** autorização por convenção de rota (frágil); cabeçalhos só no Caddy (o CSP com nonce exige o Next.js).

**ADRs:** ADR-0003, ADR-0008, ADR-0004

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** rate limiting em memória (uma instância); overhead desprezível — sem medição dedicada.
- **Segurança:** OWASP A01 (acesso quebrado), A05 (configuração) e A07 (autenticação): cobertos por IT-01 a IT-05 e pelo checklist do G4.
- **Privacidade e dados pessoais:** o rate limiting usa o IP só em memória, sem persistir — revisão no G4.
- **Disponibilidade e resiliência:** o limite responde 429 com `Retry-After`, sem derrubar o serviço — IT-03.
- **Acessibilidade (UI):** a CSP não pode quebrar a página nem o axe — E2E-01.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `políticas de segurança HTTP`

```text
Autorização
  fallback: autenticado → 401 ProblemDetails code=auth.required
  políticas: fan | artist | admin → 403 ProblemDetails code=auth.forbidden
  endpoint público: AllowAnonymous() explícito (ex.: GET /api/v1/cities/{slug}/agenda)
CSRF
  GET /api/v1/antiforgery → 200 {token} + cookie
  mutação sem X-CSRF válido → 400 ProblemDetails code=security.csrf_invalid
Rate limiting
  excedeu → 429 ProblemDetails code=security.rate_limited + Retry-After
Cabeçalhos (todas as páginas do web)
  Content-Security-Policy: default-src 'self'; script-src 'self' 'nonce-…'; img-src 'self' data: blob:; frame-ancestors 'none'; …
  X-Content-Type-Options: nosniff ; Referrer-Policy: strict-origin-when-cross-origin ; Permissions-Policy: camera=(), microphone=(), geolocation=()
Caddy (Compose)
  HTTP → HTTPS 308 ; Strict-Transport-Security: max-age=31536000
```

**Design:** N/A — sem tela nova.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Endpoint sem política | endpoint de exemplo sem declaração | teste de arquitetura falha | IT-01 |
| Anônimo em endpoint protegido | sem cookie | 401 `auth.required` | IT-02 |
| Papel insuficiente | `fan` num endpoint `admin` | 403 `auth.forbidden` | IT-02 |
| Endpoint público | GET da agenda sem login | 200 | CT-01 |
| CSRF ausente | POST sem `X-CSRF` | 400 `security.csrf_invalid` | UT-01, IT-04 |
| Rate limit | 6 chamadas/min na política `auth` | 6ª → 429 com `Retry-After` | IT-03 |
| Cabeçalhos | qualquer página do web | CSP com nonce e demais cabeçalhos presentes | UT-02, IT-05 |
| HTTPS no Compose | `http://localhost:8080` | 308 para HTTPS; HSTS presente | IT-06 |
| Página funciona com a CSP | agenda no navegador | sem erro de CSP no console; axe ok | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o filtro de anti-forgery, quando uma requisição de mutação chega sem o cabeçalho `X-CSRF`, então retorna `security.csrf_invalid`; requisições GET passam.
- **UT-02** — Dado o gerador de CSP do middleware, quando gera os cabeçalhos para uma requisição, então o `script-src` contém um nonce novo (diferente a cada requisição) e `frame-ancestors 'none'`.

### 7.3 Testes de Integração
- **IT-01** — Dado um assembly de exemplo com um endpoint sem `RequireAuthorization` nem `AllowAnonymous`, quando o teste de arquitetura roda, então falha nomeando o endpoint; os endpoints reais passam.
- **IT-02** — Dados endpoints de teste com as políticas `artist` e `admin`, quando chamados sem login, então 401 `auth.required`; com um usuário de teste `fan` (handler de autenticação de teste), então 403 `auth.forbidden`.
- **IT-03** — Dado um endpoint com a política `auth`, quando chamado 6 vezes em 1 minuto pelo mesmo IP, então a 6ª chamada retorna 429 com `Retry-After`.
- **IT-04** — Dado um POST de teste sem token, quando chamado, então 400 `security.csrf_invalid`; com o token obtido em `/api/v1/antiforgery`, então 200.
- **IT-05** — Dado o servidor Next.js, quando `/cidade-exemplo` é requisitada, então a resposta tem `Content-Security-Policy`, `X-Content-Type-Options`, `Referrer-Policy` e `Permissions-Policy` conforme o contrato.
- **IT-06** — Dado o Compose local com o Caddy, quando `http://localhost:8080/` é requisitado, então a resposta é 308 para HTTPS e a resposta HTTPS tem `Strict-Transport-Security`.

### 7.4 Testes de Contrato
- **CT-01** — Dado o contrato SPEC-0008@1, quando `GET /api/v1/cities/cidade-exemplo/agenda` é chamado sem login e com a política de fallback ativa, então continua 200 com o mesmo schema (o endpoint declara `AllowAnonymous`).

### 7.5 Testes E2E
- **E2E-01** — Dado o app subido pelo AppHost, quando o fã abre a agenda da cidade no projeto mobile, então a página renderiza sem erros de CSP no console e sem violações do axe [jornada: agenda].

### 7.6 Outros
- Segurança: checklist OWASP Top 10 aplicado no G4 desta spec e salvo como modelo em `docs/runbooks/checklist-seguranca.md`.

**Dublês e dados de teste:** handler de autenticação de teste (`TestAuthHandler`) que injeta papéis; endpoints de exemplo só no projeto de testes.

**Ambiente de execução:** local e CI; IT-06 contra o Compose local.

## 8. Plano de Rollout
- **Estratégia:** deploy direto (sem produção até a SPEC-0003).
- **Dados/schema:** N/A
- **Compatibilidade:** o endpoint público da agenda continua anônimo (CT-01).
- **Observabilidade:** 401, 403, 429 e falhas de CSRF logados com o código, sem dados pessoais; contagem visível no Seq.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Autorização, CSRF e rate limiting (API)**
- [x] Red: UT-01, IT-01..IT-04 e CT-01 com a tag `SPEC-0010:<ID>`, falhando pelo motivo certo
- [x] Green: política de fallback, políticas `fan`/`artist`/`admin`, cookie `__Host-subsolo`, anti-forgery, rate limiting, teste de arquitetura de endpoints
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte + arquitetura (G2/G3)

**Fase 2: Cabeçalhos, Caddy e jornada**
- [x] Red: UT-02, IT-05, IT-06 e E2E-01 falhando
- [x] Green: middleware de CSP com nonce e cabeçalhos no Next.js; Caddy com HTTPS (`tls internal`) e HSTS; checklist OWASP
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte + arquitetura + E2E (G2/G3)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003; validação local pelo Compose quando aplicável
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-13 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — dfad7fe | 2026-10-16 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ next typegen && tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 27ea040 | 2026-10-16 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 1s 240ms) — 27ea040 | 2026-10-16 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED em 27ea040 — autorização negada por padrão, CSRF, rate limiting, CSP com nonce e Caddy com HTTPS/HSTS conferidos; aviso do verify justificado (helper do scaffold 663e4e4 preenchido em 18da406 sem mudar asserções); 0 bloqueantes, 0 importantes, 7 menores (X-Forwarded-For de redes privadas, partição IPv6 por /64 e teste via Caddy, bind parcial do rate limit, nosniff global no Caddy, TLS_ISSUER obrigatório no go-live, ignoreHTTPSErrors só local, regra de filtro CSRF em todo não-GET) → SPEC-0003/backlog — 27ea040 | 2026-10-16 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 6m 46s 363ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 883ms); nota: PR #16, CI run https://github.com/thomasravache/subsolo/actions/runs/39005176528 — 10/10 checks verdes; bateria local completa verde em b5bebf7 (build, lint, arch, test, integration, e2e, coverage, commands_contract) — 778f2c7 (árvore suja) | 2026-10-17 |
| H2 Integração aprovada | PASS | aprovado por thomas (delegado: piloto automático, 2026-10-16) | 2026-10-17 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live → SPEC-0003 | 2026-10-17 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 778f2c7 (árvore suja) | 2026-10-17 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-16 | G2 | spec | SPEC-0007:IT-05 recebe 401 em vez de 500: o dublê ThrowingEndpoint (/__tests/throws) cai no fallback autenticado; o arquivo está fora dos touches | Implementer conferiu que bastaria .AllowAnonymous() no dublê, sem mudar asserções | Architect | Emenda 3 — escopo ampliado e E2E local por HTTPS (https://localhost:8443) | 2026-10-16 |
| IMP-02 | 2026-10-16 | G2 | spec | Readiness do webServer do Playwright segue o 308 para https://localhost:8443/health e rejeita o certificado tls internal (timeout de 20 min); e2e/playwright.config.ts fora dos touches | ignoreHTTPSErrors testado temporariamente e revertido | Architect | Emenda 3 — escopo ampliado e E2E local por HTTPS (https://localhost:8443) | 2026-10-16 |
| IMP-03 | 2026-10-16 | G2 | spec | WebKit aplica o HSTS de localhost:8443 a todo o host localhost e quebra navegação http://localhost:8080 (SPEC-0008:E2E-01 no mobile-safari) | Opções: E2E local em https://localhost:8443 (recomendada), sem HSTS em localhost, pular mobile-safari | Architect | Emenda 3 — escopo ampliado e E2E local por HTTPS (https://localhost:8443) | 2026-10-16 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
A segurança base do ADR-0003/ADR-0008:
- API nega por padrão: política de fallback exige usuário autenticado; políticas `fan`, `artist` e `admin`; 401/403 em ProblemDetails; regra de arquitetura que reprova endpoint sem declaração.
- Endpoints anônimos só os previstos: agenda, cidades, `/health`, `/alive`, `GET /api/v1/antiforgery` (fora do OpenAPI) e OpenAPI só em Development.
- Anti-forgery por cabeçalho `X-CSRF` + cookie `__Host-subsolo-csrf` em toda mutação; cookie de sessão `__Host-subsolo` (Secure, HttpOnly, SameSite=Lax).
- Rate limiting `auth`, `report` e `default` por IP (atrás do Caddy), com 429 e `Retry-After`.
- Web com CSP com nonce em todas as páginas (`proxy.ts` + `await connection()`, ADR-0017) e cabeçalhos de segurança; Compose local por HTTPS (`https://localhost:8443`) com HSTS e redirect 308 do 8080.
- Checklist OWASP Top 10 em `docs/runbooks/checklist-seguranca.md`.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: scaffolds (663e4e4, b43cec9) → Test-writer (d4f8d81, dfad7fe) → Emenda 2 e ADR-0017 → Implementer (18da406, 662ee01, aa8a773, 1fb982a, 7594a06) → Emenda 3 (E2E local por HTTPS: 2de2742, 27ea040) → Reviewer APPROVED.
- Integração da onda 5 (SPEC-0001 IMP-01): readiness do web atrás do Caddy (f27dcf4) e Emenda 4 — falso positivo do WebKit no `<select>` nativo ignorado só nesse caso, com evidência de página mínima (b5bebf7).
- Emendas: 1 (caminhos `web/src/`), 2 (CSP com nonce em todas as páginas, AllowAnonymous do health), 3 (HTTPS local na 8443, Playwright aceitando o certificado `tls internal`), 4 (WebKit/`<select>`).
- Nenhum pacote novo além do shared framework do ASP.NET Core.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A — type foundation.

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | mutação sem `X-CSRF` → `security.csrf_invalid`; GET passa | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| UT-02 | CSP com nonce novo a cada requisição | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| IT-01 | regra de arquitetura reprova endpoint sem autorização declarada | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| IT-02 | 401 `auth.required` sem login; 403 `auth.forbidden` sem o papel | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| IT-03 | 6ª chamada em 1 min → 429 com `Retry-After` | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| IT-04 | POST sem token 400; com token de `/api/v1/antiforgery` 200 | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| IT-05 | CSP e cabeçalhos de segurança na resposta do web | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| IT-06 | 308 para HTTPS e HSTS no Caddy | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| CT-01 | agenda continua anônima com o fallback ativo | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| E2E-01 | agenda e não encontrada sem violação de CSP e sem violações do axe, nos 3 projetos | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — bateria local e CI verdes no resultado integrado (PR #16, run 39005176528)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — CSP estrita sem `unsafe-inline` (UT-02, IT-05, E2E-01); HSTS (IT-06)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — ambiente-alvo é a `main`: merge do PR #16 (ac71d56); G6 N/A até a SPEC-0003
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — rollback = revert do merge commit
- [x] Documentação raiz e CHANGELOG atualizados (G7) — README e CHANGELOG atualizados
- [x] Pendências registradas como novas specs (ou nenhuma) — apontadas para o épico SPEC-0002 e a SPEC-0003 (ver Pendências)

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente implantado (G6 N/A → SPEC-0003). Entrega na `main` pelo PR #16 (https://github.com/thomasravache/subsolo/pull/16), merge commit ac71d56 em 2026-10-17, com H2 delegado pelo Thomas (autorização de piloto automático) e merge pelo Architect (ADR-0007).

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- SPEC-0003: Caddy com `SITE_ADDRESS`, `HTTP_PORT=80` e `TLS_ISSUER` obrigatório; `nosniff` global no Caddy; X-Forwarded-For aceito só do Caddy/sub-rede do Compose; `ignoreHTTPSErrors` só na pilha local.
- Épico SPEC-0002: partição do rate limit IPv6 por /64 e teste via Caddy; valores padrão por campo na configuração do rate limit; regra que exija o filtro de CSRF em todo endpoint não-GET.
## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0010`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (escopo) | 2026-10-17 | Emenda 4 (integração da onda 5, SPEC-0001 IMP-01): o WebKit dispara `style-src-attr` para o `<select>` nativo (estilo interno do shadow DOM do controle, `sample` vazio) mesmo sem nenhum atributo `style` no HTML — falso positivo do motor, reproduzido com uma página mínima; o Chromium não dispara. O `recordCspViolations` do SPEC-0010:E2E-01 ignora apenas, no projeto WebKit: violações `style-src-attr` com `sample` vazio cujo alvo é um `SELECT` ou o `#document` (o `<select>` criado pelo React antes de ser anexado) quando a página tem um `<select>`, e o erro de console `Refused to apply a stylesheet because its hash, its nonce, or 'unsafe-inline'…` no máximo tantas vezes quanto as violações descartadas — evidência: página mínima com `<select>` produz os três sinais no WebKit e nenhum no Chromium, e as páginas reais sem o `<select>` ficam limpas nos três motores; o webServer do Compose no `playwright.config.ts` espera `${appURL}/` (web atrás do Caddy), e o `compose-up.sh` também, para não dar 502 nos primeiros testes; a política (`style-src 'self' 'nonce-…'`, sem `'unsafe-inline'`) e o `<select>` nativo do `CitySwitcher` (SPEC-0011, acessibilidade) ficam como estão. O `Card` passou a usar `<img>` simples (o `next/image` injeta `style="color:transparent"`), prevenindo violação real quando houver flyers | SPEC-0010:E2E-01 vermelho no mobile-safari na bateria da onda 5 | SPEC-0011 (Card sem `next/image`) | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
| 1 (escopo) | 2026-10-16 | Emenda 3 (impedimentos IMP-01..03 do Implementer): entram nos `touches` `tests/Common/Subsolo.Api.IntegrationTests/ThrowingEndpoint.cs` (o dublê `/__tests/throws` da SPEC-0007:IT-05 ganha `.AllowAnonymous()`, sem mudar asserções — mudança do Test-writer), `e2e/playwright.config.ts` e `tools/compose-up.sh`. A pilha local é acessada por HTTPS: o padrão de `BASE_URL`/`appURL` passa a ser `https://localhost:8443`; o Playwright aceita o certificado `tls internal` (`ignoreHTTPSErrors` no `use` e no `webServer`); o `httpURL()` de `caddy.spec.ts` mapeia 8443→8080; o readiness de `compose-up.sh` consulta `https://localhost:8443/health` (com `-k`), porque o HSTS que o WebKit grava para `localhost` quebrava a navegação por `http://localhost:8080`. O HSTS continua no contrato (IT-06). Nota para a SPEC-0003: o Caddyfile não tem mais `HTTPS_PORT`; em produção, `SITE_ADDRESS=<domínio>` e HTTPS na 443 | IMP-01, IMP-02, IMP-03 (test e test_e2e vermelhos no G2) | SPEC-0007 (dublê de teste), SPEC-0008 (E2E passa a usar HTTPS local), SPEC-0009 (também toca `playwright.config.ts` e `compose-up.sh`: conflito resolvido na integração), SPEC-0003 | thomas (delegado: piloto automático, 2026-10-16, recomendação do Architect) |
| 1 (escopo) | 2026-10-16 | Emenda 2 (impedimentos do Test-writer): IMP-B → ADR-0017: a CSP com nonce vale em todas as páginas renderizando-as por requisição (`await connection()`), mantendo o Cache Components; IMP-A → entram nos `touches` o `Subsolo.slnx` (projetos de `tests/Security`), `web/src/proxy.test.ts` (UT-02), o layout raiz, `page.tsx` e `not-found.tsx` (renderização por requisição), o AppHost e seus testes (porta HTTPS do Caddy para o IT-06) e `src/Subsolo.ServiceDefaults/Extensions.cs` só para `/health` e `/alive` declararem `AllowAnonymous` (arquivo também da SPEC-0009; o Integrator resolve o merge); `GET /api/v1/antiforgery` fica fora do OpenAPI (`ExcludeFromDescription`), sem mudar o snapshot nem o cliente | IMP-A e IMP-B do Test-writer | SPEC-0009 (mesmo arquivo do ServiceDefaults), SPEC-0011 (layout raiz) | thomas (delegado: piloto automático, 2026-10-16) |
| 1 (escopo) | 2026-10-16 | Emenda 1 (preparação do Architect): no Next 16 o middleware chama-se `proxy.ts` e já existe em `web/src/proxy.ts` (status da agenda, ADR-0016); a CSP e os cabeçalhos entram nesse mesmo proxy, ampliando o matcher para todas as páginas sem mudar o comportamento da agenda (E2E-01/IT-04 da SPEC-0008 continuam verdes; `e2e/tests/agenda/**` entra só se for preciso ajustar o ambiente desses testes, nunca as asserções). Se a CSP com nonce conflitar com o Cache Components do ADR-0013 (nonce exige renderização dinâmica), o Test-writer/Implementer reporta impedimento antes de escolher | Estrutura real do projeto e convivência com o ADR-0016 | SPEC-0008 | thomas (delegado: piloto automático, 2026-10-16) |
