# Product Catalog API

## Scope and context

Build a small, production-quality product catalog REST API. Prefer a complete, easy-to-run solution over additional infrastructure.
When available, read `../local-context/case-study.md` and `../local-context/vacancy.md` before planning or implementing features. Those files are private reference material outside this repository. Do not copy or commit them, their source PDF, employer details or application notes into this repository. Do not use `git add --force` to import them.
The assignment defines required behavior; the vacancy provides background. Explicit user instructions take priority. If private context is unavailable, use checked-in requirements/README; ask only about material missing requirements. Do not fabricate them.
The user has expanded the scope to a high-load production design, Kafka, RabbitMQ, JWT protection, Kubernetes manifests and CI/CD. When available, read `../local-context/implementation-plan.md`; the RabbitMQ business scenario and hosting target are explicitly deferred. Do not treat the proposed export as approved or replace mandatory endpoints with it.

## Layered architecture

Use one API host with four focused layers under `src/`, plus separate Worker hosts for agreed background processing:

- `ProductCatalog.Domain`: product model and domain rules; no ASP.NET Core, EF Core or infrastructure dependencies.
- `ProductCatalog.Application`: use cases and narrow persistence contracts; depends on Domain only.
- `ProductCatalog.Infrastructure`: EF Core/SQL Server persistence and a mock-data implementation of the same application contract; depends on Application and Domain.
- `ProductCatalog.Api`: HTTP endpoints/controllers, request/response DTOs, validation, error mapping and the DI composition root; depends on Application and references Infrastructure only to wire it up.

Keep business rules out of controllers and SQL concerns out of Application. Do not expose EF entities or IQueryable as the public API. Select the storage implementation at startup through configuration; do not scatter mock/database switches through use cases.

## SOLID and implementation practices

- Keep each class focused; inject dependencies through constructors and depend on small interfaces at real boundaries.
- Both storage implementations must obey the same observable contract. Avoid service locators, static mutable global state and inheritance used only to share incidental code.
- Prefer straightforward code and composition. Avoid generic repository frameworks, a redundant unit-of-work wrapper around DbContext, CQRS/MediatR, microservices or speculative abstractions unless a concrete requirement justifies them.
- Use nullable reference types, async I/O and CancellationToken propagation; avoid sync-over-async.
- Use decimal with explicit SQL precision for prices. Define and document ID type and validation limits.
- Separate transport DTOs from domain models; explicit mapping is sufficient for this small model.
- Validate at the HTTP boundary and enforce actual domain invariants within the domain. Never trust client input.
- Use structured ILogger logging and centralized ProblemDetails responses; never expose secrets, connection strings or stack traces to API consumers.
- Store environment-specific configuration outside source control. Include safe configuration examples, never real credentials.

## Runtime and packages

- Use the newest stable supported .NET LTS, not legacy .NET Framework or preview/RC. Verified baseline on 2026-10-22: .NET 10 LTS, ASP.NET Core 10 and EF Core 10 (`net10.0`). Before initial scaffolding, verify the official support policy: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core . Do not upgrade the major version on unrelated tasks.
- Pin an actual installed compatible SDK version in `global.json`; SDK versions differ from runtime patch versions. Use stable C# defaults for that SDK, not preview language features.
- Use EF Core SQL Server provider, migrations and deterministic, repeatable seed data. Keep compatible package majors aligned and inspect library compatibility before choosing versions.
- Prefer built-in DI, configuration, logging, validation and error handling. Add libraries only where they solve a demonstrated need.
- Use xUnit for tests, ASP.NET Core WebApplicationFactory for HTTP contract checks, and fakes/mocks at application boundaries. Add real SQL integration tests for persistence behavior that mocks cannot verify.

## HTTP contract

- Provide product listing v1, a paginated listing v2 (default page size 10), lookup by ID and partial update of Description. Keep other product fields unchanged during updates.
- A successful `GET /api/v1/products` returns every available product as one JSON array in a normal `200 OK` HTTP response, or `[]` when empty. Never substitute `202 Accepted`, a job ID, a file URL, a hidden row limit or a single page. Async JSON streaming is an implementation detail that preserves this contract.
- Read product GETs from SQL-primary for committed current data. Kafka projections and potentially stale product caches must not replace that consistency choice. GETs must not wait for brokers or background workers.
- For large v1 responses, use no-tracking DTO projection and bounded-memory asynchronous streaming, propagate cancellation and release readers/connections. Admission limits are allowed before the response starts; never return a silently truncated catalog as success. After response headers are sent, handle failures by aborting/logging rather than inserting ProblemDetails into JSON.
- Prefer explicit URL versioning (`/api/v1/products`, `/api/v2/products`). Preserve the unpaginated v1 contract. Use stable ordering, validate pagination inputs and document any maximum page size.
- Use correct HTTP verbs/status codes, meaningful 400/404 responses and consistent ProblemDetails. Define how an omitted description differs from explicit null; do not implement a full generic patch engine without a need.
- Generate OpenAPI for the versions and serve working Swagger UI at `/swagger` in the documented local setup. Document request/response schemas, pagination, validation and error responses.
- Do not add create/delete endpoints or unrelated features just to enlarge the solution. As explicitly requested, protect writes with JWT/OIDC and scopes while keeping catalog GETs and Swagger public; isolate test credentials from production.

## Containers and distributed infrastructure

- Docker and Docker Compose are useful for a repeatable API + SQL Server setup. Use a multi-stage build, a non-root runtime where supported, environment-based configuration, health checks and safe local credentials supplied by the developer.
- Preserve direct IDE startup and a mock-data mode that requires neither SQL Server nor Docker. Verify host/container architecture compatibility before committing to database images; do not silently change SQL providers to hide environment issues.
- Kubernetes manifests and local/CI validation are now part of the requested scope: resource requests/limits, probes, external secret configuration, rollout and stateless scaling. Direct IDE startup must still work; the cloud hosting target remains unselected.
- Kafka carries product-change events from a transactional SQL Outbox to an independent idempotent audit consumer. Preserve per-product version ordering, define retention/replay boundaries and monitor backlog. Do not claim end-to-end exactly-once behavior across brokers and SQL.
- RabbitMQ is required for a distinct asynchronous work use case, but the user deferred the proposed export's business contract. Record this as an open decision; do not invent a job or declare the RabbitMQ feature complete based on a running container. Any eventual export must remain a separate operation from GET all v1/v2.
- If messaging is introduced, specify a real producer/consumer, delivery semantics, idempotency, retries and failure handling; address database/message consistency (for example, an outbox). Do not introduce Event Sourcing or NoSQL without a requirement.

## CI/CD with GitHub Actions

CI/CD and a real deployment are user-requested additions to the project scope. Implement the workflows alongside the runnable application and Dockerfile; do not create a permanently skipped or placeholder deployment and describe it as working.

- On pull requests: restore, Release build, unit/HTTP contract tests, and Docker build validation without publishing or deployment. Run SQL integration tests in a separate job with a real compatible database when implemented.
- On pushes to `main`: run the same validation, then build and publish a container image to GitHub Container Registry (GHCR) only after all required checks pass. Tag with the commit SHA and record the image digest. A published image is an artifact, not a completed deployment.
- Deploy the validated image by digest to the selected hosting target; do not rebuild it during deployment or depend on a mutable `latest` tag. The deployment platform is not selected yet; obtain it from the user before implementing platform-specific deployment or provisioning resources.
- Start with one staging environment and serialize deployments with a concurrency group. Do not cancel an in-progress deployment midway through migrations. Support a documented manual redeployment/rollback of a previously validated image.
- Use a GitHub environment where supported by the repository's plan and visibility; check availability before depending on approval or secret-protection features. Never make the repository public just to unlock a feature.
- Default workflow permissions to `contents: read`; grant `packages: write` only to the publish job and `id-token: write` only where OIDC authentication is used. Prefer OIDC for cloud deployment and GITHUB_TOKEN for GHCR publication. Store deployment credentials in appropriate GitHub secrets, never in YAML or logs.
- Pin third-party actions to verified full commit SHAs with version comments; keep SDK selection aligned with `global.json`. Set job timeouts. Do not expose deployment secrets to PRs or run untrusted PR code in a privileged `pull_request_target` workflow.
- Make database migrations a deliberate deployment step with failure handling, persistent storage and a rollback/compatibility strategy. An application rollback does not automatically roll back the database.
- Check readiness after rollout and run read-only smoke checks against a known product and the v2 listing. Mark a failed rollout as failed; document recovery and retain useful diagnostics without secrets. Do not expose SQL Server publicly.
- Document triggers, required secrets/variables, hosting prerequisites, deployment URL, smoke checks and rollback in the public README or deployment guide without private hiring context.
- Validate workflow syntax and scripts locally, then verify actual Actions runs and the deployed endpoint once hosting and credentials are configured. Report separately what is implemented, what ran and what still needs configuration.

## Tests, documentation and workflow

- Cover behavior: listing and pagination boundaries, lookup success/missing IDs, invalid inputs, description-only updates and identical mock/database semantics. Use a fresh fixture per test and avoid time-dependent or order-dependent tests.
- Explicitly test v1 with more products than a v2 page size: every product including the last is returned once, with no export or broker dependency. Test streaming cancellation and failures separately. Evaluate full-catalog v1 throughput/bytes/memory separately from the 10k RPS target for small responses; do not imply 10k full-catalog transfers per second.
- Keep unit tests independent of containers/network. Mark SQL integration tests separately and document prerequisites. Do not present EF's in-memory provider as proof of SQL Server behavior.
- Add tests when implementing meaningful behavior; do not add tests that merely mirror code or verify these Markdown instructions.
- README must explain prerequisites, configuration, IDE and container startup, migrations/seed, mock/database switching, Swagger, and test commands. Record material technical tradeoffs without private hiring context.
- Once a solution exists, run `dotnet restore`, `dotnet build --no-restore` and `dotnet test --no-build` from this repository, plus checks relevant to changed integrations. Report what actually ran and any environment blockers; never claim unrun checks passed.
- Preserve development history. Do not create artificial history, rewrite published history or change repository visibility based on a source document.

## Mandatory atomic commits

- After completing each atomic, independently reviewable unit of work, run the relevant checks and immediately create a Git commit before starting the next unit. This is the user's standing authorization to make local commits for completed work; do not repeatedly ask for permission to commit.
- Examples of separate units: create the solution/project scaffold; add one middleware with its configuration; implement one endpoint; introduce a v1 controller; introduce a v2 controller/version; add one migration; add one messaging handler; add one CI workflow; update a coherent set of documentation or repository rules.
- Choose the smallest complete behavior as the boundary. If a controller is introduced with its first endpoint, commit them together; later endpoints get their own commits. Do not split essential wiring, contracts or migrations into commits that leave the repository broken, and do not accumulate several finished endpoints into a single end-of-phase commit.
- Include the unit's necessary tests and directly related documentation in the same commit. A completed unit should build and pass its applicable tests. If checks fail, fix the unit before committing; if verification is blocked by the environment, report the limitation explicitly and do not claim the unit is verified.
- Before staging, inspect `git status` and the diff. Stage only explicit paths/hunks belonging to the unit, then inspect `git diff --cached` and run `git diff --cached --check`. Exclude secrets, private context, generated artifacts and unrelated user changes. Avoid blanket `git add .` / `git add -A` and never discard other changes to obtain a clean tree.
- Use short, descriptive English Conventional Commit messages, for example `chore: scaffold solution`, `feat(api): add exception middleware`, `feat(api): add v1 product listing`, `feat(api): add v2 pagination`, `test(api): cover description updates`, or `docs: define atomic commit workflow`.
- Verify that the commit succeeded and inspect the remaining status. Report commit hashes and any remaining relevant changes in the handoff. If Git identity, hooks or permissions block a commit, preserve the work and report the blocker; never invent an identity, disable hooks or claim a commit exists when it does not.
- Do not defer all commits until the end of a task, create empty commits, fabricate intermediate history after the fact, or squash/amend completed units without an explicit request. Local commit authorization does not itself authorize push, merge, release, deployment or a visibility change.
