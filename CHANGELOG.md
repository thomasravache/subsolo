# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Cada entrada cita a spec de origem (`SPEC-NNNN`); gere-as com `spec_graph.py report SPEC-NNNN --changelog`.

## [Unreleased]

### Added
- Design system escuro e shell do app (SPEC-0011): identidade "Subsolo Dark" (`docs/design/DESIGN.md`, tokens Tailwind 4 com contraste WCAG testado, fontes self-hosted), componentes base acessíveis (Card, Badge, Chip, Header, Footer, BottomNav, CitySwitcher), shell aplicado à agenda, ícones do app e `GET /api/v1/cities` com a segunda cidade seed.
- Segurança base (SPEC-0010): API nega por padrão (políticas `fan`, `artist`, `admin`), anti-forgery por `X-CSRF`, cookies `__Host-`, rate limiting com `Retry-After`, CSP com nonce em todas as páginas do web (ADR-0017), pilha local por HTTPS com HSTS (https://localhost:8443) e checklist OWASP Top 10.
- Observabilidade base (SPEC-0009): logs e traces da API e do web por OTLP no Seq com um trace único até o banco, campos `module`/`feature`/`deployment.environment`, redator de dados pessoais (logs, spans, exceções e console), `/health` com PostgreSQL e outbox, alertas por e-mail ("Erros 5xx" e "Falha de job") com runbooks.
- Agenda da cidade, walking skeleton (SPEC-0008): `/{cidade}` renderizada no servidor a partir da API (`GET /api/v1/cities/{slug}/agenda`) e do PostgreSQL, com estado vazio, 404 e 503 reais, cliente TypeScript gerado do OpenAPI e Compose gerado pelo Aspire com Caddy (`tools/compose-up.sh`, http://localhost:8080) testado de ponta a ponta no CI.
- Base do monólito modular (SPEC-0007): blocos Common (Result/Error, comandos e consultas com decorators de validação e log, outbox/inbox por módulo com Quartz, schema por módulo, ProblemDetails), API com OpenAPI e health checks, ServiceDefaults com OpenTelemetry e AppHost do Aspire que sobe PostgreSQL 18, Seq, Mailpit, API e front com `aspire run`; regras de arquitetura do ADR-0001 aplicadas a todo módulo.
- Harness de testes (SPEC-0005): xUnit v3 + Shouldly, PostgreSQL 18 e Mailpit reais via Testcontainers, testes de arquitetura com ArchUnitNET, Vitest + Testing Library no front, Playwright com verificação de acessibilidade (axe) em mobile e desktop, cobertura das linhas alteradas (`tools/coverage.sh`) e os comandos de teste do sdd-config.
- Pipeline de CI no GitHub Actions (SPEC-0006): build, lint, testes unitários, de integração e E2E, segurança (gitleaks, Semgrep, auditoria NuGet/npm), CodeQL e verificação do processo SDD em todo PR, como checks obrigatórios da `main`.
- Repositório e ferramentas (SPEC-0004): solution .NET 10 com Central Package Management, app Next.js 16.4 vazio, `tools/build.sh` e `tools/lint.sh`, hooks de commit (Conventional Commits + referência à spec) e de segredos (gitleaks), Dependabot, CODEOWNERS, ferramenta SDD vendorizada e proteção da `main` (PR obrigatório, somente merge commit).

### Fixed
- Ajustes do tooling (SPEC-0012): `.env.example` versionável, `.claude/` ignorado e testes dos hooks cobrindo `[no-spec]` e chave Google API.
