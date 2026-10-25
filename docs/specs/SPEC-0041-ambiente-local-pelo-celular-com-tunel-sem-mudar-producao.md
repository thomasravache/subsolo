---
id: SPEC-0041
title: Ambiente local pelo celular com túnel, sem mudar produção
tier: lite
type: fix
user_facing: false
status: in-progress
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
- [ ] Red: UT-01, UT-02, UT-03 e IT-01 com a tag `SPEC-0041:<ID>`, falhando pelo motivo certo
- [ ] Green: opção `Subsolo:PublicUrl` no AppHost, `allowedDevOrigins` no `next.config.ts`, `tools/tunnel.sh` e README
- [ ] Validar: build + suíte + arquitetura (G2/G3)
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 70cd98a | 2026-10-25 |
| G1 Red | PENDING | | |
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
