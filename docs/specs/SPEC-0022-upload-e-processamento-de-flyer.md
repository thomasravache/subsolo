---
id: SPEC-0022
title: Upload e processamento de flyer
tier: full
type: feature
user_facing: false
status: approved
created: 2026-10-17
parent: SPEC-0002
depends_on: [SPEC-0021]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Common/Subsolo.Common.Application/**, src/Common/Subsolo.Common.Infrastructure/**, tests/Common/**, src/Modules/Events/**, tests/Modules/Events/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/Subsolo.Api.csproj, src/Subsolo.Api/packages.lock.json, src/Subsolo.Api/appsettings.json, src/Subsolo.Api/appsettings.Development.json, src/Subsolo.AppHost/**, infra/caddy/**, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, Directory.Packages.props, docs/api/**, web/src/api/**]
adrs: [ADR-0001, ADR-0004, ADR-0009, ADR-0011, ADR-0015]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0022 — Upload e processamento de flyer

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Produtores e o admin enviam o flyer de um show; a API confere o arquivo, remove os metadados (inclusive localização), gera as três versões do ADR-0015 — `thumb` (400 px), `full` (1080 px) e `og` (1200×630, o flyer inteiro sobre fundo escuro desfocado, sem cortar a arte) — e guarda tudo em disco com nome pelo conteúdo. O cadastro de show (spec seguinte) usa o identificador devolvido. Esta spec não tem tela: o campo de upload faz parte da tela "Cadastrar show".

## 2. Motivação & Escopo
**Motivação:** o flyer é o herói da agenda e da prévia no WhatsApp (épico, ADR-0013, ADR-0015). A SPEC-0007 (Emenda v1) deixou o `IFileStorage` para esta feature.

**Objetivos (dentro do escopo):**
- `IFileStorage` em `Common.Application` (gravar, abrir, apagar por caminho relativo) com implementação em disco em `Common.Infrastructure`, diretório configurável `Media:Root`.
- `IImageProcessor` em `Common.Application` com implementação NetVips em `Common.Infrastructure`: detecta o tipo pelo conteúdo (JPEG, PNG, WebP), limita a 10 MB e a 40 megapixels, remove EXIF/ICC/XMP e gera `thumb`, `full` (WebP) e `og` (JPEG 1200×630).
- `POST /api/v1/flyers` (multipart, campo `file`) no módulo `Events`: devolve `flyerId` (hash SHA-256 do arquivo original) e as URLs das variantes; o mesmo arquivo enviado de novo devolve o mesmo id sem reprocessar.
- Tabela `events.flyers` (id, enviado por, criado em, ligado em); flyers não ligados a um show há mais de 24 h são apagados (tarefa periódica). A ligação é feita pela spec de cadastro de show pela interface interna `IFlyerCatalog` (`AttachAsync`, `ExistsAsync`).
- Servir `/media/flyers/*`: no `aspire run`, pela API (arquivos estáticos do diretório `Media:Root`); no Compose e em produção, pelo Caddy, com o volume `media` montado na API e no Caddy; sempre com `Cache-Control: public, max-age=31536000, immutable` e `X-Content-Type-Options: nosniff`.

**Não-objetivos (fora do escopo):**
- Tela ou componente de upload (spec de cadastro de show).
- Object storage (R2): troca futura por configuração, fora do MVP.
- Moderação automática da arte (a moderação é humana, ADR-0015).
- Recorte ou edição do flyer pelo usuário.

## 3. Dependências
- **Implementações necessárias:** SPEC-0021 — o papel `artist` (concedido na aprovação de perfil) é quem pode enviar; antes dela só o admin poderia testar.
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** pacotes `NetVips` e `NetVips.Native` (MIT; libvips LGPL ligada dinamicamente), previstos no ADR-0015 — versões fixadas e verificadas na implementação (ADR-0009).

## 4. Decisão Arquitetural
**Contexto:** blocos `Common` (SPEC-0007), módulo `Events` (SPEC-0008/0017), segurança e rate limit (SPEC-0010/0014), Caddy e Compose gerados pelo AppHost (ADR-0004, SPEC-0008).

**Decisão:** abstrações em `Common.Application` e implementações em `Common.Infrastructure` (só ele referencia NetVips e `System.IO` de mídia, ADR-0015); o slice `UploadFlyer` no `Events` orquestra validação, processamento, gravação e registro; o AppHost declara o volume `media` e o Caddy ganha a rota `/media/*` com os cabeçalhos de cache.

**Justificativa:** ADR-0015 (NetVips, disco local atrás de `IFileStorage`, nome por hash, cache imutável), ADR-0004 (Caddy serve `/media`), ADR-0001 (flyer é dado da agenda, módulo `Events`).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** guardar o flyer no PostgreSQL (banco e backup pesados); processar no Next.js com `sharp` (a API é dona dos dados e da regra; dois lugares processando imagem); manter o arquivo original (traz EXIF e pesa à toa).

**ADRs:** ADR-0001, ADR-0004, ADR-0009, ADR-0011, ADR-0015

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** processar um flyer de 10 MB (4000×6000) em ≤ 3 s p95 e com pico de memória do processo ≤ 300 MB acima do repouso (IT-02); só um processamento por vez por instância (fila de 1), para caber na VPS de 8 GB.
- **Segurança:** só `artist` ou `admin` (anônimo 401, fã 403); `X-CSRF`; rate limit `report` (10/h por IP); tipo pelo conteúdo, nunca pela extensão; limite de tamanho no servidor (10 MB) antes de ler tudo; limite de pixels contra "bomba de descompressão"; nome sem dado do usuário (hash) — IT-03, IT-04, UT-01.
- **Privacidade e dados pessoais:** EXIF removido, inclusive GPS (IT-01); `uploaded_by` só para auditoria e limpeza.
- **Disponibilidade e resiliência:** falha no processamento → 422 `events.flyer_unprocessable` sem deixar arquivos pela metade (gravação em arquivo temporário e troca atômica) — IT-05.
- **Acessibilidade (UI):** N/A — sem tela nesta spec (o texto alternativo do flyer é da página do show).
- **Custo:** zero (disco da VPS; o backup do `media` já está no ADR-0011).

## 6. Artefato A — Contrato
**Interface:** `POST /api/v1/flyers` · `GET /media/flyers/{arquivo}` · `IFileStorage` · `IImageProcessor` · `IFlyerCatalog`

```yaml
POST /api/v1/flyers:                     # RequireAuthorization(artist ou admin) + X-CSRF + RequireRateLimiting(report)
  body: multipart/form-data { file }     # até 10 MB; JPEG, PNG ou WebP pelo conteúdo; ≤ 40 megapixels
  201: { flyerId: string (64 hex), thumbUrl, fullUrl, ogUrl }   # /media/flyers/<id>-thumb.webp etc.
  200: mesmo corpo quando o arquivo já existe (mesmo hash)
  400: ProblemDetails code=events.flyer_invalid_type | events.flyer_too_large | events.flyer_too_many_pixels | events.flyer_missing
  422: ProblemDetails code=events.flyer_unprocessable
  401 auth.required ; 403 auth.forbidden ; 400 security.csrf_invalid ; 429 security.rate_limited
GET /media/flyers/<id>-thumb.webp | <id>-full.webp | <id>-og.jpg:
  200 com Cache-Control: public, max-age=31536000, immutable ; X-Content-Type-Options: nosniff ; 404 se não existe
```

```csharp
// Subsolo.Common.Application
public interface IFileStorage { Task SaveAsync(string path, Stream content, CancellationToken ct); Task<Stream?> OpenAsync(string path, CancellationToken ct); Task DeleteAsync(string path, CancellationToken ct); }
public interface IImageProcessor { Task<Result<FlyerImages>> ProcessFlyerAsync(Stream original, CancellationToken ct); }
public sealed record FlyerImages(byte[] Thumb, byte[] Full, byte[] Og, string ContentHash);
// Subsolo.Modules.Events (interno ao módulo, usado pelo cadastro de show)
public interface IFlyerCatalog { Task<bool> ExistsAsync(string flyerId, CancellationToken ct); Task AttachAsync(string flyerId, CancellationToken ct); }
```

```text
Configuração: Media:Root (local: volume do Aspire; Compose/produção: volume media montado na API e no Caddy)
Banco: events.flyers(id char(64) PK, uploaded_by uuid, created_at_utc, attached_at_utc null)
Limpeza: flyers com attached_at nulo e criados há > 24 h → arquivos e linha apagados (tarefa periódica de hora em hora)
Caddy: handle /media/* { root * /srv/media ; file_server ; header Cache-Control "public, max-age=31536000, immutable" }
```

**Design:** N/A — sem interface; o campo de upload está na tela "Cadastrar Show - Subsolo" (0eda9cdf97fa484188933d53f1605783), implementado pela spec de cadastro de show.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Enviar flyer | artista, JPEG válido com EXIF/GPS | 201; três variantes nas medidas certas; sem EXIF | UT-02, IT-01 |
| Mesmo arquivo de novo | mesmo hash | 200 com o mesmo id, sem reprocessar | IT-01 |
| Tipo falso | `.jpg` que é PDF ou texto | 400 `flyer_invalid_type` | UT-01, IT-03 |
| Grande demais | > 10 MB; > 40 megapixels | 400 `flyer_too_large` / `flyer_too_many_pixels`, sem ler o arquivo todo | UT-01, IT-03 |
| Arquivo corrompido | cabeçalho válido, dados quebrados | 422 `flyer_unprocessable`, nenhum arquivo deixado | IT-05 |
| Acesso | anônimo, fã, artista, admin | 401, 403, 201, 201 | IT-04 |
| Servir | variante existente; inexistente | 200 com cache imutável e `nosniff`; 404 | IT-06 |
| Limpeza | flyer sem show há 25 h; ligado | o sem show some (arquivos e linha); o ligado fica | IT-07 |
| Desempenho | 10 MB, 4000×6000 | ≤ 3 s p95; memória dentro do limite | IT-02 |
| Arquitetura | dependências | só `Common.Infrastructure` referencia NetVips | IT-08 |
| Compose e Caddy | publicação | volume `media` na API e no Caddy; rota `/media/*` com os cabeçalhos | IT-09 |
| Contrato publicado | snapshot OpenAPI | `POST /flyers` com corpos e códigos do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado o validador de upload, quando recebe os primeiros bytes de JPEG, PNG, WebP, PDF, GIF e texto, e tamanhos de 10 MB e 10 MB + 1, então só os três tipos de imagem até 10 MB passam, com os códigos do §6 nos outros casos.
- **UT-02** — Dado o cálculo das medidas das variantes, quando recebe flyers 1080×1920, 4000×6000 e 1200×600, então `thumb` tem 400 px de largura e `full` 1080 px (sem aumentar imagem menor), e `og` é sempre 1200×630 com o flyer inteiro dentro (contido, sem corte).

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL e o NetVips reais e um JPEG com EXIF e GPS, quando um artista envia, então 201 com o id igual ao SHA-256 do original, as três variantes existem em `Media:Root` com as medidas do UT-02, nenhuma tem EXIF nem GPS, e `events.flyers` tem a linha; enviando de novo, 200 com o mesmo id e as datas de modificação dos arquivos não mudam.
- **IT-02** — Dado um JPEG de 10 MB e 4000×6000, quando enviado 10 vezes, então o p95 é ≤ 3 s e o pico do working set do processo fica ≤ 300 MB acima do repouso.
- **IT-03** — Dados um PDF renomeado para `.jpg`, um arquivo de 10 MB + 1 e um PNG de 8000×8000, quando enviados, então 400 com `flyer_invalid_type`, `flyer_too_large` e `flyer_too_many_pixels`, e nada é gravado.
- **IT-04** — Dados anônimo, fã, artista e admin, quando enviam um flyer válido, então 401, 403, 201 e 201; sem `X-CSRF`, 400 `security.csrf_invalid`; o 11º envio na hora pelo mesmo IP dá 429.
- **IT-05** — Dado um JPEG com cabeçalho válido e dados quebrados, quando enviado, então 422 `flyer_unprocessable` e nenhum arquivo nem linha ficam para trás.
- **IT-06** — Dado um flyer enviado, quando `GET /media/flyers/<id>-thumb.webp` é pedido à API no modo de desenvolvimento, então 200 `image/webp` com `Cache-Control: public, max-age=31536000, immutable` e `nosniff`; um nome inexistente dá 404; um caminho com `..` dá 404.
- **IT-07** — Dados um flyer não ligado criado há 25 h e outro ligado (`FakeTimeProvider`), quando a limpeza roda, então o primeiro some do disco e do banco e o segundo fica.
- **IT-08** — Dados os testes de arquitetura, quando rodam, então só `Common.Infrastructure` referencia `NetVips`, e o `Events` usa só `IFileStorage`/`IImageProcessor`.
- **IT-09** — Dado o Compose publicado pelo AppHost e o `infra/caddy/Caddyfile`, quando lidos, então a API e o Caddy montam o volume `media`, a API recebe `Media__Root`, e o Caddy tem a rota `/media/*` com `file_server` e o `Cache-Control` imutável.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então `POST /api/v1/flyers` tem o corpo multipart, as respostas e os códigos do §6, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- N/A — nenhuma jornada de usuário muda nesta spec: o upload aparece para o usuário na tela de cadastro de show, cujo E2E ([jornada: cadastro-show]) cobre o envio do flyer de ponta a ponta.

### 7.6 Outros
- Segurança: G4 confere a checagem de tipo pelo conteúdo, os limites antes de ler tudo, o caminho sem `..` e a ausência de EXIF.

**Dublês e dados de teste:** imagens de teste geradas no próprio teste (com EXIF e GPS sintéticos), sem arquivos binários grandes no repositório; `FakeTimeProvider`.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL) e diretório temporário como `Media:Root`; `--max-parallel-test-modules 1`.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** tabela nova `events.flyers` (aditiva); volume `media` novo.
- **Compatibilidade:** endpoint novo; o Caddy ganha uma rota nova sem mudar as existentes.
- **Observabilidade:** histograma `subsolo.flyers.processing_duration`, contador `subsolo.flyers.uploads` (por resultado); logs sem nome de arquivo do usuário.
- **Rollback:** reverter o merge commit; o volume e a tabela podem ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Limite de pixels? — 40 megapixels (cobre flyers de story em alta e barra arquivos feitos para estourar a memória) (recomendação do Architect; piloto automático do Thomas, 2026-10-17)
- [x] Quem pode enviar? — `artist` e `admin`, os mesmos que cadastram show (recomendação do Architect; piloto automático do Thomas, 2026-10-17)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"); sem tela nesta spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 9b9e13b | 2026-10-17 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0022`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
