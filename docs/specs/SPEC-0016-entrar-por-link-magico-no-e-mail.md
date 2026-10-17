---
id: SPEC-0016
title: Entrar por link mágico no e-mail
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-17
parent: SPEC-0002
depends_on: [SPEC-0014, SPEC-0018]
consumes_contract: [SPEC-0018@1]
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Users/**, tests/Modules/Users/**, docs/api/**, web/src/api/**, web/src/app/(public)/entrar/**, web/src/app/(public)/conta/**, web/src/components/ui/BottomNav.tsx, web/src/components/ui/Header.tsx, web/src/components/ui/AppShell.tsx, web/src/components/ui/shell.ts, web/src/components/ui/__tests__/**, web/src/app/(public)/layout.tsx, e2e/tests/auth/**, e2e/tests/security/**, web/src/security/**, e2e/playwright.config.ts, tools/compose-up.sh, docs/design/**, src/Subsolo.Api/Security/**, tests/Security/**]
adrs: [ADR-0003, ADR-0008, ADR-0013, ADR-0014, ADR-0016, ADR-0017]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0016 — Entrar por link mágico no e-mail

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O fã entra no Subsolo sem senha: digita o e-mail em `/entrar`, recebe um link que vale por 15 minutos e só funciona uma vez, abre o link, confirma com um toque e volta logado para onde estava. A aba "Conta" passa a levar a `/entrar` (anônimo) ou a `/conta` (logado). Por enquanto, `/conta` mostra o e-mail e o botão "Sair"; exportar e excluir chegam na onda 3.

## 2. Motivação & Escopo
**Motivação:** seguir, "vou", cadastro de show e o painel do admin exigem login (resposta do Thomas no épico: agenda aberta, ações com conta). O link mágico é o método principal (ADR-0003). O Google entra numa spec própria, porque depende do provedor OIDC falso no ambiente local e do cliente real no go-live.

**Objetivos (dentro do escopo):**
- `POST /api/v1/auth/magic-link` pede o link: gera um token de 256 bits (guardado só como hash), válido por 15 minutos e de uso único, e manda o e-mail pela `INotificationsApi` (SPEC-0018). A resposta é igual exista a conta ou não, para não revelar quem tem conta.
- `POST /api/v1/auth/magic-link/consume` troca o token pela sessão (`ISignInService`, SPEC-0014).
- Limites: o de IP que já existe (`auth`, 5 por minuto) e um novo, por e-mail: 3 links a cada 15 minutos. O pedido que passa do limite por e-mail responde igual, mas não envia nada.
- `returnTo` aceita só caminho relativo do próprio site, para não virar redirecionamento aberto.
- Telas (Stitch, aprovadas no H1): `/entrar` (formulário), "Confira seu e-mail" (reenviar em 60 s, usar outro e-mail), `/entrar/confirmar` (botão "Entrar no Subsolo", sem consumir o token ao carregar, porque leitores de e-mail abrem links sozinhos) e o estado "link expirado ou já usado". Mais `/conta` mínima, com o e-mail e "Sair".
- A sessão vive só no cookie `__Host-subsolo` (HttpOnly): nada de token ou dado da sessão em `localStorage`, `sessionStorage` ou cache. Apagou os cookies, recarregou → deslogado. `/conta` é dinâmica e responde `Cache-Control: no-store` (nem o Next nem o navegador guardam a página).
- Nenhuma variável de ambiente chega ao navegador: proibido `NEXT_PUBLIC_*`; `process.env` só em código de servidor; o navegador fala com a API pelo mesmo domínio (`/api/...`), sem URL vinda do ambiente.
- Navegação: a aba "Conta" (mobile) e o link "Entrar"/"Conta" no cabeçalho (desktop) mudam conforme a sessão (`GET /api/v1/me`).

**Não-objetivos (fora do escopo):**
- Botão "Continuar com Google" (spec da onda 3; a tela do Stitch já o prevê, e o botão só aparece quando aquela spec ligar o provedor).
- Exportar dados, excluir conta e editar nome (spec "Minha conta", onda 3).
- Convite para instalar o PWA (spec de push).

## 3. Dependências
- **Implementações necessárias:** SPEC-0014 — usuário, sessão, `ISignInService`, `/me` e `/logout`.
- **Contratos consumidos:** SPEC-0018@1 — `INotificationsApi.SendEmailAsync` com o modelo `MagicLink` (dublê nos testes unitários; o IT e o E2E usam o módulo real com o Mailpit).
- **Pré-requisitos externos:** Mailpit (já no AppHost e no Compose); API HTTP do Mailpit acessível ao Playwright por `MAILPIT_URL`.

## 4. Decisão Arquitetural
**Contexto:** slices do módulo `Users` (SPEC-0014) no padrão `Features/<Caso>/` do `Events` (SPEC-0008); páginas SSR em `web/src/app/(public)/**` com o shell e os componentes da SPEC-0011; cliente gerado do OpenAPI (`web/src/api/`); anti-forgery `X-CSRF` nas mutações (SPEC-0010); status HTTP das páginas pelo proxy (ADR-0016) e CSP com nonce (ADR-0017).

**Decisão:** dois slices no `Users` (`RequestMagicLink`, `ConsumeMagicLink`) e uma tabela `magic_link_tokens` (hash, e-mail normalizado, `return_to`, criado, expira, usado). As páginas do Next chamam a API pelo navegador (`fetch` com `credentials: "include"`), passando pelo Caddy no mesmo domínio, e o cookie `__Host-subsolo` volta direto ao navegador. O token vai na URL (`/entrar/confirmar?token=`) e é enviado no corpo do POST pela página, nunca consumido num GET.

**Justificativa:** ADR-0003 (link de 256 bits, guardado como hash, 15 min, uso único, limites por e-mail e IP). Consumir só no POST evita que o pré-carregamento de links do Gmail ou do Outlook gaste o token. A resposta igual para qualquer e-mail atende o ADR-0008 (não revela quem tem conta).

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** consumir o token no GET (scanners de e-mail gastam o link); código de 6 dígitos no lugar do link (mais atrito no celular, que é o caso principal); sessão criada pelo Next (o cookie é da API pelo ADR-0003).

**ADRs:** ADR-0003, ADR-0008, ADR-0013, ADR-0014, ADR-0016, ADR-0017

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** do envio do formulário até a tela "Confira seu e-mail" em ≤ 3 s p95 com o Mailpit (E2E-01); consumir o link em ≤ 300 ms p95 (IT-02).
- **Segurança:** token de 256 bits só como hash; uso único com proteção a corrida (dois consumos simultâneos → um só sucesso, IT-03); `returnTo` só relativo (`/` seguido de algo que não seja `/` ou `\\`), senão vira `/` (UT-03); limites por IP (429) e por e-mail (silencioso) (IT-04); mutações com X-CSRF; sessão nova a cada entrada (contra fixação, SPEC-0014); sessão só no cookie HttpOnly, sem cópia no navegador, e `/conta` com `no-store` (E2E-03); nenhuma variável de ambiente no código do navegador (UT-05, E2E-04).
- **Privacidade e dados pessoais:** e-mail e token nunca em claro nos logs (IT-05); tokens usados ou expirados apagados depois de 24 h por uma tarefa periódica (IT-06); resposta igual exista a conta ou não (IT-01).
- **Disponibilidade e resiliência:** SMTP fora do ar → 503 `notifications.email_unavailable`, e a tela mostra "Não conseguimos mandar o e-mail agora. Tente de novo em alguns minutos." (IT-07, E2E-02).
- **Acessibilidade (UI):** WCAG 2.2 AA nas quatro telas, com axe no E2E; rótulo visível no campo; erro ligado ao campo (`aria-describedby`); o contador de reenvio anunciado por `aria-live="polite"`; alvos de toque ≥ 44 px.
- **Custo:** zero (Mailpit; o provedor real é do go-live).

## 6. Artefato A — Contrato
**Interface:** `POST /api/v1/auth/magic-link` · `POST /api/v1/auth/magic-link/consume` · telas `/entrar`, `/entrar/confirmar`, `/conta`

```yaml
POST /api/v1/auth/magic-link:            # AllowAnonymous + RequireRateLimiting(auth) + X-CSRF
  body: { email: string (≤ 254), returnTo: string|null }
  202: {}                                 # sempre igual, exista a conta ou não; acima do limite por e-mail não envia
  400: ProblemDetails code=users.invalid_email | security.csrf_invalid
  429: ProblemDetails code=security.rate_limited (+ Retry-After)
  503: ProblemDetails code=notifications.email_unavailable

POST /api/v1/auth/magic-link/consume:    # AllowAnonymous + RequireRateLimiting(auth) + X-CSRF
  body: { token: string }
  200: { returnTo: string }               # Set-Cookie __Host-subsolo (sessão nova); returnTo saneado, padrão "/"
  400: ProblemDetails code=users.magic_link_invalid   # inexistente, expirado ou já usado (mesmo código para os três)
       | security.csrf_invalid
  429: ProblemDetails code=security.rate_limited
```

```text
E-mail: modelo MagicLink (SPEC-0018) com link = {App:PublicUrl}/entrar/confirmar?token={token} e minutes = 15.

/entrar (SSR; logado → redireciona para /conta)
  título "Entrar | Subsolo"; h1 "ENTRAR"; texto do Stitch; campo "Seu e-mail" (rótulo visível); botão "Mandar link de acesso";
  "Sem senha: o link vale por 15 minutos." ; links Termos e Privacidade (destino criado pela spec das páginas institucionais;
  até lá, apontam para /privacidade e /termos)
  erro de formato → "Digite um e-mail válido." no campo
  → após 202: estado "CONFIRA SEU E-MAIL": "Mandamos o link de acesso para {email}. Ele vale por 15 minutos e só funciona uma vez.",
    "Não chegou? Olhe a caixa de spam.", botão "Reenviar link" desabilitado com "Reenviar em 0:SS" por 60 s, "Usar outro e-mail"
  429 → "Muitas tentativas. Espere um minuto e tente de novo."
  503 → "Não conseguimos mandar o e-mail agora. Tente de novo em alguns minutos."
/entrar/confirmar?token=… (SSR; não consome no carregamento)
  h1 "QUASE LÁ"; botão "Entrar no Subsolo" → POST consume → navega para returnTo
  400 → "Este link expirou ou já foi usado." + botão "Pedir outro link" (/entrar)
/conta (SSR dinâmica, Cache-Control: no-store; anônimo → redireciona para /entrar?returnTo=/conta)
  h1 "MINHA CONTA"; seção "SEUS DADOS" com o e-mail (só leitura); botão "Sair" → POST /api/v1/auth/logout → /entrar
Navegação: aba "Conta" (mobile) e link no cabeçalho (desktop) → /entrar (anônimo) ou /conta (logado)
```

**Design:** Stitch, projeto "Subsolo — Agenda de shows underground" (15194126668426659178): telas "Entrar - Subsolo" (eefd8a676a5d4a8881cec1c1abb29738) e "Confira seu e-mail - Subsolo" (52a2270f7b25402eb6ec7428afda013d), mobile, aprovadas no H1. Ajustes combinados: sem o selo "ANTI-SPAM" e sem o rótulo "IDENTIFICAÇÃO RÁPIDA"; o botão do Google fica de fora até a spec do Google; o bloco "manifesto" vai para o rodapé do shell, que já existe. As telas próprias no Stitch: "Quase lá - Subsolo" (5583b548089a48b399ef15892ba82d18) para `/entrar/confirmar`, sem o e-mail no texto (a página não consulta o token antes do clique: "Toque no botão para entrar no Subsolo.") e sem "Não recebeu o link? Reenviar"; "Link inválido - Subsolo" (ead4698691cd43018d4264cf4cb1d95e) para o erro; `/conta` mínima usa "Minha Conta - Subsolo" (3b3765cf36c344f6bb48e52429570e12) só com a seção do e-mail e "Sair" (nome, exportar e excluir entram na onda 3; sem o selo "Entrou com Google" e sem o cartão de aparelho/cidade). Nas três, o topo segue o shell atual. Capturas em `docs/design/stitch/` depois do H1.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Pedir o link | e-mail válido | 202; token gravado como hash; e-mail `MagicLink` com o link e 15 min | UT-01, IT-01, E2E-01 |
| Conta inexistente | e-mail nunca usado | mesma resposta 202 e mesmo e-mail (a conta nasce no consumo) | IT-01 |
| E-mail inválido | sem arroba ou > 254 | 400 `users.invalid_email`; tela "Digite um e-mail válido." | UT-01, E2E-02 |
| Limite por e-mail | 4º pedido em 15 min | 202 sem envio | UT-02, IT-04 |
| Limite por IP | 6º pedido no minuto | 429 `security.rate_limited` + `Retry-After`; mensagem na tela | IT-04, E2E-02 |
| SMTP fora do ar | `email_unavailable` do Notifications | 503 `notifications.email_unavailable`; mensagem na tela | IT-07, E2E-02 |
| Entrar pelo link | token válido, POST consume | 200 com `returnTo`; cookie de sessão; token marcado como usado | UT-01, IT-02, E2E-01 |
| Link usado, expirado ou inexistente | 2º consumo, 16 min depois, token aleatório | 400 `users.magic_link_invalid`; tela "Este link expirou ou já foi usado." | UT-01, IT-02, E2E-02 |
| Consumo simultâneo | dois POST com o mesmo token | um 200 e um 400 | IT-03 |
| GET não consome | abrir `/entrar/confirmar?token=` | token continua válido até o clique | E2E-01 |
| returnTo seguro | `//evil.com`, `https://evil.com`, `/\\evil.com` | vira `/`; `/curitiba` é mantido | UT-03 |
| Sem anti-forgery | POST sem X-CSRF | 400 `security.csrf_invalid` | IT-08 |
| Dados fora dos logs | pedido e consumo com captura | e-mail mascarado; token ausente | IT-05 |
| Limpeza | tokens usados ou expirados há > 24 h | apagados pela tarefa periódica | IT-06 |
| Navegação conforme a sessão | anônimo × logado | "Conta" leva a `/entrar` ou a `/conta`; `/conta` anônimo → `/entrar?returnTo=/conta`; `/entrar` logado → `/conta` | UT-04, E2E-01 |
| Sair | botão "Sair" em `/conta` | sessão revogada; volta a `/entrar` | E2E-01 |
| Cookies apagados | logado, apaga cookies e dados do site e recarrega | deslogado: `/conta` → `/entrar?returnTo=/conta`; "Conta" leva a `/entrar`; voltar no navegador depois de sair não mostra o e-mail | E2E-03 |
| Sessão fora do navegador | após entrar | nada da sessão em `localStorage`/`sessionStorage`; cookie HttpOnly; `/conta` com `no-store` | E2E-03 |
| Variáveis de ambiente | código do navegador e HTML servido | nenhum `NEXT_PUBLIC_*`, nenhum `process.env` em código de cliente, nenhum valor do ambiente do container | UT-05, E2E-04 |
| Achados da revisão da SPEC-0014 (Emenda 2) | cookie × sessão do banco, primeira entrada simultânea, evento `UserRegistered`, 503 no OpenAPI, 429 com `no-store` | como no §7.6 | UT-06, IT-09, CT-03 |
| Contratos consumidos | `INotificationsApi` e `/me` | como em SPEC-0018@1 e SPEC-0014 | CT-01, CT-02 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — slices e telas novos; a navegação inferior atual segue coberta pelos testes da SPEC-0011.

### 7.2 Testes Unitários
- **UT-01** — Dado um dublê de `INotificationsApi` e um `FakeTimeProvider`, quando `RequestMagicLink` recebe `fulano@email.com`, então grava o hash (nunca o token), expira em 15 min e chama o modelo `MagicLink` com o link e `minutes=15`; dado `"sem-arroba"`, retorna `users.invalid_email` sem chamar o dublê; quando `ConsumeMagicLink` recebe o token válido, chama `ISignInService` e marca o token como usado; usado, expirado (16 min) ou aleatório → `users.magic_link_invalid`.
- **UT-02** — Dados 3 pedidos para o mesmo e-mail em 15 min, quando chega o 4º, então retorna sucesso sem chamar o dublê; aos 15 min e 1 s, envia de novo.
- **UT-03** — Dados `returnTo` = `/curitiba`, `//evil.com`, `https://evil.com`, `/\\evil.com`, `javascript:alert(1)`, vazio e `null`, quando saneados, então só `/curitiba` é mantido e os outros viram `/`.
- **UT-04** — Dado o componente de navegação com e sem usuário, quando renderizado (Vitest + Testing Library), então "Conta" aponta para `/conta` ou `/entrar`, e o item ativo tem `aria-current="page"`.
- **UT-06** — Dado o handler `PublishUserRegistered`, quando recebe o evento de domínio, então publica `UserRegisteredIntegrationEvent` com o mesmo `UserId`, a data e nenhum e-mail; o `SignInService` exige `ILogger` (sem parâmetro opcional).
- **UT-05** — Dados os arquivos de `web/src/**` e `web/next.config.ts`, quando o teste (Vitest) os varre, então nenhum usa `NEXT_PUBLIC_`, nenhum arquivo com `"use client"` lê `process.env`, e o `next.config.ts` não declara `env` nem `publicRuntimeConfig`; um arquivo de exemplo com `process.env.X` num componente cliente é reprovado.

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL e o Mailpit reais, quando `POST /magic-link` é chamado para um e-mail com conta e outro sem, então as duas respostas são 202 com o mesmo corpo, as duas mensagens chegam ao Mailpit com o link, e a tabela guarda só hashes.
- **IT-02** — Dado o link lido do Mailpit, quando `POST /magic-link/consume` é chamado, então 200 com o `returnTo` pedido, `Set-Cookie __Host-subsolo`, e `/me` responde 200; o segundo consumo dá 400; com o relógio 16 min à frente, um token novo dá 400; o p95 de 20 consumos fica ≤ 300 ms.
- **IT-03** — Dado um token válido, quando dois `consume` concorrentes são enviados, então exatamente um recebe 200 e o outro 400.
- **IT-04** — Dados 4 pedidos para o mesmo e-mail, então o Mailpit recebe só 3; com o limite de IP configurado para 5 por minuto, o 6º pedido do mesmo IP recebe 429 com `Retry-After`.
- **IT-05** — Dados os logs e spans capturados durante pedido e consumo, quando inspecionados, então o e-mail aparece só mascarado e o token não aparece.
- **IT-06** — Dados tokens usados e expirados há 25 h e um válido, quando a tarefa de limpeza roda, então só o válido continua no banco.
- **IT-07** — Dado o Mailpit parado, quando `POST /magic-link` é chamado, então 503 `notifications.email_unavailable` em ≤ 11 s.
- **IT-09** — Dado o host real com `FakeTimeProvider`: um usuário que entra, usa até o dia 14 e volta no dia 40 continua logado (a validade do cookie acompanha a sessão do banco) e no dia 44+ não; dois consumos de links diferentes do mesmo e-mail novo ao mesmo tempo dão 200 e um único usuário; o 429 de `/me` tem `Cache-Control: no-store`.
- **IT-08** — Dados os dois POST sem X-CSRF, quando chamados, então 400 `security.csrf_invalid` e nada é gravado ou enviado.

### 7.4 Testes de Contrato
- **CT-01** — Dado o dublê de `INotificationsApi` usado nos unitários, quando comparado ao contrato SPEC-0018@1 (assinatura, `EmailTemplate.MagicLink`, chaves `link` e `minutes`, códigos de erro), então batem; o IT-01 confirma contra o módulo real.
- **CT-03** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então `/api/v1/me` e `/api/v1/auth/logout` declaram 503 `service.unavailable`.
- **CT-02** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então os dois endpoints têm os corpos, status e códigos do §6, e `(cd web && pnpm api:check)` passa com o cliente regenerado.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa com o Mailpit, nos projetos mobile-chrome e mobile-safari: o fã em `/cidade-exemplo` toca em "Conta" e cai em `/entrar`; digita o e-mail e manda; vê "Confira seu e-mail" com o contador; o teste lê o link na API do Mailpit e o abre (o token continua válido depois do carregamento); toca em "Entrar no Subsolo" e volta para `/cidade-exemplo`. Agora "Conta" leva a `/conta`, que mostra o e-mail; "Sair" leva de volta a `/entrar`, e `/conta` passa a redirecionar para `/entrar?returnTo=/conta`. Sem violações do axe nas telas visitadas. O tempo do envio até "Confira seu e-mail" é ≤ 3 s p95 em 5 repetições. [jornada: entrar]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: e-mail inválido mostra "Digite um e-mail válido." ligado ao campo; o link reaproveitado mostra "Este link expirou ou já foi usado." com "Pedir outro link"; com o limite de IP baixo (configuração de teste), aparece "Muitas tentativas…"; com o Mailpit parado, aparece "Não conseguimos mandar o e-mail agora…". Sem violações do axe. [jornada: entrar]
- **E2E-03** — Dada a aplicação completa, nos projetos mobile-chrome e mobile-safari: o fã entra pelo link do Mailpit; o teste confere que o cookie `__Host-subsolo` é HttpOnly, que `localStorage` e `sessionStorage` não guardam nada da sessão e que a resposta de `/conta` tem `Cache-Control: no-store`; então apaga os cookies e os dados do site (`context.clearCookies()` e limpeza do storage) e recarrega `/conta` → cai em `/entrar?returnTo=/conta`, e "Conta" leva a `/entrar`. Em outro passo, depois de "Sair", o botão voltar do navegador não mostra o e-mail. [jornada: entrar]
- **E2E-04** — Dada a pilha do Compose com valores sentinela no ambiente do container `web` (injetados pelo `tools/compose-up.sh`: uma variável comum, uma com nome de segredo) e a URL interna da API, quando o teste baixa o HTML de `/`, `/entrar`, `/entrar/confirmar` e `/conta` e todo script `/_next/static/**` que eles referenciam, então nenhum desses valores aparece, nem `Password=` ou `ConnectionStrings`.

### 7.6 Outros
- Acessibilidade: axe nas quatro telas (E2E-01 e E2E-02).
- Sessão e variáveis de ambiente: pedidos do Thomas no H1 (2026-10-17) — E2E-03, E2E-04 e UT-05.
- Herança da revisão da SPEC-0014 (Emenda 2): (a) a validade do cookie segue a da sessão no banco — cookie sem janela própria menor que a da sessão (hoje o sliding do ASP.NET renova só depois de 15 dias e pode expirar o cookie antes dos 30 dias de inatividade); (b) duas primeiras entradas simultâneas com o mesmo e-mail não dão 500 (violação do índice único tratada, nova busca); (c) teste unitário de `PublishUserRegistered` (campos, mesmo Id, sem e-mail); (d) 503 `service.unavailable` declarado no OpenAPI de `/me` e `/logout`; (e) `ILogger` obrigatório no `SignInService`; (f) 429 de `/me` também com `no-store`. Da revisão da Emenda v3 da 0014: (g) `unstable_rethrow` também no `catch` de fora de `getCityAgenda` e `listCities` (ou ajustar o comentário); (h) `DEFAULT_RATE_LIMIT` do `tools/compose-up.sh` limitado a 2147483647. Os testes desses itens entram no Red desta spec com a tag `SPEC-0016:UT-06` (c, e), `SPEC-0016:IT-09` (a, b, f) e `SPEC-0016:CT-03` (d).
- Segurança: checklist OWASP de autenticação no G4 (enumeração de contas, redirecionamento aberto, força bruta, fixação de sessão).

**Dublês e dados de teste:** dublê de `INotificationsApi` nos unitários; Mailpit real no IT e no E2E; e-mails `@subsolo.test`; limites de taxa reduzidos por configuração só no ambiente de teste.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL e Mailpit); E2E contra a pilha do Compose (`https://localhost:8443`) com `MAILPIT_URL` exposto ao Playwright; `--workers=1`.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** migração aditiva no schema `users` (tabela `magic_link_tokens`).
- **Compatibilidade:** endpoints novos em `/api/v1`; a aba "Conta" já existia no design e passa a ter destino.
- **Observabilidade:** contadores `subsolo.users.magic_links_requested` (resultado: enviado, limitado, falha) e `subsolo.users.magic_links_consumed` (resultado: ok, inválido); alerta no Seq para falhas de envio acima de 5 em 10 min (mesmo canal dos alertas da SPEC-0009).
- **Rollback:** reverter o merge commit; a tabela nova pode ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Telas Entrar e Confira seu e-mail — geradas no Stitch em 2026-10-17 e apresentadas no H1 com os ajustes do §6 (aprovação junto com o H1)
- [x] Para onde volta quem entra? — Para a página de onde saiu (`returnTo`); sem ela, para `/` (recomendação do Architect, 2026-10-17; confirmada no H1)
- [x] Telas Quase lá e Link inválido — o Thomas exige tela no Stitch para toda funcionalidade com interface (2026-10-17); geradas no mesmo dia e listadas no §6
- [x] Sessão e variáveis de ambiente — o Thomas pediu (2026-10-17) que apagar os cookies deslogue de verdade e que nenhuma variável de ambiente chegue ao navegador: E2E-03, E2E-04 e UT-05

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Slices do link mágico**
- [ ] Red: UT-01..UT-03 com a tag `SPEC-0016:<ID>`, falhando pelo motivo certo
- [ ] Green: `RequestMagicLink`, `ConsumeMagicLink`, limite por e-mail, saneamento do `returnTo`
- [ ] Refactor mantendo tudo verde

**Fase 2: API, persistência e contratos**
- [ ] Red: IT-01..IT-09, CT-01..CT-03 e UT-06 falhando pelo motivo certo
- [ ] Green: tabela `magic_link_tokens`, endpoints, limpeza periódica, OpenAPI e cliente TS regenerados
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 3: Telas e jornada E2E**
- [ ] Red: UT-04, UT-05 e E2E-01..E2E-04 falhando pelo motivo certo
- [ ] Green: `/entrar`, "Confira seu e-mail", `/entrar/confirmar`, link inválido e `/conta` mínima conforme as telas do Stitch e os ajustes do §6; navegação conforme a sessão; sentinelas no `compose-up.sh`
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte + E2E nos projetos mobile (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 54ab48c (árvore suja) | 2026-10-17 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0016`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 (escopo e testes) | 2026-10-17 | Entram os 6 achados menores da revisão da SPEC-0014 (lista no §7.6) com UT-06, IT-09 e CT-03; `touches` ganha `src/Subsolo.Api/Security/**` e `tests/Security/**` | A 0016 é a primeira consumidora real da entrada e da sessão (corrida na primeira entrada, janela do cookie) e já mexe no módulo `Users` | nenhuma | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
| 1 (ordem) | 2026-10-17 | `depends_on` ganha SPEC-0018: a 0016 roda depois do módulo Notifications implementado | O IT e o E2E desta spec usam o Notifications real com o Mailpit, e as duas specs regravam os mesmos `packages.lock.json` de teste; sem a ordem, o `waves` punha a 0018 depois da 0016 | nenhuma | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
