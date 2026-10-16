---
id: SPEC-0008
title: "Walking skeleton: agenda da cidade"
tier: full
type: foundation
user_facing: true
status: in-progress
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0006, SPEC-0007]
consumes_contract: []
contract_version: 1
touches: [.semgrep.yml, web/public/robots.txt, web/src/proxy.ts, web/.nvmrc, web/.dockerignore, tests/Common/Subsolo.Api.IntegrationTests/packages.lock.json, tests/Subsolo.ArchitectureTests/**, .editorconfig, .gitignore, .github/workflows/ci.yml, Directory.Packages.props, src/Subsolo.Api/**, e2e/playwright.config.ts, e2e/package.json, e2e/pnpm-lock.yaml, web/pnpm-lock.yaml, tests/Common/Subsolo.AppHost.IntegrationTests/**, src/Modules/Events/**, src/Subsolo.Api/Program.cs, src/Subsolo.AppHost/**, tests/Modules/Events/**, web/src/app/(public)/**, web/src/api/**, web/package.json, e2e/tests/agenda/**, infra/caddy/**, tools/compose-up.sh, docs/api/**, docs/specs/sdd-config.yml, Subsolo.slnx]
adrs: [ADR-0016, ADR-0001, ADR-0002, ADR-0004, ADR-0012, ADR-0013]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0008 — Walking skeleton: agenda da cidade

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
A jornada mais fina possível, passando por todas as camadas: o fã abre `/{cidade}` no celular e vê a agenda da cidade, por enquanto vazia ("Nenhum show anunciado ainda"), renderizada no servidor pelo Next.js a partir da API .NET (módulo `Events` mínimo) e do PostgreSQL. A mesma aplicação também roda pelo **Docker Compose gerado pelo Aspire** (`aspire publish`), localmente, com smoke test, provando que o caminho até a VPS já existe quando chegar o go-live.

## 2. Motivação & Escopo
**Motivação:** provar ponta a ponta (UI → API → banco → Compose) que a stack escolhida funciona junta antes de qualquer feature, para que cada spec seguinte seja um incremento pequeno num caminho que já funciona.

**Objetivos (dentro do escopo):**
- Módulo `Events` mínimo: entidade `City` (slug, nome, UF, fuso horário), migração no schema `events` e seed de desenvolvimento com uma cidade de exemplo (`cidade-exemplo`).
- Feature `GetCityAgenda`: `GET /api/v1/cities/{slug}/agenda?from=&to=` retorna a cidade e a lista de shows (vazia nesta spec; o contrato do item de show já é definido).
- Cliente TypeScript gerado do OpenAPI (`openapi-typescript` + `openapi-fetch`) em `web/src/api/`, com geração e verificação em `(cd web && pnpm api:check)`.
- Página `web/src/app/(public)/[cidade]/page.tsx` com SSR, título "Agenda {Cidade}", estado vazio e 404 para cidade inexistente; layout mínimo escuro (o design system completo vem na SPEC-0011).
- Publicação: `AddDockerComposeEnvironment("compose")` no AppHost; `aspire publish -o artifacts/compose` gera o `docker-compose.yaml` e o `.env`; script `tools/compose-up.sh` sobe localmente com o Caddy na frente (`/api` → API, resto → web) em `http://localhost:8080`.
- `smoke_test` do sdd-config: `(cd e2e && pnpm test --grep @smoke)` contra uma `BASE_URL`.
- `critical_journeys` no sdd-config: `agenda: "Fã vê a agenda da cidade no celular"`.

**Não-objetivos (fora do escopo):**
- Cadastro de shows, filtros e página do show (épico SPEC-0002).
- Deploy em VPS, domínio e TLS real (SPEC-0003).
- Visual final (SPEC-0011).

## 3. Dependências
- **Implementações necessárias:** SPEC-0006 — o E2E e o smoke entram nos checks do CI; SPEC-0007 — blocos Common, registro de módulo e AppHost.
- **Contratos consumidos:** N/A — tudo o que é usado vem de specs em `depends_on`.
- **Pré-requisitos externos:** Docker local; Aspire CLI 13.6.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; padrões do ADR-0001 (vertical slice `Features/GetCityAgenda`), ADR-0002, ADR-0004 (Compose + Caddy) e ADR-0013 (Next.js SSR + cliente OpenAPI).

**Decisão:** primeiro slice do módulo `Events`, com contrato HTTP versionado (`/api/v1`) e página SSR que consome o cliente gerado. O Caddy do Compose local é o mesmo arquivo usado depois na VPS.

**Justificativa:** exercita todas as camadas e o artefato de deploy sem gastar nada (ADR-0012).

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** página estática sem API (não prova a integração); chamar o banco direto do Next.js (viola o ADR-0013).

**ADRs:** ADR-0001, ADR-0002, ADR-0004, ADR-0012, ADR-0013

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** p95 do SSR de `/{cidade}` < 800 ms localmente pelo Compose (SLO do ADR-0010) — medido pela duração da navegação no E2E-02 em 20 requisições.
- **Segurança:** `slug` validado (`^[a-z0-9-]{2,60}$`), com 400 se inválido; endpoint público só de leitura (`AllowAnonymous` explícito) — UT-02, IT-02.
- **Privacidade e dados pessoais:** N/A — nenhum dado pessoal (cidade é dado público).
- **Disponibilidade e resiliência:** se a API estiver fora, a página mostra uma mensagem de indisponibilidade com status 503 em vez de quebrar — IT-04.
- **Acessibilidade (UI):** WCAG 2.2 AA na página da agenda — axe no E2E-01.
- **Custo:** zero; Compose local (ADR-0012).

## 6. Artefato A — Contrato
**Interface:** `GET /api/v1/cities/{slug}/agenda` · tela `/{cidade}`

```yaml
GET /api/v1/cities/{slug}/agenda:
  parameters:
    slug: string, ^[a-z0-9-]{2,60}$
    from: date (opcional, padrão = hoje no fuso da cidade)
    to:   date (opcional, padrão = from + 60 dias; máximo 180 dias)
  200:
    city: { slug: string, name: string, state: string(2), timeZone: string }
    shows: ShowSummary[]        # vazio nesta spec
  ShowSummary:                  # contrato já definido para as specs do épico SPEC-0002
    id: uuid, slug: string, title: string, startsAt: date-time (UTC), venueName: string,
    lineup: string[], styles: string[], status: scheduled|cancelled|postponed,
    flyerThumbUrl: string|null, attendingCount: integer
  400: ProblemDetails code=agenda.invalid_slug | agenda.invalid_range (to < from ou > 180 dias)
  404: ProblemDetails code=agenda.city_not_found
```

```text
Tela /{cidade} (SSR):
  200 → <h1>Agenda {Cidade}</h1> + lista de shows ou o estado vazio "Nenhum show anunciado ainda em {Cidade}."
  cidade inexistente → página 404 "Cidade não encontrada" (status 404)
  API indisponível → "A agenda está fora do ar por alguns instantes. Tente de novo." (status 503)
Compose local:
  tools/compose-up.sh → http://localhost:8080 ; /api/* → api ; /* → web ; /health → 200
```

**Design:** layout mínimo provisório (fundo escuro, tipografia do sistema); o design aprovado no Stitch substitui na SPEC-0011.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Agenda vazia | cidade existente, sem shows | 200 com a cidade e `shows: []`; a tela mostra o estado vazio | UT-01, IT-01, E2E-01 |
| Período padrão | sem `from`/`to` | de hoje (fuso da cidade) até +60 dias | UT-03 |
| Slug inválido | `slug=Cidade_X` | 400 `agenda.invalid_slug` | UT-02, IT-02 |
| Período inválido | `to < from` ou > 180 dias | 400 `agenda.invalid_range` | UT-02 |
| Cidade inexistente | slug não cadastrado ou fora do padrão na tela | API 404 `agenda.city_not_found`; tela 404 (nunca soft 404) | IT-03, E2E-01 |
| Endpoint público | metadata do endpoint | `AllowAnonymous` explícito | UT-04 |
| Compose publicado | `aspire publish` | API em Production por padrão; sem dashboard | IT-05 |
| API fora do ar | API parada | tela 503 com a mensagem amigável | IT-04 |
| Cliente TS em dia | OpenAPI muda sem regenerar | `api:check` falha | CT-01 |
| Compose gerado sobe | `aspire publish` + `compose-up.sh` | `/health` 200 e agenda acessível em :8080 | E2E-02 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dada uma cidade e nenhum show, quando `GetCityAgendaQueryHandler` é executado, então retorna a cidade e uma lista vazia.
- **UT-02** — Dados `slug` com maiúscula ou sublinhado, `to < from` ou um intervalo de 181 dias, quando validados, então retornam `agenda.invalid_slug` ou `agenda.invalid_range`.
- **UT-03** — Dada uma cidade com fuso `America/Sao_Paulo` e um `FakeTimeProvider` em 2026-10-14T02:30Z (ainda dia 13 em São Paulo), quando o período é omitido, então `from` = 2026-10-13 e `to` = 2026-12-12.

### 7.3 Testes de Integração
- **IT-01** — Dada a cidade seed num PostgreSQL real, quando `GET /api/v1/cities/cidade-exemplo/agenda` é chamado, então a resposta é 200 com a cidade e `shows: []`.
- **IT-02** — Dado um slug inválido, quando a API é chamada, então a resposta é 400 com `ProblemDetails` e `code = agenda.invalid_slug`.
- **IT-03** — Dado um slug não cadastrado, quando a API é chamada, então a resposta é 404 com `agenda.city_not_found`.
- **IT-04** — Dado o servidor Next.js de produção (`next build` + `next start`) com `API_URL` apontando para uma porta sem serviço, quando `/cidade-exemplo` é requisitada, então o status é 503 e o HTML contém a mensagem de indisponibilidade.

- **IT-05** — Dado o AppHost publicado com `aspire publish` (ou o modelo de publicação equivalente em teste), quando o Compose é gerado, então a API recebe `ASPNETCORE_ENVIRONMENT` = `Production` por padrão (sobrescrevível) e nenhum serviço de dashboard do Aspire entra no artefato (Emenda 5).
- **UT-04** — Dado o endpoint `GetCityAgenda` mapeado, quando sua metadata é lida, então contém `IAllowAnonymous` (Emenda 5).

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json` (SPEC-0007), quando `(cd web && pnpm api:check)` roda, então o cliente gerado é idêntico ao versionado; uma mudança no OpenAPI sem regenerar o cliente faz o check falhar.

- **CT-02** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então tem as restrições do §6: `pattern` do slug, `state` com 2 caracteres, enum de status do `ShowSummary`, `startsAt` como date-time e `code` no `ProblemDetails` dos erros 400/404 (Emenda 5/6).

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa (a mesma pilha do Compose gerado pelo Aspire, subida pelo `webServer` do Playwright), quando o fã abre `/cidade-exemplo` no projeto mobile, então vê "Agenda Cidade Exemplo" e o estado vazio, sem violações do axe; ao abrir `/cidade-inexistente` e `/Cidade_X` (slug fora do padrão), vê "Cidade não encontrada" com status 404; um cabeçalho interno de estado enviado pelo cliente é ignorado; arquivos públicos (ex.: `/robots.txt`) nunca recebem o 404 do proxy [jornada: agenda] @smoke.
- **E2E-02** — Dado o Compose gerado por `aspire publish` e iniciado por `tools/compose-up.sh`, quando o smoke roda com `BASE_URL=http://localhost:8080`, então `/health` responde 200, a agenda da cidade de exemplo carrega e o p95 de 20 carregamentos fica abaixo de 800 ms [jornada: agenda].

### 7.6 Outros
- Acessibilidade: axe sem violações sérias ou críticas na página da agenda (dentro do E2E-01).
- Desempenho: p95 < 800 ms medido no E2E-02.

**Dublês e dados de teste:** seed de desenvolvimento e de teste com a cidade `cidade-exemplo` (fuso `America/Sao_Paulo`); nenhum dado real.

**Ambiente de execução:** IT local e no CI (Testcontainers); E2E-01 contra o AppHost; E2E-02 contra o Compose local e no job `e2e` do CI.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003); entregue na `main` e validado pelo Compose local.
- **Dados/schema:** migração inicial do schema `events` (tabela `cities`), aditiva.
- **Compatibilidade:** `/api/v1` é o primeiro contrato público; mudanças incompatíveis exigem `/api/v2` ou campo opcional.
- **Observabilidade:** trace de ponta a ponta web → api → postgres visível no Seq local, com o mesmo trace id.
- **Rollback:** reverter o merge commit; no Compose, subir a imagem anterior com `tools/compose-up.sh <tag>`.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Slice GetCityAgenda (API)**
- [x] Red: UT-01..UT-03, IT-01..IT-03 com a tag `SPEC-0008:<ID>`, falhando pelo motivo certo
- [x] Green: módulo `Events` (City, migração do schema `events`, seed `cidade-exemplo`), feature `GetCityAgenda`, endpoint `GET /api/v1/cities/{slug}/agenda` com `AllowAnonymous`
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte + arquitetura (G2/G3)

**Fase 2: Página SSR e cliente tipado**
- [x] Red: IT-04 e CT-01 falhando
- [x] Green: cliente OpenAPI em `web/src/api/` + `api:check`; página `/[cidade]` com estados vazio, 404 e 503
- [x] Refactor mantendo tudo verde

**Fase 3: Jornada E2E e Compose**
- [x] Red: E2E-01 e E2E-02 falhando pelo motivo certo
- [x] Green: `AddDockerComposeEnvironment`, `aspire publish`, `infra/caddy/Caddyfile`, `tools/compose-up.sh`, `smoke_test` e `critical_journeys` no sdd-config; jornada completa passando localmente
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
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — ce9c7a3 | 2026-10-16 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ next typegen && tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — b4e70c0 | 2026-10-16 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 885ms) — b4e70c0 | 2026-10-16 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente (3ª rodada): APPROVED em b4e70c0 — B1 (Aspire CLI nos jobs test/integration) e M4 (matcher sem arquivos públicos) resolvidos, minors 1–3 da 2ª rodada resolvidos; lockfiles fora de red justificados. Minors: soft 404 para caminhos de um segmento com ponto (limitação registrada no ADR-0016; favicon na SPEC-0011); teste de política da CLI na SPEC-0013 (prova atual: CI) — b4e70c0 | 2026-10-16 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 1m 12s 884ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 844ms); nota: CI do PR #14 verde (10/10 checks, incluindo e2e com a pilha Compose) em 11eecff: https://github.com/thomasravache/subsolo/actions/runs/38880460959; bateria local no integrado 5a35c0e verde (E2E 23 passaram, test_commands_contract e coverage) — 11eecff | 2026-10-16 |
| H2 Integração aprovada | PASS | aprovado por thomas (delegado): autorização de piloto automático em 2026-10-16; integração verde (G5) em 11eecff | 2026-10-16 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live → SPEC-0003; o Compose gerado sobe localmente e no CI (E2E-02); entrega na main pelo merge do PR #14 | 2026-10-16 |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
A primeira jornada ponta a ponta do Subsolo: o fã abre `/{cidade}` no celular e vê a agenda da cidade (por enquanto vazia), renderizada no servidor pelo Next.js a partir da API .NET e do PostgreSQL.
- Módulo `Events` (vertical slice `GetCityAgenda`): entidade `City`, schema `events`, seed de desenvolvimento `cidade-exemplo`, `GET /api/v1/cities/{slug}/agenda` público (`AllowAnonymous`) com validação do slug e do período no fuso da cidade e erros `agenda.invalid_slug`, `agenda.invalid_range` e `agenda.city_not_found`.
- Contrato OpenAPI fiel ao §6 (snapshot em `docs/api/openapi.v1.json`) e cliente TypeScript gerado (`openapi-typescript` + `openapi-fetch`) com `pnpm api:check` no CI.
- Página SSR `web/src/app/(public)/[cidade]` com "Agenda {Cidade}", estado vazio, 404 real para cidade inexistente ou slug fora do padrão e 503 amigável com a API fora (ADR-0016), acessível (axe).
- Compose gerado pelo Aspire (`aspire publish`) com Caddy (`/api` → API, resto → web), sem dashboard e com a API em Production por padrão; `tools/compose-up.sh` sobe tudo em `http://localhost:8080`.
- CI: o job `e2e` sobe a pilha completa e roda as jornadas; `smoke_test` e `critical_journeys` (`agenda`) no sdd-config.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: scaffold (14c780f) → Test-writer (d3ec52f, ce9c7a3, 3c2d0c7) → Implementer (01caebe…3a8674a) → Reviewer 1 (3 majors: soft 404, ambiente da API no Compose, dashboard nightly) → testes (c3895bf, 170a395, 698ab37) e correções (4aecea6…0aab194) → Reviewer 2 (blocker: CLI do Aspire no CI; major: arquivos públicos) → testes (66b7ef8, 46450fd) e correções (5da1038…b4e70c0) → Reviewer 3 APPROVED → CI do PR apontou o Semgrep nas migrações geradas (948e733).
- Emendas 1–7 (escopo e testes, contrato da API inalterado) e ADR-0016 (status das páginas via proxy do Next).
- Pacotes: openapi-fetch 0.17.0 e openapi-typescript 7.13.0 (MIT), Aspire.Hosting.Docker 13.6.1 (MIT), pin transitivo de MessagePack 2.5.307 (GHSA-qhrr-8q5h-9q3h); imagem caddy:2.11.7-alpine.
- Riscos registrados: `unstable_paramMatching` e até 3 chamadas à API por página (ADR-0016); `PublishAsPackageScript` em avaliação no Aspire 13.6; caminhos com ponto que não são arquivo recebem soft 404.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A — type foundation.

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | handler retorna a cidade e lista vazia | PASS | `gate G2 --run` em b4e70c0; `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| UT-02 | slug e período inválidos (incluindo só `to` antes de hoje) → códigos do contrato | PASS | `gate G2 --run` em b4e70c0; `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| UT-03 | período padrão no fuso da cidade (13/10 → 12/12) | PASS | `gate G2 --run` em b4e70c0; `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| UT-04 | endpoint com `AllowAnonymous` na metadata | PASS | `gate G2 --run` em b4e70c0; `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| IT-01 | API real: 200 com a cidade seed e `shows: []` | PASS | `test_integration` em `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| IT-02 | slug inválido → 400 `agenda.invalid_slug` | PASS | `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| IT-03 | cidade não cadastrada → 404 `agenda.city_not_found` | PASS | `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| IT-04 | Next de produção com API fora → 503 com a mensagem | PASS | `test_e2e` em `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) (job e2e) |
| IT-05 | Compose publicado: API em Production por padrão, sem dashboard | PASS | `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) (jobs test e integration com a Aspire CLI) |
| CT-01 | `api:check` falha quando o cliente diverge do snapshot | PASS | `gate G2 --run` em b4e70c0; `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| CT-02 | snapshot OpenAPI com as restrições do §6 | PASS | `gate G2 --run` em b4e70c0; `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| E2E-01 | fã vê a agenda vazia no celular sem violações do axe; 404 para inexistente e `/Cidade_X`; cabeçalho forjado ignorado; `/robots.txt` 200 | PASS | `test_e2e` (23 passaram) em `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |
| E2E-02 | Compose local: `/health` 200, agenda carrega, p95 de 20 carregamentos < 800 ms | PASS | `gate G5 --run` em 11eecff; CI do PR #14 em 11eecff (run 38880460959, 10/10 checks) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — CI verde em 11eecff (run 38880460959)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — p95 < 800 ms (E2E-02), slug validado (UT-02/IT-02), AllowAnonymous (UT-04), 503 amigável (IT-04), axe (E2E-01)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — artefato Compose validado localmente e no CI; G6 N/A até a SPEC-0003; merge do PR #14 (f53fcad)
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — rollback local com `tools/compose-up.sh <tag anterior>`; revert do merge commit
- [x] Documentação raiz e CHANGELOG atualizados (G7) — CHANGELOG e README atualizados
- [x] Pendências registradas como novas specs (ou nenhuma)

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente implantado (G6 N/A → SPEC-0003); o artefato de deploy (Compose gerado + Caddy) já sobe localmente e no CI. Entrega na `main` pelo PR #14 (https://github.com/thomasravache/subsolo/pull/14), merge commit f53fcad em 2026-10-16, com H2 delegado pelo Thomas (autorização de piloto automático).

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- SPEC-0003 — endurecimento do artefato de deploy (cabeçalhos do Caddy, web sem root, imagens base com versão completa, Seq com autenticação, sem Mailpit em produção) e telemetria do Compose para o Seq.
- SPEC-0011 — ícones do app (favicon e PWA).
- SPEC-0013 — teste de política da Aspire CLI nos jobs do CI.
## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0008`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (escopo) | 2026-10-16 | Emenda 7 (CI do PR #14): `.semgrep.yml` entra nos `touches` para a regra `subsolo-csharp-suppression-without-reason` ignorar `**/Migrations/*.cs` (código gerado pelo EF, já marcado como gerado no `.editorconfig`) | Job `security` reprovou as migrações geradas do módulo Events | SPEC-0004 (regra própria do Semgrep, comportamento igual fora de migrações) | thomas (delegado: piloto automático, 2026-10-16) |
| 1 (escopo e testes) | 2026-10-16 | Emenda 6 (2ª revisão G4): a Aspire CLI 13.6.1 entra nos jobs `test` e `integration` do CI (o IT-05 publica com ela); o matcher do proxy exclui segmentos com ponto (arquivos públicos e metadata) — ressalva: um caminho com ponto que não é arquivo público recebe a página de não encontrada com 200 (limitação registrada no ADR-0016) e `web/public/robots.txt` entra nos `touches` com um caso no E2E-01; o teste de fidelidade do OpenAPI vira CT-02; `POSTGRES_DB` no Postgres publicado para evitar o ruído do EF no start | B1 e M4 da 2ª rodada | Nenhuma | thomas (delegado: piloto automático, 2026-10-16) |
| 1 (escopo e testes) | 2026-10-16 | Emenda 5 (revisão G4): tela devolve 404 também para slug fora do padrão e ignora o cabeçalho interno vindo do cliente (E2E-01 ampliado); IT-04 contra o build de produção; novos UT-04 (`AllowAnonymous`) e IT-05 (Compose publicado com API em Production por padrão e sem dashboard nightly); OpenAPI fiel ao contrato §6 (padrão do slug, UF com 2 letras, enum de status, `startsAt` com fuso, `code` no ProblemDetails); `tools/compose-up.sh` falha com logs se `/health` não sobe e confere `curl`; riscos `PublishAsPackageScript` e até 3 chamadas por página registrados no ADR-0016; endurecimento do artefato de deploy (Caddy, root, tags completas, Seq/Mailpit) movido para o épico SPEC-0003 | Majors M1–M3 e minors da revisão G4 | SPEC-0003 | thomas (delegado: piloto automático, 2026-10-16) |
| 1 (escopo) | 2026-10-16 | Emenda 4 (achados do Implementer): entram nos `touches` `web/src/proxy.ts` (o Next só aceita o proxy em `src/` ou na raiz), o `packages.lock.json` dos testes da API (muda ao referenciar o Events) e `web/.nvmrc`/`web/.dockerignore` (Node 24 na imagem gerada pelo Aspire, que hoje usa `node:22`); ADR-0016 registra o status 404/503 via proxy do Next; `smoke_test` gravado no sdd-config; falta um UT para `to` antes de hoje sem `from` (`agenda.invalid_range`) | G4 do verify reprovou escopo; Node fora do ADR-0009 na imagem | Nenhuma | thomas (delegado: piloto automático, 2026-10-16) |
| 1 (escopo) | 2026-10-16 | Emenda 3 (achados do Test-writer): `touches` corrigido para `web/src/app/(public)/**` (o app Next fica em `web/src/app`); `tests/Subsolo.ArchitectureTests/**` entra para referenciar os projetos do Events nas regras (IT-06/IT-09 da SPEC-0007); a consulta é validada no endpoint com o `GetCityAgendaQueryValidator`, devolvendo o código específico (`agenda.invalid_slug`/`agenda.invalid_range`), porque o decorator comum não valida consultas; E2E-02 também marcado `@smoke`; com `BASE_URL` definido nenhum servidor é subido e o IT-04 é pulado | Caminho errado do app na spec; regra de arquitetura exige referência de todo módulo | Nenhuma | thomas (delegado: piloto automático, 2026-10-16) |
| 1 (escopo) | 2026-10-16 | Emenda 2: o comando `test_e2e` continua `(cd e2e && pnpm test)`; o `webServer` do Playwright sobe a pilha completa via `tools/compose-up.sh` (Compose gerado por `aspire publish`) para as jornadas, e o job `e2e` do CI ganha .NET SDK + Aspire CLI; E2E-01 e E2E-02 rodam contra essa pilha (o AppHost em modo dev já é provado pelo IT-07 da SPEC-0007). Entram nos `touches`: `.gitignore` (`artifacts/`), `.github/workflows/ci.yml`, `Directory.Packages.props` (Aspire.Hosting.Docker), `src/Subsolo.Api/**`, `e2e/playwright.config.ts`, `e2e/package.json`, lockfiles do `e2e/` e do `web/` e o teste do AppHost | Preparação do Architect antes do Red: sem isso o E2E não tem aplicação para testar no CI e o escopo ficaria fora dos `touches` | SPEC-0006 (o job `e2e` muda, mantendo o comando literal) | thomas (delegado: piloto automático, 2026-10-16) |
| 1 (escopo) | 2026-10-16 | `.editorconfig` entra nos `touches` para marcar `**/Migrations/*.cs` como código gerado (as migrações do módulo Events quebram o build com IDE0161 sem isso); consome o contrato v3 da SPEC-0007 (`IDomainEvent` com Id, `AddModule`, `AddEndpoints`/`MapEndpoints`) | Revisão G4 da SPEC-0007 | Nenhuma | thomas (delegado: piloto automático, 2026-10-16) |
| 1 | 2026-10-13 | Comandos pnpm chamados de dentro de `web/` e `e2e/` em vez de `pnpm -C`. Contrato HTTP e de tela inalterados. | Emenda v2 da SPEC-0004 / IMP-02 | — | thomas (chat, 2026-10-13) |
