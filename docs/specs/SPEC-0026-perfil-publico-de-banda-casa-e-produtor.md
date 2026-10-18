---
id: SPEC-0026
title: Perfil público de banda, casa e produtor
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0021, SPEC-0024]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Profiles/**, tests/Modules/Profiles/**, src/Modules/Events/**, tests/Modules/Events/**, docs/api/**, web/src/api/**, web/src/app/(public)/banda/**, web/src/app/(public)/casa/**, web/src/app/(public)/produtor/**, web/src/app/(public)/[cidade]/**, web/src/components/profiles/**, web/src/components/shows/**, web/src/proxy.ts, web/src/proxy.test.ts, e2e/tests/perfis/**, tests/**/packages.lock.json, docs/design/**]
adrs: [ADR-0001, ADR-0013, ADR-0016]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0026 — Perfil público de banda, casa e produtor

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Cada perfil aprovado de banda, casa ou produtor ganha uma página pública — `/banda/{slug}`, `/casa/{slug}` e `/produtor/{slug}` — com nome, cidade, estilos, links para Instagram, Bandcamp e Metal Archives em destaque, os próximos shows e os passados. As páginas de show passam a ligar a casa e as bandas do line-up aos perfis delas. A página leva o fã para as redes da banda: complementa o Instagram, não compete.

## 2. Motivação & Escopo
**Motivação:** critério de aceite do épico "Perfil de banda, casa e produtor lista os shows futuros e passados, com links para as redes"; regra inegociável "cada página leva para as redes da banda/casa".

**Objetivos (dentro do escopo):**
- `GET /api/v1/profiles/{type}/{slug}` público (módulo `Profiles`): tipo, nome, slug, cidade, estilos e links; só perfis aprovados (pendente, recusado ou inexistente → 404).
- `GET /api/v1/shows?profileId={id}&period=upcoming|past` público (módulo `Events`): shows em que o perfil é organizador, casa ou banda do line-up; próximos por data crescente (até 50), passados por data decrescente (até 20); mesmo resumo de show da agenda (SPEC-0008).
- Páginas SSR `/banda/{slug}`, `/casa/{slug}`, `/produtor/{slug}` com status real pelo proxy (404 "Perfil não encontrado"), metadados (`<title>` "{nome} · {Banda|Casa|Produtor} · Subsolo", `description`, Open Graph sem imagem própria: usa o flyer do próximo show quando houver, senão o padrão do site).
- Links externos com `rel="noopener noreferrer"` e nome acessível ("Instagram de {nome}"); estado vazio "Nenhum show anunciado ainda." nos próximos.
- Página do show (SPEC-0024): casa e bandas com perfil viram links para a página do perfil.

**Não-objetivos (fora do escopo):**
- Botão "Seguir" e contagem de seguidores (spec "Seguir").
- Foto do perfil (não existe no cadastro; o topo usa a tipografia sobre fundo escuro).
- Página de cidade como perfil (a agenda já é a página da cidade).

## 3. Dependências
- **Implementações necessárias:** SPEC-0021 (perfis aprovados) e SPEC-0024 (página do show, que ganha os links).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** `Profiles` (SPEC-0021) e `Events` (SPEC-0023/0024); páginas SSR com status pelo proxy (ADR-0016); slugs `banda`, `casa` e `produtor` já reservados para cidades (SPEC-0017).

**Decisão:** dois endpoints públicos, um em cada módulo, e a página compõe os dois no servidor; nenhum módulo lê o banco do outro (ADR-0001). O `Events` filtra shows pelo id do perfil nas colunas que já guarda (organizador, casa, line-up).

**Justificativa:** ADR-0001 (fronteiras), ADR-0013 (SSR e Open Graph), ADR-0016 (status real das páginas).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** um endpoint no `Profiles` que buscasse os shows pelo `IEventsCatalog` (acopla o `Profiles` à agenda); URL `/{cidade}/banda/{slug}` (banda toca em várias cidades).

**ADRs:** ADR-0001, ADR-0013, ADR-0016

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** cada endpoint em ≤ 150 ms p95 com um perfil de 200 shows (IT-02); índice GIN no line-up (jsonb) ou coluna de ids de perfil para o filtro.
- **Segurança:** só leitura, rate limit `default`; textos escapados; links externos só os validados no cadastro (`https` dos domínios permitidos) e com `noopener noreferrer`.
- **Privacidade e dados pessoais:** a página não mostra dono, contato nem e-mail; só dados públicos do perfil.
- **Disponibilidade e resiliência:** se o endpoint de shows falhar, a página mostra o perfil com "Não foi possível carregar os shows agora." (degradação), e o status continua 200 com o perfil.
- **Acessibilidade (UI):** axe nas três páginas; links externos com nome acessível; datas em `<time>`; listas com cabeçalhos "PRÓXIMOS SHOWS" e "SHOWS PASSADOS".
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `GET /api/v1/profiles/{type}/{slug}` · `GET /api/v1/shows?profileId=&period=` · páginas `/banda|casa|produtor/{slug}`

```yaml
GET /api/v1/profiles/{type}/{slug}:      # AllowAnonymous + default ; type ∈ band|venue|producer
  200: { id, type, name, slug, city: { slug, name }, styles: [{ slug, name }], links: { instagram?, bandcamp?, metalArchives? } }
  404: ProblemDetails code=profiles.not_found          # inexistente, pendente ou recusado
GET /api/v1/shows?profileId={id}&period=upcoming|past:   # AllowAnonymous + default
  200: { shows: ShowSummary[] }          # resumo da SPEC-0008; upcoming ≤ 50 por data asc ; past ≤ 20 por data desc
  400: ProblemDetails code=events.invalid_query
```

```text
/banda/{slug} · /casa/{slug} · /produtor/{slug} (SSR dinâmica; o tipo da URL tem de bater com o do perfil, senão 404)
  <title>{nome} · {Banda|Casa|Produtor} · Subsolo</title> ; description "{tipo} de {cidade} · {estilos}" ; og:title/description ; og:image = flyer og do próximo show ou padrão
  topo: nome (Anton), cidade, chips de estilo (banda) ; links "Instagram", "Bandcamp", "Metal Archives" em destaque
  "PRÓXIMOS SHOWS": cards (link para a página do show) ; vazio → "Nenhum show anunciado ainda."
  "SHOWS PASSADOS": lista compacta (data e nome)
  falha nos shows → "Não foi possível carregar os shows agora."
Página do show: casa e bandas com perfil viram links para /casa/{slug} e /banda/{slug}
```

**Design:** Stitch, projeto 15194126668426659178: "Perfil da Banda - Podridão - Subsolo" (bdf4a66e751846d5a73dca2f606aac6a), usado também para casa e produtor. Ajustes: sem foto (o perfil não tem foto), sem "Seguir" e contagem de seguidores (spec "Seguir"), sem "Vou" nos cards até a spec "Vou".

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Ver perfil | perfil aprovado | 200 com dados do §6; página com links e listas | UT-01, IT-01, E2E-01 |
| Perfil não público | pendente, recusado, inexistente, tipo errado na URL | 404 "Perfil não encontrado" | IT-01, IT-04, E2E-02 |
| Shows do perfil | organizador, casa ou banda do line-up | aparece em próximos ou passados, na ordem e limite do §6 | UT-02, IT-02 |
| Sem shows | perfil novo | "Nenhum show anunciado ainda." | E2E-01 |
| Falha nos shows | endpoint de shows fora | perfil aparece com o aviso; status 200 | IT-05 |
| Links no show | página do show com casa e banda com perfil | links para as páginas dos perfis | E2E-01 |
| Metadados | HTML da página | título, descrição e OG do §6 | UT-01, IT-03 |
| Desempenho | 200 shows | ≤ 150 ms p95 nos dois endpoints | IT-02 |
| Contrato | OpenAPI | os dois endpoints com corpos e códigos | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — páginas e endpoints novos; a página do show segue coberta pelos testes da SPEC-0024.

### 7.2 Testes Unitários
- **UT-01** — Dados um perfil de banda com estilos e um de casa sem próximo show, quando os metadados são montados, então título, descrição e `og:image` saem como no §6 (flyer do próximo show ou padrão).
- **UT-02** — Dado o filtro de shows por perfil, quando o perfil é organizador de um show, casa de outro e banda de um terceiro, então os três entram, em ordem e limite certos para `upcoming` e `past`.

### 7.3 Testes de Integração
- **IT-01** — Dados perfis aprovado, pendente e recusado, quando `GET /api/v1/profiles/band/{slug}` é chamado, então 200 só para o aprovado (sem dono nem contato no corpo) e 404 `profiles.not_found` para os outros.
- **IT-02** — Dado um perfil com 200 shows entre passados e futuros, quando `GET /api/v1/shows?profileId=…&period=upcoming` e `past` são chamados, então vêm os limites e a ordem do §6, em p95 ≤ 150 ms; `period` inválido → 400.
- **IT-03** — Dada a página `/banda/{slug}` servida pela pilha local, quando o HTML é baixado sem JavaScript, então tem o `<title>`, a descrição e o OG do §6.
- **IT-04** — Dados `/casa/{slug-de-banda}` e `/banda/inexistente`, quando pedidos, então 404 real com "Perfil não encontrado".
- **IT-05** — Dado o endpoint de shows indisponível (simulado), quando a página do perfil é pedida, então 200 com o perfil e "Não foi possível carregar os shows agora.".

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então os dois endpoints têm corpos, parâmetros e códigos do §6, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa com uma banda e uma casa com perfil e um show com as duas, nos projetos mobile-chrome e mobile-safari: o fã abre a página do show, toca no nome da banda e cai em `/banda/{slug}` com os links das redes e o show em "PRÓXIMOS SHOWS"; volta, toca na casa e vê `/casa/{slug}`; um perfil sem shows mostra "Nenhum show anunciado ainda.". Sem violações do axe. [jornada: compartilhar-show]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: `/banda/nao-existe` mostra "Perfil não encontrado" com status 404. [jornada: compartilhar-show]

### 7.6 Outros
- Acessibilidade: nome acessível dos links externos e axe nas páginas.
- Herança da revisão da SPEC-0021: teste de integração com a mesma reivindicação aprovada por dois admins ao mesmo tempo (um 200 e o outro 409 `profiles.request_decided`), em `tests/Modules/Profiles/**`.

**Dublês e dados de teste:** perfis criados e aprovados pela API (admin) e shows cadastrados pela API nos IT e no E2E.

**Ambiente de execução:** IT com Testcontainers; IT-03..IT-05 e E2E contra a pilha do Compose; Playwright com um worker.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** índice para o filtro de shows por perfil (aditivo).
- **Compatibilidade:** endpoints e rotas novas; a página do show só ganha links.
- **Observabilidade:** contador de 404 de perfil (consulta no Seq); trace da página até os dois endpoints.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] URL dos perfis? — `/banda/{slug}`, `/casa/{slug}` e `/produtor/{slug}` (slugs já reservados na SPEC-0017) (recomendação do Architect; piloto automático do Thomas, 2026-10-18)
- [x] Imagem de prévia do perfil? — Flyer do próximo show; sem show, a imagem padrão do site (recomendação do Architect; piloto automático do Thomas, 2026-10-18)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"); tela do Stitch já existente.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Metadados e filtro**
- [ ] Red: UT-01, UT-02 com a tag `SPEC-0026:<ID>`, falhando pelo motivo certo
- [ ] Green e Refactor

**Fase 2: Endpoints e páginas**
- [ ] Red: IT-01..IT-05, CT-01 falhando pelo motivo certo
- [ ] Green: dois endpoints, índice, páginas SSR, proxy, OpenAPI e cliente TS
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [ ] Green: links na página do show e jornada completa

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — f10aaa7 | 2026-10-18 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0026`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
