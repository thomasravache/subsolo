---
id: ADR-0013
title: Front-end em Next.js 16 com PWA
status: accepted
origin: decision
date: 2026-10-13
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: E2E de prévia Open Graph e de instalação/push (Playwright) + compilação do cliente OpenAPI no CI
---

# ADR-0013 — Front-end em Next.js 16 com PWA

## Contexto e Problema
O fã usa o celular. A página de cada show precisa render no servidor para que o WhatsApp e o Instagram mostrem o flyer na prévia do link (Open Graph). Os avisos exigem uma PWA com service worker (Web Push). O cadastro do show precisa ser rápido (< 2 min) e o visual é escuro, com a cara da cena. As telas vêm do **Stitch** (mobile e desktop), escolha do Thomas.

## Direcionadores da Decisão
- SSR e metadados Open Graph por página (flyer na prévia).
- PWA instalável, com Web Push (Android e iPhone com o site na tela de início).
- TypeScript (escolha do usuário).
- Aproveitar o código exportado pelo Stitch (HTML + Tailwind).
- Roda em container na mesma VPS, atrás do Caddy (ADR-0004), orquestrado pelo Aspire (`Aspire.Hosting.JavaScript`).

## Opções Consideradas
- Next.js 16 (App Router, React 19)
- SvelteKit
- Astro 7

## Resultado da Decisão
**Opção escolhida:** Next.js 16 (recomendação aceita pelo Thomas em 2026-10-13), porque oferece SSR e `generateMetadata` para Open Graph prontos, roda em VPS com `output: 'standalone'`, tem o maior ecossistema e é onde os agentes de IA rendem mais.

- **Renderização:** páginas públicas (agenda da cidade, show, perfil) renderizadas no servidor com cache curto; o painel do artista/admin é interativo, com Server Actions apenas como ponte, porque a regra de negócio fica sempre na API .NET.
- **API:** o Next.js chama a API .NET pelo cliente tipado gerado do OpenAPI (`openapi-typescript` + `openapi-fetch`). Nenhuma regra de negócio é duplicada no front.
- **Estilo:** Tailwind CSS 4 com os tokens do design system definido no Stitch (tema escuro por padrão, contraste AA verificado).
- **PWA:** `manifest.webmanifest`, service worker próprio para push e cache mínimo da agenda; o botão "ativar avisos" só aparece depois de um gesto do usuário.
- **Compartilhar:** Web Share API no celular, com fallback para links de WhatsApp e cópia do link.
- **Estrutura:** `web/` organizada por feature (`app/(public)`, `app/(painel)`, `features/<nome>`), espelhando os módulos da API.

### Consequências
- **Boa**, porque as prévias com flyer, o SEO da agenda e a PWA saem com recursos nativos do framework.
- **Ruim**, porque o Next.js é mais pesado na VPS do que as alternativas (~150–250 MB de RAM) e parte das otimizações pensa no Vercel; no self-hosting, o cache fica no disco do container.

### Confirmação (G3)
E2E (Playwright) que lê as meta tags `og:image`/`og:title` da página do show; compilação do cliente OpenAPI no CI (contrato); regra de ESLint proibindo `fetch` direto para a API fora do cliente gerado.

## Prós e Contras das Opções
| Critério (peso) | Next.js 16 | SvelteKit | Astro 7 |
|---|---|---|---|
| SSR + Open Graph (3) | nativo | nativo | nativo |
| Interatividade do painel (2) | alta | alta | exige ilhas/framework extra |
| Maturidade da versão (3) | linha 16 estável desde 2025 | 3.0 lançado em 2026-10-01 (novo demais) | estável |
| Ecossistema e fluência dos agentes (3) | o maior | médio | médio |
| Peso na VPS (2) | maior | menor | menor |

## Mais Informações
- next 16.4.0 (publicado em 2026-10-06), react 19.3.0, @sveltejs/kit 3.0.1 (3.0.0 em 2026-10-01), astro 7.3.8, tailwindcss 4.3.3, openapi-fetch 0.17.0 — https://registry.npmjs.org (verificado em 2026-10-13).
- Self-hosting com `output: 'standalone'` — documentação do Next.js (deploying/self-hosting), não reverificada nesta data.
- Web Push no iOS exige o site adicionado à tela de início (Safari 16.4+) — conhecimento prévio, não reverificado nesta data.
