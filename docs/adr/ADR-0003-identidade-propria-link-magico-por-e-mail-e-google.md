---
id: ADR-0003
title: Identidade própria: link mágico por e-mail e Google
status: accepted
origin: decision
date: 2026-10-13
pillars: [seguranca-acesso]
baseline: seguranca-acesso@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: testes de autorização (acesso negado) em cada spec + teste de arquitetura de endpoints sem política explícita
---

# ADR-0003 — Identidade própria: link mágico por e-mail e Google

## Contexto e Problema
Autenticação e autorização improvisadas em cada tela ou endpoint geram brechas difíceis de achar depois. O Subsolo tem três perfis de uso: o fã (navega sem conta e entra para seguir e marcar "vou"), o artista/produtor (aprovado manualmente, cadastra shows) e o admin (aprova, modera, cadastra em nome de terceiros). O Thomas escolheu login por link mágico no e-mail + Google, sem senha.

## Direcionadores da Decisão
- Login sem senha, de um toque no celular (escolha do usuário: link no e-mail + Google).
- Nenhum serviço extra consumindo RAM na VPS.
- Identidade dentro do monólito (módulo `Users`), com as regras de permissão num só lugar.
- Proteção de dados pessoais (ADR-0008).

## Opções Consideradas
- Módulo `Users` próprio com a autenticação do ASP.NET Core (cookie) + Google OIDC + link mágico
- Keycloak (como no curso Evently do Milan Jovanovic)
- Auth.js no Next.js

## Resultado da Decisão
**Opção escolhida:** módulo `Users` próprio (recomendação aceita pelo Thomas em 2026-10-13), porque não exige serviço extra, mantém as permissões no back-end e suporta link mágico sem extensões.

- **Sessão:** cookie `__Host-` HttpOnly, Secure e SameSite=Lax emitido pela API no mesmo domínio do front (o Caddy roteia `/api` para a API; ver ADR-0004), com expiração deslizante de 30 dias e revogação pelo servidor (sessões guardadas no banco).
- **Google:** OpenID Connect (handler `Microsoft.AspNetCore.Authentication.Google`/OIDC), vinculando a conta pelo e-mail verificado.
- **Link mágico:** token aleatório de 256 bits, guardado só como hash, de uso único, válido por 15 minutos e com limite de envio por e-mail e por IP.
- **Papéis:** `fan` (padrão), `artist` (concedido quando um perfil de banda/casa/produtor é aprovado e vinculado) e `admin`. A autorização por recurso (só quem é dono do perfil edita os shows dele) é feita no handler do caso de uso.
- **CSRF:** cookie SameSite=Lax + exigência de cabeçalho anti-forgery nas mutações.

**Regras (verificáveis):**
- Autenticação centralizada no módulo `Users`; não há senha armazenada.
- Autorização negada por padrão: todo endpoint declara `RequireAuthorization(<política>)` ou `AllowAnonymous()` explicitamente (teste de arquitetura).
- Menor privilégio para usuários, serviços e credenciais de CI.
- Sessões e tokens com expiração e revogação; o logout invalida a sessão no servidor.
- O plano de testes de cada spec com regra de acesso inclui o caso de acesso negado.
- Os riscos do OWASP Top 10 aplicáveis são checados na revisão (G4).
- Limite de taxa (rate limiting do ASP.NET Core) no login, no envio de link e nas denúncias.

### Consequências
- **Boa**, porque o login custa zero, não usa RAM extra, e o fluxo "navega sem conta → entra num toque" fica sob controle do produto.
- **Ruim**, porque a segurança da autenticação passa a ser responsabilidade do projeto (tokens, sessões, rate limit), e SSO corporativo ou login social extra exigiriam trabalho que o Keycloak já entregaria.

### Confirmação (G3)
Teste de arquitetura: nenhum endpoint sem política explícita. Testes de integração dos casos de acesso negado em cada spec. Checklist OWASP no G4.

## Prós e Contras das Opções
| Critério (peso) | Users próprio | Keycloak | Auth.js |
|---|---|---|---|
| RAM/infra na VPS (3) | nenhuma extra | 0,5–1 GB + banco | nenhuma extra |
| Link mágico (3) | nativo, sob controle | precisa de extensão | nativo |
| Permissões num só lugar (3) | sim (.NET) | sim | dividido entre front e back |
| Esforço de segurança próprio (2) | médio | baixo | médio |
| Aderência ao curso do Milan (1) | parcial | total | nenhuma |

## Mais Informações
- Autenticação e cookies no ASP.NET Core 10 — https://learn.microsoft.com/aspnet/core/security/authentication/ (documentação oficial; consulta de versão em 2026-10-13: .NET 10.0.12).
- Referências neutras: OWASP ASVS, OWASP Top 10, OWASP Cheat Sheet de autenticação e sessão.
