---
id: SPEC-0025
title: Agenda da cidade com filtro por data e estilo
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0023]
consumes_contract: []
contract_version: 2
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Events/**, tests/Modules/Events/**, docs/api/**, web/src/api/**, web/src/app/(public)/[cidade]/**, web/src/components/agenda/**, web/src/components/ui/Chip.tsx, e2e/tests/agenda/**, tests/**/packages.lock.json, docs/design/**, tests/Security/Subsolo.Security.IntegrationTests/AgendaUnderFallbackPolicyTests.cs]
adrs: [ADR-0013, ADR-0016]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0025 — Agenda da cidade com filtro por data e estilo

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
A agenda da cidade (`/{cidade}`) ganha filtros por período ("Esta semana", "Este mês", "Escolher data") e por estilo (chips death, black, grind, doom, thrash e os que o admin criar). Os filtros ficam na URL, então a agenda filtrada pode ser compartilhada e é renderizada no servidor. O cabeçalho mostra quantos shows há no período, e um bloco no fim convida produtores a cadastrar o show deles.

## 2. Motivação & Escopo
**Motivação:** critério de aceite do épico "O fã vê a agenda da cidade e filtra por data e estilo" (jornada `agenda`). A SPEC-0023 deixou a agenda listando os shows de 6 h atrás até 90 dias, sem filtros.

**Objetivos (dentro do escopo):**
- `GET /api/v1/cities/{slug}/agenda` aceita `from` e `to` (datas locais da cidade, `aaaa-mm-dd`, intervalo de no máximo 180 dias, `to ≥ from`) e `styles` (lista de slugs separada por vírgula); sem parâmetros, mantém a janela da SPEC-0023. A resposta continua com o contrato da SPEC-0008, mais `period: { from, to }` e `total`.
- Página `/{cidade}` lê `?periodo=semana|mes`, `?de=&ate=` e `?estilo=death,grind` e chama a API com os valores; links dos chips mudam a URL (navegação no cliente com `next/link`); "Escolher data" abre um seletor de intervalo acessível.
- "Esta semana" = de hoje até o próximo domingo; "Este mês" = de hoje até o último dia do mês; tudo no fuso da cidade.
- Cabeçalho "AGENDA {CIDADE}" com "PRÓXIMOS 90 DIAS · N SHOWS" (ou o período escolhido); estado vazio com filtro: "Nenhum show com esses filtros." e "Limpar filtros"; bloco "Produzindo show em {cidade}? Cadastrar evento na agenda" que leva a `/conta/shows/novo` (ou a `/entrar?returnTo=…` se anônimo).

**Não-objetivos (fora do escopo):**
- Contagem "X vão" nos cards (spec "Vou").
- Busca por texto (aba "Buscar" fica para depois do MVP).
- Paginação (o volume de uma cidade no MVP cabe numa página; revisitar acima de 300 shows no período).

## 3. Dependências
- **Implementações necessárias:** SPEC-0023 (shows na agenda) e SPEC-0017 (estilos como dado).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** `GetCityAgenda` (SPEC-0008/0023), página SSR com status pelo proxy (ADR-0016), chips e cards do design system (SPEC-0011).

**Decisão:** o slice `GetCityAgenda` ganha os filtros (validação no endpoint, como no resto do `Events`); a página é dinâmica e monta a consulta a partir de `searchParams`; os chips são `next/link` com a URL já calculada.

**Justificativa:** ADR-0013 (SSR, URL compartilhável), ADR-0016 (status da página pelo proxy continua igual).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** filtrar no navegador com tudo carregado (pesado no 4G e sem URL compartilhável); endpoint novo só para filtro (duplica a agenda).

**ADRs:** ADR-0013, ADR-0016

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** agenda filtrada com 300 shows no período em ≤ 300 ms p95 na API (IT-02); índice em `(city, starts_at_utc)` e consulta por estilo com o array.
- **Segurança:** parâmetros validados (datas, intervalo máximo, slugs no padrão; estilos inexistentes ignorados); rate limit `default` (já existente).
- **Privacidade e dados pessoais:** N/A — só dados públicos.
- **Disponibilidade e resiliência:** API fora → 503 da página (comportamento da SPEC-0008).
- **Acessibilidade (UI):** chips como links com `aria-current="page"` no selecionado; seletor de datas com rótulos; o total anunciado em `aria-live` ao trocar o filtro; foco visível; axe no E2E.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `GET /api/v1/cities/{slug}/agenda?from=&to=&styles=` · página `/{cidade}?periodo=&de=&ate=&estilo=`

```yaml
GET /api/v1/cities/{slug}/agenda:        # AllowAnonymous + default (inalterado)
  query: from?: aaaa-mm-dd, to?: aaaa-mm-dd (to ≥ from; ≤ 180 dias), styles?: "death,grind"
  200: { city, shows: ShowSummary[] (contrato da SPEC-0008), period: { from, to }, total }
  400: ProblemDetails code=agenda.invalid_range (to < from, > 180 dias ou data mal formada; código atual da SPEC-0008, mantido) | agenda.invalid_styles
  period: datas locais da cidade; sem from/to → { from: hoje, to: hoje + 90 } (a busca segue a janela de 6 h atrás da SPEC-0023)
  404: agenda.city_not_found (inalterado)
```

```text
/{cidade} (SSR dinâmica)
  ?periodo=semana → de hoje até domingo ; ?periodo=mes → de hoje até o fim do mês ; ?de=aaaa-mm-dd&ate=aaaa-mm-dd → intervalo escolhido
  ?estilo=death,grind → só shows com algum desses estilos ; parâmetros inválidos → ignorados (agenda padrão) sem erro na página
  cabeçalho: "AGENDA {CIDADE}" + "PRÓXIMOS 90 DIAS · N SHOWS" | "{de} A {ate} · N SHOWS" (datas dd/MM, ex. "12/11 A 15/11"; singular "1 SHOW"); o total também é anunciado em aria-live
  chips de período (links com aria-current="page" no ativo): "ESTA SEMANA", "ESTE MÊS", "ESCOLHER DATA" (abre um formulário GET com os campos de data "De" e "Até" e o botão "Aplicar", que também envia com Enter) ; chips de estilo (vindos de GET /api/v1/styles)
  vazio com filtro → "Nenhum show com esses filtros." + "Limpar filtros" ; vazio sem filtro → texto atual da SPEC-0008
  bloco final: "PRODUZINDO SHOW EM {CIDADE}?" + "Cadastrar evento na agenda" → /conta/shows/novo
```

**Design:** Stitch, projeto 15194126668426659178: "Agenda Curitiba - Subsolo" (5d1946463ad9484b83c67cc2e6d78fd3, mobile) e "Agenda Curitiba - Desktop" (a9c7a58a62a5431bb833a132609c0c70). Ajustes: "PRÓXIMOS 90 DIAS" (janela da SPEC-0023, não 60); sem a contagem "X vão" nos cards até a spec "Vou"; o ícone vermelho solto no canto do título fica de fora.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Agenda padrão | sem parâmetros | janela da SPEC-0023, `total` e `period` | IT-01 |
| Esta semana / este mês | `periodo` | só os shows do período no fuso da cidade | UT-01, IT-01, E2E-01 |
| Intervalo escolhido | `de`/`ate` | só o intervalo; 181 dias ou `ate < de` → 400 na API e agenda padrão na página | UT-01, IT-03, E2E-02 |
| Estilo | `estilo=death,grind` | só shows com algum desses estilos; estilo inexistente ignorado | UT-02, IT-02, E2E-01 |
| Combinação | período + estilo | os dois filtros juntos | IT-02, E2E-01 |
| Vazio com filtro | nada no filtro | "Nenhum show com esses filtros." + "Limpar filtros" | E2E-02 |
| URL compartilhável | abrir a URL filtrada direto | SSR já filtrado, chips marcados | E2E-01 |
| CTA de produtor | anônimo / artista | `/entrar?returnTo=/conta/shows/novo` / `/conta/shows/novo` | E2E-02 |
| Desempenho | 300 shows | ≤ 300 ms p95 | IT-02 |
| Contrato | OpenAPI | parâmetros, `period`, `total` e códigos | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- **CH-01** — Dada a agenda sem parâmetros (SPEC-0023), quando chamada, então devolve os shows de 6 h atrás até 90 dias, ordenados (guarda: passa antes da mudança).

### 7.2 Testes Unitários
- **UT-01** — Dados "hoje" 2026-11-12 (quinta) em `America/Sao_Paulo` e o fim do mês, quando os períodos são calculados, então "semana" = 12 a 15/11 e "mês" = 12 a 30/11; e `de`/`ate` válidos, `ate < de` e 181 dias são aceitos ou recusados como no §6.
- **UT-02** — Dado o leitor de parâmetros da página, quando recebe `estilo=death,,GRIND,xpto` e `periodo=foo`, então normaliza para `death,grind` (comparando com os estilos conhecidos de `GET /api/v1/styles`, que a página já carrega para os chips; a API também ignora o inexistente) e ignora o período inválido.

### 7.3 Testes de Integração
- **IT-01** — Dados shows em Curitiba hoje, no domingo, no próximo mês e daqui a 100 dias, quando a agenda é pedida sem parâmetros, com a semana e com o mês, então cada resposta traz só os do período, com `period` e `total` certos.
- **IT-02** — Dados 300 shows com estilos variados, quando a agenda é pedida com `styles=death,grind` e um período, então vêm só os com algum desses estilos no período, em p95 ≤ 300 ms; `styles=xpto` não quebra (lista ignorada).
- **IT-03** — Dados `from` depois de `to`, intervalo de 181 dias e data mal formada, quando pedidos, então 400 `agenda.invalid_range`; e `styles` com slug fora do padrão → 400 `agenda.invalid_styles`.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então a agenda tem os parâmetros, o `period`, o `total` e os códigos do §6, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa com shows de estilos e datas variados, nos projetos mobile-chrome e mobile-safari: o fã abre a agenda, toca em "Esta semana" e depois em "DEATH"; a URL muda, só os shows certos aparecem e o total no cabeçalho bate; abrindo a mesma URL numa aba nova, a página já vem filtrada com os chips marcados. Sem violações do axe. [jornada: agenda]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: "Escolher data" com um intervalo sem shows mostra "Nenhum show com esses filtros." e "Limpar filtros" volta à agenda padrão; anônimo tocando em "Cadastrar evento na agenda" vai para `/entrar?returnTo=/conta/shows/novo`. [jornada: agenda]

### 7.6 Outros
- Acessibilidade: `aria-current` nos chips e total anunciado (E2E-01).

**Dublês e dados de teste:** shows criados pela API (perfil aprovado + flyer) nos IT e no E2E; `FakeTimeProvider` para "hoje".

**Ambiente de execução:** IT com Testcontainers; E2E contra a pilha do Compose; `--workers=1`.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** índice `(city, starts_at_utc)` em `events.shows` (aditivo).
- **Compatibilidade:** sem parâmetros a agenda responde como antes (mais `period` e `total`).
- **Observabilidade:** tags de filtro (período, número de estilos) no span da consulta, sem dados pessoais.
- **Rollback:** reverter o merge commit; o índice pode ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Janela padrão: 60 dias (Stitch) ou 90 (SPEC-0023)? — 90 dias, como já entregue pela SPEC-0023 (recomendação do Architect; piloto automático do Thomas, 2026-10-18)
- [x] Paginação no MVP? — Não; uma cidade cabe numa página no lançamento (recomendação do Architect; piloto automático do Thomas, 2026-10-18)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"); telas do Stitch já existentes.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 0: Caracterização**
- [ ] CH-01 escrito, passando no código atual, commitado antes de qualquer mudança

**Fase 1: Períodos e parâmetros**
- [ ] Red: UT-01, UT-02 com a tag `SPEC-0025:<ID>`, falhando pelo motivo certo
- [ ] Green e Refactor

**Fase 2: API e contrato**
- [ ] Red: IT-01..IT-03, CT-01 falhando pelo motivo certo
- [ ] Green: filtros na consulta, índice, OpenAPI e cliente TS
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Tela e jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [ ] Green: chips, seletor de datas, estados e CTA conforme o Stitch e os ajustes

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 794f227 | 2026-10-18 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 7ec7168 | 2026-10-19 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0025`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 | 2026-10-19 | (a) Data inválida, `to < from` e mais de 180 dias seguem com o código atual `agenda.invalid_range` (SPEC-0008), em vez de `agenda.invalid_period` — mudança sem quebra; IT-03 e CT-01 ajustados. (b) `period` sem `from`/`to` = hoje a hoje + 90 nas datas locais da cidade. (c) UT-02: o leitor filtra pelos estilos conhecidos de `GET /api/v1/styles`. (d) "Escolher data" abre um formulário GET com "De", "Até" e "Aplicar"; chips são links com `aria-current`. (e) Cabeçalho com datas dd/MM, singular "1 SHOW" e total em aria-live. | Relatório do Test-writer (SD-1..SD-5) | — | Architect (piloto automático do Thomas, 2026-10-17) |
| 2 | 2026-10-19 | Sem mudança de contrato. (a) `tests/Security/Subsolo.Security.IntegrationTests/AgendaUnderFallbackPolicyTests.cs` (SPEC-0010:CT-01) entra em `touches`: o corpo da agenda passa a ter `period` e `total`, e o Test-writer ajusta a lista de campos. (b) A contagem "X vão" dos cards fica: já é entregue e testada desde a SPEC-0008 (`Card.tsx`), e a SPEC-0027 ("Vou") a torna real — o ajuste "sem a contagem" do §6 deixa de valer. (c) Desvios aceitos do Implementer: "Escolher data" como link `?periodo=escolher` que abre o formulário no servidor; tocar no chip ativo remove o filtro; `ChipLink` novo ao lado do `Chip` de alternância; chips de estilo no visual atual do `Chip`; no desktop o título fica acima da grade de duas colunas; CTA com sublinhado vermelho por contraste; vazio com filtro com marcação própria; data mal formada → 400 por filtro de endpoint (produção) e `IExceptionHandler` do módulo (desenvolvimento). | Relatório do Implementer | — | Architect (piloto automático do Thomas, 2026-10-17) |
