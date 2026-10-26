---
id: SPEC-0043
title: Admin local no aspire run
tier: lite
type: feature
user_facing: false
status: in-progress
created: 2026-10-26
parent:
depends_on: [SPEC-0041]
consumes_contract: []
touches: [src/Subsolo.AppHost/**, tests/Common/Subsolo.AppHost.IntegrationTests/**, README.md]
adrs: []
external: []
size: S
approved_by: thomas
approved_at: 2026-10-26
---

# SPEC-0043 — Admin local no aspire run

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
No `aspire run`, e portanto no `./tools/tunnel.sh --run`, nenhum e-mail tem o papel admin. `Users:AdminEmails` está vazio em `appsettings.json`, e o admin `admin@subsolo.test` só existe na pilha do `tools/compose-up.sh`, usada pela E2E. Sem admin, quem testa localmente não consegue aprovar o próprio pedido de perfil (SPEC-0021) e por isso não chega em "+ Novo show" (SPEC-0023). Também não alcança a fila de moderação (SPEC-0033) nem as cidades e estilos (SPEC-0017). O Thomas encontrou o problema em 2026-10-26, testando pelo celular.

- Esperado: um e-mail escolhido por quem roda a aplicação localmente vira admin no `aspire run`, sem mudar a produção nem o Compose publicado.

## 2. Causa Raiz
O AppHost (`src/Subsolo.AppHost/Program.cs`) não repassa nenhum admin para a API no modo run. A configuração existe na API (`Users:AdminEmails`, `UsersOptions`), mas só o `compose-up.sh` a preenche (`Users__AdminEmails__0`).

## 3. Mudança Proposta
- Opção `Subsolo:AdminEmail` do AppHost, **só no modo run**, no mesmo molde de `Subsolo:PublicUrl` (SPEC-0041). Com a opção, a API recebe `Users__AdminEmails__0` com o e-mail. Sem ela, nada muda.
- A opção é validada antes do `Build()`: precisa ser um único e-mail válido (`MailAddress` sem nome de exibição). Se for inválida, sai um `InvalidOperationException` com a mensagem explicando o formato.
- Pode vir da linha de comando (`dotnet run --project src/Subsolo.AppHost -- --Subsolo:AdminEmail=eu@exemplo.com`) ou, uma vez só, dos user-secrets do AppHost, que já tem `UserSecretsId` e ficam fora do repositório: `dotnet user-secrets set Subsolo:AdminEmail eu@exemplo.com --project src/Subsolo.AppHost`. Pelos user-secrets, vale também para o `tools/tunnel.sh --run`.
- README: uma linha em "Admin" e outra em "Pelo celular".
- O que NÃO muda: o modo publish e o Compose gerado (o operador da produção define `Users__AdminEmails__N` como já faz hoje), o `compose-up.sh`, a API e o `tools/tunnel.sh`.

**Padrão seguido:** `Subsolo:PublicUrl` em `src/Subsolo.AppHost/Program.cs` (opção só do run, validada antes do `Build()`), com os testes em `tests/Common/Subsolo.AppHost.IntegrationTests/RunModePublicUrlTests.cs`.

**Rollback:** revert do commit.

**Outras ocorrências:** Nenhuma. O Google falso e o Mailpit do run já vêm configurados pelo AppHost.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — sem a opção, o modelo do run já é coberto pelos testes do AppHost (`RunModePublicUrlTests`, `RunModeCaddyTests`), e eles continuam valendo.
- **UT-01** — Dado o AppHost no modo run com `Subsolo:AdminEmail=admin@local.test`, quando o modelo é construído, então a API tem `Users__AdminEmails__0=admin@local.test`. Dado o mesmo sem a opção, então a API não tem `Users__AdminEmails__0`.
- **UT-02** — Dado `Subsolo:AdminEmail` inválido (`nao-e-email`, `Fulano <a@b.c>`, dois e-mails separados por vírgula), quando o AppHost é construído no run, então sai `InvalidOperationException` citando `Subsolo:AdminEmail`.
- **UT-03** — Dado o modo publish com `Subsolo:AdminEmail` definido, quando o Compose é gerado, então nenhum serviço recebe `Users__AdminEmails__0` da opção (a produção não muda).
- Integração/E2E: N/A — é configuração do orquestrador local. O papel admin a partir de `Users:AdminEmails` já está provado pelos IT da SPEC-0014 e pela E2E da SPEC-0017 na pilha do Compose.

## 5. Questões em Aberto
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
- [ ] Red: UT-01, UT-02 e UT-03 com a tag `SPEC-0043:<ID>`, falhando pelo motivo certo
- [ ] Green: opção `Subsolo:AdminEmail` no AppHost (só run, validada antes do `Build()`) e README
- [ ] Validar: build + suíte + arquitetura (G2/G3)
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 16d10ef | 2026-10-26 |
| G1 Red | PASS | MANUAL: verify (worktree): red antes do green, primeiro red 9aff7a4; red 4 FAIL + 2 guardas PASS (/tmp/claude-1000/-home-thomas-dev-repos-thomasravache-subsolo/1a529b84-b1e3-4630-850c-b7767ec890d7/scratchpad/spec0043-red.log); mutação H43-04 (sem guarda IsPublishMode → UT-03 falha); green 6/6 em 78f7609 | 2026-10-26 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 78f7609 | 2026-10-26 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 9s 680ms) — 78f7609 | 2026-10-26 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (sem blocker/major; H43-01 corrigido em 78f7609; H43-04 mutação confirmou a guarda do publish; H43-02 menor vai para Pendências: o AppHost aceita a@localhost e "a b"@c.d, que o EmailAddress.Normalize da API recusa; H43-03 aceito) — 78f7609 | 2026-10-26 |
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
