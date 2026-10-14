---
id: SPEC-0008
title: "Walking skeleton: agenda da cidade"
tier: full
type: foundation
user_facing: true
status: approved
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0006, SPEC-0007]
consumes_contract: []
contract_version: 1
touches: [src/Modules/Events/**, src/Subsolo.Api/Program.cs, src/Subsolo.AppHost/**, tests/Modules/Events/**, web/app/(public)/**, web/src/api/**, web/package.json, e2e/tests/agenda/**, infra/caddy/**, tools/compose-up.sh, docs/api/**, docs/specs/sdd-config.yml, Subsolo.slnx]
adrs: [ADR-0001, ADR-0002, ADR-0004, ADR-0012, ADR-0013]
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
- Página `web/app/(public)/[cidade]/page.tsx` com SSR, título "Agenda {Cidade}", estado vazio e 404 para cidade inexistente; layout mínimo escuro (o design system completo vem na SPEC-0011).
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
| Cidade inexistente | slug não cadastrado | API 404 `agenda.city_not_found`; tela 404 | IT-03, E2E-01 |
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
- **IT-04** — Dado o servidor Next.js com `API_URL` apontando para uma porta sem serviço, quando `/cidade-exemplo` é requisitada, então o status é 503 e o HTML contém a mensagem de indisponibilidade.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json` (SPEC-0007), quando `(cd web && pnpm api:check)` roda, então o cliente gerado é idêntico ao versionado; uma mudança no OpenAPI sem regenerar o cliente faz o check falhar.

### 7.5 Testes E2E
- **E2E-01** — Dado o app subido pelo AppHost, quando o fã abre `/cidade-exemplo` no projeto mobile, então vê "Agenda Cidade Exemplo" e o estado vazio, sem violações do axe; ao abrir `/cidade-inexistente`, vê "Cidade não encontrada" [jornada: agenda] @smoke.
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
- [ ] Red: UT-01..UT-03, IT-01..IT-03 com a tag `SPEC-0008:<ID>`, falhando pelo motivo certo
- [ ] Green: módulo `Events` (City, migração do schema `events`, seed `cidade-exemplo`), feature `GetCityAgenda`, endpoint `GET /api/v1/cities/{slug}/agenda` com `AllowAnonymous`
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 2: Página SSR e cliente tipado**
- [ ] Red: IT-04 e CT-01 falhando
- [ ] Green: cliente OpenAPI em `web/src/api/` + `api:check`; página `/[cidade]` com estados vazio, 404 e 503
- [ ] Refactor mantendo tudo verde

**Fase 3: Jornada E2E e Compose**
- [ ] Red: E2E-01 e E2E-02 falhando pelo motivo certo
- [ ] Green: `AddDockerComposeEnvironment`, `aspire publish`, `infra/caddy/Caddyfile`, `tools/compose-up.sh`, `smoke_test` e `critical_journeys` no sdd-config; jornada completa passando localmente
- [ ] Validar: build + suíte + arquitetura + E2E (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003; validação local pelo Compose quando aplicável
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-13 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0008`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 | 2026-10-13 | Comandos pnpm chamados de dentro de `web/` e `e2e/` em vez de `pnpm -C`. Contrato HTTP e de tela inalterados. | Emenda v2 da SPEC-0004 / IMP-02 | — | thomas (chat, 2026-10-13) |
