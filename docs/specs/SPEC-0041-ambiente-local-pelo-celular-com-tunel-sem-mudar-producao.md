---
id: SPEC-0041
title: Ambiente local pelo celular com túnel, sem mudar produção
tier: lite
type: fix
user_facing: false
status: implemented
created: 2026-10-25
parent:
depends_on: [SPEC-0040]
consumes_contract: []
touches: [src/Subsolo.AppHost/**, tests/Common/Subsolo.AppHost.IntegrationTests/**, web/next.config.ts, web/src/next-config.test.ts, tools/tunnel.sh, tests/Subsolo.Tooling.Tests/**, README.md]
adrs: [ADR-0004, ADR-0008]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-25
---

# SPEC-0041 — Ambiente local pelo celular com túnel, sem mudar produção

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
O Thomas quer verificar o ambiente local pelo celular (pedido de 2026-10-25: "solucione pra eu conseguir verificar pelo telefone de maneira que não afete o produto"). Com um túnel do Cloudflare (`cloudflared tunnel --url https://localhost:8443`) a página abre, mas:
- os botões não funcionam: o `next dev` responde 403 aos scripts de desenvolvimento (`/_next/static/...`) quando a origem do navegador não é `localhost` (proteção de origens do modo dev);
- o link mágico do e-mail aponta para `https://localhost:8443`, que no celular é o próprio celular;
- hoje só funciona com um proxy improvisado que reescreve `Origin`/`Referer` e trocando o endereço do link à mão.

## 2. Causa Raiz
- `web/next.config.ts` não declara `allowedDevOrigins`: o `next dev` só aceita `localhost` para os recursos de desenvolvimento.
- `src/Subsolo.AppHost/Program.cs` fixa `App__PublicUrl` e `SUBSOLO_PUBLIC_URL` em `https://localhost:8443` no modo run (SPEC-0040), sem jeito de informar outro endereço público.
- Não existe um comando do repositório que abra o túnel com os parâmetros certos (cabeçalho `Host` e nome TLS de `localhost`, para o Caddy casar o site e o certificado).

## 3. Mudança Proposta
Tudo só no ambiente local (modo run do AppHost e `next dev`):
- **Opção do AppHost `Subsolo:PublicUrl`** (só no modo run; padrão `https://localhost:8443`): quando informada (`dotnet run --project src/Subsolo.AppHost -- --Subsolo:PublicUrl=https://xxx.trycloudflare.com`, ou `aspire run -- ...`), vira o `App__PublicUrl` da API e o `SUBSOLO_PUBLIC_URL` do web, e o web recebe `SUBSOLO_DEV_ORIGINS` com o host dela. Valor que não seja URL absoluta `https` sem caminho → o AppHost para na montagem com mensagem clara. No modo de publicação a opção é ignorada.
- **`web/next.config.ts`**: `allowedDevOrigins` lido de `SUBSOLO_DEV_ORIGINS` (hosts separados por vírgula; vazio ou ausente → não declara). Só o `next dev` usa essa opção; o build e o `next start` de produção não. A variável é só do servidor (sem `NEXT_PUBLIC_`).
- **`tools/tunnel.sh`**: abre o túnel rápido do Cloudflare (`cloudflared tunnel --url https://localhost:8443 --no-tls-verify --http-host-header localhost:8443 --origin-server-name localhost`), espera o endereço `https://*.trycloudflare.com`, mostra o endereço e o comando do AppHost com `--Subsolo:PublicUrl`; com `--run`, sobe também o AppHost com esse endereço e encerra os dois juntos (Ctrl+C). Sem `cloudflared` instalado → mensagem com o link de instalação e saída 1. Sem custo e sem conta (túnel rápido).
- **README**: seção "Pelo celular" com os dois jeitos (script com `--run`, ou túnel + AppHost pela IDE para depurar) e os limites abaixo.

**Fora do escopo (limites documentados):** o "Continuar com Google" pelo celular (o Google falso escuta só em `127.0.0.1:8090` no computador); o limite de requisições conta todos os acessos do túnel como um só IP; o Mailpit e o dashboard do Aspire não passam pelo túnel.

**Não muda:** o modo de publicação (o Compose gerado sai idêntico; testes de publicação sem alteração), o Caddyfile, a API, o `compose-up.sh` e o E2E. Sem a opção, o `aspire run` se comporta como na SPEC-0040.

**Padrão seguido:** opções do AppHost lidas de `builder.Configuration` (`Subsolo:EphemeralDatabase`, `Subsolo:FakeGooglePort`); variáveis só do servidor do web (`SUBSOLO_PUBLIC_URL`, `API_URL` no `next.config.ts`); scripts em `tools/*.sh` com testes em `tests/Subsolo.Tooling.Tests`; testes do modelo do AppHost em `RunModeCaddyTests`.

**Rollback:** reverter o merge commit.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — o comportamento sem a opção já é fixado pelos testes da SPEC-0040 (seguem sem mudança).
- **UT-01** — Dado o modelo do AppHost em modo run com `--Subsolo:PublicUrl=https://celular.example`, quando se leem as variáveis, então `App__PublicUrl` da `api` e `SUBSOLO_PUBLIC_URL` do `web` valem `https://celular.example` e o `web` recebe `SUBSOLO_DEV_ORIGINS=celular.example`; sem a opção, valem `https://localhost:8443` e não há `SUBSOLO_DEV_ORIGINS`; com `http://celular.example` ou `https://celular.example/caminho`, a montagem falha com mensagem que cita `Subsolo:PublicUrl`.
- **UT-02** — Dado `SUBSOLO_DEV_ORIGINS="a.example,b.example"`, quando o `next.config.ts` é carregado, então `allowedDevOrigins` é `["a.example","b.example"]`; sem a variável ou vazia, `allowedDevOrigins` não é declarado.
- **UT-03** — Dado um `cloudflared` falso no `PATH` que imprime a linha do endereço `https://teste-123.trycloudflare.com`, quando se roda `tools/tunnel.sh`, então ele chama o `cloudflared` com `--url https://localhost:8443 --http-host-header localhost:8443 --origin-server-name localhost` e mostra o endereço e o comando com `--Subsolo:PublicUrl=https://teste-123.trycloudflare.com`; sem `cloudflared` no `PATH`, sai 1 com a mensagem de instalação.
- **IT-01** — Dado o AppHost no ar em modo run com `--Subsolo:PublicUrl=https://celular.example`, quando se pede um script de desenvolvimento do web por `https://localhost:8443/_next/static/...` com `Origin: https://celular.example`, então a resposta é 200 (hoje 403); e o link mágico que chega ao Mailpit começa com `https://celular.example/entrar/confirmar`.
- E2E: N/A — sem jornada nova; o túnel real depende de internet e fica fora do CI.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
- [x] Red: UT-01, UT-02, UT-03 e IT-01 com a tag `SPEC-0041:<ID>`, falhando pelo motivo certo
- [x] Green: opção `Subsolo:PublicUrl` no AppHost, `allowedDevOrigins` no `next.config.ts`, `tools/tunnel.sh` e README
- [x] Validar: build + suíte + arquitetura (G2/G3)
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 70cd98a | 2026-10-25 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 76c08e9 | 2026-10-25 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — b8c59b7 | 2026-10-25 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 10s 103ms) — b8c59b7 | 2026-10-25 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (H41-01..07 resolvidos em 5d5abdd e c9274a8, após CHANGES REQUESTED) — c9274a8 | 2026-10-25 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 18m 44s 632ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 11s 768ms) — a8a1089 | 2026-10-25 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-26 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live — SPEC-0003 (deploy guiado pelo Thomas) | 2026-10-26 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 9c972fa (árvore suja) | 2026-10-26 |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
- `./tools/tunnel.sh --run` abre um túnel rápido do Cloudflare (sem conta e sem custo), mostra o endereço `https://….trycloudflare.com` e sobe o AppHost com ele: pelo celular a página funciona (scripts do `next dev` liberados para o host do túnel) e o link mágico chega com o endereço do túnel. Sem `--run`, só o túnel, para rodar o AppHost pela IDE e depurar.
- Opção `Subsolo:PublicUrl` do AppHost, só no modo run e validada (https, sem caminho).
- Produção não muda: a opção é ignorada no publish, `allowedDevOrigins` só é lido pelo servidor de desenvolvimento do Next e nenhuma variável chega ao navegador.

### Como foi feito
- TDD com papéis separados: Test-writer (76c08e9; regressões da revisão em 5d5abdd) → Implementer (b8c59b7; correções da revisão em c9274a8) → Reviewer: CHANGES REQUESTED (H41-01, túnel órfão num segundo Ctrl+C) e depois APPROVED.
- `tools/tunnel.sh` no estilo do `compose-up.sh`: cada filho num grupo de processos próprio; no encerramento ignora novos sinais, para o `cloudflared` primeiro (fecha a exposição na hora) e depois o AppHost; trata INT, TERM e HUP.
- O `cloudflared` reescreve `Host` e o nome TLS para `localhost:8443`, então o Caddy e o certificado locais não mudam.

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
- Antes: no G1 a IT-01 recebeu 403 do `next dev` no script pedido com a origem do túnel, o link saiu `https://localhost:8443`, o `tools/tunnel.sh` não existia e o UT-02 viu `allowedDevOrigins` indefinido; na revisão, o UT-03 com `--run` mostrou o `cloudflared` vivo 5 s após dois sinais. Depois: passam em c9274a8 e na integração (bfa1dde, G5).

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | com `--Subsolo:PublicUrl=https://celular.example` a `api` e o `web` recebem o endereço e o `web` recebe `SUBSOLO_DEV_ORIGINS=celular.example`; sem a opção, `https://localhost:8443` e sem `SUBSOLO_DEV_ORIGINS`; `http://…` ou com caminho → `InvalidOperationException` citando `Subsolo:PublicUrl` | PASS | `gate G2 --run` em b8c59b7; `gate G5 --run` na integração (bfa1dde); CI do PR #52 (run 40263800093, checks verdes em 9c972fa) |
| UT-02 | `SUBSOLO_DEV_ORIGINS="a.example,b.example"` vira `allowedDevOrigins` `["a.example","b.example"]`; ausente ou vazia, a chave não é declarada | PASS | `gate G2 --run` em b8c59b7; `gate G5 --run` na integração (bfa1dde); CI do PR #52 (run 40263800093, checks verdes em 9c972fa) |
| UT-03 | `tools/tunnel.sh` chama o `cloudflared` com `--url https://localhost:8443 --http-host-header localhost:8443 --origin-server-name localhost`, mostra o endereço e o comando com `--Subsolo:PublicUrl`; sem `cloudflared` sai 1 com o link de instalação; SIGTERM, SIGHUP e dois sinais seguidos com `--run` encerram o túnel em até 5 s | PASS | `gate G2 --run` em b8c59b7; `gate G5 --run` na integração (bfa1dde); CI do PR #52 (run 40263800093, checks verdes em 9c972fa) |
| IT-01 | AppHost no ar com `--Subsolo:PublicUrl=https://celular.example`: o script de desenvolvimento pedido com `Origin: https://celular.example` responde 200 (antes 403) e o link mágico chega ao Mailpit com `https://celular.example/entrar/confirmar` | PASS | `gate G2 --run` em b8c59b7; `gate G5 --run` na integração (bfa1dde); CI do PR #52 (run 40263800093, checks verdes em 9c972fa) |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5) — CI verde no PR #52 (run 40263800093)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — G6 N/A até a SPEC-0003
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7) — CHANGELOG e README atualizados
- [x] Outras ocorrências registradas como novas specs (ou nenhuma) — nenhuma

### Deploy
Sem ambiente implantado (G6 N/A → SPEC-0003, guiado pelo Thomas); mudança só do ambiente local. Entrega na `main` pelo PR #52 (https://github.com/thomasravache/subsolo/pull/52), merge commit 2f54a7f em 2026-10-25, com H2 pela política auto-on-green.

### Pendências
- Nenhuma. Limites documentados no README: Google falso, Mailpit e dashboard só no computador; limite de requisições compartilhado pelo túnel; redirecionamentos do web apontam para `localhost:8443`.
## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
