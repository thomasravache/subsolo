---
id: ADR-0015
title: Armazenamento e processamento de flyers
status: accepted
origin: decision
date: 2026-10-13
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: teste de arquitetura (só Common.Infrastructure referencia NetVips e o sistema de arquivos) + testes de integração de upload
---

# ADR-0015 — Armazenamento e processamento de flyers

## Contexto e Problema
Cada show tem um flyer, geralmente uma imagem vertical pesada feita para story. Ele precisa aparecer leve no celular, na agenda e na página do show, e virar uma imagem de prévia no formato 1200×630 (Open Graph), para que o WhatsApp e o Instagram mostrem o flyer ao compartilhar. Arte extrema é bem-vinda; a moderação é humana.

## Direcionadores da Decisão
- Prévia de link com o flyer (WhatsApp, Instagram, Telegram).
- Leve no 4G: variantes em WebP e cache longo.
- Sem custo até o go-live (ADR-0012), na mesma VPS.
- Licença compatível (ADR-0009).

## Opções Consideradas
- Processamento: NetVips (libvips) · SixLabors.ImageSharp 4 · SkiaSharp
- Armazenamento: disco local (volume) servido pelo Caddy · object storage S3 compatível (Cloudflare R2)

## Resultado da Decisão
**Opção escolhida:** NetVips para processar e disco local (volume `media`) para armazenar, atrás de uma interface `IFileStorage`, para que a troca para o R2 no futuro seja só configuração.

- **Upload:** JPEG, PNG ou WebP, até 10 MB, com o tipo verificado pelo conteúdo (não pela extensão) e os metadados EXIF removidos (incluindo localização).
- **Variantes:** `thumb` (400 px de largura, WebP), `full` (1080 px de largura, WebP) e `og` (1200×630 JPEG: o flyer inteiro centralizado sobre um fundo escuro desfocado do próprio flyer, sem cortar a arte).
- **Nome:** hash do conteúdo (`/media/flyers/<hash>-<variante>.<ext>`), imutável, com `Cache-Control: public, max-age=31536000, immutable`.
- **Local:** o Aspire monta o volume e a API serve `/media`; em produção, quem serve é o Caddy (ADR-0004).
- **Backup:** o diretório `media` entra no restic (ADR-0011).

### Consequências
- **Boa**, porque as prévias saem bonitas sem cortar a arte, as páginas ficam leves, e não há custo nem dependência externa.
- **Ruim**, porque os arquivos ficam presos à VPS até a migração para object storage, e o NetVips traz um binário nativo (libvips, LGPL, ligado dinamicamente) para a imagem Docker.

### Confirmação (G3)
Teste de arquitetura: só `Common.Infrastructure` referencia NetVips e `System.IO` para mídia. Testes de integração: upload válido gera as três variantes; tipo falso e arquivo acima do limite são rejeitados; o EXIF é removido.

## Prós e Contras das Opções
| Critério (peso) | NetVips | ImageSharp 4 | SkiaSharp |
|---|---|---|---|
| Licença (3) | MIT (libvips LGPL dinâmica) | Six Labors Split License, que exige aceite; comercial acima de um teto de receita | MIT |
| Memória e velocidade (3) | a melhor | média | boa |
| Manutenção (2) | ativa (nativo 8.18.7 em 2026-09-28) | ativa | ativa |

| Critério (peso) | Disco local | Cloudflare R2 |
|---|---|---|
| Custo e conta antes do go-live (3) | zero, sem conta | zero até 10 GB, mas exige conta |
| Simplicidade (3) | alta | média |
| Escala e CDN (1) | não | sim |

## Mais Informações
- NetVips 3.2.0 e NetVips.Native 8.18.7 (2026-09-28); SixLabors.ImageSharp 4.1.3 com `requireLicenseAcceptance=true`; SkiaSharp 4.153.1 — nuget.org (verificado em 2026-10-13).
- Protocolo Open Graph — https://ogp.me (imagem recomendada de 1200×630 para prévias grandes; conhecimento prévio, não reverificado nesta data).
