---
id: SPEC-0027
title: Seguir cidade, banda e casa
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0025, SPEC-0026]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Engagement/**, tests/Modules/Engagement/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/Subsolo.Api.csproj, src/Subsolo.Api/packages.lock.json, Subsolo.slnx, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, docs/api/**, web/src/api/**, web/src/app/(public)/seguindo/**, web/src/app/(public)/[cidade]/**, web/src/app/(public)/banda/**, web/src/app/(public)/casa/**, web/src/app/(public)/produtor/**, web/src/components/engagement/**, web/src/components/agenda/**, web/src/components/profiles/**, web/src/components/ui/BottomNav.tsx, web/src/components/ui/shell.ts, web/src/components/ui/__tests__/**, web/src/proxy.ts, web/src/proxy.test.ts, e2e/tests/seguir/**, docs/design/**]
adrs: [ADR-0001, ADR-0008]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0027 — Seguir cidade, banda e casa

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O fã logado segue a cidade (pela agenda), bandas, casas e produtores (pela página do perfil) e vê tudo o que segue na aba "Seguindo", onde também pode deixar de seguir. É a base dos avisos de show novo: o módulo de avisos vai perguntar a este módulo quem segue o quê. Cria o módulo `Engagement` previsto no ADR-0001.

## 2. Motivação & Escopo
**Motivação:** critério de aceite do épico "O fã segue cidade, banda ou casa, recebe o aviso de show novo e o lembrete na véspera" (jornada `seguir-e-avisar`); esta spec entrega o "segue", os avisos vêm nas specs de push.

**Objetivos (dentro do escopo):**
- Módulo `Engagement` (ADR-0001, schema `engagement`) com `Follow` (usuário, alvo: cidade por slug ou perfil por id, criado em); único por usuário e alvo.
- `PUT /api/v1/me/follows/cities/{slug}` e `PUT /api/v1/me/follows/profiles/{profileId}` (seguir, idempotente, 204); `DELETE` nas mesmas rotas (deixar de seguir, idempotente, 204); `GET /api/v1/me/follows` (o que o usuário segue, com nome e endereço).
- Contagem pública `GET /api/v1/profiles/{profileId}/followers` → `{ count }` e `GET /api/v1/cities/{slug}/followers` → `{ count }`.
- Interface pública `IEngagementApi.GetFollowersAsync(citySlug, profileIds)` (ids de usuários que seguem a cidade ou algum dos perfis) para a spec de avisos, que mora no próprio `Engagement` e entrega pelo `INotificationsApi.EnqueueAsync` (ADR-0014 e ADR-0001 esclarecidos em 2026-10-18: `Engagement → Notifications`, nunca o contrário).
- Botão "Seguir {cidade}" / "Seguindo" no cabeçalho da agenda e "Seguir" / "Seguindo" + contagem na página do perfil; anônimo que toca vai para `/entrar?returnTo=…`.
- Aba "Seguindo" (`/seguindo`) na navegação inferior: lista por tipo (cidades, bandas, casas, produtores) com "Deixar de seguir"; vazio: "Você ainda não segue ninguém. Siga sua cidade na agenda e as bandas e casas nos perfis."
- Seção "follows" no export (`IPersonalDataSection`) e remoção dos follows no `UserDeleted`.

**Não-objetivos (fora do escopo):**
- Preferências de aviso e inscrição de push (a parte "Avisos" da tela é da spec de push).
- Seguir outros fãs; lista pública de quem segue (só a contagem é pública).
- Sugestões do que seguir.

## 3. Dependências
- **Implementações necessárias:** SPEC-0025 (cabeçalho da agenda) e SPEC-0026 (página do perfil); por elas, perfis, cidades e contas.
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** módulos novos seguem o padrão de `Profiles`/`Notifications` (SPEC-0018/0021); regras do ADR-0001 com o esclarecimento de 2026-10-18 (sem ciclos; evento no contrato do consumidor).

**Decisão:** módulo `Engagement` com slices `FollowCity`, `FollowProfile`, `Unfollow*`, `ListMyFollows`, `CountFollowers`; valida o alvo por `IEventsCatalog.CityExistsAsync` e `IProfilesApi.GetAsync` (perfil aprovado); consome `UserDeleted` e implementa `IPersonalDataSection` do `Users`. Dependências: `Engagement → Users, Events, Profiles` (nenhum deles depende do `Engagement`); a spec de avisos fará `Notifications → Engagement` pelo `IEngagementApi`.

**Justificativa:** ADR-0001 (módulo `Engagement`: seguir e "vou"), ADR-0008 (o que o fã segue é dado pessoal: export e exclusão).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** guardar os follows no `Users` (mistura conta com engajamento e faria o `Notifications` depender do `Users` para tudo); lista pública de seguidores (exposição sem necessidade).

**ADRs:** ADR-0001, ADR-0008

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** seguir/deixar de seguir em ≤ 100 ms p95; `GetFollowersAsync` para uma cidade com 10.000 seguidores em ≤ 200 ms (IT-04).
- **Segurança:** seguir e listar exigem `fan`; `X-CSRF` no PUT/DELETE; rate limit `default`; alvo validado (cidade existente, perfil aprovado); contagem pública sem ids nem nomes.
- **Privacidade e dados pessoais:** o que o fã segue é pessoal (ADR-0008): não aparece para outros (só a contagem), entra no export e some com a conta (IT-05).
- **Disponibilidade e resiliência:** operações idempotentes; corrida de dois PUT iguais → um follow só (índice único) e 204 nos dois (IT-02).
- **Acessibilidade (UI):** botão com `aria-pressed` e nome completo ("Seguir Curitiba" / "Deixar de seguir Curitiba"); mudança anunciada; aba "Seguindo" com cabeçalhos por tipo; axe no E2E.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `/api/v1/me/follows*` · `/api/v1/profiles/{id}/followers` · `/api/v1/cities/{slug}/followers` · `IEngagementApi` · telas

```yaml
PUT    /api/v1/me/follows/cities/{slug}:          # fan + X-CSRF + default ; 204 (idempotente) ; 404 engagement.target_not_found
DELETE /api/v1/me/follows/cities/{slug}:          # fan + X-CSRF + default ; 204 (idempotente)
PUT    /api/v1/me/follows/profiles/{profileId}:   # fan + X-CSRF + default ; 204 ; 404 engagement.target_not_found (inexistente ou não aprovado)
DELETE /api/v1/me/follows/profiles/{profileId}:   # 204
GET    /api/v1/me/follows:                        # fan + default ; no-store
  200: { cities: [{ slug, name }], profiles: [{ id, type, name, slug }] }
GET /api/v1/profiles/{profileId}/followers:       # AllowAnonymous + default ; 200 { count } ; 404 engagement.target_not_found
GET /api/v1/cities/{slug}/followers:              # AllowAnonymous + default ; 200 { count } ; 404
# 401 auth.required ; 400 security.csrf_invalid ; 429 security.rate_limited
```

```csharp
// Subsolo.Modules.Engagement.IntegrationEvents (público)
public interface IEngagementApi {
    Task<IReadOnlyCollection<Guid>> GetFollowersAsync(string citySlug, IReadOnlyCollection<Guid> profileIds, CancellationToken ct);
}
```

```text
Banco (schema engagement): follows(user_id, target_kind city|profile, target_key, created_at_utc; PK(user_id, target_kind, target_key)); índice por (target_kind, target_key)
Agenda: botão "SEGUIR {CIDADE}" (contorno) / "SEGUINDO" (vermelho, check) no cabeçalho ; anônimo → /entrar?returnTo=/{cidade}
Perfil: botão "SEGUIR" / "SEGUINDO" + "{n} seguidores"
/seguindo (aba "Seguindo"): seções CIDADES, BANDAS, CASAS, PRODUTORES com "Deixar de seguir" ; vazio → texto do §2 ; anônimo → /entrar?returnTo=/seguindo
Navegação inferior: Agenda, Seguindo, Apoie, Conta
```

**Design:** Stitch, projeto 15194126668426659178: "Agenda Curitiba - Subsolo" (5d1946463ad9484b83c67cc2e6d78fd3, com o botão "SEGUIR CURITIBA" incluído em 2026-10-18), "Perfil da Banda - Podridão" (bdf4a66e751846d5a73dca2f606aac6a, botão "Seguir" e contagem) e "Avisos e Seguindo" (35b282364c47425c9e9cfe89e7b1c1e2, só a parte "Seguindo"). Ajustes: a parte "Avisos" da tela entra com a spec de push; a navegação mantém "Apoie" (decisão da SPEC-0011) e troca "Buscar" (fora do MVP) por "Seguindo"; contagem por extenso ("1.400 seguidores"), sem "1.4k".

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Seguir cidade / perfil | fã, alvo válido | 204; aparece em `/me/follows` e na aba; contagem +1 | UT-01, IT-01, E2E-01 |
| Repetir ou corrida | dois PUT iguais (seguidos ou simultâneos) | 204 nos dois; um follow só | IT-02 |
| Alvo inválido | cidade inexistente; perfil pendente ou inexistente | 404 `engagement.target_not_found` | UT-01, IT-01 |
| Deixar de seguir | DELETE (seguindo ou não) | 204; some da lista; contagem −1 | IT-01, E2E-01 |
| Anônimo | toca "Seguir" | vai para `/entrar?returnTo=…`; API 401 | IT-03, E2E-02 |
| Sem anti-forgery | PUT/DELETE sem `X-CSRF` | 400 e nada muda | IT-03 |
| Contagem pública | qualquer visitante | só o número, sem ids | IT-01 |
| Quem segue (avisos) | `GetFollowersAsync` cidade + perfis | ids únicos de quem segue algum dos alvos, ≤ 200 ms com 10.000 | UT-02, IT-04 |
| Privacidade | export; `UserDeleted` | seção "follows"; follows apagados | IT-05 |
| Arquitetura | módulo novo | ADR-0001 vale; sem ciclos | IT-06 |
| Contrato | OpenAPI e `IEngagementApi` | corpos, códigos e assinatura do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — módulo e telas novos.

### 7.2 Testes Unitários
- **UT-01** — Dados catálogo e perfis de teste, quando um fã segue a cidade `curitiba` e o perfil aprovado X, então os follows são criados; seguir de novo não duplica; cidade inexistente ou perfil pendente dá `engagement.target_not_found`.
- **UT-02** — Dados follows de três usuários (um segue a cidade, outro o perfil A, outro os dois), quando `GetFollowersAsync("curitiba", [A, B])` é chamado, então devolve os três ids sem repetição.

### 7.3 Testes de Integração
- **IT-01** — Dados a API e o PostgreSQL com uma cidade e um perfil aprovado, quando o fã segue, lista, conta e deixa de seguir, então 204, a lista e as contagens refletem cada passo, e alvos inválidos dão 404.
- **IT-02** — Dados quatro PUT iguais em paralelo do mesmo fã, quando processados, então todos 204 e um follow só no banco.
- **IT-03** — Dados anônimo e fã sem `X-CSRF`, quando chamam PUT/DELETE e `/me/follows`, então 401 e 400, sem mudança.
- **IT-04** — Dados 10.000 seguidores da cidade e 500 de um perfil, quando `GetFollowersAsync` é chamado, então devolve os ids únicos em ≤ 200 ms p95.
- **IT-05** — Dado um fã com follows, quando `GET /me/export` é chamado, então a seção "follows" traz cidades e perfis; quando o `UserDeleted` dele é processado, os follows somem e as contagens caem.
- **IT-06** — Dados os testes de arquitetura, quando rodam com o `Engagement`, então as regras do ADR-0001 valem, `IEngagementApi` está em `IntegrationEvents` e não há ciclos (guarda para os módulos existentes).

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json` e os tipos públicos, quando lidos, então os endpoints e o `IEngagementApi` têm os corpos, códigos e assinaturas do §6, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa com uma banda com perfil, nos projetos mobile-chrome e mobile-safari: o fã logado toca em "Seguir Curitiba" na agenda (vira "Seguindo"), segue a banda na página do perfil (contagem +1), abre a aba "Seguindo" e vê as duas; deixa de seguir a banda e ela some. Sem violações do axe. [jornada: seguir-e-avisar]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: anônimo tocando em "Seguir Curitiba" vai para `/entrar?returnTo=/curitiba`; abrindo `/seguindo` vai para `/entrar?returnTo=/seguindo`. [jornada: seguir-e-avisar]

### 7.6 Outros
- **Itens herdados da revisão da SPEC-0026 (G4):**
  - H26-03 — `profile-metadata.ts` `ogImageOf`: montar a URL do og a partir do id do flyer (não por regex sobre `flyerThumbUrl`) e escolher como "próximo show" o primeiro não cancelado e ainda não iniciado.
  - H26-04 — sonda de um dia da agenda (H25-01): usar a data de hoje no fuso da cidade, não em UTC.
  - H26-05 — IT-05 da SPEC-0026: um teste único que passe por `loadProfile`/`ProfileRoute` com a lista de shows indisponível (200 com o aviso).
  - H26-06 — testes 503 do proxy para perfil: verificar que `/api/v1/profiles/...` foi chamado.
  - H26-08 — `reservedSegments` do proxy: teste para `/apoie` e um teste que compare com a lista de slugs reservados do backend (SPEC-0017).
- Acessibilidade: `aria-pressed` e anúncio da mudança (E2E-01).

**Dublês e dados de teste:** catálogo e perfis de teste no UT; perfis aprovados pela API (admin) e cidades semeadas nos IT e no E2E.

**Ambiente de execução:** IT com Testcontainers; E2E contra a pilha do Compose; Playwright com um worker.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** schema novo `engagement` (aditivo).
- **Compatibilidade:** endpoints e aba novos; a navegação troca "Buscar" (que não existia) por "Seguindo".
- **Observabilidade:** contadores `subsolo.engagement.follows` e `unfollows` (por tipo de alvo); logs com ids.
- **Rollback:** reverter o merge commit; o schema pode ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] A navegação inferior perde "Apoie"? — Não: fica Agenda, Seguindo, Apoie, Conta ("Buscar" fica fora do MVP) (recomendação do Architect; piloto automático do Thomas, 2026-10-18)
- [x] Lista pública de seguidores? — Não; só a contagem é pública (ADR-0008) (recomendação do Architect; piloto automático do Thomas, 2026-10-18)
- [x] Botão "Seguir cidade" no Stitch? — Incluído na tela da agenda em 2026-10-18, com o Thomas acordado

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"), com a tela da agenda atualizada no Stitch com ele acordado.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Domínio**
- [ ] Red: UT-01, UT-02 com a tag `SPEC-0027:<ID>`, falhando pelo motivo certo
- [ ] Green e Refactor

**Fase 2: Módulo, API e contrato**
- [ ] Red: IT-01..IT-06, CT-01 falhando pelo motivo certo
- [ ] Green: módulo `Engagement`, schema, endpoints, consumidor do `UserDeleted`, seção do export, OpenAPI e cliente TS
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Telas e jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [ ] Green: botões na agenda e no perfil, aba Seguindo, navegação

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — eb0bfe7 | 2026-10-18 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0027`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
