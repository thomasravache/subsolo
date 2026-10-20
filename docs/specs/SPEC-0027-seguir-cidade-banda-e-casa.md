---
id: SPEC-0027
title: Seguir cidade, banda e casa
tier: full
type: feature
user_facing: true
status: implemented
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0025, SPEC-0026]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Engagement/**, tests/Modules/Engagement/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/Subsolo.Api.csproj, src/Subsolo.Api/packages.lock.json, Subsolo.slnx, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, docs/api/**, web/src/api/**, web/src/app/(public)/seguindo/**, web/src/app/(public)/[cidade]/**, web/src/app/(public)/banda/**, web/src/app/(public)/casa/**, web/src/app/(public)/produtor/**, web/src/components/engagement/**, web/src/components/agenda/**, web/src/components/profiles/**, web/src/components/ui/BottomNav.tsx, web/src/components/ui/shell.ts, web/src/components/ui/__tests__/**, web/src/proxy.ts, web/src/proxy.test.ts, e2e/tests/seguir/**, docs/design/**, e2e/tests/perfis/perfil-publico.spec.ts]
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

**Decisão:** módulo `Engagement` com slices `FollowCity`, `FollowProfile`, `Unfollow*`, `ListMyFollows`, `CountFollowers`; valida o alvo por `IEventsCatalog.CityExistsAsync` e `IProfilesApi.GetAsync` (perfil aprovado); consome `UserDeleted` e implementa `IPersonalDataSection` do `Users`. Dependências: `Engagement → Users, Events, Profiles` (nenhum deles depende do `Engagement`); a spec de avisos mora no próprio `Engagement` e entrega pelo `INotificationsApi` (`Engagement → Notifications`, nunca o contrário; Emenda de 2026-10-19).

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
  - H26-03 — `profile-metadata.ts` `ogImageOf`: escolher como "próximo show" o primeiro não cancelado e ainda não iniciado. (A montagem da URL pelo id do flyer foi para a SPEC-0031, que pode mudar o contrato do `Events`; Emenda de 2026-10-19.)
  - H26-04 — sonda de um dia da agenda (H25-01): o `from` da sonda é a data de hoje no fuso da cidade, não em UTC, e a sonda cobre no máximo um dia; o modo de obter o fuso (consulta ou janela a partir da véspera em UTC) fica a cargo da implementação (Emenda de 2026-10-19).
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
- [x] Red: UT-01, UT-02 com a tag `SPEC-0027:<ID>`, falhando pelo motivo certo
- [x] Green e Refactor

**Fase 2: Módulo, API e contrato**
- [x] Red: IT-01..IT-06, CT-01 falhando pelo motivo certo
- [x] Green: módulo `Engagement`, schema, endpoints, consumidor do `UserDeleted`, seção do export, OpenAPI e cliente TS
- [x] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Telas e jornada E2E**
- [x] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [x] Green: botões na agenda e no perfil, aba Seguindo, navegação

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — eb0bfe7 | 2026-10-18 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — ba68720 | 2026-10-19 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 4b83b52 | 2026-10-19 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 7s 357ms) — 4b83b52 | 2026-10-19 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (0 bloqueadores/majors; desvios 1–11 aceitos; itens menores H27-01..09 levados à SPEC-0029) — 4b83b52 | 2026-10-19 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 13m 13s 692ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 7s 919ms) — 42e8def | 2026-10-19 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-19 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live — SPEC-0003 (deploy guiado pelo Thomas) | 2026-10-19 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — c39e4b5 (árvore suja) | 2026-10-19 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
- Seguir cidade, banda, casa e produtor: botão "Seguir {cidade}" na agenda e "Seguir" com contagem de seguidores por extenso no perfil, como no Stitch; anônimo vai para o login e volta.
- Aba "Seguindo" (`/seguindo`) na navegação inferior (Agenda, Seguindo, Apoie, Conta), com as listas por tipo e "Deixar de seguir".
- API `PUT`/`DELETE /api/v1/me/follows/...`, `GET /api/v1/me/follows`, contagens públicas de seguidores (sem lista) e `IEngagementApi.GetFollowersAsync` para os avisos; follows na exportação de dados e removidos ao apagar a conta.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: Test-writer (28eaf65 scaffold; 0b65b30, 184a4c3, 2ab8aba, ea0b5fb, ba68720, 4b83b52) → Implementer (ca170eb, 6c480c8, a5e94cb, 30bc915, 8490af9) → Reviewer: APPROVED WITH MINOR ITEMS em 4b83b52.
- Módulo novo `Engagement` (schema `engagement`, outbox/inbox), no padrão do `Profiles`: depende só dos `IntegrationEvents` de Users, Events e Profiles; consome `UserDeleted` e implementa `IPersonalDataSection`; corrida de follows resolvida pela chave primária (violação única = sucesso). Contadores `subsolo.engagement.follows` e `unfollows`.
- Web: estado do botão lido no navegador por `GET /me/follows` (páginas iguais para todos); contagem no servidor, degradando se falhar; proxy trata `/seguindo` como `/conta`.
- Emenda sem mudança de contrato: E2E do perfil da SPEC-0026 no escopo; URL do og pelo id do flyer para a SPEC-0031; sonda da agenda esclarecida; direção `Engagement → Notifications` corrigida no §4.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | seguir cidade e perfil aprovado; repetir não duplica; cidade inexistente, perfil pendente ou inexistente → `engagement.target_not_found` sem gravar | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| UT-02 | `GetFollowersAsync` devolve os ids sem repetição | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| IT-01 | seguir, listar, contar e deixar de seguir com 204 e contagens certas; contagem pública sem ids nem e-mails; alvo inválido → 404 | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| IT-02 | quatro PUT iguais em paralelo (5 rodadas): todos 204 e um follow só | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| IT-03 | anônimo → 401 `auth.required`; sem `X-CSRF` ou inválido → 400 `security.csrf_invalid`, sem mudança | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| IT-04 | 10.000 seguidores da cidade e 500 do perfil: ids únicos em p95 ≤ 200 ms | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| IT-05 | exportação com a seção `follows`; `UserDeleted` remove os follows e baixa as contagens | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| IT-06 | regras do ADR-0001 com o `Engagement`, `IEngagementApi` em `IntegrationEvents`, sem ciclos | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| CT-01 | OpenAPI e `IEngagementApi` como no §6; `pnpm api:check` passa | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| E2E-01 | seguir cidade e banda, aba "Seguindo", deixar de seguir (mobile-chrome e mobile-safari); axe | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| E2E-02 | anônimo em "Seguir Curitiba" e em `/seguindo` vai para `/entrar?returnTo=…` | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |
| H26-03..06, H26-08 | próximo show não cancelado e não iniciado no og; sonda da agenda de no máximo um dia; teste único do perfil com shows indisponíveis; testes 503 conferem a chamada à API de perfis; `/apoie` e lista completa de slugs reservados no proxy | PASS | `gate G2 --run` em 4b83b52; `gate G5 --run` na integração (42e8def); CI do PR #39 (run 39347130673, 10/10 checks em c39e4b5) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — CI verde no PR #39 (run 39347130673)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — G6 N/A até a SPEC-0003
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout
- [x] Documentação raiz e CHANGELOG atualizados (G7) — CHANGELOG atualizado
- [x] Pendências registradas como novas specs (ou nenhuma) — ver Pendências

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente implantado (G6 N/A → SPEC-0003, guiado pelo Thomas). Entrega na `main` pelo PR #39 (https://github.com/thomasravache/subsolo/pull/39), merge commit 1473b33 em 2026-10-19, com H2 pela política auto-on-green.

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- Menores da revisão G4 → SPEC-0029 (§7.6, H27-01..09: dependências e foco do botão de seguir, nome acessível constante, "Seguindo" no cabeçalho do desktop, busca de perfis em lote, teste da sonda ao meio-dia, captura do Stitch da agenda, follows de perfil não aprovado).
## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0027`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (sem mudança de contrato) | 2026-10-19 | `e2e/tests/perfis/perfil-publico.spec.ts` no escopo (o E2E-01 da SPEC-0026 afirma que não há botão "Seguir" no perfil); H26-03 fica só com a escolha do próximo show, e a URL do og pelo id do flyer vai para a SPEC-0031; H26-04 esclarecido (`from` na data da cidade, no máximo um dia); §4 corrigido: avisos moram no `Engagement`, que depende do `Notifications`, nunca o contrário (como no §2 e no ADR-0001). | SPEC_DEFECTs do Test-writer no G1 | SPEC-0031 | piloto automático do Thomas (2026-10-17) |
