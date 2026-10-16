# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Cada entrada cita a spec de origem (`SPEC-NNNN`); gere-as com `spec_graph.py report SPEC-NNNN --changelog`.

## [Unreleased]

### Added
- Harness de testes (SPEC-0005): xUnit v3 + Shouldly, PostgreSQL 18 e Mailpit reais via Testcontainers, testes de arquitetura com ArchUnitNET, Vitest + Testing Library no front, Playwright com verificação de acessibilidade (axe) em mobile e desktop, cobertura das linhas alteradas (`tools/coverage.sh`) e os comandos de teste do sdd-config.
- Pipeline de CI no GitHub Actions (SPEC-0006): build, lint, testes unitários, de integração e E2E, segurança (gitleaks, Semgrep, auditoria NuGet/npm), CodeQL e verificação do processo SDD em todo PR, como checks obrigatórios da `main`.
- Repositório e ferramentas (SPEC-0004): solution .NET 10 com Central Package Management, app Next.js 16.4 vazio, `tools/build.sh` e `tools/lint.sh`, hooks de commit (Conventional Commits + referência à spec) e de segredos (gitleaks), Dependabot, CODEOWNERS, ferramenta SDD vendorizada e proteção da `main` (PR obrigatório, somente merge commit).
