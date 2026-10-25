---
id: SPEC-0040
title: Aplicação completa no aspire run, com o Caddy em HTTPS
tier: lite
type: fix
user_facing: false
status: in-progress
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
- `/media/*` no run continua saindo da API (o Caddy não tem o volume no run; o rewrite do `next.config.ts` segue valendo).
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
- [ ] Red: UT-01 e IT-01 com a tag `SPEC-0040:<ID>`, falhando pelo motivo certo
- [ ] Green: Caddy no modo run em https://localhost:8443 e endereço público apontando para ele; README atualizado
- [ ] Validar: build + suíte + arquitetura (G2/G3)
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — e26ae3a | 2026-10-25 |
| G1 Red | FAIL | verify G1: FAIL — testes do plano sem tag no código versionado: SPEC-0040:IT-10 — 7f99980 | 2026-10-25 |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
