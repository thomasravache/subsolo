---
id: SPEC-0007
title: Base do monólito modular com Aspire
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0005]
consumes_contract: []
contract_version: 2
touches: [src/Common/**, src/Subsolo.Api/**, src/Subsolo.ServiceDefaults/**, src/Subsolo.AppHost/**, tests/Common/**, tests/Subsolo.ArchitectureTests/**, docs/api/**, Directory.Packages.props, Subsolo.slnx]
adrs: [ADR-0001, ADR-0002, ADR-0004, ADR-0010]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0007 — Base do monólito modular com Aspire

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Constrói a base do monólito modular (ADR-0001) e o AppHost do Aspire (ADR-0004): os blocos `Common` (Result/Error, abstrações de comando e consulta com decorators, endpoints, outbox e inbox com Quartz, base de EF por schema), o host `Subsolo.Api`, os `ServiceDefaults` e o `Subsolo.AppHost`, que sobe localmente a API, o Next.js, o PostgreSQL 18, o Seq e o Mailpit com `aspire run`. Os testes de arquitetura passam a garantir as regras do ADR-0001 para qualquer módulo novo.

## 2. Motivação & Escopo
**Motivação:** todo módulo do produto repete as mesmas peças; construí-las uma vez, testadas e com regras de arquitetura automáticas, é o que permite aos agentes criar módulos em paralelo sem divergir.

**Objetivos (dentro do escopo):**
- `Subsolo.Common.Domain`: `Entity`, `Result`/`Result<T>`, `Error` (tipos: Validation, NotFound, Conflict, Forbidden, Unauthorized), `IDomainEvent`.
- `Subsolo.Common.Application`: `ICommand`/`ICommand<T>`/`IQuery<T>` e handlers; decorators de validação (FluentValidation 12) e de log; `IIntegrationEvent`, `IEventBus`, `IIntegrationEventHandler<T>`; `IDateTimeProvider` via `TimeProvider`.
- `Subsolo.Common.Infrastructure`: base `ModuleDbContext` (schema por módulo), tabelas `outbox_messages`/`inbox_messages` por schema, interceptor que grava os eventos de domínio na outbox na mesma transação, jobs Quartz `ProcessOutbox`/`ProcessInbox` por módulo. (`IFileStorage` saiu na Emenda v1: entra com a feature de flyer do épico SPEC-0002, junto com os testes do ADR-0015.)
- `Subsolo.Common.Presentation`: `IEndpoint`, descoberta de endpoints por módulo, `Result` → `ProblemDetails` (RFC 9457).
- `Subsolo.Api`: compõe os módulos via `services.AddXModule(config)` / `app.MapXModuleEndpoints()`, OpenAPI em `/openapi/v1.json`, health checks.
- `Subsolo.ServiceDefaults`: OpenTelemetry, health checks, resiliência de HttpClient e service discovery (template do Aspire 13.6).
- `Subsolo.AppHost`: PostgreSQL 18 (volume persistente local), Seq, Mailpit, API (com as migrações aplicadas no start **só em desenvolvimento**) e `web` via `AddJavaScriptApp`/pnpm.
- Um módulo de exemplo `Modules/_Template` (só nos testes) prova as regras e a ponta a ponta da outbox.
- Regras de arquitetura do ADR-0001 em `tests/Subsolo.ArchitectureTests`, aplicadas automaticamente a todo assembly `Subsolo.Modules.*`.

**Não-objetivos (fora do escopo):**
- Módulos de negócio (Events entra na SPEC-0008; os demais, no épico SPEC-0002).
- Autenticação (módulo Users, no épico do produto); a política de negar por padrão é preparada na SPEC-0010.
- Publicação em Docker Compose (SPEC-0008) e deploy (SPEC-0003).

## 3. Dependências
- **Implementações necessárias:** SPEC-0005 — os testes desta spec usam o `PostgresFixture`, o `FakeTimeProvider` e o helper `ArchitectureRules`.
- **Contratos consumidos:** N/A — os comandos de build vêm da SPEC-0004, implementada antes da SPEC-0005.
- **Pré-requisitos externos:** Aspire CLI 13.6 instalada localmente; Docker.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; padrão do ADR-0001 (monólito modular com vertical slices, estilo Milan Jovanovic) e do ADR-0002 (schema por módulo).

**Decisão:** blocos `Common` próprios, com mediação sem MediatR (handlers e decorators registrados por varredura de assembly com Scrutor, MIT), outbox/inbox por módulo e Quartz no processo.

**Justificativa:** reproduz o padrão do Milan Jovanovic sem as bibliotecas com licença comercial (ADR-0001) e garante a consistência entre a gravação e a publicação de eventos.

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** MediatR 13+ e MassTransit 9 (licença; ADR-0001); Wolverine (outro framework inteiro para aprender, e foge do padrão escolhido).

**ADRs:** ADR-0001, ADR-0002, ADR-0004, ADR-0010

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** evento publicado na outbox é entregue ao handler de outro módulo em ≤ 5 s com o job a cada 2 s — medido no IT-03.
- **Segurança:** `ProblemDetails` nunca expõe stack trace fora de Development — verificado no IT-05.
- **Privacidade e dados pessoais:** o decorator de log não registra o payload dos comandos, só o nome e o resultado — verificado no UT-04.
- **Disponibilidade e resiliência:** inbox idempotente (o mesmo evento entregue duas vezes é processado uma vez) e retry da outbox até o sucesso, sem reprocessar depois — IT-04 (o intervalo crescente do backoff é implementado, mas não medido: não é verificável de forma determinística).
- **Acessibilidade (UI):** N/A — sem tela nesta spec.
- **Custo:** zero; tudo local (ADR-0012).

## 6. Artefato A — Contrato
**Interface:** `blocos Common + registro de módulo + AppHost` (consumidos por todo módulo)

```csharp
// Domain
public abstract class Entity { IReadOnlyList<IDomainEvent> DomainEvents; void Raise(IDomainEvent e); }
public sealed record Error(string Code, string Description, ErrorType Type); // Validation|NotFound|Conflict|Forbidden|Unauthorized|Failure
public class Result { bool IsSuccess; Error Error; } public class Result<T> : Result { T Value; }

// Application (vertical slice = Features/<CasoDeUso>/{Command, Handler, Validator})
public interface ICommand : IBaseCommand; public interface ICommand<TResponse>;
public interface ICommandHandler<in TCommand> { Task<Result> Handle(TCommand c, CancellationToken ct); }
public interface IQueryHandler<in TQuery, TResponse> { Task<Result<TResponse>> Handle(TQuery q, CancellationToken ct); }
// decorators: ValidationDecorator (FluentValidation → Error.Validation com a lista de campos), LoggingDecorator
public interface IIntegrationEvent { Guid Id; DateTime OccurredOnUtc; }
public interface IEventBus { Task PublishAsync<T>(T e, CancellationToken ct) where T : IIntegrationEvent; }
public interface IIntegrationEventHandler<in T> where T : IIntegrationEvent { Task Handle(T e, CancellationToken ct); }
public interface IDomainEventHandler<in T> where T : IDomainEvent { Task Handle(T e, CancellationToken ct); }   // ProcessOutbox → IDomainEventHandler → IEventBus → inbox do outro módulo → ProcessInbox → IIntegrationEventHandler

// Infrastructure
public abstract class ModuleDbContext : DbContext { protected ModuleDbContext(DbContextOptions options, string schema); }   // HasDefaultSchema(schema) + outbox/inbox
services.AddCommonInfrastructure(configuration);              // uma vez no host (connection string "subsolo", Quartz, EventBus)
services.AddModule<TDbContext>(configuration, params Assembly[] moduleAssemblies);   // DbContext, handlers, decorators, jobs da outbox/inbox do módulo

// Presentation
public interface IEndpoint { void MapEndpoint(IEndpointRouteBuilder app); }
// Result → HTTP: Validation 400 | NotFound 404 | Conflict 409 | Unauthorized 401 | Forbidden 403 | Failure 500 (ProblemDetails, code em "extensions.code")

// Registro de módulo (padrão)
services.AddEventsModule(configuration);   // chama AddModule<EventsDbContext>(...) e services.AddEndpoints(typeof(...).Assembly)
app.MapEndpoints();                         // uma vez no host: mapeia todo IEndpoint registrado
// Result → HTTP: ApiResults.Problem(result) ; Error.ValidationErrors (campo → mensagens)
```

```text
AppHost (aspire run):
  recursos: postgres (18.6, volume subsolo-pg) → database por ambiente "subsolo"
            seq ; mailpit (contêiner axllent/mailpit com tag fixa; SMTP 1025, UI 8025)
            api (Subsolo.Api) → referencia postgres, seq, mailpit ; /health, /alive, /openapi/v1.json
            web (pnpm dev em web/) → referencia api (variável API_URL)
Erros:
  Docker parado → aspire run falha com a mensagem do Aspire e nenhum recurso sobe
  migração pendente em Development → aplicada no start ; em outros ambientes → nunca aplicada pela API
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Comando válido | comando de exemplo com dados válidos | handler executa e retorna `Result.Success` | UT-01 |
| Comando inválido | validador com 2 campos inválidos | `Error.Validation` com os 2 campos; handler não executa | UT-02 |
| Mapeamento de erro | cada `ErrorType` | status HTTP e `ProblemDetails` com `code` | UT-03, IT-05 |
| Log sem payload | comando com e-mail no payload | log registra nome e resultado, sem o e-mail | UT-04 |
| Evento de domínio → outbox | entidade levanta evento e é salva | linha na outbox na mesma transação; rollback não grava | IT-01 |
| Schema por módulo | DbContext de exemplo | tabelas criadas no schema do módulo | IT-02 |
| Entrega entre módulos | evento na outbox | handler do outro módulo executa em ≤ 5 s | IT-03 |
| Entrega duplicada | mesmo evento entregue 2× | inbox processa 1× | IT-04 |
| Erro interno fora de Development | exceção não tratada | 500 com `ProblemDetails` sem stack trace | IT-05 |
| Regras de arquitetura | assemblies `Subsolo.Modules.*` | regras do ADR-0001 passam; violação de exemplo falha | IT-06 |
| AppHost sobe tudo | `aspire run` / teste com `DistributedApplicationTestingBuilder` | api e web saudáveis; `/health` 200 | IT-07 |
| OpenAPI publicado | GET `/openapi/v1.json` | documento válido gerado | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado um `ICommandHandler` de exemplo decorado, quando recebe um comando válido, então retorna `Result.Success` e o decorator de validação não bloqueia.
- **UT-02** — Dado um validador que reprova 2 campos, quando o comando passa pelo `ValidationDecorator`, então o resultado é `Error.Validation` listando os 2 campos e o handler não é chamado.
- **UT-03** — Dado cada `ErrorType`, quando convertido pelo mapeador `Result` → HTTP, então o status corresponde à tabela do contrato e o `code` vai em `extensions.code`.
- **UT-04** — Dado um comando que contém um e-mail, quando passa pelo `LoggingDecorator`, então o log contém o nome do comando e o resultado, mas não o e-mail.

### 7.3 Testes de Integração
- **IT-01** — Dada uma entidade de exemplo que levanta um evento de domínio, quando `SaveChanges` é chamado num PostgreSQL real, então a outbox recebe a mensagem na mesma transação; se a transação for revertida, nada é gravado.
- **IT-02** — Dado o `ModuleDbContext` do módulo de exemplo, quando as migrações são aplicadas, então as tabelas (incluindo outbox e inbox) existem apenas no schema `template`.
- **IT-03** — Dado um evento de integração publicado pelo módulo A, quando o job da outbox roda (com `FakeTimeProvider` avançando), então o handler do módulo B é executado em ≤ 5 s de tempo simulado.
- **IT-04** — Dado o mesmo evento entregue duas vezes à inbox, quando o job da inbox roda, então o handler executa uma única vez.
- **IT-05** — Dado um endpoint de exemplo que lança uma exceção, quando chamado com `ASPNETCORE_ENVIRONMENT=Production` via `WebApplicationFactory`, então a resposta é 500 com `ProblemDetails` sem stack trace.
- **IT-06** — Dados os assemblies `Subsolo.Modules.*` e um assembly de violação de exemplo, quando os testes de arquitetura rodam, então as regras do ADR-0001 passam para os reais e falham para o de violação, nomeando a regra.
- **IT-07** — Dado o `Subsolo.AppHost`, quando iniciado com `DistributedApplicationTestingBuilder` (Aspire.Hosting.Testing), então os recursos `api` e `web` ficam saudáveis e `GET /health` da API retorna 200.

### 7.4 Testes de Contrato
- **CT-01** — Dada a API em execução, quando `GET /openapi/v1.json` é chamado, então o documento é OpenAPI 3.1 válido e igual ao snapshot versionado em `docs/api/openapi.v1.json` (contrato consumido pelo cliente TypeScript na SPEC-0008).

### 7.5 Testes E2E
- N/A — nenhuma jornada de usuário (UI ou API pública) é afetada; a primeira jornada vem no walking skeleton (SPEC-0008).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** módulos de exemplo `Template` e `TemplateB` apenas no projeto de testes; `FakeTimeProvider` para os jobs.

**Ambiente de execução:** local e CI, com PostgreSQL real via Testcontainers e AppHost via Aspire.Hosting.Testing.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — ainda não há produção (go-live na SPEC-0003).
- **Dados/schema:** migrações iniciais das tabelas outbox/inbox por módulo; em desenvolvimento, aplicadas no start; o bundle EF para o pipeline fica na SPEC-0003.
- **Compatibilidade:** N/A — primeira versão do contrato.
- **Observabilidade:** ServiceDefaults ativo; traces de comando, consulta e outbox visíveis no dashboard do Aspire e no Seq local.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Common (Domain/Application/Presentation)**
- [ ] Red: UT-01..UT-04 com a tag `SPEC-0007:<ID>`, falhando pelo motivo certo (scaffold `chore` com as assinaturas do contrato)
- [ ] Green: `Result`/`Error`, `Entity`, abstrações de comando/consulta, decorators de validação e log, mapeamento `Result` → `ProblemDetails`
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte (G2)

**Fase 2: Infraestrutura, outbox/inbox e arquitetura**
- [ ] Red: IT-01..IT-06 falhando
- [ ] Green: `ModuleDbContext`, outbox/inbox por schema, jobs Quartz, `IEventBus`, `IDomainEventHandler`, regras ArchUnitNET do ADR-0001; migrações dos módulos de exemplo (dublês em `tests/Common/Modules/**`, geradas pelo Implementer — artefato gerado, não asserção)
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Host e Aspire**
- [ ] Pré-requisito: Aspire CLI 13.6 instalada (`dotnet tool install -g Aspire.Cli`)
- [ ] Red: IT-07 e CT-01 falhando
- [ ] Green: `Subsolo.Api`, `Subsolo.ServiceDefaults`, `Subsolo.AppHost` (Postgres, Seq, Mailpit, api, web), OpenAPI com snapshot em `docs/api/openapi.v1.json`
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003; validação local pelo Compose quando aplicável
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-13 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0007`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 | 2026-10-16 | Emenda v1 (lacunas do esboço do contrato apontadas pelo Test-writer): `IDomainEventHandler<T>` como ponte outbox → evento de integração; registro `AddCommonInfrastructure` + `AddModule<TDbContext>`; endpoints com `AddEndpoints(assembly)` por módulo e `app.MapEndpoints()` uma vez no host (no lugar de `MapXModuleEndpoints`); `ModuleDbContext(DbContextOptions, schema)`; `ApiResults.Problem` e `Error.ValidationErrors`; `IFileStorage` sai para a feature de flyer (SPEC-0002), onde o ADR-0015 define os testes; backoff da outbox implementado mas não medido; Mailpit como contêiner com tag fixa; Quartz 4.5.1 (patch acima do citado no ADR-0001) | Contrato era esboço; testes precisavam de assinaturas concretas; código sem teste não entra | Nenhuma (primeira versão consumida pela SPEC-0008) | thomas (delegado: piloto automático, 2026-10-16, recomendações do Architect) |
