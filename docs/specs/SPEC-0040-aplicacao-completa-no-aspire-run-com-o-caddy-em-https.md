---
id: SPEC-0040
title: Aplicação completa no aspire run, com o Caddy em HTTPS
tier: lite
type: fix
user_facing: false
status: implemented
created: 2026-10-25
parent:
depends_on: []
consumes_contract: []
touches: [src/Subsolo.AppHost/**, tests/Common/Subsolo.AppHost.IntegrationTests/**, README.md]
adrs: [ADR-0004, ADR-0003]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-25
---

# SPEC-0040 — Aplicação completa no aspire run, com o Caddy em HTTPS

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
No `aspire run` (ou `dotnet run --project src/Subsolo.AppHost`, que é a mesma coisa) só a parte pública funciona. Entrar, sair, seguir, "vou", avisos, cadastro de show, denúncia e admin quebram: o navegador chama `/api/*` no endereço do `next dev`, que responde 404. Hoje o teste completo exige o `./tools/compose-up.sh`, que roda tudo em contêiner e não deixa depurar a API nem o web pela IDE.

Esperado (pedido do Thomas, 2026-10-25): `aspire run` ou `dotnet run` no AppHost sobe a aplicação inteira, com login, em `https://localhost:8443`, dá para pôr breakpoint na API, e nada muda no que vai para produção.

## 2. Causa Raiz
- `src/Subsolo.AppHost/Program.cs`: o Caddy, que manda `/api/*` e `/health` para a API e o resto para o web (`infra/caddy/Caddyfile`), só é criado dentro de `if (builder.ExecutionContext.IsPublishMode)`. No modo run o navegador fala direto com o `next dev`.
- `web/next.config.ts` só redireciona `/media/*`; não há rota `/api/*` no web (de propósito: o navegador nunca fala com a API por outro caminho, ADR-0004).
- O cookie de sessão é `__Host-subsolo` (`Secure`, ADR-0003) e o link mágico e o `SUBSOLO_PUBLIC_URL` apontam para o endpoint HTTP do web (SPEC-0016 Emenda 4, SPEC-0024 Emenda v2).

## 3. Mudança Proposta
Só no modo run (`!IsPublishMode`):
- O AppHost sobe o mesmo Caddy (imagem `caddy:2.11.7-alpine`, o mesmo `infra/caddy/Caddyfile` montado só leitura) em `https://localhost:8443` (porta fixa, sem proxy do Aspire, como o Google falso), com `API_UPSTREAM` e `WEB_UPSTREAM` apontando para a API e o web que rodam na máquina (túnel de contêiner do Aspire 13.6). O HTTP do Caddy (`HTTP_PORT`) fica em 8080, redirecionando para o HTTPS, como no Compose. Certificado da CA interna do Caddy (`tls internal`, `skip_install_trust`): o navegador mostra o aviso uma vez, como hoje no `compose-up.sh`.
- `App__PublicUrl` da API e `SUBSOLO_PUBLIC_URL` do web passam a ser `https://localhost:8443` (o link mágico, o retorno do Google e o Open Graph saem pelo Caddy).
- `/media/*` no run sai do Caddy, como em produção: `artifacts/media` (o `Media__Root` do run, criado pelo AppHost) montado só leitura em `/srv/media`. A API continua servindo `/media/flyers/*` na porta dela e o rewrite do `next.config.ts` não muda. (Emenda 1)
- O Caddy do run guarda a CA interna no volume `subsolo-caddy-data` (`/data`): o aviso de certificado aparece uma vez, não a cada subida. (Emenda 1)
- O README troca o aviso "pelo `aspire run` o login não funciona" pelo caminho novo, com como depurar (rodar o AppHost pela IDE com o depurador; a API e o web são processos da máquina).

**Não muda:** nada do modo de publicação. O Compose gerado por `aspire publish` sai idêntico (os testes de publicação existentes continuam passando sem alteração), o Caddyfile, o `compose-up.sh`, o E2E (que usa a pilha do Compose) e o código da API e do web. `aspire run` e `compose-up.sh` continuam não rodando ao mesmo tempo (já disputavam a porta 8090 do Google falso; agora também a 8443).

**Padrão seguido:** o Caddy do modo de publicação em `src/Subsolo.AppHost/Program.cs` e o Google falso em porta fixa (`isProxied: false`); testes em `tests/Common/Subsolo.AppHost.IntegrationTests/PublicUrlReferenceTests.cs` (modelo montado sem subir recursos) e `AppHostTests.cs` (aplicação no ar).

**Rollback:** reverter o merge commit.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — o modo de publicação já é fixado pelos testes de publicação do Compose (`ComposePublishTests`, `MediaComposePublishTests`, `NotificationsComposePublishTests`), que rodam sem mudança.
- **UT-01** — Dado o modelo do AppHost em modo run (montado sem subir os recursos), quando se leem os recursos, então existe o `caddy` com o Caddyfile do repositório montado só leitura, o endpoint HTTPS fixo em 8443 sem proxy, `API_UPSTREAM` e `WEB_UPSTREAM` referenciando os endpoints `http` da `api` e do `web`; e `App__PublicUrl` da `api` e `SUBSOLO_PUBLIC_URL` do `web` valem `https://localhost:8443`. (Substitui a asserção do teste do endereço público da SPEC-0016, Emenda 4, que apontava para o endpoint do web no run.)
- **IT-01** — Dado o AppHost no ar em modo run (`DistributedApplicationTestingBuilder`, banco descartável), quando se pede um link mágico por `https://localhost:8443/api/v1/auth/magic-link` (com o CSRF), se abre o link que chegou ao Mailpit e se chama `GET https://localhost:8443/api/v1/me` com o cookie recebido, então a resposta é 200 com a conta; e `GET https://localhost:8443/` devolve a página do web. Hoje falha: não há nada em 8443.
- E2E: N/A — sem jornada nova; o E2E continua na pilha do Compose.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

Pedido do Thomas em 2026-10-25: "Eu preciso que local eu consigo rodar apenas com o aspire run ou com o dotnet run [...] Que não impacte quando for pra produção".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
- [x] Red: UT-01 e IT-01 com a tag `SPEC-0040:<ID>`, falhando pelo motivo certo
- [x] Green: Caddy no modo run em https://localhost:8443 e endereço público apontando para ele; README atualizado
- [x] Validar: build + suíte + arquitetura (G2/G3)
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — e26ae3a | 2026-10-25 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — f8cf142 | 2026-10-25 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 3329706 | 2026-10-25 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 10s 605ms) — 3329706 | 2026-10-25 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (H40-01..05 corrigidos em d8f2b4e e 4c83631) — 4c83631 | 2026-10-25 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 18m 15s 509ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 11s 843ms) — 0f40b2d | 2026-10-25 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-25 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live — SPEC-0003 (deploy guiado pelo Thomas) | 2026-10-25 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 3d841c5 (árvore suja) | 2026-10-25 |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
- `aspire run` ou `dotnet run --project src/Subsolo.AppHost` sobe a aplicação inteira em https://localhost:8443, com o mesmo Caddy e o mesmo Caddyfile de produção: login, CSRF, limite de requisições e flyers funcionam como na pilha do Compose, e a API e o web rodam como processos da máquina (depuráveis pela IDE).
- O link mágico, o retorno do Google e o Open Graph apontam para https://localhost:8443 no run.
- O modo de publicação não muda: o `aspire publish` gera o mesmo Compose (comparado com `diff -r` antes e depois; os testes de publicação passam sem alteração).

### Como foi feito
- TDD com papéis separados: Test-writer (7f99980; testes em série e `/media` em 3329706; itens da revisão H40-01..04 em d8f2b4e) → Implementer (811b13e; README H40-05 em 4c83631) → Reviewer: APPROVED WITH MINOR ITEMS.
- `AddCaddy(configure)` concentra o comum aos dois modos (imagem, Caddyfile, upstreams, `WaitFor`); cada modo passa as portas e a mídia. No run as portas 8443/8080 são fixas e sem proxy, como o Google falso.
- Pelo túnel de contêiner do Aspire 13.6 o Caddy chega à API pelo loopback, que o `ForwardedHeaders` já aceita: a API não mudou.
- Emenda 1: `/media/*` no run sai do Caddy (`artifacts/media` só leitura), volume `subsolo-caddy-data` para a CA interna e os testes que sobem o AppHost numa coleção sem paralelismo.

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
- Antes: no G1 o UT-01 falhou (sem recurso `caddy`) e o IT-01 não teve resposta em https://localhost:8443 (Connection refused). Depois: passam em 3329706 (G2) e na integração (d3a71c0, G5).

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | no modelo do AppHost em modo run há o `caddy` com o Caddyfile só leitura, https 8443 e http 8080 sem proxy, upstreams da `api` e do `web`, `artifacts/media` só leitura em `/srv/media` e o volume `subsolo-caddy-data`; `App__PublicUrl` e `SUBSOLO_PUBLIC_URL` = `https://localhost:8443` | PASS | `gate G2 --run` em 3329706; `gate G5 --run` na integração (d3a71c0); CI do PR #51 (run 40220968768, checks verdes em 3d841c5) |
| IT-01 | AppHost no ar: CSRF, link mágico pelo 8443 com o link no Mailpit começando em `https://localhost:8443`, consumo do link e `GET /api/v1/me` 200; `/` devolve o web; `/media/flyers/<inexistente>` dá 404 do Caddy | PASS | `gate G2 --run` em 3329706; `gate G5 --run` na integração (d3a71c0); CI do PR #51 (run 40220968768, checks verdes em 3d841c5) |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5) — CI verde no PR #51 (run 40220968768)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — G6 N/A até a SPEC-0003
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7) — CHANGELOG e README atualizados
- [x] Outras ocorrências registradas como novas specs (ou nenhuma) — nenhuma

### Deploy
Sem ambiente implantado (G6 N/A → SPEC-0003, guiado pelo Thomas); mudança só do ambiente local. Entrega na `main` pelo PR #51 (https://github.com/thomasravache/subsolo/pull/51), merge commit 9c527d9 em 2026-10-25, com H2 pela política auto-on-green.

### Pendências
- Nenhuma (H40-01..05 resolvidos nesta spec).
## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (sem mudança de contrato) | 2026-10-25 | `/media/*` no run sai do Caddy (`artifacts/media` só leitura em `/srv/media`); volume `subsolo-caddy-data` para a CA interna; os testes do AppHost que sobem a aplicação (portas fixas 8090/8443/8080 e `next dev` único por pasta) rodam sem paralelismo entre si | Implementer: pelo Caddyfile o `/media/*` que passa pelo Caddy sempre vai para `/srv/media` (sem a montagem, flyers 404 em 8443); TEST_DEFECT: `RunModeCaddyTests` e `AppHostTests` subiam o AppHost ao mesmo tempo | SPEC-0040 | thomas (piloto automático, 2026-10-23) |
