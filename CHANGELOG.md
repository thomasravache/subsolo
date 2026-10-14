# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Cada entrada cita a spec de origem (`SPEC-NNNN`); gere-as com `spec_graph.py report SPEC-NNNN --changelog`.

## [Unreleased]

### Added
- Repositório e ferramentas (SPEC-0004): solution .NET 10 com Central Package Management, app Next.js 16.4 vazio, `tools/build.sh` e `tools/lint.sh`, hooks de commit (Conventional Commits + referência à spec) e de segredos (gitleaks), Dependabot, CODEOWNERS, ferramenta SDD vendorizada e proteção da `main` (PR obrigatório, somente merge commit).
