---
id: SPEC-0024
title: Página do show com Open Graph e compartilhar
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
touches: [src/Subsolo.AppHost/**, tools/compose-up.sh, src/Subsolo.Api/packages.lock.json, src/Common/Subsolo.Common.Infrastructure/**, tests/Common/**, infra/caddy/**, web/src/app/(public)/conta/shows/**, tests/**/packages.lock.json, src/Modules/Events/**, tests/Modules/Events/**, docs/api/**, web/src/api/**, web/src/app/(public)/[cidade]/**, web/src/components/shows/**, web/src/components/agenda/**, web/src/proxy.ts, web/src/proxy.test.ts, e2e/tests/shows/**, e2e/tests/agenda/**, docs/design/**, .github/workflows/ci.yml]
adrs: [ADR-0013, ADR-0015, ADR-0016, ADR-0017]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0024 — Página do show com Open Graph e compartilhar

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Cada show ganha uma página própria (`/{cidade}/{show}`) renderizada no servidor, com o flyer inteiro em destaque, data e hora, casa, line-up, estilos, preço, botão de ingresso e "Compartilhar". A página traz os metadados Open Graph com a imagem `og` do flyer (1200×630), para que o link mandado no WhatsApp, no Instagram ou no Telegram mostre o flyer na prévia. O card da agenda passa a levar a essa página.

## 2. Motivação & Escopo
**Motivação:** critério de aceite do épico "O link do show compartilhado no WhatsApp mostra o flyer na prévia" (jornada `compartilhar-show`); ADR-0013 (SSR e Open Graph por página) e ADR-0015 (variante `og`).

**Objetivos (dentro do escopo):**
- `GET /api/v1/cities/{city}/shows/{slug}` público com os detalhes do show (situação inclusa; cancelado e adiado continuam com página, com selo).
- Página `/{cidade}/{show}` (SSR, dinâmica): flyer `full` inteiro (`object-fit: contain`) sobre fundo desfocado do próprio flyer, título, data/hora no fuso da cidade, casa, line-up, estilos, preço, botão "Ingresso" (quando houver link, abre em nova aba com `rel="noopener noreferrer"`), selo CANCELADO/ADIADO, "Compartilhar" (Web Share API no celular; senão copia o link e mostra "Link copiado.").
- Metadados: `<title>` "{título} · {cidade} · Subsolo", `description` (data, casa e line-up), `og:title`, `og:description`, `og:image` (variante `og`, URL absoluta pelo `App:PublicUrl`), `og:image:width/height` (1200/630), `og:type=website`, `og:url` canônica, `twitter:card=summary_large_image`, e `link rel=canonical`.
- Status HTTP real (ADR-0016): show inexistente → 404 com a página "Show não encontrado", cidade inexistente → 404; API fora → 503.
- O card da agenda vira `next/link` para a página; é a primeira página interna 200 alcançável por `next/link`, então leva o E2E do título na navegação no cliente (herança da SPEC-0015) e investiga o achado do título "Subsolo" na cidade inexistente.

**Não-objetivos (fora do escopo):**
- Botão "Vou" e contagem (spec "Vou"); "Seguir" e "Denunciar" (specs próprias) — a página deixa o espaço, sem botões falsos.
- Página do perfil público (links da casa e das bandas entram quando ela existir; até lá, só texto).
- Imagem OG gerada por página (usa a variante `og` do flyer, ADR-0015).

## 3. Dependências
- **Implementações necessárias:** SPEC-0023 (shows cadastrados, agenda com shows) e, por ela, SPEC-0022 (variantes do flyer).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** agenda SSR com status pelo proxy (SPEC-0008, ADR-0016), CSP com nonce (ADR-0017), shell e componentes (SPEC-0011), flyers servidos em `/media` (SPEC-0022), `App:PublicUrl` (SPEC-0016).

**Decisão:** slice `GetShowDetail` no `Events`; rota do Next `app/(public)/[cidade]/[show]/page.tsx` com `generateMetadata`; o proxy (ADR-0016) consulta a API para definir o status da rota de dois segmentos, como já faz com a cidade; o card da agenda usa `next/link`.

**Justificativa:** ADR-0013 (SSR + Open Graph), ADR-0016 (status real das páginas pelo proxy), ADR-0015 (variante `og` pronta, sem gerar imagem na hora).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** URL `/shows/{id}` (pior para compartilhar e para SEO; o slug com a data já é único por cidade); gerar a imagem OG por página com `next/og` (custa CPU na VPS; o flyer já tem a variante `og`).

**ADRs:** ADR-0013, ADR-0015, ADR-0016, ADR-0017

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** página com LCP ≤ 2,5 s no perfil mobile do Lighthouse contra a pilha local (E2E-03) e API de detalhe em ≤ 150 ms p95 (IT-01); a imagem `full` com `width`/`height` declarados (sem CLS).
- **Segurança:** endpoint público só leitura com rate limit `default`; textos do show escapados; link do ingresso com `rel="noopener noreferrer"` e só `https` (já validado no cadastro); CSP com nonce mantida (IT-03, E2E-02).
- **Privacidade e dados pessoais:** a página não expõe quem criou o show nem dados de conta; só dados públicos do show e dos perfis.
- **Disponibilidade e resiliência:** API fora → 503 com página de erro, nunca 200 vazio (IT-04).
- **Acessibilidade (UI):** WCAG 2.2 AA com axe; `alt` do flyer "Flyer de {título}"; data em `<time datetime>`; selos com texto; "Link copiado." anunciado por `aria-live`; título nunca vazio na navegação no cliente (E2E-04).
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `GET /api/v1/cities/{city}/shows/{slug}` · página `/{cidade}/{show}` · metadados Open Graph

```yaml
GET /api/v1/cities/{city}/shows/{slug}:   # AllowAnonymous + RequireRateLimiting(default)
  200: { id, slug, title, city: { slug, name, timeZone }, startsAt (UTC), venue: { name, profileSlug|null },
         lineup: [{ name, profileSlug|null }], styles: [{ slug, name }], price, ticketUrl|null,
         status: scheduled|cancelled|postponed, flyer: { thumbUrl, fullUrl, ogUrl, width, height } }
  404: ProblemDetails code=events.show_not_found | events.city_not_found
  503: ProblemDetails (API sem banco)
```

```text
/{cidade}/{show} (SSR dinâmica)
  <title>{título} · {cidade} · Subsolo</title> ; meta description "{dia} {hora} · {casa} · {line-up}"
  og:title, og:description, og:image = {App:PublicUrl}{ogUrl}, og:image:width 1200, og:image:height 630, og:type website,
  og:url canônica, twitter:card summary_large_image, link rel=canonical
  conteúdo: flyer full (contain, fundo desfocado) ; selo CANCELADO / ADIADO ; h1 título ; <time> data e hora locais ; casa ; line-up separado por " / " ;
    chips de estilo ; preço ; botão "Ingresso" (se houver) ; "Compartilhar" (Web Share ou copiar → "Link copiado.")
  404 → "Show não encontrado" + link "Ver a agenda de {cidade}" ; 503 → página de erro
Agenda: o card do show é um next/link para /{cidade}/{show}
```

**Design:** Stitch, projeto 15194126668426659178: "Página do Show - Noite da Peste II" (98cc07285ec5439197d3273da0f7a11d, mobile) e "(Desktop)" (ddae50bdb35f4cdaa2437a12f23fe257). Ajustes: sem "Vou", "Quem vai", "Seguir" e "Denunciar" (entram com as specs deles), links de perfil só quando a página do perfil existir.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Ver o show | slug existente | 200 com os dados do §6; página com flyer, dados e botões | UT-01, IT-01, E2E-01 |
| Prévia no WhatsApp | HTML da página | metadados OG do §6 com a imagem `og` absoluta 1200×630 | IT-02, E2E-01 |
| Cancelado / adiado | situação | 200 com selo na página | IT-01, E2E-01 |
| Show inexistente | slug ou cidade desconhecidos | 404 real com "Show não encontrado" | IT-03, E2E-02 |
| API fora | banco indisponível | 503 com página de erro | IT-04 |
| Compartilhar | com e sem Web Share | abre o compartilhamento ou copia e mostra "Link copiado." | UT-02, E2E-01 |
| Card da agenda | toque no card | navega no cliente para a página; título nunca vazio | E2E-04 |
| Desempenho | perfil mobile | LCP ≤ 2,5 s; sem CLS da imagem | E2E-03 |
| Segurança | CSP e textos | CSP com nonce sem violações; textos escapados | IT-03, E2E-02 |
| Contrato publicado | snapshot OpenAPI | detalhe do show com corpo e códigos do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- **CH-01** — Dada a página de cidade inexistente carregada direto (achado da SPEC-0015), quando o título é lido, então o comportamento atual fica registrado (hoje "Subsolo" em vez de "Cidade não encontrada · Subsolo"); o teste vira guarda do título correto quando esta spec corrigir (guarda: passa antes da mudança no que já está certo — status 404).

### 7.2 Testes Unitários
- **UT-01** — Dados detalhes de show de teste, quando os metadados são montados, então `title`, `description`, `og:*`, `twitter:card` e `canonical` saem como no §6, com a URL da imagem absoluta pelo `App:PublicUrl`, e com selo no título para cancelado ("Cancelado — {título} …").
- **UT-02** — Dado o botão "Compartilhar" com e sem `navigator.share`, quando tocado, então chama o Web Share com título e URL, ou copia o link e anuncia "Link copiado." em `aria-live`.

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL, um show agendado e outro cancelado com flyer, quando `GET /api/v1/cities/curitiba/shows/{slug}` é chamado, então 200 com o corpo do §6, as URLs das variantes e a situação; p95 de 20 chamadas ≤ 150 ms.
- **IT-02** — Dada a página do show servida pelo Next na pilha local, quando o HTML é baixado (sem JavaScript, como um robô de prévia), então tem `og:image` absoluta para a variante `og` com 1200×630, `og:title`, `og:description`, `og:url`, `twitter:card` e `canonical`.
- **IT-03** — Dados um slug inexistente e uma cidade inexistente, quando a página é pedida, então 404 real com "Show não encontrado"; e a página de um show com `<script>` no título mostra o texto escapado e a CSP com nonce.
- **IT-04** — Dada a API sem banco, quando a página do show é pedida, então 503 com a página de erro.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então `GET /api/v1/cities/{city}/shows/{slug}` tem o corpo e os códigos do §6, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa com um show cadastrado, nos projetos mobile-chrome e mobile-safari: o fã abre a agenda da cidade, toca no card do show e vê a página com flyer, data, casa, line-up, estilos, preço e "Ingresso"; toca em "Compartilhar" (com o compartilhamento simulado) e vê o link do show; um show cancelado mostra o selo "CANCELADO". Sem violações do axe. [jornada: compartilhar-show]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: um link de show inexistente mostra "Show não encontrado" com status 404 e o link de volta para a agenda; a página do show carrega sem erros de CSP. [jornada: compartilhar-show]
- **E2E-03** — Dada a pilha local, no projeto mobile-chrome: a página do show tem LCP ≤ 2,5 s (medido pelo `PerformanceObserver`) e a imagem do flyer tem `width` e `height`. [jornada: compartilhar-show]
- **E2E-04** — Dada a agenda com um show, nos projetos mobile-chrome e mobile-safari: um `MutationObserver` registra todo `document.title` durante o toque no card (`next/link`); nenhum valor fica vazio, o título final é o da página do show, e o axe `document-title` passa logo depois da troca de URL (herança da SPEC-0015). [jornada: agenda]

### 7.6 Outros
- Acessibilidade e desempenho: axe nas páginas; LCP e CLS no E2E-03.
- Achado da SPEC-0015 (épico): investigar e corrigir o título da cidade inexistente carregada direto (CH-01).
- Herança da revisão da SPEC-0023 (G4 em 28717fe; cada item com teste próprio com a tag `SPEC-0024:H23-NN`):
  - **H23-01, limpeza × reenvio:** antes de apagar os arquivos de um id, a limpeza confere que a linha não voltou (reenvio do mesmo hash no meio); se voltou, mantém os arquivos. O envio regrava as variantes se os arquivos faltarem.
  - **H23-02, criar/editar × limpeza:** se a limpeza apagou o flyer entre `ExistsAsync` e o `SaveChanges` (FK ou concorrência), a resposta é 400 `events.flyer_not_found`, nunca 500; o `Renew` do envio com a linha apagada reprocessa em vez de dar 500.
  - **H23-03, `JpegHeaderCleaner`:** só aloca a cópia ao achar o primeiro byte solto; UT com entrada malformada (comprimento truncado, `FF00` no cabeçalho, sem SOS) devolvendo os bytes originais.
  - **H23-04, Caddy:** `@served` com `not path */`, para um diretório não ganhar o cache imutável.
  - **H23-05, sobras:** remover o `unitOfWork` sem uso da limpeza, o segundo construtor de teste da agenda e o `ListUnattachedCreatedBeforeAsync` morto, num `test(...)` que ajusta os dublês antes do `refactor(...)`.
  - **H23-06, fuso em "Meus shows":** `GET /api/v1/me/shows` ganha `timeZone` (da cidade) em cada item, campo aditivo; o web deixa de consultar a agenda pública só para ler o fuso.
  - **H23-07, acessibilidade:** cada campo da line-up liga o erro por `aria-describedby`.

**Dublês e dados de teste:** shows e flyers criados pela API nos IT e no E2E; flyer gerado no teste.

**Ambiente de execução:** IT da API com Testcontainers; IT-02/IT-03 e E2E contra a pilha do Compose; `--workers=1`.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** colunas `width` e `height` da variante `full` em `events.flyers`, nulas e preenchidas no envio (Emenda v2; migração aditiva).
- **Compatibilidade:** rota nova de dois segmentos; a agenda continua igual, com o card agora clicável.
- **Observabilidade:** trace da página até a API; contador de 404 de show no Seq (consulta) para achar links quebrados.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] URL da página? — `/{cidade}/{show}` com o slug que já leva a data (recomendação do Architect; piloto automático do Thomas, 2026-10-18)
- [x] Página de show cancelado continua no ar? — Sim, com o selo; links compartilhados não quebram (decisão do Thomas no épico, 2026-10-13)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"); telas do Stitch já existentes.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 0: Caracterização**
- [ ] CH-01 escrito, passando no código atual, commitado antes de qualquer mudança

**Fase 1: Metadados e compartilhar**
- [ ] Red: UT-01, UT-02 com a tag `SPEC-0024:<ID>`, falhando pelo motivo certo
- [ ] Green e Refactor

**Fase 2: API e página**
- [ ] Red: IT-01..IT-04, CT-01 falhando pelo motivo certo
- [ ] Green: slice, página SSR, proxy, OpenAPI e cliente TS
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Jornada E2E**
- [ ] Red: E2E-01..E2E-04 falhando pelo motivo certo
- [ ] Green: jornada completa passando localmente

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — fd9ca7e | 2026-10-18 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 65845bb | 2026-10-18 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 154e458 | 2026-10-18 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 6s 075ms) — 154e458 | 2026-10-18 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (0 blocker, 0 major, 7 minor → herdados H24-02..08) — 154e458 | 2026-10-18 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 11m 45s 275ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 6s 432ms) — 25a8682 | 2026-10-19 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-19 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0024`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 | 2026-10-18 | (a) O web recebe o endereço público por variável **só do servidor**, `SUBSOLO_PUBLIC_URL` (sem `NEXT_PUBLIC_`, nunca no navegador), definida pelo AppHost e pelo `compose-up.sh` com o mesmo valor do `App:PublicUrl` da API; `tools/compose-up.sh` e `src/Subsolo.AppHost/**` entram em `touches`. (b) `flyer.width`/`height` são as medidas da variante `full`, gravadas no envio em colunas novas de `events.flyers` (migração aditiva; flyers antigos sem medida → `null`, e a página usa a proporção 3:4). (c) `description`: "{ddd dd/MM} {HH'h'mm no fuso da cidade} · {casa} · {line-up separado por ' / '}"; só o cancelado ganha selo no título. (d) API fora → 503 com a página "Show indisponível" e mensagem em `role=alert`, como a da agenda. (e) O tipo `ShowDetail` do web passa a ser o gerado do OpenAPI. (f) H23-04 fica com os dois testes (Caddyfile e E2E de guarda). | Relatório do Test-writer: endereço público ausente no web, origem das medidas, formato da descrição e texto do 503 | — | Architect (piloto automático do Thomas, 2026-10-17) |
| 2 | 2026-10-18 | Sem mudança de contrato. Aceitos os desvios do Implementer: (1) E2E-01 passa a localizar o `main` visível (`getByRole("main")`), porque com `cacheComponents` a agenda continua no DOM, oculta (Activity), após a navegação pelo card — correção do teste feita pelo Test-writer, sem mudar asserções; (2) a remoção do método morto `ListUnattachedCreatedBeforeAsync` no dublê de `UploadFlyerContentHashTests` (consequência mecânica de H23-05, nenhuma asserção alterada); (3) medidas do flyer lidas do cabeçalho WebP da variante `full` (`WebPSize`), `null` em cabeçalho desconhecido — mover a medida para `FlyerImages`/`NetVipsImageProcessor` fica como herdado H24-01 da próxima spec que tocar `Common.Application/Media`; (4) o 503 `service.unavailable` é tratado no próprio `GetShowDetailEndpoint` (não há tratador global de banco fora em `src/Subsolo.Api`); (5) títulos 404 vêm do `not-found` mais próximo (comportamento do Next 16.4, coberto por E2E); (6) line-up como lista " / " e ícones de compartilhar locais em `components/shows/show-icons.tsx`. | Relatório do Implementer | — | Architect (piloto automático do Thomas, 2026-10-17) |
| 2 | 2026-10-19 | Sem mudança de contrato. `.github/workflows/ci.yml` entra em `touches`: o job `integration` passa de `timeout-minutes: 20` para 30 (a suíte de integração cresceu para ~17–20 min no runner e o PR #31 foi cancelado no último módulo, com tudo passando). | CI do PR #31 cancelado por tempo | — | Architect (piloto automático do Thomas, 2026-10-17) |
