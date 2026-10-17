# Checklist de segurança (OWASP Top 10) — revisão G4

Modelo aplicado pelo Reviewer no G4 de toda spec que cria ou muda endpoint, página, dado pessoal, dependência ou
infraestrutura (ADR-0003, ADR-0008). Copie a tabela para o relatório de review, marque cada item como **OK**,
**N/A** (com o motivo) ou **Achado** (com severidade e arquivo/linha). Um item sem resposta reprova o G4.

Referência: OWASP Top 10:2021 (A01–A10), adaptado ao Subsolo: API .NET (Minimal APIs, módulos), web Next.js e
Caddy no Compose.

## Como verificar (comandos)

| O quê | Comando |
|---|---|
| Endpoint sem declaração de autorização | `dotnet test tests/Subsolo.ArchitectureTests` (regra `EndpointAuthorizationRules`) e `dotnet test tests/Security/Subsolo.Security.IntegrationTests` |
| 401/403/429/CSRF da API | `dotnet test Subsolo.slnx --filter "Kind=Integration"` |
| Cabeçalhos, CSP com nonce e HTTPS | `(cd e2e && pnpm test tests/security)` |
| Pacotes vulneráveis | `dotnet list package --vulnerable --include-transitive` e `(cd web && pnpm audit --audit-level high)` |
| Segredos no código | `gitleaks detect` (também no hook de pre-commit) |

## Checklist

| # | Risco (OWASP) | Verificação no Subsolo | Resultado |
|---|---|---|---|
| A01 | Controle de acesso quebrado | Todo endpoint novo declara `RequireAuthorization(<política>)` (`fan`, `artist`, `admin`) ou `AllowAnonymous()`; nada depende só da política de fallback. Público só o que a spec diz que é público. O plano de testes tem o caso de acesso negado (401 `auth.required` e 403 `auth.forbidden`). Autorização por recurso (dono do perfil/show) é feita no handler do caso de uso, não no front. | |
| A01 | CSRF | Mutações (POST/PUT/PATCH/DELETE) passam pelo `AntiforgeryFilter` (cabeçalho `X-CSRF` com o token de `GET /api/v1/antiforgery`); nenhuma mutação via GET. Cookies de sessão `SameSite=Lax`. | |
| A02 | Falhas criptográficas | Tráfego só por HTTPS (Caddy com HSTS, HTTP → 308). Cookies `__Host-`, `Secure`, `HttpOnly`. Tokens (link mágico, sessão) aleatórios com 256 bits e guardados só como hash. Nada de criptografia caseira. | |
| A03 | Injeção | Acesso a dados só pelo EF Core/consultas parametrizadas (sem SQL concatenado). Entrada validada (FluentValidation) na fronteira com código de erro. No web, nada de `dangerouslySetInnerHTML` com dado da API; a CSP com nonce bloqueia script injetado. | |
| A04 | Design inseguro | A spec tem os casos de abuso: limite de taxa nas rotas sensíveis (`auth` 5/min, `report` 10/h, `default` 100/min por IP), enumeração (respostas iguais para e-mail existente/inexistente), limites de tamanho e paginação. | |
| A05 | Configuração insegura | Fora de Development: ProblemDetails sem stack trace, OpenAPI fora do ar, migrações pelo pipeline. Cabeçalhos do web presentes (CSP com nonce, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, `frame-ancestors 'none'`). A API só é alcançável pelo Caddy (sem porta publicada no Compose). | |
| A06 | Componentes vulneráveis | Dependência nova tem ADR e versão fixa (lock files); sem vulnerabilidade alta/crítica conhecida nos comandos acima. Imagens com tag fixa. | |
| A07 | Falhas de identificação e autenticação | Sessão com expiração e revogação no servidor; logout invalida a sessão; rate limiting no login e no envio de link; nada de senha armazenada (ADR-0003). | |
| A08 | Falhas de integridade | Dependências pelos lock files com restore travado no CI; scripts de terceiros no web só com nonce (nenhum CDN externo sem decisão); pipeline sem segredos em logs. | |
| A09 | Falhas de log e monitoramento | Recusas 401, 403, 429 e CSRF registradas com o código (`Subsolo.Security`) e visíveis no Seq; logs sem dados pessoais (e-mail, IP, token, conteúdo de denúncia) — ADR-0008. | |
| A10 | SSRF | O servidor não busca URL informada pelo usuário; se precisar (ex.: prévia de link, flyer remoto), lista de destinos permitidos e bloqueio de redes internas. | |

## Dados pessoais (LGPD, ADR-0008)

| Verificação | Resultado |
|---|---|
| Dado pessoal novo está mapeado (finalidade, base legal, retenção) na spec | |
| O IP usado no rate limiting fica só em memória, sem persistir nem logar | |
| Exportação/exclusão do titular continuam cobrindo o dado novo | |
