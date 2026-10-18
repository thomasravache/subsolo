---
id: SPEC-0019
title: "Minha conta: nome, exportar dados e excluir conta"
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-17
parent: SPEC-0002
depends_on: [SPEC-0014, SPEC-0016]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Users/**, tests/Modules/Users/**, docs/api/**, web/src/api/**, web/src/app/(public)/conta/**, web/src/components/account/**, e2e/tests/conta/**, docs/design/**, src/Subsolo.Api/Security/**, src/Subsolo.Api/packages.lock.json, tests/**/packages.lock.json, tests/Common/Subsolo.Api.IntegrationTests/**, tests/Security/**, web/src/app/page.tsx, web/src/app/page.test.tsx, e2e/tests/auth/journey.ts]
adrs: [ADR-0001, ADR-0003, ADR-0008, ADR-0011]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0019 — Minha conta: nome, exportar dados e excluir conta

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
A tela "Minha conta" (`/conta`) passa a deixar o fã mudar o nome de exibição, baixar todos os seus dados num arquivo e excluir a conta. Atende aos direitos do titular da LGPD (art. 18: acesso, portabilidade e eliminação) previstos no ADR-0008. A exclusão apaga a conta, as sessões e os links pendentes, e avisa os outros módulos por evento para que apaguem o que guardam do usuário.

## 2. Motivação & Escopo
**Motivação:** o ADR-0008 promete que o fã baixa e exclui os próprios dados pela conta antes do lançamento; o "Radar LGPD" do épico lista esta spec. A SPEC-0016 deixou `/conta` só com o e-mail e "Sair".

**Objetivos (dentro do escopo):**
- `PUT /api/v1/me` — muda o nome de exibição (1 a 60 caracteres, sem espaços nas pontas; vazio remove o nome).
- `GET /api/v1/me/export` — baixa um JSON com tudo o que o Subsolo guarda do usuário: conta, papéis, sessões (criada, método, último uso, expira; nunca o hash do token) e as seções dos outros módulos.
- Ponto de extensão `IPersonalDataSection` (público em `Subsolo.Modules.Users.IntegrationEvents`): cada módulo que guardar dado do usuário registra a sua seção no export. As specs seguintes (seguir, "vou", push) só implementam a interface.
- `DELETE /api/v1/me` — exclui a conta depois de o fã digitar o próprio e-mail: apaga `users`, `sessions` e os `magic_link_tokens` do e-mail, apaga o cookie e publica `UserDeletedIntegrationEvent(UserId, DeletedAtUtc)` pela outbox, para os outros módulos apagarem o que guardam.
- Tela `/conta` completa conforme o Stitch: seção "Seus dados" (e-mail só leitura, nome com "Salvar"), "Privacidade & dados" ("Baixar meus dados"), "Sair", e "Excluir conta" com confirmação.

**Não-objetivos (fora do escopo):**
- Mudar o e-mail da conta (quem quiser outro e-mail exclui e entra de novo).
- Lista de sessões ativas e "sair de todos os aparelhos" (fora do PRD; SPEC-0014 §2).
- Selo "Entrou com Google" (entra com a spec do Google).
- Apagar dados dos backups na hora: somem no fim da retenção (até 6 meses, ADR-0011), como a política de privacidade vai dizer.

## 3. Dependências
- **Implementações necessárias:** SPEC-0014 (usuário, sessão, `/me`) e SPEC-0016 (`/conta` mínima, tabela `magic_link_tokens`, navegação conforme a sessão).
- **Contratos consumidos:** N/A — usa o módulo `Users` por dentro e publica um evento novo.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** slices do módulo `Users` (SPEC-0014: `GetMe`, `SignOut`) e a tela `/conta` da SPEC-0016; outbox e eventos de integração da SPEC-0009; página SSR com chamadas do navegador à API (`fetch` com cookie e `X-CSRF`) como na SPEC-0016.

**Decisão:** três slices novos no `Users` (`UpdateDisplayName`, `ExportPersonalData`, `DeleteAccount`). O export junta a seção do próprio `Users` com as de cada `IPersonalDataSection` registrada no contêiner; a exclusão é local ao `Users` e o resto acontece pelo evento `UserDeleted`, consumido pelos módulos que guardarem dados do usuário (cada um com teste próprio na sua spec).

**Justificativa:** ADR-0001 (módulos só falam pela superfície pública e por eventos), ADR-0008 (acesso, portabilidade e eliminação pela conta) e ADR-0003 (sessão revogável: excluir derruba todas as sessões).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** excluir chamando cada módulo de forma síncrona (acopla os módulos e falha parcial deixa lixo); marcar como excluído sem apagar ("soft delete") (guarda dado pessoal sem finalidade, contra o ADR-0008); confirmar a exclusão por link no e-mail (atrito sem ganho: a sessão já prova quem é, e digitar o e-mail evita clique acidental).

**ADRs:** ADR-0001, ADR-0003, ADR-0008, ADR-0011

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** export em ≤ 1 s p95 para uma conta com 50 sessões (IT-02); exclusão em ≤ 500 ms p95 (IT-03).
- **Segurança:** os três endpoints exigem sessão (`fan`), mutações com `X-CSRF`, rate limit `default`, `Cache-Control: no-store`; a exclusão exige o e-mail da própria conta (IT-03, IT-04, IT-05); o export nunca leva hash de token nem dado de outro usuário (UT-02, IT-02).
- **Privacidade e dados pessoais:** atende ao art. 18 da LGPD (acesso, portabilidade, eliminação); e-mail fora dos logs (IT-06); exclusão sem "soft delete"; backups seguem a retenção do ADR-0011.
- **Disponibilidade e resiliência:** a exclusão grava a remoção e o evento na mesma transação (outbox); um módulo fora do ar processa o `UserDeleted` quando voltar (SPEC-0009).
- **Acessibilidade (UI):** WCAG 2.2 AA em `/conta` e na confirmação, com axe no E2E; a confirmação é um `dialog` com foco preso, rótulo visível e erro ligado ao campo.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `PUT /api/v1/me` · `GET /api/v1/me/export` · `DELETE /api/v1/me` · `IPersonalDataSection` · evento `UserDeleted` · tela `/conta`

```yaml
PUT /api/v1/me:                        # RequireAuthorization(fan) + X-CSRF + RequireRateLimiting(default)
  body: { displayName: string|null }   # trim; 1..60; vazio ou null remove o nome
  200: { id, email, displayName, roles }   # mesmo corpo do GET /me
  400: ProblemDetails code=users.invalid_display_name | security.csrf_invalid
  401: auth.required ; 429: security.rate_limited
GET /api/v1/me/export:                 # RequireAuthorization(fan) + RequireRateLimiting(default)
  200: application/json; Content-Disposition: attachment; filename="subsolo-meus-dados.json"
       { exportedAtUtc, account: { id, email, displayName, roles, createdAtUtc },
         sessions: [{ createdAtUtc, method, lastSeenAtUtc, expiresAtUtc, absoluteExpiresAtUtc, revokedAtUtc }],
         sections: { "<módulo>": <objeto da seção> } }
  401: auth.required ; 429: security.rate_limited
DELETE /api/v1/me:                     # RequireAuthorization(fan) + X-CSRF + RequireRateLimiting(default)
  body: { confirmEmail: string }       # comparado ao e-mail da conta, sem diferenciar maiúsculas e espaços
  204: conta, sessões e links do e-mail apagados; Set-Cookie apaga __Host-subsolo; UserDeleted na outbox
  400: ProblemDetails code=users.delete_confirmation_mismatch | security.csrf_invalid
  401: auth.required ; 429: security.rate_limited
  # toda resposta dos três: Cache-Control: no-store
```

```csharp
// Subsolo.Modules.Users.IntegrationEvents (público)
public interface IPersonalDataSection {
    string Name { get; }                                     // chave em "sections" (ex.: "follows")
    Task<object?> ExportAsync(Guid userId, CancellationToken ct);   // null → seção omitida
}
record UserDeletedIntegrationEvent(Guid UserId, DateTimeOffset DeletedAtUtc);   // só o id, nunca o e-mail
```

```text
/conta (SSR dinâmica, no-store; anônimo → /entrar?returnTo=/conta)
  h1 "MINHA CONTA"
  "SEUS DADOS": e-mail (só leitura) ; "Nome de exibição" + "Salvar" → PUT /me → "Nome salvo." (aria-live)
     erro → "Use de 1 a 60 caracteres." ligado ao campo
  "PRIVACIDADE & DADOS": "Baixe uma cópia de todos os dados que o Subsolo guarda de você." + "Baixar meus dados" → GET /me/export (download do arquivo)
  "SESSÃO ATUAL": botão "Sair da conta" (SPEC-0016)
  "EXCLUIR CONTA": "Atenção: isso não dá para desfazer. Sua conta e tudo o que o Subsolo guarda de você são apagados agora; as cópias de segurança somem em até 6 meses." + "Excluir minha conta" → dialog "Excluir conta?":
     "Isso apaga sua conta e tudo o que o Subsolo guarda de você. Não dá para desfazer."
     campo "Digite seu e-mail para confirmar" + "Excluir para sempre" (vermelho) + "Cancelar"
     e-mail diferente → "O e-mail não confere." ligado ao campo
     204 → vai para "/" com aviso "Sua conta foi excluída." ; a aba "Conta" leva a /entrar
```

**Design:** Stitch, projeto 15194126668426659178: "Minha Conta - Subsolo" (3b3765cf36c344f6bb48e52429570e12, mobile) e "Excluir Conta - Subsolo" (aeb47861556747ba9bacc732f8c05ae8, confirmação). Ajustes combinados: sem a engrenagem do topo; sem o selo "Entrou com Google" (entra com a spec do Google); "Sessão atual" só com o botão "Sair da conta", sem aparelho e cidade (não guardamos localização).

**Arquivos/módulos afetados:** ver `touches` no frontmatter. Slices em `src/Modules/Users/**/Features/{UpdateDisplayName,ExportPersonalData,DeleteAccount}`; tela em `web/src/app/(public)/conta/**` e componentes em `web/src/components/account/**`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Mudar o nome | nome válido | 200 com o nome novo; aparece em `/conta` | UT-01, IT-01, E2E-01 |
| Remover o nome | vazio ou null | 200 com `displayName: null` | UT-01, IT-01 |
| Nome inválido | 61 caracteres ou só espaços com texto invisível | 400 `users.invalid_display_name`; mensagem no campo | UT-01, IT-01, E2E-02 |
| Exportar | sessão válida | arquivo JSON com conta, sessões e seções; sem hash de token | UT-02, IT-02, E2E-01 |
| Seção de outro módulo | `IPersonalDataSection` registrada | aparece em `sections` com o nome dela; `null` é omitida | UT-02, IT-02 |
| Excluir | `confirmEmail` igual (com outra caixa e espaços) | 204; conta, sessões e links apagados; cookie apagado; `UserDeleted` na outbox | UT-03, IT-03, E2E-01 |
| Confirmação errada | outro e-mail | 400 `users.delete_confirmation_mismatch`; nada apagado; mensagem no campo | UT-03, IT-04, E2E-02 |
| Depois de excluir | mesmo cookie; entrar de novo com o e-mail | 401; nova conta sem nome, sem as sessões antigas | IT-03, E2E-01 |
| Sem anti-forgery | PUT ou DELETE sem `X-CSRF` | 400 `security.csrf_invalid`; nada muda | IT-04 |
| Anônimo | sem cookie | 401 `auth.required` nos três | IT-05 |
| Sem cache | qualquer resposta dos três | `Cache-Control: no-store` | IT-05 |
| E-mail fora dos logs | os três fluxos com captura | e-mail só mascarado | IT-06 |
| Contrato publicado | snapshot OpenAPI | os três endpoints com corpos e códigos do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — slices novos; `/conta` mínima já coberta pelos E2E da SPEC-0016.

### 7.2 Testes Unitários
- **UT-01** — Dado o validador de `UpdateDisplayName`, quando recebe "  Caio Grind  ", "", null, 60 e 61 caracteres, então guarda "Caio Grind", remove o nome nos dois vazios, aceita 60 e retorna `users.invalid_display_name` para 61.
- **UT-02** — Dado um usuário com 2 sessões e duas `IPersonalDataSection` de teste (uma devolve objeto, outra `null`), quando o export é montado, então traz a conta, as 2 sessões sem nenhum campo de hash ou token, a seção que devolveu objeto pelo nome dela e não traz a que devolveu `null`.
- **UT-03** — Dado o handler de `DeleteAccount`, quando `confirmEmail` é " FULANO@email.com " para a conta `fulano@email.com`, então remove o usuário e as sessões e gera `UserDeletedDomainEvent` com só o id; com outro e-mail, retorna `users.delete_confirmation_mismatch` e não remove nada.

### 7.3 Testes de Integração
- **IT-01** — Dado o PostgreSQL real e um usuário logado, quando `PUT /api/v1/me` muda e depois remove o nome, então as respostas e o `GET /me` refletem cada mudança; 61 caracteres dá 400 com o código.
- **IT-02** — Dado um usuário com 50 sessões e uma `IPersonalDataSection` de teste registrada no host, quando `GET /api/v1/me/export` é chamado, então 200 com `Content-Disposition: attachment; filename="subsolo-meus-dados.json"`, `no-store`, corpo no formato do §6, nenhum `id_hash` nem valor de cookie no arquivo, a seção de teste presente, e p95 de 20 chamadas ≤ 1 s.
- **IT-03** — Dado um usuário com 2 sessões e um link mágico pendente, quando `DELETE /api/v1/me` é chamado com o e-mail certo e `X-CSRF`, então 204, `Set-Cookie` apaga o cookie, não sobra linha em `users`, `sessions` nem `magic_link_tokens` desse e-mail, a outbox tem `UserDeleted` só com id e data, o mesmo cookie em `/me` dá 401, e um novo `SignInAsync` com o e-mail cria um usuário com outro id e sem nome; p95 de 20 exclusões ≤ 500 ms.
- **IT-04** — Dado um usuário logado, quando `DELETE` vem com outro e-mail, então 400 `users.delete_confirmation_mismatch` e nada muda; quando `PUT` ou `DELETE` vêm sem `X-CSRF`, então 400 `security.csrf_invalid` e nada muda.
- **IT-05** — Dado nenhum cookie, quando os três endpoints são chamados, então 401 `auth.required`; e toda resposta dos três (200, 204, 400, 401) tem `Cache-Control: no-store`.
- **IT-06** — Dados os logs e spans capturados durante mudar o nome, exportar e excluir, quando inspecionados, então o e-mail só aparece mascarado.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então `PUT /api/v1/me`, `GET /api/v1/me/export` e `DELETE /api/v1/me` têm os corpos, status e códigos do §6, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa com o Mailpit, nos projetos mobile-chrome e mobile-safari: o fã entra pelo link mágico, vai a `/conta`, troca o nome e vê "Nome salvo."; toca em "Baixar meus dados" e o arquivo baixado tem o e-mail e o nome; toca em "Excluir minha conta", digita o e-mail e confirma; cai em `/` com "Sua conta foi excluída.", e "Conta" leva a `/entrar`; entrando de novo com o mesmo e-mail, `/conta` mostra o nome vazio. Sem violações do axe em `/conta` e no diálogo. [jornada: conta]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: nome com 61 caracteres mostra "Use de 1 a 60 caracteres." ligado ao campo; e-mail errado na confirmação mostra "O e-mail não confere." e a conta continua; o foco fica preso no diálogo e "Cancelar" devolve o foco ao botão. [jornada: conta]

### 7.6 Outros
- Segurança: revisão no G4 de autorização, CSRF e ausência de dados de outro usuário no export.
- LGPD: o G4 confere que nada do usuário sobra nas tabelas do `Users` depois da exclusão (IT-03).
- Herança da revisão da SPEC-0016 (pequenos ajustes no `Users`, sem teste novo além dos da própria 0016): expor `UsersModule.SessionAbsoluteLifetime` para o host não importar `Users.Domain`; `PublicUrlOptionsValidator` recusar `App:PublicUrl` com query ou fragmento.

**Dublês e dados de teste:** `IPersonalDataSection` de teste registradas só nos hosts de teste; e-mails `@subsolo.test`; Mailpit real no E2E.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL); E2E contra a pilha do Compose (`https://localhost:8443`) com `MAILPIT_URL`; `--workers=1`.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** nenhuma tabela nova; a exclusão usa as FKs de `sessions` (cascata) e apaga `magic_link_tokens` pelo e-mail.
- **Compatibilidade:** endpoints novos em `/api/v1`; `UserDeleted` é evento novo, sem consumidores ainda.
- **Observabilidade:** contadores `subsolo.users.exports` e `subsolo.users.deletions`; logs com o id do usuário, nunca o e-mail.
- **Rollback:** reverter o merge commit; sem migração.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Confirmar a exclusão como? — Digitando o próprio e-mail, sem link por e-mail (recomendação do Architect; Thomas delegou no piloto automático, 2026-10-17)
- [x] Exclusão apaga ou marca como excluído? — Apaga de verdade; backups somem pela retenção do ADR-0011 (ADR-0008, decisão do Thomas de 2026-10-17 sobre dados pessoais)
- [x] Tela de confirmação "Excluir conta" gerada no Stitch? — Sim, "Excluir Conta - Subsolo" (aeb47861556747ba9bacc732f8c05ae8), em 2026-10-17 com o Thomas acordado; título do diálogo fica "Excluir conta?"

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"), com as telas do Stitch geradas com ele acordado.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Domínio e regras**
- [ ] Red: UT-01, UT-02, UT-03 com a tag `SPEC-0019:<ID>`, falhando pelo motivo certo
- [ ] Green: implementar o mínimo seguindo o padrão de referência
- [ ] Refactor mantendo tudo verde

**Fase 2: API, persistência e contratos**
- [ ] Red: IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, CT-01 falhando pelo motivo certo
- [ ] Green: endpoints, migração aditiva, OpenAPI e cliente TS regenerados
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 3: Telas e jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [ ] Green: telas conforme o Stitch e os ajustes do §6; jornada completa passando localmente
- [ ] Validar: build + suíte + E2E (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 88cc681 (árvore suja) | 2026-10-17 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 35f6b0e | 2026-10-18 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 838200d | 2026-10-18 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 2s 866ms) — 838200d | 2026-10-18 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED em 838200d — export completo e sem hashes nem dados de outros, exclusão numa transação com o UserDeleted só com ids, cookie limpo, confirmação por e-mail, fan + X-CSRF + no-store, e-mail fora dos logs, diálogo acessível, itens herdados da 0016; testes não enfraquecidos; o texto de privacidade foi corrigido depois por emenda em 2583ec5 (só texto; web 75/75 e E2E de conta verdes); o aviso da página inicial é coberto pelo E2E-01; menores → SPEC-0021 — 2583ec5 | 2026-10-18 |
| G5 Integração & CI | FAIL | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 8m 30s 141ms); test_e2e exit 1 ([ELIFECYCLE] Test failed. See above for more details.); arch_test exit 0 (duration: 2s 903ms) — 717b5d2 | 2026-10-18 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0019`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (escopo e testes) | 2026-10-18 | `touches` ganha `e2e/tests/auth/journey.ts`: o helper de login espera o formulário estar interativo antes de digitar e sempre espera a navegação depois de entrar (itens que estavam herdados pela SPEC-0020) | G5 da onda 5 falhou no E2E-03 da SPEC-0016 (mobile-chrome) com a mesma instabilidade já vista no mobile-safari: o e-mail foi digitado antes da hidratação | SPEC-0020 (os dois itens saem da herança dela) | thomas (delegado: piloto automático, 2026-10-18, recomendação do Architect) |
| 1 (texto) | 2026-10-18 | Textos de "Privacidade & dados" e "Excluir conta" fixados no §6, sem citar dados que ainda não existem e avisando que as cópias de segurança somem em até 6 meses (ADR-0011) | Revisão G4: o texto do Stitch dizia "permanentemente apagados dos servidores", impreciso diante dos backups (transparência, LGPD art. 6º, VI) | nenhuma | thomas (delegado: piloto automático, 2026-10-18, recomendação do Architect) |
| 1 (escopo) | 2026-10-18 | `touches` ganha `web/src/app/page.tsx` (e um teste dela): depois de excluir, o fã vai para `/?conta=excluida` e a página inicial mostra "Sua conta foi excluída." em `role="status"`; o resto da página inicial não muda | Relatório do Test-writer: o aviso do §6 fica em `/`, fora dos `touches` | nenhuma | thomas (delegado: piloto automático, 2026-10-18, recomendação do Architect) |
| 1 (escopo) | 2026-10-18 | `touches` ganha `src/Subsolo.Api/Security/**` (o host passa a ler `UsersModule.SessionAbsoluteLifetime`, herança da revisão da SPEC-0016), os lock files, `tests/Common/Subsolo.Api.IntegrationTests/**` (contrato do `/me`) e `tests/Security/**` | Antes do início da onda, pelo que as ondas 1–4 mostraram | nenhuma | thomas (delegado: piloto automático, 2026-10-18, recomendação do Architect) |
