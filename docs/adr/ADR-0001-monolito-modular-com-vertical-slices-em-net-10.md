---
id: ADR-0001
title: Monólito modular com vertical slices em .NET 10
status: accepted
origin: user
date: 2026-10-13
pillars: [arquitetura]
baseline: arquitetura@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: tests/Subsolo.ArchitectureTests (ArchUnitNET), rodando no CI
---

# ADR-0001 — Monólito modular com vertical slices em .NET 10

## Contexto e Problema
Sem fronteiras explícitas e verificadas, os módulos se acoplam aos poucos e cada mudança passa a exigir mexer em tudo. O Subsolo começa pequeno (uma cidade, uma pessoa com agentes de IA, uma VPS), mas precisa crescer por partes: carona, cadastro pelo flyer com IA, WhatsApp e merch são próximos passos já previstos. O Thomas definiu o estilo: **Modular Monolith com Vertical Slices, como ensina o Milan Jovanovic**, em .NET.

## Direcionadores da Decisão
- Escolha do usuário (origin: user): monólito modular no estilo Milan Jovanovic, com vertical slices.
- Novas capacidades (IA, WhatsApp, carona, merch) devem entrar como módulos ou canais novos, sem reescrever os existentes.
- Um único deploy numa VPS: um processo de API, sem custo de sistema distribuído.
- Agentes de IA trabalham em paralelo: fronteiras claras reduzem conflito entre specs (`touches`).

## Opções Consideradas
- Monólito modular com vertical slices dentro de cada módulo (escolha do usuário)
- N/A — escolha do usuário; alternativas (microsserviços, camadas tradicionais) não reabertas

## Resultado da Decisão
**Opção escolhida:** monólito modular em .NET 10 (LTS) com vertical slices, porque é a escolha do Thomas e atende a todos os direcionadores.

**Módulos iniciais** (cada um com schema próprio no PostgreSQL, ver ADR-0002):

| Módulo | Responsabilidade |
|---|---|
| `Users` | contas, login (link mágico, Google), sessões, papéis (fã, artista/produtor, admin) |
| `Profiles` | perfis de banda, casa e produtor; pedido e aprovação de cadastro; perfil sem dono e reivindicação; links para redes e Metal Archives |
| `Events` | cidades, estilos, shows (data, horário, casa, line-up, preço, ingresso, flyer, situação), agenda por cidade, histórico |
| `Engagement` | seguir cidade/banda/casa; "vou" e contagem |
| `Notifications` | inscrições de push, e-mail, aviso de show novo, lembrete na véspera; canais plugáveis (WhatsApp depois) |
| `Moderation` | denúncias de show falso ou cancelado e a fila de decisão do admin |

**Estrutura** (padrão Milan Jovanovic, organização por feature):

```
src/
  Subsolo.AppHost/                 ← Aspire (ADR-0004)
  Subsolo.ServiceDefaults/         ← OpenTelemetry, health checks, resiliência
  Subsolo.Api/                     ← host único: compõe os módulos
  Common/
    Subsolo.Common.Domain/         ← Entity, Result, DomainEvent
    Subsolo.Common.Application/    ← ICommand/IQuery + handlers, decorators, IEventBus
    Subsolo.Common.Infrastructure/ ← outbox/inbox, Quartz, EF, armazenamento de arquivos
    Subsolo.Common.Presentation/   ← IEndpoint, mapeamento de Result → HTTP
  Modules/<Módulo>/
    Subsolo.Modules.<M>.Domain/
    Subsolo.Modules.<M>.Application/      ← Features/<CasoDeUso>/ (comando, handler, validador) = vertical slice
    Subsolo.Modules.<M>.Infrastructure/   ← DbContext do schema, repositórios, jobs
    Subsolo.Modules.<M>.Presentation/     ← endpoints Minimal API por feature
    Subsolo.Modules.<M>.IntegrationEvents/ ← contrato público do módulo
web/                                ← Next.js (ADR-0013)
```

**Comunicação entre módulos:** somente por eventos de integração (`<M>.IntegrationEvents`), publicados via **outbox** transacional no schema do módulo e consumidos com **inbox** idempotente; os jobs de despacho rodam com Quartz.NET dentro do processo. Leituras síncronas entre módulos, quando inevitáveis, passam por uma interface pública em `IntegrationEvents` (por exemplo `IProfilesApi`), nunca acessando o banco do outro módulo.

**Mediação sem MediatR/MassTransit:** abstrações próprias e finas (`ICommandHandler<T>`, `IQueryHandler<T>`, decorators de validação e log, `IEventBus` em processo). O MediatR 13+ e o MassTransit 9 passaram a ter licença comercial/RPL, e o próprio Milan Jovanovic recomenda avaliar isso; para o Subsolo, as abstrações próprias custam poucas dezenas de linhas e evitam o risco de licença.

**Regras (verificáveis):**
- Cada módulo expõe um contrato público (`Subsolo.Modules.<M>.IntegrationEvents`); os demais só dependem desse contrato.
- Dependências proibidas, verificadas por teste de arquitetura no CI:
  - `<M>.Domain` não depende de Application, Infrastructure, Presentation nem de EF Core/ASP.NET.
  - `<M>.Application` não depende de Infrastructure nem de Presentation.
  - Um módulo não referencia projetos de outro módulo, exceto `<Outro>.IntegrationEvents`.
  - `IntegrationEvents` não depende de nada além de `Common.Application` e dos tipos de resultado `Result`, `Result<T>`, `Error` e `ErrorType` de `Common.Domain` (esclarecimento de 2026-10-17, SPEC-0018: interfaces públicas como `INotificationsApi` devolvem `Result`, como os slices; entidades e eventos de domínio continuam proibidos).
  - Sem ciclos entre módulos, contando as dependências de `IntegrationEvents`. Quando um módulo A já depende de B e B precisa reagir a algo de A, o evento é definido no contrato de **B** (o consumidor) e publicado por A (esclarecimento de 2026-10-18, SPEC-0021: `ArtistRoleGranted` fica em `Users.IntegrationEvents` e é publicado pelo `Profiles`).
  - Tipos de Domain/Application/Infrastructure de um módulo são `internal` sempre que possível (`sealed` por padrão).
- Cada `DbContext` acessa apenas o schema do seu módulo.
- Não há ciclos de dependência entre módulos.
- Um caso de uso = uma pasta em `Features/` (comando/consulta, handler, validador e endpoint correspondentes).
- Mudar uma fronteira exige um ADR novo (que substitui este).

### Consequências
- **Boa**, porque WhatsApp vira um canal novo em `Notifications`, cadastro por flyer com IA vira uma feature/módulo novo, e carona vira o módulo `Rides`, sem tocar no resto.
- **Boa**, porque fronteiras testadas permitem que agentes trabalhem em módulos diferentes sem conflito, e extrair um módulo para outro serviço no futuro fica barato.
- **Ruim**, porque há mais projetos e mais cerimônia (outbox, inbox, eventos) do que num monólito simples, e consistência entre módulos é eventual (por exemplo, a contagem de seguidores pode atrasar alguns segundos).

### Confirmação (G3)
Testes de arquitetura com **ArchUnitNET** (`TngTech.ArchUnitNET.xUnitV3` 0.13.4) em `tests/Subsolo.ArchitectureTests`, rodando no CI (comando `arch_test`). O NetArchTest, usado nos cursos do Milan, foi descartado porque não tem release desde 2021.

## Prós e Contras das Opções
N/A — escolha do usuário (origin: user). Riscos registrados: complexidade inicial maior (mitigada pelo épico de fundação com o esqueleto pronto) e consistência eventual entre módulos (mitigada por contagens recalculáveis e pela inbox idempotente).

## Mais Informações
- .NET 10 é LTS, com suporte até 2028-11-14; patch atual 10.0.12 — https://dotnet.microsoft.com/platform/support/policy/dotnet-core (verificado em 2026-10-13).
- Licença do MediatR/MassTransit — https://www.milanjovanovic.tech/blog/mediatr-and-masstransit-going-commerical-what-this-means-for-you (verificado em 2026-10-13).
- ArchUnitNET xUnit v3 0.13.4 e NetArchTest.Rules 1.3.2 (publicado em 2021-05-23) — nuget.org (verificado em 2026-10-13).
- Quartz.NET 4.5.0 e FluentValidation 12.2.0 (Apache-2.0) — nuget.org (verificado em 2026-10-13).
- Referência neutra: C4 model (diagramas de contexto e contêiner).
