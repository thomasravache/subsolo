# Design system "Subsolo Dark"

Identidade visual do Subsolo: escura, crua e com a cara da cena (zine xerocado, cartaz de show), sem abrir mão de
acessibilidade (WCAG 2.2 AA). Fonte da verdade do design: o **Stitch**; fonte da verdade do código:
`web/src/styles/tokens.css` (Tailwind 4, `@theme`). Este documento liga os dois e diz como usar.

## Origem no Stitch (aprovada pelo Thomas em 2026-10-13)

- Projeto: "Subsolo — Agenda de shows underground" —
  <https://stitch.withgoogle.com/projects/15194126668426659178>
- Design system: "Subsolo Dark" (asset `16563151862933382783`).
- Telas aprovadas (capturas em `docs/design/stitch/`):

| Tela | Dispositivo | Screen no Stitch | Captura |
|---|---|---|---|
| Agenda da cidade (com o shell) | celular | `5d1946463ad9484b83c67cc2e6d78fd3` | [agenda-mobile.png](stitch/agenda-mobile.png) |
| Agenda da cidade (com o shell) | desktop | `a9c7a58a62a5431bb833a132609c0c70` | [agenda-desktop.png](stitch/agenda-desktop.png) |
| Página do show (estrutura) | celular | `98cc07285ec5439197d3273da0f7a11d` | [show-mobile.png](stitch/show-mobile.png) |
| Página do show (estrutura) | desktop | `ddae50bdb35f4cdaa2437a12f23fe257` | [show-desktop.png](stitch/show-desktop.png) |
| Admin - Cidades e Estilos (SPEC-0017) | desktop | `a14b19f0df03454483d4dd012d44ea8f` | pendente (capturada pelo Architect) |
| Minha Conta (SPEC-0019) | celular | `3b3765cf36c344f6bb48e52429570e12` | pendente (capturada pelo Architect) |
| Excluir Conta — confirmação (SPEC-0019) | celular | `aeb47861556747ba9bacc732f8c05ae8` | pendente (capturada pelo Architect) |

O HTML exportado pelo Stitch é só referência visual: os componentes são reescritos com os tokens, com HTML semântico
e com contraste verificado (nada de fonte de terceiros, CDN do Tailwind ou ícones por fonte remota).

## Tokens

### Cores

| Token | Valor | Uso |
|---|---|---|
| `--color-bg` | `#0B0B0C` | fundo da página (único tema: escuro) |
| `--color-surface` | `#161618` | cards, navegação inferior, campos |
| `--color-surface-raised` | `#1F1F22` | camadas acima da superfície (menus, link de pular) |
| `--color-text` | `#E8E2D4` | texto principal (osso, não branco puro) |
| `--color-text-muted` | `#A9A398` | texto secundário, rótulos, metadados |
| `--color-text-on-accent` | `#FFFFFF` | texto sobre o destaque (botão primário, chip ativo) |
| `--color-accent` | `#D7263D` | destaque: ação principal, chip ativo, dia do show, sublinhado do "Apoie" |
| `--color-danger` | `#FF5A5F` | situação "Cancelado" |
| `--color-warning` | `#F2A93B` | situação "Adiado" |
| `--color-border` | `#2E2E33` | divisórias e contornos decorativos |
| `--color-focus` | `#E8E2D4` | anel de foco (`:focus-visible`, 2 px, afastado 2 px) |

Utilitários do Tailwind gerados pelos tokens: `bg-bg`, `bg-surface`, `text-text`, `text-text-muted`, `bg-accent`,
`text-danger`, `border-border` etc.

### Tipografia

| Token | Família | Uso |
|---|---|---|
| `--font-display` (`font-display`) | Anton | títulos, wordmark, dia do show — sempre em caixa alta |
| `--font-body` (`font-body`, padrão do `body`) | Inter (variável) | texto corrido, line-up |
| `--font-label` (`font-label`) | Space Grotesk (variável) | rótulos, chips, selos, datas e números |

Fontes self-hosted (sem chamada a terceiros, compatível com a CSP), em `web/public/fonts/`, carregadas por
`next/font/local` no layout raiz com `font-display: swap` e subset latino:

| Arquivo | Família | Licença | Origem |
|---|---|---|---|
| `anton-latin-400-normal.woff2` | Anton 400 | SIL OFL 1.1 (`Anton-OFL.txt`) | Fontsource `@fontsource/anton` 5.3.0 |
| `inter-latin-wght-normal.woff2` | Inter variável 100–900 | SIL OFL 1.1 (`Inter-OFL.txt`) | Fontsource `@fontsource-variable/inter` 5.3.0 |
| `space-grotesk-latin-wght-normal.woff2` | Space Grotesk variável 300–700 | SIL OFL 1.1 (`SpaceGrotesk-OFL.txt`) | Fontsource `@fontsource-variable/space-grotesk` 5.3.0 |

Os arquivos são copiados sem modificação (só os WOFF2, não os pacotes npm); a licença acompanha cada fonte.

### Cantos e espaçamento

- Cantos: **4 px** em tudo (`--radius-sm`, `--radius-md`, `--radius-lg`; `rounded-md`). Só selos pequenos usam 2 px
  (`--radius-xs`, `rounded-xs`). Nada de cantos arredondados grandes nem pílulas.
- Espaçamento: escala de 4 px (`--space-1` = 4 px … `--space-12` = 48 px), a mesma da escala padrão do Tailwind
  (`p-1` = 4 px, `p-4` = 16 px). Margem lateral da página: 16 px. Alvo de toque: `--space-touch` = 44 px
  (`min-h-11`). Altura da navegação inferior: `--space-bottom-nav` = 64 px.

## Pares de contraste

Verificados pelo teste UT-01 (`web/src/components/ui/__tests__/tokens-contrast.test.ts`), que lê esta tabela: cada
linha com dois tokens (primeiro o da frente, depois o do fundo) e o mínimo exigido. Texto: ≥ 4,5:1; componentes,
ícones, anel de foco e texto grande (≥ 24 px, ou ≥ 18,66 px em negrito): ≥ 3:1.

| Uso | Frente | Fundo | Mínimo | Medido |
|---|---|---|---|---|
| Texto principal sobre o fundo | `--color-text` | `--color-bg` | 4,5:1 | 15,23:1 |
| Texto principal sobre o card | `--color-text` | `--color-surface` | 4,5:1 | 13,99:1 |
| Texto principal sobre a superfície elevada | `--color-text` | `--color-surface-raised` | 4,5:1 | 12,73:1 |
| Texto secundário sobre o fundo | `--color-text-muted` | `--color-bg` | 4,5:1 | 7,85:1 |
| Texto secundário sobre o card | `--color-text-muted` | `--color-surface` | 4,5:1 | 7,21:1 |
| Texto secundário sobre a superfície elevada | `--color-text-muted` | `--color-surface-raised` | 4,5:1 | 6,56:1 |
| Texto do botão primário e do chip ativo | `--color-text-on-accent` | `--color-accent` | 4,5:1 | 4,96:1 |
| "Cancelado" sobre o fundo | `--color-danger` | `--color-bg` | 4,5:1 | 6,45:1 |
| "Cancelado" sobre o card | `--color-danger` | `--color-surface` | 4,5:1 | 5,92:1 |
| "Adiado" sobre o fundo | `--color-warning` | `--color-bg` | 4,5:1 | 9,85:1 |
| "Adiado" sobre o card | `--color-warning` | `--color-surface` | 4,5:1 | 9,05:1 |
| Destaque (componente, texto grande) sobre o fundo | `--color-accent` | `--color-bg` | 3:1 | 3,97:1 |
| Destaque (dia do show, texto grande) sobre o card | `--color-accent` | `--color-surface` | 3:1 | 3,64:1 |
| Anel de foco sobre o fundo | `--color-focus` | `--color-bg` | 3:1 | 15,23:1 |
| Anel de foco sobre o card | `--color-focus` | `--color-surface` | 3:1 | 13,99:1 |
| Anel de foco junto ao botão primário | `--color-focus` | `--color-accent` | 3:1 | 3,84:1 |

Regras que saem da tabela:

- `--color-accent` **nunca** é cor de texto pequeno (3,97:1 não basta para 4,5:1): só em componentes, ícones, texto
  grande (dia do show em Anton 24 px) e sublinhados. Link de texto em destaque = texto `--color-text` com sublinhado
  `--color-accent` (ex.: "Apoie" no rodapé).
- `--color-border` (1,46:1) é decorativo: nenhum componente depende só da borda para ser reconhecido (o texto ou o
  preenchimento o identificam).
- A cor nunca é a única pista: "Cancelado" e "Adiado" são escritos no selo (com papel `status`), e o título do show
  cancelado é riscado.

## Componentes (`web/src/components/ui/`)

| Componente | Props principais | Regras |
|---|---|---|
| `Button` | `variant="primary\|secondary\|ghost"`, `size="md\|lg"` | alvo ≥ 44 px; `buttonClasses()` aplica o mesmo visual a um link |
| `Card` | `href flyerSrc? title date timeZone venue lineup styles status attendingCount` | campos do `ShowSummary`; data e hora no fuso da cidade (`timeZone` IANA); o card inteiro é clicável pelo link do título |
| `Badge` | `kind="style\|cancelled\|postponed"` | `cancelled`/`postponed` têm papel `status` e o texto "Cancelado"/"Adiado" |
| `Chip` | `selected onToggle` | filtro com `aria-pressed`; ativo = preenchido com o destaque |
| `EmptyState` | `title hint action?` | moldura tracejada; mensagem em texto principal, dica em rótulo |
| `Header` | `city cities onCityChange` | wordmark "Subsolo", navegação (só desktop), `CitySwitcher` |
| `CitySwitcher` | `city cities onCityChange` | `<select>` nativo com rótulo "Cidade" (teclado e leitor de tela sem script extra) |
| `Footer` | `links={apoie, instagram, privacidade}` | "Apoie" em destaque; Instagram abre em nova aba (avisado ao leitor de tela) |
| `BottomNav` | `city current?` | navegação inferior fixa, só no celular (< 768 px), com `aria-current`; o item ativo tem texto claro, ícone e barra superior no destaque |
| `AppShell` | `cities` | link "Pular para o conteúdo", `Header`, conteúdo, `Footer`, `BottomNav`; a cidade atual vem da URL |

Painel do admin (`web/src/components/admin/`, SPEC-0017):

| Componente | Props principais | Regras |
|---|---|---|
| `AdminNav` | `current` | menu do painel só com as páginas que existem, `aria-current="page"`; coluna lateral no desktop, linha no celular |
| `AdminPanel` | `cities styles` | colunas CIDADES e ESTILOS (lado a lado a partir de 1024 px) e a região `role="status"` (`aria-live="polite"`) das mensagens de sucesso |
| `CitiesPanel` | `initialCities onSuccess` | tabela Nome/UF/Fuso/Endereço; "Nova cidade" com a prévia `agendasubsolo.com.br/{slug}` gerada do nome (endereço apagado volta ao gerado); "Editar" com o endereço só leitura |
| `StylesPanel` | `initialStyles onSuccess` | estilos em chips com "Renomear"; campo "Novo estilo" + "Adicionar" |
| `Field` | `id label error? hint?` | rótulo visível; erro e dica ligados ao controle por `aria-describedby`, com `aria-invalid` no erro |

Ajustes aprovados sobre a tela do Stitch: sem textos de exclusão nem a caixa "Regra da cena", sem contagens de shows,
menu só com o que existe, endereço só leitura na edição e o domínio decidido (`agendasubsolo.com.br`, ADR-0004) no lugar de "subsolo.art".

Minha conta (`web/src/components/account/`, SPEC-0019):

| Componente | Props principais | Regras |
|---|---|---|
| `DisplayNameForm` | `initialName` | "Nome de exibição" + "Salvar"; sucesso anunciado em `role="status"` ("Nome salvo."); erro ligado ao campo pelo `Field` |
| `ExportDataButton` | — | "Baixar meus dados": baixa `subsolo-meus-dados.json` pela API; falha num alerta |
| `DeleteAccountDialog` | — | botão de contorno `--color-danger` "Excluir minha conta" e o `dialog` modal "Excluir conta?": foco preso (Tab/Shift+Tab giram dentro), foco de volta ao botão ao fechar, "Excluir para sempre" no botão primário (vermelho do destaque) |

Ajustes aprovados sobre as telas do Stitch: sem a engrenagem do topo, sem o selo "Entrou com Google" (entra com a spec
do Google) e "Sessão atual" só com o botão "Sair da conta", sem aparelho nem cidade (a localização não é guardada).

Ícones: SVG de traço inline (`icons.tsx`), decorativos (`aria-hidden`), sempre ao lado de um texto.

## Shell

- Layout de `(public)` (`web/src/app/(public)/layout.tsx`): `AppShell` com as cidades de `GET /api/v1/cities`
  (API fora do ar → cabeçalho sem seletor). Cada página traz o próprio `<main id="conteudo">`.
- Celular (< 768 px): cabeçalho fixo no topo (wordmark + seletor), navegação inferior fixa (Agenda, Apoie), conteúdo
  com folga de 64 px embaixo para não ficar sob a navegação.
- Desktop (≥ 768 px): cabeçalho completo (wordmark, Agenda, Apoie, seletor), sem navegação inferior; agenda em grade
  de 2–3 colunas com cards verticais.
- Foco: anel `--color-focus` em todo elemento focável (`:focus-visible` global). Não remova `outline` sem pôr outro
  indicador.

## Flyers de arte extrema

Flyers de metal underground usam logos ilegíveis, arte densa e formatos variados (A4 retrato, quadrado, paisagem).
Cortar o flyer corta informação (data, casa, line-up) e a arte. Por isso:

- **Nunca cortar:** a miniatura usa `object-fit: contain` (`object-contain`) dentro de uma moldura de proporção fixa
  (5:7 no celular, 4:5 no desktop) com fundo `--color-bg`; o que sobra vira tarja escura, não corte. `object-cover` é
  proibido em flyer.
- **Sem filtros na arte**, exceto no show cancelado (escala de cinza, com o selo "Cancelado" e o título riscado).
- **Texto nunca sobre o flyer:** título, data e selos ficam fora da imagem (a arte não garante contraste).
- **Texto alternativo:** "Flyer de {título do show}"; o card já traz os dados em texto.
- **Sem flyer:** placeholder escuro com o nome do show em Anton (`aria-hidden`, o título já é o nome acessível).
- Na página do show, o flyer aparece inteiro, com opção de abrir no tamanho original.

## Ícones do app

`web/src/app/favicon.ico` (16/32/48 px), `web/src/app/icon.png` (512 px) e `web/public/icons/` (192 e 512 px, para o
PWA): monograma "S" em Anton, osso sobre o fundo `#0B0B0C`, com a barra em `#D7263D`, derivado do wordmark do Stitch.
