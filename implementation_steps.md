# IntelliQueryAI: Architectural Implementation Guide & Developer Playbook

This document details the recent key architectural implementations, bug resolutions, and system enhancements across **IntelliQueryAI**. It is designed as an authoritative technical manual for human engineers and autonomous AI agents who need to understand, maintain, modify, or extend the platform.

---

## Table of Contents

1. [Executive Overview & System Architecture](#1-executive-overview--system-architecture)
2. [Feature 1: Dynamic Schema Discovery & Semantic SQL Generation](#2-feature-1-dynamic-schema-discovery--semantic-sql-generation)
   - [Problem Statement & Root Cause](#problem-statement--root-cause-ebs-transactions-failure)
   - [Architectural Solution & Pipeline](#architectural-solution--pipeline)
   - [Detailed Component Breakdown](#detailed-component-breakdown)
   - [Verification & Automated Tests](#verification--automated-tests)
   - [How to Modify or Extend the AI Pipeline](#how-to-modify-or-extend-the-ai-pipeline)
3. [Feature 2: Frontend Authentication, 401 Interception & Silent Token Rotation](#3-feature-2-frontend-authentication-401-interception--silent-token-rotation)
   - [Problem Statement & Root Cause](#problem-statement--root-cause-401-session-loop)
   - [Architectural Solution](#architectural-solution)
   - [Detailed Component Breakdown](#detailed-component-breakdown-auth)
   - [How to Modify or Extend Authentication & API Requests](#how-to-modify-or-extend-authentication--api-requests)
4. [Feature 3: Enterprise Design System & Vector Branding](#4-feature-3-enterprise-design-system--vector-branding)
   - [Design Rationale & Symbolic Anatomy](#design-rationale--symbolic-anatomy)
   - [Component Structure & Variants](#component-structure--variants)
   - [Site-wide Integrations & Asset Delivery](#site-wide-integrations--asset-delivery)
   - [How to Modify or Extend Brand Components](#how-to-modify-or-extend-brand-components)
5. [Feature 4: Dashboard Query History Pagination](#5-feature-4-dashboard-query-history-pagination)
   - [Overview & Implementation](#overview--implementation)
   - [Architectural Solution](#architectural-solution-1)
6. [Feature 5: Workspace Name Resolution & Historical GUID Sanitization](#6-feature-5-workspace-name-resolution--historical-guid-sanitization)
7. [Feature 6: Analytical Query vs. Saved Report vs. Executive Briefing Architecture & Persistence Fix](#7-feature-6-analytical-query-vs-saved-report-vs-executive-briefing-architecture--persistence-fix)
8. [Feature 7: Frontend Environment Configuration & API Base URI Resolution](#8-feature-7-frontend-environment-configuration--api-base-uri-resolution)
9. [Feature 8: Business Knowledge & Semantic Layer (Phase A)](#9-feature-8-business-knowledge--semantic-layer-phase-a)
   - [Architectural Solution & Semantic Injection](#architectural-solution--semantic-injection)
   - [Domain Entities & Database Persistence](#domain-entities--database-persistence)
   - [CQRS Commands, Queries & Semantic Resolver](#cqrs-commands-queries--semantic-resolver)
   - [AI Coordinator Context Injection](#ai-coordinator-context-injection)
   - [Frontend UI, State & Interactive Tester](#frontend-ui-state--interactive-tester)
   - [Verification & Automated Tests](#verification--automated-tests-phase-a)
10. [Feature 9: AI Root-Cause Investigation Engine (Phase B)](#10-feature-9-ai-root-cause-investigation-engine-phase-b)
    - [Architectural Solution & Investigation Workflow](#architectural-solution--investigation-workflow)
    - [Domain Entities, Persistence & PostgreSQL Migration](#domain-entities-persistence--postgresql-migration)
    - [Microsoft Agent Framework Investigation Coordinator](#microsoft-agent-framework-investigation-coordinator)
    - [CQRS Commands, Queries & Endpoints](#cqrs-commands-queries--endpoints)
    - [Frontend Studio, Waterfall Analytics & Diagnostic Reports](#frontend-studio-waterfall-analytics--diagnostic-reports)
    - [Verification & Automated Tests](#verification--automated-tests-phase-b)
11. [Developer & AI Agent Operational Playbook](#11-developer--ai-agent-operational-playbook)
    - [Solution Layout & Directory Structure](#solution-layout--directory-structure)
    - [Running the Application Locally](#running-the-application-locally)
    - [Running the Test Suites](#running-the-test-suites)
    - [Production Build Validation](#production-build-validation)
    - [Core Architectural Rules & Invariants](#core-architectural-rules--invariants)

---

## 1. Executive Overview & System Architecture

**IntelliQueryAI** is an enterprise-grade multi-agent Natural Language to SQL analytics platform. It bridges natural human language with distributed relational databases (PostgreSQL, Microsoft SQL Server, MySQL) by synthesizing verified SQL, streaming tabular records, and rendering interactive visualization dashboards.

```
                      ┌────────────────────────────────────────┐
                      │    Next.js 16 Web Frontend (Turbopack) │
                      │      Zustand Store + TanStack Query     │
                      └───────────────────┬────────────────────┘
                                          │ HTTP / JSON (JWT + Bearer)
                                          ▼
                      ┌────────────────────────────────────────┐
                      │    IntelliQueryAI.Api (Minimal APIs)   │
                      └───────────────────┬────────────────────┘
                                          │ CQRS (IDispatcher)
                                          ▼
                      ┌────────────────────────────────────────┐
                      │     IntelliQueryAI.Application         │
                      │     (Commands, Queries, Validators)    │
                      └─────────┬───────────────────┬──────────┘
                                │                   │
       Entity Framework Core /  │                   │ Microsoft Agent Framework /
       Dapper Read Repositories │                   │ Azure OpenAI (IChatClient)
                                ▼                   ▼
      ┌───────────────────────────────┐   ┌───────────────────────────────┐
      │  IntelliQueryAI.Infrastructure│   │     IntelliQueryAI.AI         │
      │   (Postgres, PgVector, SQL    │   │  (Coordinator, Evaluator,     │
      │    Server, MySQL Extractors)  │   │   Tools, Embedding Indexer)   │
      └───────────────────────────────┘   └───────────────────────────────┘
```

### Core Architecture Tenets
- **Backend**: .NET 10 & C# 13 Clean Architecture.
- **CQRS & In-Process Dispatching**: Handled via in-process `IDispatcher` returning `Result<T>` or `Result`.
- **Relational Persistence**: EF Core for write operations; Dapper for high-throughput reads; PgVector for semantic embeddings.
- **Frontend**: Next.js 16 (App Router), TailwindCSS, TypeScript, Zustand for global auth and workspace state, TanStack React Query for data caching.

---

## 2. Feature 1: Dynamic Schema Discovery & Semantic SQL Generation

### Problem Statement & Root Cause ("EBS Transactions" Failure)
When users connected an enterprise datasource and prompted the system with natural language such as:
> *"Show me top 20 ebs transactions"*

The AI engine previously produced placeholder dummy responses:
```sql
SELECT TOP (20) N'The provided schema does not include an EBS transactions table. Please provide its table name and columns.' AS [Message];
```

#### Why did this happen?
1. **Schema Extractor Incompleteness**: The database schema extractor queries in [`SchemaExtractors.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/SchemaDiscovery/Extractors/SchemaExtractors.cs) were filtered exclusively on `TABLE_TYPE = 'BASE TABLE'` or `type = 'U'`. Enterprise databases frequently expose business entities (e.g. EBS transactions, financial views) as database views (`VIEW` or `type = 'V'`).
2. **Missing Vector Embedding Ingestion**: When datasources were created via `RegisterDataSourceCommand`, table and column schemas were saved to PostgreSQL metadata tables, but embeddings were never calculated or written to PgVector. Thus, vector retrieval returned zero matches.
3. **Keyword-Only Fallback Fragility**: When vector search returned empty, the schema retrieval tool in [`AiTools.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Tools/AiTools.cs) failed to correlate abbreviations (e.g., "ebs" matching `ebs_transactions`, `tbl_ebs_tx`, or `ElectronicBankingSystem_Transactions`).
4. **Agent Permissiveness**: The evaluation guardrail service [`AgentEvaluationService.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Evaluations/AgentEvaluationService.cs) did not reject dummy SQL containing explanatory text messages like `AS [Message]`.

---

### Architectural Solution & Pipeline

```
User Query: "Show me top 20 ebs transactions"
                      │
                      ▼
┌────────────────────────────────────────────────────────┐
│ AgentCoordinator.cs: JIT Schema Verification           │
│ Checks if DataSource has schema cached in metadata db. │
│ If empty -> Triggers on-the-fly discovery & indexing.  │
└─────────────────────┬──────────────────────────────────┘
                      │
                      ▼
┌────────────────────────────────────────────────────────┐
│ AiTools.cs: Hybrid Schema Retrieval                    │
│ 1. Vector Search (PgVector cosine similarity)          │
│ 2. Keyword/Abbreviation Scoring (Tokens & substrings)  │
│ 3. Foreign Key Graph Expansion (Related joins)         │
└─────────────────────┬──────────────────────────────────┘
                      │
                      ▼
┌────────────────────────────────────────────────────────┐
│ IChatClient / Azure OpenAI Model                       │
│ Receives complete, relevant DDL schema with columns.   │
│ Synthesizes clean SQL (e.g. SELECT TOP 20 * FROM ...)  │
└─────────────────────┬──────────────────────────────────┘
                      │
                      ▼
┌────────────────────────────────────────────────────────┐
│ AgentEvaluationService.cs: Guardrails & Validation     │
│ Enforces read-only SELECT, blocks dummy placeholder    │
│ queries, and verifies syntax before execution.         │
└────────────────────────────────────────────────────────┘
```

---

### Detailed Component Breakdown

#### 1. View & Table Extraction Across Engines
**File**: [`src/IntelliQueryAI.Infrastructure/SchemaDiscovery/Extractors/SchemaExtractors.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/SchemaDiscovery/Extractors/SchemaExtractors.cs)
- **SQL Server**: Updated query from `WHERE t.type = 'U'` to `WHERE t.type IN ('U', 'V')` and expanded column extraction to join `sys.objects` so columns for both tables and views are completely extracted.
- **PostgreSQL**: Updated query to inspect `information_schema.tables` where `table_type IN ('BASE TABLE', 'VIEW', 'FOREIGN TABLE')`.
- **MySQL**: Expanded to include both `BASE TABLE` and `VIEW`.

#### 2. Vector Embedding Indexer
**Files**:
- [`src/IntelliQueryAI.AI/Embeddings/SchemaEmbeddingIndexer.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Embeddings/SchemaEmbeddingIndexer.cs)
- [`src/IntelliQueryAI.AI/DependencyInjection.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/DependencyInjection.cs)

Implements `ISchemaEmbeddingIndexer`:
```csharp
public interface ISchemaEmbeddingIndexer
{
    Task IndexDataSourceSchemaAsync(
        Guid dataSourceId,
        IReadOnlyList<TableSchemaDto> tables,
        CancellationToken cancellationToken = default);
}
```
- Concatenates table names, column names, data types, and primary/foreign key definitions into dense textual representations.
- Calls Azure OpenAI `text-embedding-3-small` (or configured embedding model) to generate 1536-dimensional vectors.
- Saves vector records directly to PgVector (`table_embeddings` table) for subsequent semantic similarity searches.
- Registered in DI container via `services.AddScoped<ISchemaEmbeddingIndexer, SchemaEmbeddingIndexer>()`.

#### 3. Automatic Discovery on Creation and Sync
**Files**:
- [`src/IntelliQueryAI.Application/Features/DataSources/Commands/RegisterDataSource/RegisterDataSourceCommand.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/DataSources/Commands/RegisterDataSource/RegisterDataSourceCommand.cs)
- [`src/IntelliQueryAI.Application/Features/DataSources/Commands/SyncDataSource/SyncDataSourceCommand.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/DataSources/Commands/SyncDataSource/SyncDataSourceCommand.cs)

When a datasource is added or synchronized:
1. Validates connection credentials against the target database.
2. Runs the appropriate `ISchemaExtractor`.
3. Stores extracted metadata in `IDataSourceRepository`.
4. Invokes `ISchemaEmbeddingIndexer.IndexDataSourceSchemaAsync()` in the background to guarantee embeddings are immediately available for AI reasoning.

#### 4. Hybrid Schema Retrieval with FK Expansion
**File**: [`src/IntelliQueryAI.AI/Tools/AiTools.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Tools/AiTools.cs)
- Implemented `RetrieveRelevantSchemaAsync(dataSourceId, queryText)`:
  - **Stage 1 (Vector)**: Queries PgVector using cosine distance `<->` against the prompt vector.
  - **Stage 2 (Keyword/Fuzzy Scoring)**: Tokenizes user prompt (e.g. `["ebs", "transactions"]`) and calculates relevance scores against table names, column names, and table descriptions.
  - **Stage 3 (Graph Expansion)**: Automatically includes tables linked by foreign keys to the top matching tables.
  - **Stage 4 (Null Safety & Fallback)**: If zero vector matches are returned, falls back to the top scored keyword tables or the top 10 primary schema tables, ensuring the LLM never receives an empty schema context.

#### 5. Coordinator JIT Schema Discovery & Prompt Routing
**File**: [`src/IntelliQueryAI.AI/Coordinator/AgentCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/AgentCoordinator.cs)
- Added pre-execution checks: if a datasource has zero tables cached, invokes JIT discovery before LLM invocation.
- Ensures the full user question text is passed into the schema retrieval tool rather than an arbitrary table filter.

#### 6. Anti-Dummy SQL Guardrails
**File**: [`src/IntelliQueryAI.AI/Evaluations/AgentEvaluationService.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Evaluations/AgentEvaluationService.cs)
- Added regex and heuristic checks that detect fallback dummy responses such as:
  `SELECT ... 'The provided schema does not include...' AS [Message]`
- Rejects such outputs with a retry instruction to the model to examine the available retrieved schema.

#### 7. Frontend Query UI Enhancements
**File**: [`web/src/app/(app)/query/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/query/page.tsx)
- Added dynamic schema detection status banner.
- Added a one-click "Discover Schema Now" trigger for on-demand re-indexing without leaving the query workspace.

---

### Verification & Automated Tests
**File**: [`tests/IntelliQueryAI.Application.Tests/SchemaRetrievalAndEbsQueryTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/SchemaRetrievalAndEbsQueryTests.cs)
- Validated vector and keyword retrieval scoring on terms like "ebs transactions", "orders", and "customers".
- Verified that schema extractors preserve table columns and foreign key relations.
- **Results**: All 63 Application tests, 57 Domain tests, and 8 Integration tests pass with zero failures.

---

### How to Modify or Extend the AI Pipeline
If you need to add support for a new database engine (e.g. Snowflake or Oracle) or change LLM models:
1. **Add Extractor**: Create `OracleSchemaExtractor : ISchemaExtractor` in [`src/IntelliQueryAI.Infrastructure/SchemaDiscovery/Extractors`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/SchemaDiscovery/Extractors). Make sure to extract both tables and views.
2. **Register in Factory**: Add the engine to `SchemaExtractorFactory.cs`.
3. **Change LLM Model**: Update model deployments in `appsettings.json` under `AzureOpenAI:ChatDeploymentName` and `AzureOpenAI:EmbeddingDeploymentName`.
4. **Adjust Tools**: In [`AiTools.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Tools/AiTools.cs), tool methods are annotated with `[Description(...)]` which Microsoft Agent Framework uses for function calling.

---

## 3. Feature 2: Frontend Authentication, 401 Interception & Silent Token Rotation

### Problem Statement & Root Cause (401 Session Loop)
When opening the root URL (`GET / 200`) or refreshing any page, the browser console produced:
```
[browser] API request error: Request failed with status 401 
    at request (src/lib/api-client.ts:422:13)
    at async initialize (src/stores/auth-store.ts:161:30)
[browser] Failed to fetch workspaces during auth initialization Error: Request failed with status 401
```

#### Why did this happen?
1. **Zombie Authentication State**: In [`auth-store.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/stores/auth-store.ts), `initialize()` read `iq_token` and `iq_user` from `localStorage` and immediately executed:
   ```ts
   set({ token, user, isAuthenticated: true });
   ```
   It then invoked `api.workspaces.getUserWorkspaces()`. If the token had expired (JWT lifetimes are typically 60 minutes), the backend rejected the request with `401 Unauthorized`.
2. **Missing Token Rotation / Silent Refresh**: The backend exposed `POST /api/auth/refresh` taking a `RefreshTokenCommand(string RefreshToken, Guid? CurrentWorkspaceId)` which rotates refresh tokens and returns fresh access tokens. However:
   - `api-client.ts` had no `api.auth.refresh` client call.
   - `request<T>()` threw immediately on 401 without attempting a silent refresh.
3. **Absence of Token Cleanup**: When 401 was caught in `initialize()`, it merely logged an error to `console.error` and did nothing else. `isAuthenticated` stayed `true` with invalid credentials, trapping the user in a broken interface where all subsequent queries crashed with 401 errors.

---

### Architectural Solution

```
Frontend Request (e.g., getUserWorkspaces())
                      │
                      ▼
┌────────────────────────────────────────────────────────┐
│ api-client.ts: request<T>()                            │
│ Sends request with Authorization: Bearer <token>       │
└─────────────────────┬──────────────────────────────────┘
                      │
                      ▼
               HTTP 401 Unauthorized?
               ┌──────┴──────┐
             No│             │Yes (non-auth route)
               ▼             ▼
          Return data   ┌────────────────────────────────────────┐
                        │ attemptTokenRefresh()                  │
                        │ Uses singleton mutex promise           │
                        │ POST /api/auth/refresh with            │
                        │ iq_refresh_token + currentWorkspaceId  │
                        └────────────┬───────────────────────────┘
                                     │
                             Refresh Successful?
                             ┌───────┴───────┐
                           Yes               │No
                             ▼               ▼
                 Store new tokens in       clearAuthTokens()
                 localStorage.             Dispatch iq_auth_unauthorized.
                 Dispatch iq_auth_refreshed. Throw 401.
                 Replay original request   Auth store calls logout().
                 with fresh token!         Redirect cleanly to /auth/login.
```

---

### Detailed Component Breakdown (Auth)

#### 1. Mutex Deduplication & Token Refresh Interceptor
**File**: [`web/src/lib/api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts)
- **`clearAuthTokens()`**: Utility that clears `iq_token`, `iq_refresh_token`, `iq_user`, `iq_workspace_id`, and `iq_workspace_slug`.
- **`attemptTokenRefresh()`**:
  - Implements a singleton promise: `let refreshPromise: Promise<string | null> | null = null;`
  - **Why?** If 5 components make simultaneous API calls on mount and all get 401 at the same millisecond, deduplication guarantees only **one** refresh request is sent to the backend. This prevents refresh token reuse violations (which would revoke the token).
  - Validates GUID structure of `iq_workspace_id` before transmitting to prevent backend JSON deserialization errors.
  - Updates `localStorage` and emits a window custom event `iq_auth_refreshed` with fresh tokens and user details.
- **`request<T>()`**:
  - Intercepts `401 Unauthorized` responses on all routes except `/api/auth/login`, `/api/auth/register`, and `/api/auth/refresh`.
  - Automatically calls `attemptTokenRefresh()` and, upon success, replays the original request with `isRetry = true`.
  - Suppresses unneeded `console.error` spam on 401 responses that are cleanly handled.
- **`api.auth.refresh`**: Exposed on the `api` object for explicit programmatic refresh workflows.

#### 2. Robust Session Initialization & Safe State Teardown
**File**: [`web/src/stores/auth-store.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/stores/auth-store.ts)
- **Event Listeners**: Attached once on bootstrap:
  - `iq_auth_refreshed`: Updates in-memory access token, refresh token, user, and workspace.
  - `iq_auth_unauthorized`: Calls `get().logout()` to purge all state.
- **`initialize()`**:
  - Sets optimistic state in memory.
  - Validates session with `api.workspaces.getUserWorkspaces()`.
  - If validation fails and silent refresh cannot recover the session, cleanly invokes `get().logout()`.
  - Guarantees `isAuthenticated: false` and `isLoading: false` without leaving dead tokens or corrupt state in `localStorage`.

---

### How to Modify or Extend Authentication & API Requests
1. **Adding an Authenticated Endpoint**: Use `request<T>("/api/your-path", { method: "..." })`. It automatically injects `Authorization: Bearer <token>`, `X-Tenant-Id`, and handles 401 refresh retries transparently.
2. **Accessing Current User or Workspace**:
   ```tsx
   import { useAuthStore } from "@/stores/auth-store";
   const { user, currentWorkspace, isAuthenticated } = useAuthStore();
   ```
3. **Extending JWT Claims**: If you add claims (e.g. custom roles or permissions), update `IJwtTokenGenerator.cs` in backend Infrastructure and mirror properties in `UserDto` within [`api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts).

---

## 4. Feature 3: Enterprise Design System & Vector Branding

### Design Rationale & Symbolic Anatomy
The frontend previously used generic `<Sparkles>` icons in plain colored squares. This was replaced with a bespoke, scalable SVG vector brand emblem designed specifically for **IntelliQueryAI**:

```
                       [ Top: SQL Synthesis Agent ]
                                    (●)
                                   / | \
                                  /  |  \
   (●)--------------------------[ ◆ AI CORE ]--------------------------(●)
[ Bottom-Left:                       |                     [ Top-Right:
  Schema & Vector Retrieval ]         \                      Execution & Guardrails ]
                                       \===> [ Q-Vector Energy Tail ]
```

1. **The "Q" Query Orbit**: A geometric torus loop forming the letter **Q** (Query), terminating in a precision 45° vector energy emitter beam representing the journey from plain English to verified SQL.
2. **The Neural Diamond Prism**: A 4-point star prism at the center with a supernova radiant core representing semantic intelligence and vector reasoning.
3. **Multi-Agent Constellation Nodes**: Three satellite nodes representing the platform's multi-agent architecture (SQL Agent, Schema/Vector Agent, Execution Agent) linked via dashed circuit traces.
4. **Chromatic Palette**: Electric Indigo (`#6366F1`) &bull; Radiant Violet (`#A855F7`) &bull; Cyber Cyan (`#06B6D4`).
5. **Illuminated AI Badge**: Micro-pill badge with a live pulsing cyan core dot next to the brand typography.

---

### Component Structure & Variants
**File**: [`web/src/components/shared/Logo.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/shared/Logo.tsx)

Exports two components:
- `LogoIcon`: Standalone SVG icon with customizable `size`, `className`, and `glow`.
- `Logo`: Full lockup supporting typography, badge, and responsive container styling.

#### Supported Props:
| Prop | Types | Default | Description |
|---|---|---|---|
| `size` | `"xs" \| "sm" \| "md" \| "lg" \| "xl"` | `"sm"` | Controls icon dimensions, container size, and typography scale |
| `variant` | `"default" \| "glow" \| "clean" \| "minimal"` | `"default"` | Controls background squircle container, border, and glow effects |
| `showText` | `boolean` | `true` | Toggles whether "IntelliQuery AI" text is displayed |
| `subtext` | `string` | `undefined` | Optional sub-caption (e.g., "Enterprise Analytics") |
| `iconOnly` | `boolean` | `false` | When true, renders only the icon |
| `href` | `string` | `undefined` | Wraps component in Next.js `<Link>` |

#### SVG ID Sanitization:
Uses React's `useId()` with sanitized strings (`rawId.replace(/[^a-zA-Z0-9_-]/g, "")`) so that multiple logos rendered on the same DOM page (e.g. navbar + footer + modal) never suffer from SVG gradient ID conflicts.

---

### Site-wide Integrations & Asset Delivery
1. **Navbar**: [`web/src/components/layout/Navbar.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/layout/Navbar.tsx) (`<Logo size="sm" href="/" />`).
2. **Dashboard Sidebar**: [`web/src/components/layout/Sidebar.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/layout/Sidebar.tsx) (`<Logo size="sm" subtext="Enterprise Analytics" href="/dashboard" />`).
3. **Login View**: [`web/src/app/auth/login/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/auth/login/page.tsx) (`<Logo size="lg" href="/" />`).
4. **Register View**: [`web/src/app/auth/register/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/auth/register/page.tsx) (`<Logo size="lg" href="/" />`).
5. **Forgot Password View**: [`web/src/app/auth/forgot-password/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/auth/forgot-password/page.tsx) (`<Logo size="lg" href="/" />`).
6. **Landing Page Footer**: [`web/src/app/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/page.tsx) (`<Logo size="xs" href="/" />`).
7. **Browser Favicon & Manifest Icons**:
   - [`web/src/app/icon.svg`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/icon.svg) (automatically registered by Next.js as the dynamic app favicon).
   - [`web/public/icon.svg`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/public/icon.svg) & [`web/public/logo.svg`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/public/logo.svg) (static assets for external embeds or SEO).
   - Metadata icons configured in [`web/src/app/layout.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/layout.tsx).

---

### How to Modify or Extend Brand Components
- **To add a new logo size**: Update `sizeConfig` in [`Logo.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/shared/Logo.tsx) with icon dimensions and Tailwind typography classes.
- **To embed in a dark or light context**: Use `variant="clean"` for transparent backgrounds or `variant="glow"` for highlighted promotional cards.

---

## 5. Developer & AI Agent Operational Playbook

### Solution Layout & Directory Structure

```
IntelliQueryAI/
├── src/
│   ├── IntelliQueryAI.Domain/               # Entities, Enums, Value Objects, Domain Errors
│   ├── IntelliQueryAI.SharedKernel/         # Result<T>, Error primitives, Common abstractions
│   ├── IntelliQueryAI.Application/          # Commands, Queries, Handlers, Validators, DTOs
│   │   ├── Abstractions/                    # IDispatcher, ICommand, IQuery, IRepository
│   │   └── Features/                        # Vertical Slices (DataSources, Identity, Query, etc.)
│   ├── IntelliQueryAI.Infrastructure/       # EF Core DbContext, Dapper Repositories, Schema Extractors
│   ├── IntelliQueryAI.AI/                   # Multi-Agent Coordinator, AI Tools, Embeddings, Evaluations
│   └── IntelliQueryAI.Api/                  # ASP.NET Core Minimal APIs, Middleware, Endpoint Mapping
├── tests/
│   ├── IntelliQueryAI.Domain.Tests/         # Domain business invariant tests
│   ├── IntelliQueryAI.Application.Tests/    # Application CQRS and unit tests
│   └── IntelliQueryAI.IntegrationTests/     # Integration and DB tests
└── web/                                     # Next.js 16 Web Application
    ├── src/
    │   ├── app/                             # Next.js App Router pages and layouts
    │   ├── components/                      # UI components (shared, layout, query, etc.)
    │   ├── lib/                             # api-client.ts, utils.ts
    │   ├── providers/                       # QueryClientProvider, AppProviders
    │   └── stores/                          # Zustand stores (auth-store.ts, etc.)
    └── public/                              # Static public assets (icon.svg, logo.svg)
```

---

### Running the Application Locally

#### 1. Backend API
```powershell
# Navigate to API project
cd src\IntelliQueryAI.Api

# Run API with development profile
dotnet run --launch-profile https
```
The API listens on `https://localhost:7162`. Swagger/OpenAPI documentation is available at `https://localhost:7162/scalar/v1` or `/swagger`.

#### 2. Frontend Web Application
```powershell
# Navigate to web project
cd web

# Run development server with Turbopack
npm run dev
```
The frontend listens on `http://localhost:3000`.

---

### Running the Test Suites

Execute all backend test suites using `dotnet test`:

```powershell
# Run all solution tests
dotnet test

# Run specific project test suites
dotnet test tests\IntelliQueryAI.Domain.Tests
dotnet test tests\IntelliQueryAI.Application.Tests
dotnet test tests\IntelliQueryAI.IntegrationTests
```

**Baseline Test Status**:
- `IntelliQueryAI.Domain.Tests`: **57/57 Passing**
- `IntelliQueryAI.Application.Tests`: **66/66 Passing** (includes `GetPagedQueryHistoryQueryHandlerTests` and `GetDashboardExecutiveSummaryQueryHandler_WhenReportContainsRawWorkspaceGuid_ShouldSanitizeToFriendlyWorkspaceName`)
- `IntelliQueryAI.IntegrationTests`: **8/8 Passing**
- Total: **131/131 Passing (100% Green)**

---

### Production Build Validation

To verify that the frontend compiles cleanly without TypeScript, lint, or route generation errors:

```powershell
cd web
npm run build
```
---

## 5. Feature 4: Full Server-Side Dashboard Query History Pagination

### Overview & Implementation
To support arbitrarily large query execution datasets without memory bloat or high data transfer penalties on the client, we upgraded the "Recent Analytical Queries" dashboard card from client-side slicing to **full end-to-end server-side pagination** across all architectural tiers (.NET 10 backend + Next.js 16 frontend).

---

## 6. Feature 5: Friendly Workspace Names in AI Executive Briefings

### Problem Statement
In the executive intelligence summary generated by the multi-agent pipeline, the synthesized text previously displayed raw internal database GUIDs (e.g., `"Workspace cfe6188b-312b-4162-97fa-a89936ccb15f has recorded two historical queries..."`) instead of the human-readable business name (`"Second WorkSpace"`).

### Root Cause Analysis
1. In [`src/IntelliQueryAI.AI/Coordinator/ExecutiveCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/ExecutiveCoordinator.cs), the coordinator synthesized prompts for `ExecutiveSummaryAgent` using `request.WorkspaceId` (a raw Guid) without providing the workspace's business name.
2. In [`GenerateExecutiveReportCommandHandler.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/ExecutiveIntelligence/Commands/GenerateExecutiveReport/GenerateExecutiveReportCommand.cs), the command only accepted `WorkspaceId` and did not resolve the aggregate workspace entity to fetch `workspace.Name`.
3. Historical executive report records already persisted in the database stored the raw GUID in their summary paragraphs.

### Comprehensive Solution Across Full Stack

1. **AI Coordinator & Request Contract** ([`ExecutiveCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/ExecutiveCoordinator.cs)):
   - Added `WorkspaceName` to `GenerateExecutiveReportRequest`.
   - Updated the LLM prompt to identify the organization using `Workspace Name: {displayName}` and added explicit instructions:
     `"Always refer to the workspace by its business name ('{displayName}') or 'the organization/business'. NEVER output internal database GUIDs, IDs, or technical hashes in any human-facing text."`
   - Added `SanitizeText` post-processing in `GenerateExecutiveBriefingAsync` to defensibly catch and replace any leaked raw workspace GUIDs across summaries, findings, risks, opportunities, and recommendations.

2. **Executive Summary Agent Instructions** ([`MonitoringAndExecutiveAgents.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Agents/MonitoringAndExecutiveAgents.cs)):
   - Instructed `ExecutiveSummaryAgent` to always use the business name and never emit raw GUIDs.

3. **CQRS Command & Background Job Integration**:
   - In [`GenerateExecutiveReportCommand.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/ExecutiveIntelligence/Commands/GenerateExecutiveReport/GenerateExecutiveReportCommand.cs): Injected `IWorkspaceStore`, looked up `workspace.Name`, and defaulted the report title to `"{displayName} Executive Briefing - {period}"`.
   - In [`MonitoringJobs.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Background/MonitoringJobs.cs): Passed `ws.Name` into `GenerateExecutiveReportRequest`.

4. **On-The-Fly Historical Data Sanitization (Read Queries)**:
   - In [`GetDashboardExecutiveSummaryQuery.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Dashboard/Queries/GetDashboardExecutiveSummary/GetDashboardExecutiveSummaryQuery.cs) and [`GetExecutiveReportsQuery.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/ExecutiveIntelligence/Queries/GetExecutiveReports/GetExecutiveReportsQuery.cs):
     Injected `IWorkspaceStore` and sanitized existing database records during `MapToDto`, substituting raw GUID references with `workspace.Name`.
   - Added unit test in [`MonitoringAndAlertsCommandHandlerTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/MonitoringAndAlertsCommandHandlerTests.cs) verifying sanitization.

5. **Frontend Instant Normalization** ([`web/src/lib/utils.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/utils.ts)):
   - Added `formatFriendlySummary(text, workspaceName, workspaceId)` utility that cleanly replaces `Workspace <guid>` and raw GUIDs with the friendly name.
   - Applied across [`dashboard/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/dashboard/page.tsx), [`ExecutiveSummaryCard.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/executive/ExecutiveSummaryCard.tsx), and intelligence report pages.

---

## 7. Feature 6: Analytical Query vs. Saved Report vs. Executive Briefing Architecture & Persistence Fix

### Conceptual Clarity
In IntelliQuery AI, there is a clear separation between three data tiers:
1. **Query Executions / Query History (`QueryHistory` table)**:
   - Created automatically whenever a user asks a question in the AI Query Workspace (`/query`).
   - Stored in `query_histories` and displayed under "Recent Analytical Queries".
2. **Saved Reports (`SavedReport` table, displayed on `/reports`)**:
   - Curated snapshots of an executed query, its visualization chart layout, and business insights.
   - Saved explicitly by the user clicking the **"Save Report"** bookmark action in `/query`.
   - Running a raw query logs to Query History but does *not* automatically save a curated report.
3. **Executive Intelligence Briefings (`ExecutiveReport` table, displayed on `/intelligence/reports`)**:
   - High-level multi-agent strategic briefs synthesized by the LLM (combining query volume, active monitors, anomalies, and alerts).
   - Generated on-demand via the "Generate Weekly Briefing" button or automatically every 6 hours by the Quartz.NET `ExecutiveReportJob`.

### Bug Identified & Fixed in Saved Reports Pipeline
When users attempted to save reports from `/query`:
1. **Property Name Mismatch**: The frontend sent `{ generatedSql, chartType }` while [`ReportEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/ReportEndpoints.cs) expected `SaveReportRequest(string Title, string? Description, string Question, string SqlQuery, string ChartConfigurationJson, string InsightsJson, Guid? WorkspaceId)`. Because `SqlQuery` was null, `SaveReportCommandValidator` failed validation (`RuleFor(x => x.SqlQuery).NotEmpty()`).
2. **PostgreSQL JSONB Enforcement**: Columns `ChartConfigurationJson` and `InsightsJson` are non-null `jsonb` columns. If empty strings were passed, PostgreSQL rejected the write with `invalid input syntax for type json`.
3. **DTO Field Aliasing**: [`SavedReportDto`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Ai/Dtos/AiDtos.cs) originally exposed only `SqlQuery` and `CreatedAtUtc`, while [`reports/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/reports/page.tsx) expected `generatedSql`, `chartType`, and `createdAt`.

### Full Solution Implemented:
1. **Backend Endpoint & DTO Flexibility** ([`ReportEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/ReportEndpoints.cs), [`AiDtos.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Ai/Dtos/AiDtos.cs)):
   - Supported both `SqlQuery`/`GeneratedSql`, `ChartConfigurationJson`/`ChartType`, and `InsightsJson` with valid fallback JSON (`"{}"` and `"[]"`).
   - Added `GeneratedSql`, `ChartType`, and `CreatedAt` convenience fields to `SavedReportDto`.
2. **Command & Handler Protection** ([`SaveReportCommand.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Ai/Commands/SaveReport/SaveReportCommand.cs)):
   - Fallback valid JSON guaranteed before persistence, and chart type extracted from JSON configuration.
3. **API Client & UI Harmonization** ([`api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts), [`query/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/query/page.tsx), [`reports/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/reports/page.tsx)):
   - Client passes `workspaceId`, `sqlQuery`, `generatedSql`, `chartConfigurationJson`, and `insightsJson`.
   - Card rendering on `/reports` safely uses fallback accessors.
4. **Intelligence Filter Optimization** ([`intelligence/reports/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/intelligence/reports/page.tsx)):
   - Allowed filtering by "Monthly" to match calendar month names (e.g. "October 2026") generated by the background job.
5. **Automated Unit Tests**:
   - Added [`SaveReportCommandHandlerTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/SaveReportCommandHandlerTests.cs) (133/133 tests passing).

---

## 8. Feature 7: Frontend Environment Configuration & API Base URI Resolution

### Implementation Details
1. **Environment Files Added**:
   - [`web/.env.local`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/.env.local): Local development environment file with active configuration.
   - [`web/.env`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/.env): Fallback default environment file.
   - [`web/.env.example`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/.env.example): Committed template for developers.
2. **Environment Variables Configured**:
   - `NEXT_PUBLIC_API_URL=https://localhost:7162`
   - `NEXT_PUBLIC_API_BASE_URL=https://localhost:7162`
   - `NEXT_PUBLIC_API_BASE_URI=https://localhost:7162`
3. **Resilient Resolution in API Client** ([`web/src/lib/api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts)):
   - Updated `API_BASE_URL` to evaluate `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_API_BASE_URL`, or `NEXT_PUBLIC_API_BASE_URI` with fallback to `"https://localhost:7162"`.

---

## 9. Feature 8: Business Knowledge & Semantic Layer (Phase A)

### Architectural Solution & Semantic Injection
In real-world enterprise databases, schema column and table names are frequently cryptic or generic (e.g. `is_act`, `c_stat`, `ebs_tx_val`). Furthermore, business users describe their intent with organizational terminology, KPI acronyms, and operational jargon (e.g. *"ARR"*, *"Churned Customer"*, *"Active Enterprise Tier"*).

Without a Semantic Layer, an LLM must guess what business concepts map to database columns, leading to inaccurate queries or complete hallucinations.

Phase A introduces a multi-tenant **Business Knowledge / Semantic Layer** that injects ubiquitous domain vocabulary, metric formulas, and technical database column mappings directly into the AI SQL generation context.

```
User Query: "What is our ARR from active enterprise churned accounts?"
                          │
                          ▼
            Token & Synonym Matcher
  (Queries IBusinessTermStore & IMetricStore for Workspace)
                          │
                          ▼
             Semantic Context Extracted
  • Business Term: "Churned Customer" -> subscriptions.status = 'cancelled'
  • Metric: "ARR" -> Formula: SUM(mrr) * 12
                          │
                          ▼
     Dynamic System Prompt Injection into AgentCoordinator
  [BUSINESS VOCABULARY & SEMANTIC LAYER]
  Use these official domain definitions and SQL hints:
  - Churned Customer: Any account cancelled in last 90 days.
    (SQL hint: subscriptions.status = 'cancelled' AND ...)
  - ARR: Annual Recurring Revenue.
    (Formula: SUM(mrr) * 12)
                          │
                          ▼
       Hallucination-Free Business-Aligned SQL
```

### Domain Entities & Database Persistence
- **Domain Entities** ([`BusinessKnowledgeEntities.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Entities/BusinessKnowledge/BusinessKnowledgeEntities.cs)):
  - `BusinessTerm`: Aggregate root containing `Term`, `Definition`, `Category`, `Aliases` (JSON list), `TechnicalMapping`, `ExampleUsage`, and `IsActive`.
  - `MetricDefinition`: Aggregate root containing `Name`, `Description`, `Formula`, `Unit`, `TargetValue`, `SqlExpression`, and `IsActive`.
- **Entity Framework Core Configurations** ([`BusinessKnowledgeConfigurations.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Configurations/BusinessKnowledgeConfigurations.cs)):
  - Mapped to PostgreSQL tables `business_terms` and `metric_definitions`.
  - Configured JSONB column conversions for `Aliases` list.
  - Multi-tenant tenant compound indexes: `ix_business_terms_workspace_name`, `ix_business_terms_workspace_category`, and `ix_metric_definitions_workspace_name`.
- **Repository Stores** ([`EfBusinessKnowledgeStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Stores/EfBusinessKnowledgeStores.cs)):
  - `EfBusinessTermStore`: Tenant-isolated CRUD, search by category or text, and `GetMatchingTermsAsync` scoring matches by exact term name, word boundaries, and alias equality.
  - `EfMetricStore`: Tenant-isolated CRUD and `GetMatchingMetricsAsync` scoring matches by metric name, keywords, and description.
- **PostgreSQL Database Migration** ([`migrations/PhaseA_BusinessKnowledge.sql`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/migrations/PhaseA_BusinessKnowledge.sql)):
  - Executed and validated in running PostgreSQL instance.

### CQRS Commands, Queries & Semantic Resolver
- **Commands & Handlers** ([`BusinessKnowledgeCommands.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/BusinessKnowledge/Commands/BusinessKnowledgeCommands.cs)):
  - `CreateBusinessTermCommand`, `UpdateBusinessTermCommand`, `DeleteBusinessTermCommand`, `ToggleBusinessTermStatusCommand`.
  - `CreateMetricDefinitionCommand`, `UpdateMetricDefinitionCommand`, `DeleteMetricDefinitionCommand`.
  - Full FluentValidation for terms, definitions, and names.
- **Queries & Handlers** ([`BusinessKnowledgeQueries.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/BusinessKnowledge/Queries/BusinessKnowledgeQueries.cs)):
  - `GetBusinessTermsQuery`, `GetBusinessTermByIdQuery`.
  - `GetMetricDefinitionsQuery`, `GetMetricDefinitionByIdQuery`.
  - `ResolveSemanticContextQueryHandler`: Given a natural language query, tokenizes the input, queries the stores for relevant terms and metrics, and formats a zero-waste semantic context block for prompt injection.
- **Minimal API Endpoints** ([`BusinessKnowledgeEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/BusinessKnowledgeEndpoints.cs)):
  - Registered under `/api/knowledge/terms`, `/api/knowledge/metrics`, and `/api/knowledge/resolve`.

### AI Coordinator Context Injection
In [`AgentCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/AgentCoordinator.cs):
- Injected `IBusinessTermStore` and `IMetricStore`.
- During `ExecuteQueryPipelineAsync`, retrieves matching active terms and metrics for the user's workspace.
- Appends the `[BUSINESS VOCABULARY & SEMANTIC LAYER]` block directly to `sqlGenPrompt` before delegating to `SqlGeneratorAgent`.

### Frontend UI, State & Interactive Tester
- **API Client & Types** ([`web/src/lib/api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts)):
  - Added `BusinessTermDto`, `MetricDefinitionDto`, and `SemanticContextDto`.
  - Added `api.knowledge.terms.*`, `api.knowledge.metrics.*`, and `api.knowledge.resolve()`.
- **TanStack Query Hooks** ([`web/src/hooks/use-business-knowledge.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/hooks/use-business-knowledge.ts)):
  - Reactive hooks with automated cache invalidation on mutations.
- **Sidebar Integration** ([`web/src/components/layout/Sidebar.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/layout/Sidebar.tsx)):
  - Added "Business Dictionary" navigation link with `BookOpen` icon and "Semantic" badge under Platform section.
- **Management Page & Playground** ([`web/src/app/(app)/knowledge/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/knowledge/page.tsx)):
  - Tab 1: **Business Terms** (Search, category filter, active toggle, aliases badges, SQL mapping pill, edit/delete modals).
  - Tab 2: **Metric Definitions** (Formula code blocks, target benchmarks, SQL snippets, edit/delete modals).
  - Tab 3: **Semantic Tester Playground** (Interactive tester showing matched domain terms, metrics, and live prompt injection preview).

### Verification & Automated Tests (Phase A)
- **Application Unit Tests** ([`BusinessKnowledgeTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/BusinessKnowledgeTests.cs)):
  - Tested Term creation with aliases.
  - Tested Multi-tenant isolation failure checks.
  - Tested Metric definition creation.
  - Tested Semantic resolver prompt block synthesis.
  - Full backend test suite passing: **137/137 tests green**.
- **Frontend Turbopack Production Build**:
  - `npm run build` completed cleanly with **23/23 routes generated**, including static and dynamic routes.

---

## 10. Feature 9: AI Root-Cause Investigation Engine (Phase B)

### Architectural Solution & Investigation Workflow
Traditional analytical assistants simply generate a single SQL query and provide surface-level explanations. However, when an enterprise executive asks:
> *"Why did revenue decline last month?"* or *"Investigate the sudden spike in payment failure rates"*

Understanding the true root cause requires an autonomous, evidence-driven investigation across multiple dimensions and hypotheses.

Phase B introduces the **AI Root-Cause Investigation Engine**, transforming IntelliQueryAI from reactive NL-to-SQL into a proactive, deep diagnostic business analyst.

```
User Diagnostic Question: "Why did revenue decline last month?"
                          │
                          ▼
            [1. Semantic Layer Discovery]
     (Retrieves revenue formulas & glossary from Phase A)
                          │
                          ▼
           [2. Candidate Dimension Discovery]
    (Scans schema metadata for dimensions: tier, region, channel)
                          │
                          ▼
        [3. Baseline & Historical Trend Analysis]
  (Calculates baseline vs current period performance divergence)
                          │
                          ▼
     [4. Dimension Breakdown & Segment Contribution]
(Ranks positive and negative contributors: % impact, value change)
                          │
                          ▼
          [5. Anomaly Correlation & Verification]
 (Correlates with operational monitors and detected variance spikes)
                          │
                          ▼
     [6. Microsoft Agent Framework Investigation Agent]
  (Synthesizes high-conviction root causes & verified evidence)
                          │
                          ▼
    [7. Structured Diagnostic Report & Recommendations]
(Generates waterfall impact analysis, execution SQL, and follow-ups)
```

### Domain Entities, Persistence & PostgreSQL Migration
- **Domain Aggregate & Value Records** ([`InvestigationEntities.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Entities/Investigations/InvestigationEntities.cs)):
  - `InvestigationReport`: Aggregate root storing `Question`, `TargetMetric`, `Summary`, `ConfidenceScore`, `Status`, JSON-serialized arrays of `RootCausesJson`, `ContributorsJson`, `EvidenceJson`, `RecommendationsJson`, `QueriesExecutedJson`, `FollowUpQuestionsJson`, `DurationMs`, and `TokensUsed`.
  - `InvestigationContributor`: Value object with `Dimension`, `Segment`, `ContributionPercentage`, `ImpactDirection`, `BaselineValue`, `CurrentValue`, `ChangeValue`, and `Explanation`.
  - `InvestigationEvidence`: Value object with `Title`, `Description`, `EvidenceType` (`MetricChange`, `DimensionBreakdown`, `AnomalyCorrelation`), `MetricName`, `DataPointsJson`, and `ChartType`.
  - `InvestigationQueryExecuted`: Value object tracking each query executed, its business purpose, row count, and duration.
- **Store Contract & EF Core Configuration** ([`InvestigationStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Persistence/InvestigationStores.cs), [`InvestigationConfigurations.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Configurations/InvestigationConfigurations.cs)):
  - Mapped to `public.investigation_reports` with JSONB columns and compound tenant index `ix_investigation_reports_workspace_created`.
  - `EfInvestigationReportStore`: Tenant-isolated storage registered in DI.
- **PostgreSQL Database Migration** ([`PhaseB_Investigations.sql`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/migrations/PhaseB_Investigations.sql)):
  - Executed and validated in PostgreSQL instance.

### Microsoft Agent Framework Investigation Coordinator
- **Autonomous Agent** ([`InvestigationAgent.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Agents/InvestigationAgent.cs)):
  - `RootCauseInvestigationAgent`: Subclasses `BaseAgent`, bound to `IChatClient`, telemetry, and realistic simulation fallback.
  - Generates structured JSON adhering to `InvestigationAgentFindings` (summary, confidence, root causes, contributors, evidence, recommendations, and follow-up questions).
- **Multi-Agent Coordinator** ([`InvestigationCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/InvestigationCoordinator.cs)):
  - Strict safety limits: `MaxInvestigationSteps = 5`, `MaxDiagnosticQueries = 4`, `QueryTimeoutSeconds = 10`.
  - Seamlessly integrates with Phase A semantic definitions (`IBusinessTermStore`, `IMetricStore`).
  - Discovers candidate dimensions from schema metadata (`ISchemaReadStore`).
  - Executes safe diagnostic queries (`ISafeQueryExecutor`).
  - Sanitizes leaked internal database GUIDs in human-facing text before persisting.

### CQRS Commands, Queries & Endpoints
- **Commands & Handlers** ([`InvestigationCommands.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Investigations/Commands/InvestigationCommands.cs)):
  - `StartInvestigationCommand`: Dispatches to `IInvestigationCoordinator`, stores report, and returns `InvestigationReportDto`.
  - `DeleteInvestigationCommand`: Validates workspace tenant isolation and removes report.
- **Queries & Handlers** ([`InvestigationQueries.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Investigations/Queries/InvestigationQueries.cs)):
  - `GetInvestigationsQuery`: Retrieves paginated history of past investigations.
  - `GetInvestigationByIdQuery`: Retrieves complete investigation report with tenant validation.
- **Minimal API Endpoints** ([`InvestigationEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/InvestigationEndpoints.cs)):
  - `POST /api/investigations`: Starts an autonomous investigation.
  - `GET /api/investigations`: Lists historical investigations for workspace.
  - `GET /api/investigations/{id}`: Returns full investigation details.
  - `DELETE /api/investigations/{id}`: Deletes an investigation.

### Frontend Studio, Waterfall Analytics & Diagnostic Reports
- **API Client & Hooks** ([`web/src/lib/api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts), [`web/src/hooks/use-investigations.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/hooks/use-investigations.ts)):
  - Added TypeScript DTOs and TanStack Query hooks with automated cache invalidation.
- **Navigation Integration** ([`Sidebar.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/layout/Sidebar.tsx)):
  - Added "AI Root-Cause Investigations" with `Microscope` icon and "Deep AI" badge under "Intelligence & Monitoring".
- **Studio Launcher & History Page** ([`web/src/app/(app)/investigations/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/investigations/page.tsx)):
  - Diagnostic prompt launcher with quick template chips, target metric input, and datasource selector.
  - Real-time animated multi-step progress indicator while the agent runs.
  - Investigation history cards with confidence scores, root cause highlights, and execution metrics.
- **Deep Investigation Report View** ([`web/src/app/(app)/investigations/[id]/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/investigations/[id]/page.tsx)):
  - **Executive Summary & Isolated Root Causes**: High-conviction callouts with severity markers.
  - **Ranked Dimension Contributors**: Visual waterfall/progress bars with positive/negative impact badges, baseline vs current values, and analytical explanations.
  - **Supporting Evidence & Verification**: Observation cards with metric deviation data points.
  - **Diagnostic Queries Executed**: Transparent view of all SQL executed with copy-to-clipboard functionality.
  - **Actionable Strategic Recommendations**: Priority-badged cards with rationales and step-by-step checklists.
  - **Interactive Follow-Up Questions**: Clickable suggestion chips to trigger subsequent deep-dive investigations.
- **Cross-linking from Anomaly Explorer** ([`web/src/app/(app)/anomalies/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/anomalies/page.tsx)):
  - Added one-click "Deep Root-Cause Investigation" action directly from detected anomalies.

### Verification & Automated Tests (Phase B)
- **Application Unit Tests** ([`InvestigationTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/InvestigationTests.cs)):
  - Tested multi-step coordination, persistence, and DTO mapping.
  - Tested tenant missing unauthorized check.
  - Tested tenant isolation mismatch on retrieval.
  - Tested list query mapping.
  - Full backend test suite passing: **141/141 tests green**.
- **Frontend Turbopack Production Build**:
  - `npm run build` completed cleanly with **24/24 routes generated**, including `/investigations` and `/investigations/[id]`.

---

## 11. Feature 10: Contextual "Ask AI About This" (Phase C)

### Architectural Overview & Problem Solved
In enterprise analytics platforms, users encounter unexpected deviations, alerts, or anomalies across dozens of screens—dashboards, alert feeds, anomaly lists, executive briefings, and investigation reports. Traditionally, investigating a metric requires opening a separate chatbot, manually retyping numbers, guessing column names, and formulating context from scratch.

**Phase C eliminates this friction** by embedding a unified, generic contextual intelligence layer across the entire platform. Any UI element (KPI card, alert, anomaly, chart, recommendation, or investigation) can be interrogated with a single click.

Crucially, **no redundant chatbot was created**. All contextual inquiries deep-link into and leverage the existing multi-agent pipeline (`AgentCoordinator`, `SqlGenerationAgent`, `AnalysisAgent`, `CriticAgent`) in the AI Query Workspace (`/query`).

```
[UI Surface: KPI / Alert / Anomaly / Report / Recommendation]
                         │
                         ▼
             [<AskAiAboutThis /> Component]
  (Variant: Dropdown / Button / Icon / Badge / Ghost)
  (Provides 1-click tailored diagnostic question menu)
                         │
                         ▼
        Deep-link to /query?contextType=...&entityId=...
                         │
                         ▼
     [Interactive Contextual Intelligence Banner]
   (Displays active context badge, subject, and quick chips)
                         │
                         ▼
      [ExecuteChatQueryCommand + ResolveAiContextQuery]
  • Validates tenant authorization (WorkspaceId == TenantContext)
  • Verifies entity existence and tenant ownership across stores
  • Synthesizes structured Domain Context & Diagnostic Directives
                         │
                         ▼
     [Multi-Agent Pipeline (AgentCoordinator.cs)]
  • sqlGenPrompt: Injected with entity schema, baseline, and thresholds
  • analysisPrompt: Injected with deviation context and variance drivers
                         │
                         ▼
   [Verified Analytical Answer + Auto-Generated Visualizations]
```

### Generic Context Model & Tenant Security
- **Domain Errors** ([`AiErrors.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Errors/AiErrors.cs)):
  - Added `InvalidContext`, `ContextEntityNotFound`, and `ContextTenantMismatch` ensuring strict tenant boundaries.
- **DTOs** ([`ContextualAiDtos.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Ai/Dtos/ContextualAiDtos.cs)):
  - `AiContextDto`: Generic context carrier containing `ContextType` (`kpi`, `metric`, `alert`, `anomaly`, `chart`, `dashboard`, `report`, `recommendation`, `forecast`, `investigation`, `datasource`), `EntityId`, `EntityTitle`, `Metric`, `DataSourceId`, `DateRange`, and `AdditionalMetadata`.
  - `ResolvedAiContextDto`: Contains `FormattedContextPrompt`, `SuggestedQuestions`, `EntitySummary`, and `RelatedMetrics`.
- **CQRS Context Resolver** ([`ResolveAiContextQuery.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Ai/Queries/ResolveAiContext/ResolveAiContextQuery.cs)):
  - Validates `ITenantContext.WorkspaceId == query.WorkspaceId`.
  - Resolves referenced entities across `IAlertStore`, `IMonitorStore`, `IExecutiveReportStore`, `ISavedReportStore`, `IInvestigationReportStore`, `IMetricStore`, and `IDataSourceStore`.
  - Ensures tenant ownership: if an entity belongs to another workspace, immediately returns `AiErrors.ContextTenantMismatch`.
  - Synthesizes domain-specific prompts and targeted follow-up questions tailored to the entity's exact thresholds, baseline values, and current deviations.
- **Minimal API Endpoint** ([`ChatEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/ChatEndpoints.cs)):
  - Registered `POST /context/resolve` across `/api/chat`, `/chat`, and `/api/ai`.

### Multi-Agent Pipeline Integration
- **Agent Coordinator** ([`AgentCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/AgentCoordinator.cs)):
  - Extended `CoordinatorQueryRequest` with `string? ContextPrompt`.
  - Injected `ContextPrompt` into `sqlGenPrompt` and `analysisPrompt`, ensuring SQL generation targets relevant dimensions and analysis explains specific anomalies or baseline variances.
- **Execute Chat Query Command** ([`ExecuteChatQueryCommand.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Ai/Commands/ExecuteChatQuery/ExecuteChatQueryCommand.cs)):
  - Accepts `AiContextDto? Context`.
  - Formats structured `ContextPrompt` prefix for `AgentCoordinator`.

### Reusable Frontend Component & UI Attachments
- **Reusable Component** ([`AskAiAboutThis.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/shared/AskAiAboutThis.tsx)):
  - Supports 5 visual variants: `dropdown` (split button with menu), `button`, `icon`, `badge`, and `ghost`.
  - Supports 4 sizes: `xs`, `sm`, `md`, `lg`.
  - Built-in contextual question banks covering KPIs, Alerts, Anomalies, Charts, Reports, Recommendations, and Investigations.
  - Safely handles click propagation and outside-clicks.
- **Attached Across Key Application Surfaces**:
  1. **Metric Cards & Dashboard**:
     - Added `[✨ Ask AI]` badge to every KPI card header in [`MetricCard.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/shared/MetricCard.tsx).
     - Connected all 4 primary dashboard metrics in [`dashboard/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/dashboard/page.tsx) (Connected Databases, Monitor Health, Active Alerts, Avg Query Latency).
  2. **Alerts Center** ([`alerts/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/alerts/page.tsx)):
     - Added `AskAiAboutThis` dropdown to alert cards and alert details dialog.
  3. **Anomaly Explorer** ([`anomalies/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/anomalies/page.tsx)):
     - Added `AskAiAboutThis` dropdown to anomaly table rows and details dialog.
  4. **Executive Briefings** ([`ExecutiveSummaryCard.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/executive/ExecutiveSummaryCard.tsx), [`intelligence/reports/[id]/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/intelligence/reports/[id]/page.tsx)):
     - Replaced plain links with contextual `AskAiAboutThis` dropdowns.
  5. **Strategic Recommendations** ([`RecommendationCard.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/shared/RecommendationCard.tsx)):
     - Upgraded action triggers to contextual AI dropdowns.
  6. **Root-Cause Investigations** ([`investigations/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/investigations/page.tsx), [`investigations/[id]/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/investigations/[id]/page.tsx)):
     - Added contextual inquiry triggers to investigation cards and deep diagnostic report headers.
  7. **Trajectory Charts** ([`intelligence/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/intelligence/page.tsx)):
     - Added `AskAiAboutThis` to the 30-day activity trajectory chart.
- **AI Query Workspace Deep Integration** ([`query/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/query/page.tsx)):
  - Parses contextual URL parameters (`contextType`, `entityId`, `contextTitle`, `metric`, `dataSourceId`, `dateRange`, `meta`).
  - Renders interactive **Contextual Intelligence Banner** highlighting active context type, target entity name, and quick-inquiry suggestion chips.
  - Provides a 1-click **"Clear Context"** button to reset back to open-ended query mode.
  - Automatically transmits the structured context to `api.chat.query`.

### Verification & Automated Tests (Phase C)
- **Application Unit Tests** ([`ContextualAiTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/ContextualAiTests.cs)):
  - Verified KPI context resolution with baseline comparisons.
  - Verified Alert context resolution with threshold variance calculations.
  - Verified entity tenant mismatch unauthorized failure enforcement.
  - Verified non-existent entity failure handling.
  - Verified contextual query execution with `ContextPrompt` injection.
  - Full backend test suite passing: **146/146 tests green (100% success)**.
- **Frontend Turbopack Production Build**:
  - `npm run build` completed cleanly with **24/24 routes generated** and 0 TypeScript errors.

---

## 12. Feature 11: Morning Executive Brief (Phase D)

### Architectural Overview & Problem Solved
Executive leaders and department heads do not have time to navigate multiple telemetry dashboards, query builders, and alert tables every morning to assess organizational health. They require a concise, prioritized answer to the core executive question:

> *"Good morning, {Name}. Here's what changed in your business since yesterday."*

**Phase D** delivers a personalized, dynamic, and prioritized **Morning Executive Brief** integrated into the existing Dashboard. It bridges the Phase A Semantic Layer (`IMetricStore`), active operational monitors (`IMonitorStore`), statistical anomaly detection (`IAnomalyDetectionService`), and historical query execution telemetry.

Crucially:
1. **Zero Hardcoded Business Metrics**: Metrics are dynamically resolved from the workspace's configured Semantic Layer and active AI Monitors.
2. **Configurable Time Horizons**: Executives can toggle between *"Since yesterday"*, *"Last 24 hours"*, *"This week"*, *"Week-to-date"*, and *"Month-to-date"*.
3. **Prioritization Engine**: Findings, risks, and anomalies are mathematically scored and ranked so the most critical items are displayed first.
4. **Contextual Actionability**: Every metric change, alert, and recommendation has 1-click **[Ask AI]** and **[Investigate]** deep-links.

```
[Dashboard Visit / Period Switch / On-Demand Regeneration]
                           │
                           ▼
            [GetMorningExecutiveBriefQuery]
  • Validates tenant authorization (WorkspaceId == TenantContext)
  • Resolves dynamic metrics from IMetricStore (Semantic Layer)
  • Resolves monitor executions & baseline variance from IMonitorStore
  • Resolves recent open/critical alerts from IAlertStore
  • Analyzes query volume & latency trends from IQueryHistoryStore
                           │
                           ▼
              [Prioritization Engine]
  • Rank 1: Critical Alerts & High Z-Score Anomalies (Impact: 100)
  • Rank 2: High Financial Deviations (MRR/ARR shifts > 15%)
  • Rank 3: Emerging Operational Risks & Downward Trends
  • Rank 4: Growth Opportunities & Accelerating Metrics
                           │
                           ▼
        [Multi-Agent Coordinator & Store Resolution]
  • Injects KeyMetrics, AlertSummaries, and MonitorSummaries into LLM
  • Sanitizes all text (defensively strips raw workspace GUIDs)
                           │
                           ▼
           [<MorningExecutiveBriefCard />]
  • Time-aware personalized greeting ("Good morning, {User}")
  • Period pill switcher ("Since yesterday", "This week", "Month-to-date")
  • Dynamic metrics row with % change badges and trend indicators
  • Critical Attention callout for severe alerts/spikes
  • 3-column ranked grid: Findings, Emerging Risks, Opportunities
  • Prioritized Strategic Recommendations with execution checklists
  • 1-click "Ask AI" and "Investigate Root Cause" on every item
```

### Backend Domain, CQRS & Minimal APIs
- **DTOs** ([`MorningExecutiveBriefDtos.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Dashboard/Dtos/MorningExecutiveBriefDtos.cs)):
  - `MorningExecutiveBriefDto`: Encapsulates greeting, headline, period, summary narrative, dynamic metrics, critical alerts, major anomalies, ranked findings, emerging risks, opportunities, and recommendations.
  - `ExecutiveBriefMetricDto`: Name, CurrentValue, FormattedValue, BaselineValue, ChangePercentage, ChangeDirection, Status, Priority, Category, MetricId, MonitorId, Explanation.
  - `ExecutiveBriefAlertDto`, `ExecutiveBriefAnomalyDto`, `ExecutiveBriefFindingDto`, `ExecutiveBriefRiskDto`, `ExecutiveBriefOpportunityDto`.
- **CQRS Query & Prioritization Handler** ([`GetMorningExecutiveBriefQuery.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Dashboard/Queries/GetMorningExecutiveBrief/GetMorningExecutiveBriefQuery.cs)):
  - Enforces `ITenantContext.WorkspaceId == query.WorkspaceId`.
  - Dynamically calculates time horizon cutoff based on normalized period.
  - Dynamically maps semantic metric definitions and compares latest monitor executions against baselines.
  - Implements multi-tier scoring algorithm to rank findings and emerging risks.
  - Sanitizes output to guarantee business display names rather than internal database GUIDs.
- **CQRS Generation Command** ([`GenerateMorningExecutiveBriefCommand.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Dashboard/Commands/GenerateMorningExecutiveBrief/GenerateMorningExecutiveBriefCommand.cs)):
  - Coordinates on-demand multi-agent synthesis via `IExecutiveCoordinator.GenerateExecutiveBriefingAsync`.
  - Persists structured brief in `IExecutiveReportStore`.
- **Minimal API Endpoints** ([`DashboardEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/DashboardEndpoints.cs)):
  - `GET /api/dashboard/morning-brief`: Retrieves morning brief for specified period and user.
  - `POST /api/dashboard/morning-brief/generate`: Triggers on-demand multi-agent generation for specified period.

### Frontend Component & Dashboard Enhancement
- **API Client Integration** ([`api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts)):
  - Added TypeScript contracts and `api.dashboard.morningBrief` / `api.dashboard.generateMorningBrief`.
- **Executive Card Component** ([`MorningExecutiveBriefCard.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/executive/MorningExecutiveBriefCard.tsx)):
  - Hero header with client/server time-aware greeting and period switcher segmented control.
  - Dynamic metrics row with formatted currency/rate/latency numbers, % change indicators, and status badges.
  - Critical Attention callout banner when threshold breaches or statistical anomalies occur.
  - 3-column prioritized grid highlighting Ranked Findings, Emerging Risks, and Opportunities.
  - Prioritized Actionable Recommendations with actionable step checklists.
  - Contextual **[Ask AI]** dropdowns and **[Investigate]** links on every metric, finding, and recommendation.
- **Dashboard Integration** ([`dashboard/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/dashboard/page.tsx)):
  - Placed `MorningExecutiveBriefCard` at the top of the dashboard.
  - Connected the header "Generate AI Briefing" action to execute the period-aware mutation.
  - Preserved 100% of existing functionality (KPI stat cards, tabs, query history pagination, monitors list, alerts).

### Verification & Automated Tests (Phase D)
- **Application Unit Tests** ([`MorningExecutiveBriefTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/MorningExecutiveBriefTests.cs)):
  - Verified valid workspace returns dynamic metrics and prioritized findings.
  - Verified period normalization ("This week", "Since yesterday") and horizon calculation.
  - Verified critical alerts rank first in findings.
  - Verified tenant mismatch returns unauthorized failure.
  - Verified on-demand generation calls coordinator and persists brief.
  - Full backend test suite passing: **151/151 tests green (100% success)** across Domain (57), Application (86), and Integration (8).
- **Frontend Turbopack Production Build**:
  - `npm run build` completed cleanly with **24/24 routes generated** and 0 TypeScript errors.

---

## 13. Feature 12: AI Dashboard Generator (Phase E)

### Architectural Overview & Problem Solved
Analytics dashboards are traditionally cumbersome to design, requiring manual metric selection, query authoring, visualization configuration, and layout positioning. Users want to generate complete, executive-grade analytical dashboards on the fly using natural language (e.g., *"Create an executive revenue dashboard comparing this year with last year"* or *"Build a customer retention dashboard tracking cohort churn"*).

However, allowing an LLM to generate arbitrary React or JavaScript code introduces catastrophic security, XSS, and stability vulnerabilities.

**Phase E** solves this with the **AI Dashboard Generator**:
1. **Zero Executable Frontend Code Generation**: The LLM outputs **ONLY** a safe, strictly typed JSON definition (`DashboardAgentResult`) adhering to a deterministic schema.
2. **Strict Widget Whitelist Gatekeeper**: `DashboardWidgetValidator` strictly whitelists 6 supported visualization primitives:
   - `metric` (Single KPI stat card with baseline & trend indicator)
   - `lineChart` (Time-series trend curves with smooth interpolation)
   - `barChart` (Categorical comparisons with rounded gradient bars)
   - `areaChart` (Cumulative volume distributions with gradient fills)
   - `pieChart` (Composition and donut breakdowns with legend)
   - `table` (Tabular dataset records with column-level sorting/paging)
3. **Semantic Layer & Schema Grounding**: `DashboardCoordinator` injects active metrics from `IMetricStore` (Phase A) and cataloged tables from `ISchemaReadStore` to synthesize grounded, safe SELECT SQL queries.
4. **Full Operational Lifecycle**: Users can generate with AI, save, view, edit title/description, change time horizons, duplicate, delete, refresh live widget data on-demand, and initiate deep contextual AI inquiries via **[✨ Ask AI]** on every widget.

```
                     [Natural Language Prompt]
                                │
                                ▼
                   [DashboardCoordinator]
  • Injects Active Semantic Metrics (IMetricStore)
  • Injects Cataloged Schema Metadata (ISchemaReadStore)
                                │
                                ▼
               [DashboardGeneratorAgent (MAF)]
  • Synthesizes layout, metrics, dimensions, aggregations, and safe SQL
  • Enforces JSON schema and runs in offline simulation mode if configured
                                │
                                ▼
              [DashboardWidgetValidator (Security)]
  • Validates every widget against whitelist: [metric, lineChart, barChart, areaChart, pieChart, table]
  • Rejects unauthorized scripts, tags, or unknown widget types
                                │
                                ▼
      [Persistence in PostgreSQL ("custom_dashboards")]
  • Saves layout_json, prompt, time horizon, and workspace tenant ownership
                                │
                                ▼
                 [Interactive Frontend Engine]
  • /dashboards: Gallery with instant templates, search, duplicate, delete
  • /dashboards/[id]: Multi-column responsive layout (Recharts) with live refresh & Ask AI
```

### Backend Domain, Persistence, CQRS & Minimal APIs
- **Domain Entities & Validator** ([`CustomDashboardEntities.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Entities/Dashboards/CustomDashboardEntities.cs)):
  - `CustomDashboard`: Aggregate root encapsulating `WorkspaceId`, `CreatedByUserId`, `Title`, `Description`, `Prompt`, `LayoutJson`, `DateRange`, `IsDefault`, `IsShared`, `CreatedAtUtc`, and `UpdatedAtUtc`.
  - `DashboardWidgetDefinition`: Value record defining `Id`, `Title`, `WidgetType`, `Metric`, `Dimension`, `Aggregation`, `SqlQuery`, `DateRange`, `ColSpan`, and `RowSpan`.
  - `DashboardWidgetValidator`: Strict security whitelist validating and normalizing supported widget types.
- **Persistence Store & EF Core Mapping** ([`DashboardStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Persistence/DashboardStores.cs), [`DashboardConfigurations.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Configurations/DashboardConfigurations.cs), [`EfDashboardStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Stores/EfDashboardStores.cs)):
  - Maps to table `custom_dashboards` with JSONB column `layout_json` and compound index `ix_custom_dashboards_workspace_created`.
  - Migration script executed in PostgreSQL container: [`PhaseE_CustomDashboards.sql`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/migrations/PhaseE_CustomDashboards.sql).
- **Microsoft Agent Framework** ([`DashboardAgent.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Agents/DashboardAgent.cs), [`DashboardCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/DashboardCoordinator.cs)):
  - `DashboardGeneratorAgent`: Subclasses `BaseAgent` with system prompt strictly constraining outputs to whitelisted JSON structures.
  - `DashboardCoordinator`: Coordinates context assembly and provides `ExecuteWidgetQueryAsync` for live widget hydration.
- **CQRS Application Commands & Queries** ([`CustomDashboardCommands.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Dashboards/Commands/CustomDashboardCommands.cs), [`CustomDashboardQueries.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Dashboards/Queries/CustomDashboardQueries.cs)):
  - `GenerateDashboardWithAiCommand`: Runs agent coordinator and returns parsed draft dashboard DTO.
  - `CreateCustomDashboardCommand`: Validates all widget types and persists new dashboard.
  - `UpdateCustomDashboardCommand`: Updates title, description, layout, date range, and sharing.
  - `DuplicateCustomDashboardCommand`: Clones existing dashboard with "Copy of" prefix.
  - `DeleteCustomDashboardCommand`: Removes dashboard after verifying tenant isolation.
  - `GetCustomDashboardsQuery`: Retrieves all custom dashboards for the active workspace.
  - `GetCustomDashboardByIdQuery`: Retrieves dashboard by ID, optionally hydrating live query rows into widgets.
  - `ExecuteDashboardWidgetQuery`: Refreshes individual widget data on demand.
- **Minimal API Endpoints** ([`CustomDashboardEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/CustomDashboardEndpoints.cs)):
  - `POST /api/dashboards/generate`
  - `GET /api/dashboards`
  - `POST /api/dashboards`
  - `GET /api/dashboards/{id}`
  - `PUT /api/dashboards/{id}`
  - `POST /api/dashboards/{id}/duplicate`
  - `DELETE /api/dashboards/{id}`
  - `POST /api/dashboards/{id}/widgets/{widgetId}/query`

### Frontend UI & Visualization Components
- **API Client & Hooks** ([`api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts), [`use-dashboards.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/hooks/use-dashboards.ts)):
  - Added TypeScript contracts `CustomDashboardDto`, `DashboardWidgetDto`, and full `api.dashboards` client object.
  - Added React Query hooks: `useDashboards`, `useDashboard`, `useGenerateDashboard`, `useCreateDashboard`, `useUpdateDashboard`, `useDuplicateDashboard`, `useDeleteDashboard`, and `useExecuteWidgetQuery`.
- **Sidebar Navigation** ([`Sidebar.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/layout/Sidebar.tsx)):
  - Added **"Custom Dashboards"** navigation item with `LayoutGrid` icon and **"AI Builder"** highlight badge.
- **Widget Renderer** ([`DashboardWidgetRenderer.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/dashboards/DashboardWidgetRenderer.tsx)):
  - Renders all 6 whitelisted widget types:
    - `metric`: Formatted stat card with baseline and % change pill.
    - `lineChart`: Recharts Line chart with smooth monotone interpolation, tooltips, and grid.
    - `barChart`: Recharts Bar chart with color palette and rounded corners.
    - `areaChart`: Recharts Area chart with gradient fill.
    - `pieChart`: Recharts Donut/Pie chart with custom legend.
    - `table`: Sleek dark mode table with scrollable records.
  - Individual widget live query refresh and toggle between visual chart and raw data table.
  - Deep integration with `AskAiAboutThis` (`contextType="dashboardwidget"`) on every widget card.
- **AI Generator Modal** ([`DashboardGeneratorModal.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/dashboards/DashboardGeneratorModal.tsx)):
  - Natural language prompt textarea with quick-start templates (Executive Financials, Customer Retention, API Latency, Sales Funnel).
  - Target data source and time horizon selectors.
  - Animated multi-agent coordinator progress indicators.
  - Interactive draft widget preview and verified security check before saving.
- **Dashboard Gallery Page** ([`dashboards/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/dashboards/page.tsx)):
  - Hero header with AI generator action.
  - Instant starter template cards.
  - Real-time search and filter.
  - Dashboard cards with widget type breakdown pills, duplicate, delete, and view actions.
- **Interactive Viewer & Editor Page** ([`dashboards/[id]/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/dashboards/[id]/page.tsx)):
  - Inline title editing with save/cancel controls.
  - Time horizon switcher ("Last 7 Days", "Last 30 Days", "This Quarter", "Year to Date").
  - Live "Refresh All" button.
  - Responsive 4-column CSS grid supporting 1, 2, 3, or 4 column spans.
  - Delete confirmation modal and add widget trigger.

### Verification & Automated Tests (Phase E)
- **Application Unit Tests** ([`CustomDashboardTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/CustomDashboardTests.cs)):
  - Verified `GenerateDashboardWithAiCommand` coordinates with agent and parses structured widgets.
  - Verified `CreateCustomDashboardCommand` rejects unsupported widget types (security whitelist enforcement).
  - Verified valid widget types save and persist to store.
  - Verified tenant isolation and not-found handling.
  - Verified dashboard duplication creates copy with "Copy of" title.
  - Verified `DashboardWidgetValidator` strictly whitelists valid types and rejects script/iframe injection attempts.
  - Full backend test suite passing: **157/157 tests green (100% success)** across Domain (57), Application (92), and Integration (8).
- **Frontend Turbopack Production Build**:
  - `npm run build` completed cleanly with **25/25 routes generated** (including `/dashboards` and `/dashboards/[id]`) and 0 TypeScript errors.

---

## 14. Feature 13: Enterprise Data Governance (Phase F)

### Core Objective & Invariants
In enterprise BI and analytics platforms, user roles and database permissions dictate access to sensitive data (PII, Financial records, Secrets, and Confidential metrics). When AI models (LLMs) are introduced into analytical pipelines, an unprecedented security vector emerges: **AI Data Leakage**. If sample rows or query outputs containing raw credit cards, social security numbers, or employee salaries are serialized into prompts sent to third-party AI APIs (e.g. Azure OpenAI), sensitive enterprise data exits the corporate security perimeter.

Phase F enforces **Enterprise Data Governance**:
1. **Core Principle**: AI must **NEVER** bypass user permissions or corporate data governance boundaries.
2. **Strict Zero-Leakage AI Shield**: Whenever a query is executed by the multi-agent pipeline, sample rows are masked in-memory **BEFORE** LLM prompt context is constructed. Unauthorized or restricted values never reach external AI APIs.
3. **Hierarchy**: `Data Source` -> `Schema` -> `Table` -> `Column` -> `Row`.
4. **Data Classification Tiers**:
   - `Public`: Unrestricted general data.
   - `Internal`: Operational data restricted to organization members.
   - `Confidential`: Sensitive internal business data.
   - `Restricted`: Highly sensitive operational attributes.
   - `Pii`: Personally Identifiable Information (SSN, phone, address, national ID).
   - `Financial`: PCI-DSS payment data, revenue, bank details.
   - `Secret`: Passwords, API tokens, encryption keys.
5. **Masking Strategies**:
   - `None`: Raw values pass through.
   - `Full`: Completely redacted (`********`).
   - `Partial`: Retains trailing 4 characters (e.g., `************4444` for payment cards or SSN).
   - `Email`: Masks username portion (e.g., `a***@corp.com`).
   - `Hash`: Deterministic SHA-256 fingerprint (`[HASH:1a2b3c4d5e]`).
   - `Nullify`: Masked as restricted indicator (`[RESTRICTED]`).
6. **Role-Based Access Control (RBAC)**: Allowed roles (e.g. `Owner`, `Admin`, `Analyst`) can view unmasked records in user-facing dashboards, while unauthorized roles always receive masked data.
7. **Comprehensive Audit Trail**: Every sensitive data access and policy lifecycle event is recorded in `audit_logs` table (`IAuditLogStore`).

### Architecture & Backend Implementation
- **Domain Layer**:
  - Enums: [`GovernanceEnums.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Enums/GovernanceEnums.cs) (`DataClassification`, `MaskingStrategy`).
  - Aggregate Root: [`GovernanceEntities.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Entities/Governance/GovernanceEntities.cs) (`GovernancePolicy` with `Matches()`, `IsRoleAllowed()`, and `ToggleActive()`).
  - Domain Service: [`DataMaskingEngine.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Services/DataMaskingEngine.cs) implementing `IDataMaskingEngine` with deterministic string masking, row masking, and multi-row batch transformation.
  - Persistence Contract: [`GovernanceStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Persistence/GovernanceStores.cs) (`IGovernancePolicyStore`).
  - Domain Errors: [`AiErrors.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Errors/AiErrors.cs) (`GovernancePolicyNotFound`, `GovernanceTenantMismatch`, `UnauthorizedDataFieldAccess`).
- **Infrastructure Layer**:
  - EF Core Configuration: [`GovernanceConfigurations.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Configurations/GovernanceConfigurations.cs) mapping to table `governance_policies` with composite indexes.
  - EF Core Store: [`EfGovernanceStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Stores/EfGovernanceStores.cs) (`EfGovernancePolicyStore`).
  - DbContext & DI: Registered `DbSet<GovernancePolicy> GovernancePolicies`, `IGovernancePolicyStore`, and `IDataMaskingEngine` in [`IntelliQueryDbContext.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/IntelliQueryDbContext.cs) and [`DependencyInjection.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/DependencyInjection.cs).
  - PostgreSQL Migration: [`PhaseF_EnterpriseDataGovernance.sql`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/migrations/PhaseF_EnterpriseDataGovernance.sql) applied to PostgreSQL database container `intelliquery_postgres`.
- **AI Agent Pipeline & Pre-Prompt Redaction**:
  - In [`AgentCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/AgentCoordinator.cs): Injected `IGovernancePolicyStore`, `IDataMaskingEngine`, and `IAuditLogStore`.
  - Prior to serializing sample rows into `analysisPrompt` for `DataAnalystAgent` and `vizPrompt` for `VisualizationAgent`, rows are filtered through `_maskingEngine.MaskRows(..., isAiContext: true)`. If `policy.IsAiMaskingEnforced = true`, sensitive values are masked even if the user has an Admin role.
  - Final query execution rows returned to the user are dynamically masked according to the user's role, and sensitive accesses are logged to `IAuditLogStore`.
  - In [`DashboardCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/DashboardCoordinator.cs): Masking engine applied to live widget query executions.
- **Application CQRS Handlers**:
  - DTOs: [`GovernanceDtos.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Governance/Dtos/GovernanceDtos.cs).
  - Commands: [`GovernanceCommands.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Governance/Commands/GovernanceCommands.cs) (`CreateGovernancePolicyCommand`, `UpdateGovernancePolicyCommand`, `ToggleGovernancePolicyCommand`, `DeleteGovernancePolicyCommand`).
  - Queries: [`GovernanceQueries.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Governance/Queries/GovernanceQueries.cs) (`GetGovernancePoliciesQuery`, `GetGovernancePolicyByIdQuery`, `TestMaskingPreviewQuery`, `GetGovernanceAuditLogsQuery`).
- **Minimal API Endpoints**:
  - Registered in [`GovernanceEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/GovernanceEndpoints.cs) and mapped in [`Program.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Program.cs):
    - `GET /api/governance/policies`
    - `POST /api/governance/policies`
    - `GET /api/governance/policies/{id}`
    - `PUT /api/governance/policies/{id}`
    - `DELETE /api/governance/policies/{id}`
    - `POST /api/governance/policies/{id}/toggle`
    - `POST /api/governance/preview-mask`
    - `GET /api/governance/audit`

### Frontend UI & Administration
- **API Client & Hooks** ([`api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts), [`use-governance.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/hooks/use-governance.ts)):
  - Added full TypeScript contracts and `api.governance` methods.
  - Added React Query hooks: `useGovernancePolicies`, `useGovernancePolicy`, `useCreateGovernancePolicy`, `useUpdateGovernancePolicy`, `useDeleteGovernancePolicy`, `useToggleGovernancePolicy`, `usePreviewMasking`, and `useGovernanceAuditLogs`.
- **Sidebar Navigation** ([`Sidebar.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/layout/Sidebar.tsx)):
  - Added **"Data Governance"** navigation item with `ShieldCheck` icon and **"RBAC"** badge under Platform.
- **Administration Screen** ([`governance/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/governance/page.tsx)):
  - **KPI Cards**: Active Policies, Zero-Leakage AI Shield Enforced, Governed Columns, Sensitive Access Audits.
  - **Policies Tab**: Filterable, searchable table with active toggle, target table/col badges, classification tags with color-coding, allowed role pills, edit/delete actions.
  - **Masking Playground**: Interactive sandbox allowing administrators to test masking strategies against sample inputs (Credit Card, Corporate Email, SSN, API Secret, Salary) with side-by-side before & after visual comparison.
  - **Audit Trail Tab**: Live table of all security access and governance actions.
  - **Create / Edit Modal Dialog**: Form for target table/column, classification, masking strategy, allowed roles checkboxes, and strict AI Prompt Masking enforcement.

### Verification & Automated Tests (Phase F)
- **Application Unit Tests** ([`GovernanceTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/GovernanceTests.cs)):
  - Verified all masking strategies (`Full`, `Partial`, `Email`, `Hash`, `Nullify`, `None`).
  - Verified **Critical Zero AI Data Leakage Rule**: `isAiContext = true` with `isAiMaskingEnforced = true` always masks sensitive columns even when the caller has an Admin role.
  - Verified user presentation context allows authorized roles while redacting for unauthorized members.
  - Verified command handlers (`Create`, `Toggle`) persist policies and audit logs.
  - Full test suite passing: **169/169 tests green (100% success)** across Domain (57), Application (104), and Integration (8).
- **Frontend Turbopack Production Build**:
  - `npm run build` completed cleanly with **26/26 routes generated** (including `/governance`) and 0 TypeScript errors.

---

## 15. Feature 14: What-If / Scenario Analysis (Phase G)

### Core Objective & Invariants
Enterprise leaders, financial controllers, and operational planners require the ability to simulate hypothetical business outcomes based on prospective shifts in variables (e.g. "What happens to ARR if organic pipeline expands by 8% while customer churn increases by 3%?").

Phase G enforces **Deterministic What-If / Scenario Analysis**:
1. **Core Calculation Rule**: AI must **NEVER** present speculation as fact. All numerical projections (baseline, expected compound value, best-case with tailwinds, worst-case stress-test with headwinds, and monthly step progressions) are calculated via **deterministic mathematical and statistical formulas** in domain services.
2. **AI Synergy**: The Microsoft Agent Framework `ScenarioAnalystAgent` synthesizes qualitative sensitivity drivers, downside risk vulnerabilities, upside catalysts, and strategic mitigations from the deterministic calculation outputs, semantic dictionary definitions, and business metrics.
3. **Multi-Horizon Progression**: Calculates monthly discrete progression steps across "Next Quarter" (3 months), "Next 6 Months" (6 months), and "Next 12 Months" (12 months).
4. **Weighted Levers**: Each variable lever supports customizable baseline values, percentage shifts (+/-), and importance weighting ($0.1x$ to $5.0x$) affecting the compound trajectory.
5. **Interactive Levers & Sandbox**: Instant live recalculation on client sliders and sandbox simulation endpoint (`/api/scenarios/simulate`) before persisting.

### Architecture & Backend Implementation
- **Domain Layer**:
  - Aggregate Root: [`ScenarioEntities.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Entities/Scenarios/ScenarioEntities.cs) (`Scenario : AggregateRoot` with `WorkspaceId`, `CreatedByUserId`, `Title`, `Description`, `TargetMetric`, `BaselineValue`, `TimeHorizon`, `VariablesJson`, `ProjectionsJson`, `AiExplanation`, `ConfidenceScore`, `GetVariables()`, `GetProjections()`, and `UpdateSimulationResults()`). Also defines `ScenarioVariable`, `ScenarioProjection`, and `MonthlyDataPoint`.
  - Deterministic Domain Service: [`ScenarioSimulationEngine.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Services/ScenarioSimulationEngine.cs) implementing `IScenarioSimulationEngine` computing weighted compound growth, expected trajectory, best case (+tailwind variance), worst case (stress test), and monthly step points.
  - Persistence Contract: [`ScenarioStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Persistence/ScenarioStores.cs) (`IScenarioStore`).
  - Domain Errors: [`AiErrors.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Errors/AiErrors.cs) (`ScenarioNotFound`, `ScenarioTenantMismatch`, `InvalidScenarioVariables`).
- **Infrastructure Layer**:
  - EF Core Configuration: [`ScenarioConfigurations.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Configurations/ScenarioConfigurations.cs) mapping to table `scenarios` with composite indexes `ix_scenarios_workspace_created` and `ix_scenarios_metric`.
  - EF Core Store: [`EfScenarioStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Stores/EfScenarioStores.cs) (`EfScenarioStore`).
  - DbContext & DI: Registered `DbSet<Scenario> Scenarios`, `IScenarioStore`, and `IScenarioSimulationEngine` in [`IntelliQueryDbContext.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/IntelliQueryDbContext.cs) and [`DependencyInjection.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/DependencyInjection.cs).
  - PostgreSQL Migration: [`PhaseG_ScenarioAnalysis.sql`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/migrations/PhaseG_ScenarioAnalysis.sql) applied to PostgreSQL database container `intelliquery_postgres`.
- **Microsoft Agent Framework Integration**:
  - Specialized Agent: [`ScenarioAnalystAgent.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Agents/ScenarioAnalystAgent.cs) producing structured JSON narrative (`summary`, `keyDrivers`, `riskFactors`, `upsideCatalysts`, `strategicRecommendations`) with simulation mode offline testing fallback.
  - Coordinator: [`ScenarioCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/ScenarioCoordinator.cs) implementing `IScenarioCoordinator` orchestrating deterministic calculation engine, semantic layer stores (`IBusinessTermStore`, `IMetricStore`), and `ScenarioAnalystAgent`.
  - Registered in [`DependencyInjection.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/DependencyInjection.cs).
- **Application CQRS Handlers**:
  - DTOs: [`ScenarioDtos.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Scenarios/Dtos/ScenarioDtos.cs).
  - Commands: [`ScenarioCommands.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Scenarios/Commands/ScenarioCommands.cs) (`CreateScenarioCommand`, `RerunScenarioSimulationCommand`, `UpdateScenarioCommand`, `DeleteScenarioCommand`).
  - Queries: [`ScenarioQueries.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Scenarios/Queries/ScenarioQueries.cs) (`GetScenariosQuery`, `GetScenarioByIdQuery`, `QuickSimulateScenarioQuery`).
- **Minimal API Endpoints**:
  - Registered in [`ScenarioEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/ScenarioEndpoints.cs) and mapped in [`Program.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Program.cs):
    - `GET /api/scenarios` (workspace scenarios list)
    - `POST /api/scenarios` (create & simulate)
    - `GET /api/scenarios/{id}` (scenario details)
    - `PUT /api/scenarios/{id}` (update title/description)
    - `DELETE /api/scenarios/{id}` (delete scenario)
    - `POST /api/scenarios/{id}/rerun` (recalculate simulation with modified assumptions)
    - `POST /api/scenarios/simulate` (sandbox sandbox calculation)

### Frontend UI & Workspace
- **API Client & Hooks** ([`api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts), [`use-scenarios.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/hooks/use-scenarios.ts)):
  - Added full TypeScript contracts and `api.scenarios` methods (`list`, `get`, `create`, `update`, `delete`, `rerun`, `simulate`).
  - Added React Query hooks: `useScenarios`, `useScenario`, `useCreateScenario`, `useUpdateScenario`, `useDeleteScenario`, `useRerunScenario`, `useSimulateQuickScenario`.
- **Sidebar Navigation** ([`Sidebar.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/layout/Sidebar.tsx)):
  - Added **"Scenario Analysis"** navigation item with `GitBranch` icon and **"What-If"** badge under Intelligence & Monitoring.
- **Scenarios Gallery Screen** ([`scenarios/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/scenarios/page.tsx)):
  - **Executive Starter Templates**: Revenue & Churn Shock Model, Price Increase & Demand Elasticity, Operating Margin Optimization, Marketing Channel Shift & CAC Realignment.
  - **Gallery Cards**: Target metric, baseline value, expected delta pill with trend indicator, best/worst case previews, variable lever tags, confidence badge, and delete/simulate actions.
  - **Create Modal with Sandbox Preview**: Live deterministic calculation test before saving.
- **Interactive Workspace Screen** ([`scenarios/[id]/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/scenarios/[id]/page.tsx)):
  - **4 Comparative KPI Cards**: Baseline Reference, Expected Projection (+/- delta % and amount), Best Case (Tailwinds), Worst Case (Stress Test).
  - **Comparative Trajectory Recharts Curve**: Monthly progression line and envelope spread options comparing Baseline (dashed), Worst Case (rose), Expected (indigo), and Best Case (emerald).
  - **Interactive Levers Panel**: Live sliders (-50% to +50%), weight indicators, instant client recalculation, sensitivity ranking matrix, and "Save Assumptions & Run AI" button.
  - **Executive AI Strategic Narrative**: Parsed into Executive Summary, Key Mathematical Drivers, Downside Vulnerabilities, Upside Catalysts, and Strategic Recommendations.
  - **Ask AI Integration**: Integrated `<AskAiAboutThis contextType="scenario" />` in header and narrative cards.

### Verification & Automated Tests (Phase G)
- **Application Unit Tests** ([`ScenarioTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/ScenarioTests.cs)):
  - Verified deterministic mathematical bounds (expected, tailwind best-case, and headwind worst-case stress-test).
  - Verified discrete monthly step progression points across time horizons.
  - Verified create scenario command persists aggregate with audit metadata.
  - Verified strict tenant isolation preventing cross-workspace leakage.
  - Verified delete scenario command removes persisted records.
  - Full test suite passing: **176/176 tests green (100% success)** across Domain (57), Application (111), and Integration (8).
- **Frontend Turbopack Production Build**:
  - `npm run build` completed cleanly with **27/27 routes generated** (including `/scenarios` and `/scenarios/[id]`) and 0 TypeScript errors.

---

## 16. Feature 15: Recommendation Engine (Phase H)

### Core Objective & Invariants
Enterprise BI and analytics systems often present rich metric dashboards and anomaly charts without prescriptive next steps. Phase H introduces an autonomous, evidence-backed **Recommendation Engine** that synthesizes cross-cutting signals across:
- **Anomalies** & statistical spikes
- **Monitoring Alerts** & threshold breaches
- **Root-Cause Investigations** & diagnostic findings
- **What-If Scenarios** & downside stress-test risks
- **Semantic Dictionary Metrics** & business terms

**Core Invariants**:
1. **Evidence-Backed**: Every recommendation must be anchored in concrete supporting evidence (metric anomalies, baseline variances, alert triggers, scenario risks) and explicit operational action steps.
2. **Human-In-The-Loop Safety Guardrail**: AI must **NEVER automatically execute destructive business actions** (contract renegotiation, cluster reconfiguration, database schema drops, policy changes). Recommendations are presented to authorized operators to review evidence, launch deep root-cause investigations, accept & apply, dismiss with justification, or query interactively via Ask AI.
3. **State Machine Lifecycle**: Recommendations progress through `Active` -> `UnderReview` -> `Applied` or `Dismissed`, with full reactivation capability.

### Architecture & Backend Implementation
- **Domain Layer**:
  - Aggregate Root: [`RecommendationEntities.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Entities/Recommendations/RecommendationEntities.cs) (`Recommendation : AggregateRoot` with `WorkspaceId`, `CreatedByUserId`, `Title`, `Rationale`, `Category`, `Priority`, `Status`, `TargetMetric`, `ImpactMagnitude`, `EstimatedEffort`, `EvidenceJson`, `ActionStepsJson`, `MarkApplied()`, `Dismiss()`, `SetUnderReview()`, `Reactivate()`). Also defines `RecommendationEvidenceItem` and `RecommendationActionStep`.
  - Persistence Contract: [`RecommendationStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Persistence/RecommendationStores.cs) (`IRecommendationStore`).
  - Domain Errors: [`AiErrors.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Errors/AiErrors.cs) (`RecommendationNotFound`, `RecommendationTenantMismatch`, `RecommendationAlreadyApplied`, `RecommendationAlreadyDismissed`).
- **Infrastructure Layer**:
  - EF Core Configuration: [`RecommendationConfigurations.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Configurations/RecommendationConfigurations.cs) mapping to table `recommendations` with composite indexes `ix_recommendations_workspace_status`, `ix_recommendations_workspace_created`, and `ix_recommendations_priority`.
  - EF Core Store: [`EfRecommendationStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Stores/EfRecommendationStores.cs) (`EfRecommendationStore`).
  - DbContext & DI: Registered `DbSet<Recommendation> Recommendations` and `IRecommendationStore` in [`IntelliQueryDbContext.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/IntelliQueryDbContext.cs) and [`DependencyInjection.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/DependencyInjection.cs).
  - PostgreSQL Migration: [`PhaseH_RecommendationEngine.sql`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/migrations/PhaseH_RecommendationEngine.sql) applied to PostgreSQL database container `intelliquery_postgres`.
- **Microsoft Agent Framework Integration**:
  - Specialized Agent: [`RecommendationAnalystAgent.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Agents/RecommendationAnalystAgent.cs) producing structured JSON arrays of evidence-backed recommendations with offline simulation fallback.
  - Coordinator: [`RecommendationCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/RecommendationCoordinator.cs) implementing `IRecommendationCoordinator` aggregating signals from `IInvestigationReportStore`, `IScenarioStore`, `IAlertStore`, and `IMetricStore`, deduplicating titles, and persisting new initiatives.
  - Registered in [`DependencyInjection.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/DependencyInjection.cs).
- **Application CQRS Handlers**:
  - DTOs: [`RecommendationDtos.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Recommendations/Dtos/RecommendationDtos.cs).
  - Commands: [`RecommendationCommands.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Recommendations/Commands/RecommendationCommands.cs) (`GenerateRecommendationsCommand`, `CreateRecommendationCommand`, `ApplyRecommendationCommand`, `DismissRecommendationCommand`, `ReactivateRecommendationCommand`, `DeleteRecommendationCommand`).
  - Queries: [`RecommendationQueries.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Recommendations/Queries/RecommendationQueries.cs) (`GetRecommendationsQuery`, `GetRecommendationByIdQuery`, `GetRecommendationSummaryQuery`).
- **Minimal API Endpoints**:
  - Registered in [`RecommendationEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/RecommendationEndpoints.cs) and mapped in [`Program.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Program.cs):
    - `GET /api/recommendations` (list with status/priority filtering)
    - `GET /api/recommendations/summary` (KPI statistics)
    - `POST /api/recommendations/generate` (AI cross-cutting signal synthesis)
    - `POST /api/recommendations` (manual creation)
    - `GET /api/recommendations/{id}` (detail)
    - `POST /api/recommendations/{id}/apply` (mark applied)
    - `POST /api/recommendations/{id}/dismiss` (dismiss with reason)
    - `POST /api/recommendations/{id}/reactivate` (reactivate)
    - `DELETE /api/recommendations/{id}` (delete)

### Frontend UI & Workspace
- **API Client & Hooks** ([`api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts), [`use-recommendations.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/hooks/use-recommendations.ts)):
  - Added full TypeScript contracts and `api.recommendations` methods.
  - Added React Query hooks: `useRecommendations`, `useRecommendationSummary`, `useRecommendation`, `useGenerateRecommendations`, `useCreateRecommendation`, `useApplyRecommendation`, `useDismissRecommendation`, `useReactivateRecommendation`, `useDeleteRecommendation`.
- **Sidebar Navigation** ([`Sidebar.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/layout/Sidebar.tsx)):
  - Added **"AI Recommendations"** navigation item with `Lightbulb` icon and **"Actionable"** badge under Intelligence & Monitoring.
- **Enhanced Reusable Component** ([`RecommendationCard.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/shared/RecommendationCard.tsx)):
  - Supports both `ActionableRecommendation` and full `RecommendationDto` formats.
  - Priority badge, Category tag, Target Metric pill, Impact Magnitude highlight, Estimated Effort, expandable Action Steps with numbered badges, View Evidence trigger, Accept / Apply button, Dismiss button, Reactivate button, and Ask AI integration.
- **Actionable Recommendations Dashboard Screen** ([`recommendations/page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/recommendations/page.tsx)):
  - **Hero Header**: Synthesis trigger button with animated loading state.
  - **4 Executive KPI Counters**: Active Initiatives, Critical Priority, Accepted & Applied, Dismissed / Archived.
  - **Multi-Dimensional Filters**: Search bar, Status pills (Active, Under Review, Applied, Dismissed), Priority dropdown, Category dropdown.
  - **Supporting Evidence Dialog**: Displays discrete evidence items with observed metrics, baselines, evidence type, and direct "Launch Deep Investigation" button.
  - **Apply & Dismiss Modals**: Enforces human-in-the-loop governance oversight and records justification reasons.
  - **Custom Recommendation Creator**: Form with dynamic action steps builder.

### Verification & Automated Tests (Phase H)
- **Application Unit Tests** ([`RecommendationTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/RecommendationTests.cs)):
  - Verified aggregate root initial state, evidence/action steps deserialization, and human-in-the-loop safety invariant.
  - Verified state transitions: `Active` -> `Applied`, `Active` -> `Dismissed`, and `Dismissed` -> `Reactivated`.
  - Verified command handlers (`Create`, `Apply`) enforce strict multi-tenant isolation.
  - Verified summary query accurate status counters.
  - Full test suite passing: **183/183 tests green (100% success)** across Domain (57), Application (118), and Integration (8).
- **Frontend Turbopack Production Build**:
  - `npm run build` completed cleanly with **28/28 routes generated** (including `/recommendations`) and 0 TypeScript errors.

---

## 17. Phase I: Forecasting Foundation (Statistical Models & Confidence Envelopes)

### 17.1 Implementation Summary
Phase I implements an enterprise-grade, deterministic predictive forecasting system paired with Microsoft Agent Framework narrative interpretation and confidence envelope analysis. Rather than employing opaque neural forecasting or fabricating unverified predictions, the system strictly grounds projections in four core statistical algorithms (Ordinary Least Squares Linear Trend, Simple Moving Average, Double Exponential Smoothing / Holt-Winters, and Compound Seasonal Growth), calculating formal 95% confidence intervals and standard-error dispersion. The autonomous `ForecastAnalystAgent` provides executive-ready methodological justification, goodness-of-fit appraisal ($R^2$ / MAE), and structural tail-risk disclosure.

### 17.2 Architecture & Components Delivered
1. **Domain Aggregate & Deterministic Engine**:
   - [`ForecastEntities.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Entities/Forecasting/ForecastEntities.cs): `Forecast : AggregateRoot` tracking `WorkspaceId`, `CreatedByUserId`, `DataSourceId`, `MetricName`, `ForecastPeriod`, `HistoricalPeriod`, `Method`, `ConfidenceInterval`, serialized historical observations, predicted trajectory points with lower/upper bounds, assumptions, and AI explanation.
   - [`ForecastingEngine.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Services/ForecastingEngine.cs):
     - **Ordinary Least Squares (OLS) Linear Trend**: Computes slope ($m$) and intercept ($b$), $R^2$, and $z$-score expanding standard-error confidence margins.
     - **Simple Moving Average (SMA)**: Rolling unweighted mean across trailing window with historical variance dispersion.
     - **Double Exponential Smoothing (Holt-Winters)**: Decoupled level ($\alpha = 0.35$) and trend ($\beta = 0.15$) with Mean Absolute Error (MAE) confidence bounds.
     - **Compound Seasonal Growth (CAGR)**: Multiplicative compounding: $(V_{\text{last}} / V_{\text{first}})^{1/(n-1)} - 1$ with volatility bands.
   - [`ForecastStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Persistence/ForecastStores.cs): `IForecastStore` domain persistence contract.
   - [`AiErrors.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Domain/Errors/AiErrors.cs): Added `ForecastNotFound`, `ForecastTenantMismatch`, and `InsufficientHistoricalData`.
2. **Infrastructure & Persistence**:
   - [`ForecastConfigurations.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Configurations/ForecastConfigurations.cs): Entity Framework Core mapping for table `forecasts` with JSON column storage and compound indexes.
   - [`EfForecastStores.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/Stores/EfForecastStores.cs): `EfForecastStore` implementing `IForecastStore`.
   - [`IntelliQueryDbContext.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/Persistence/IntelliQueryDbContext.cs): Registered `DbSet<Forecast> Forecasts`.
   - [`PhaseI_ForecastingFoundation.sql`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/migrations/PhaseI_ForecastingFoundation.sql): Applied PostgreSQL migration establishing table `forecasts` and its indexes in `intelliquery_db`.
   - [`DependencyInjection.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Infrastructure/DependencyInjection.cs): Registered `IForecastStore` and `IForecastingEngine`.
3. **Microsoft Agent Framework & Orchestration**:
   - [`ForecastAnalystAgent.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Agents/ForecastAnalystAgent.cs): Autonomous agent inheriting from `BaseAgent`, synthesizing qualitative explanations of methodology, goodness of fit, and downside sensitivities while strictly preserving deterministic numerical projections. Includes robust offline simulation mode.
   - [`ForecastCoordinator.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/Coordinator/ForecastCoordinator.cs): Coordinates engine execution, AI explanation synthesis, aggregate creation, and persistence. Supports instantaneous preview without DB write.
   - [`DependencyInjection.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.AI/DependencyInjection.cs): Registered `ForecastAnalystAgent` and `IForecastCoordinator`.
4. **Application CQRS**:
   - [`ForecastDtos.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Forecasting/Dtos/ForecastDtos.cs): DTO contracts and mapping helper.
   - [`ForecastCommands.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Forecasting/Commands/ForecastCommands.cs): `GenerateForecastCommand`, `DeleteForecastCommand`.
   - [`ForecastQueries.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Application/Features/Forecasting/Queries/ForecastQueries.cs): `GetForecastsQuery`, `GetForecastByIdQuery`, `QuickForecastPreviewQuery`.
5. **API Layer**:
   - [`ForecastEndpoints.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Endpoints/ForecastEndpoints.cs): Dual-route minimal APIs (`/api/forecasts` & `/forecasts`) for `GET /`, `GET /{id}`, `POST /generate`, `POST /preview`, and `DELETE /{id}`.
   - [`Program.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/src/IntelliQueryAI.Api/Program.cs): Mapped `app.MapForecastEndpoints()`.
6. **Frontend Experience**:
   - [`api-client.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/lib/api-client.ts): Added forecasting interfaces and `api.forecasts` client methods.
   - [`use-forecasts.ts`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/hooks/use-forecasts.ts): TanStack Query hooks for querying, generating, previewing, and deleting forecasts.
   - [`Sidebar.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/components/layout/Sidebar.tsx): Added "Forecast Studio" (`/forecasts`, `TrendingUp`, badge "Predictive").
   - [`page.tsx`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/web/src/app/(app)/forecasts/page.tsx): Full-featured Predictive Forecast Studio featuring:
     - 4 realistic benchmark presets (SaaS ARR Expansion, Enterprise Churn Rate, Active Workspace Users, AWS Cloud Compute Cost).
     - Algorithm switcher (LinearTrend, MovingAverage, ExponentialSmoothing, SeasonalGrowth).
     - Interactive observation table with add/remove/edit.
     - Recharts `ComposedChart` with solid actuals, projected mean dashed line, and translucent confidence interval envelope.
     - KPI metrics ribbon (Baseline, Final Projected Value, Projected Net Delta %, Confidence %).
     - Model assumptions panel.
     - AI narrative interpretation card with direct cross-links to What-If Scenarios (`/scenarios`), Root-Cause Investigations (`/investigations`), Recommendations (`/recommendations`), and `<AskAiAboutThis />`.
     - Saved forecast archive manager with quick reload and delete.
     - Method reference modal explaining mathematical formulas.

### 17.3 Verification & Quality Assurance
- **Application & Domain Unit Tests** ([`ForecastTests.cs`](file:///c:/Dev/Projects/Personal/Web/IntelliQueryAI/tests/IntelliQueryAI.Application.Tests/ForecastTests.cs)):
  - Verified OLS linear slope calculation, $R^2 > 0.99$, and upper/lower confidence bounds.
  - Verified Simple Moving Average trailing smoothing.
  - Verified Double Exponential Smoothing trend extrapolation.
  - Verified Seasonal Growth CAGR compounding.
  - Verified validation guards (throws on $<2$ historical observations).
  - Verified CQRS command and query handlers, tenant isolation, and store mocking.
  - Full backend test suite passing: **192/192 tests green (100% success)** across Domain (57), Application (127), and Integration (8).
- **Frontend Turbopack Production Build**:
  - `npm run build` completed cleanly with **29/29 routes generated** (including `/forecasts`) and 0 TypeScript errors.

---

## 18. Developer & AI Agent Operational Playbook

When implementing new features or making changes as an engineer or AI agent, you **MUST** follow these rules:

1. **CQRS & IDispatcher**:
   - Never inject EF Core `DbContext` or write logic directly inside API endpoints.
   - All mutations must be encapsulated in an `ICommand<Result<T>>` with a corresponding `ICommandHandler`.
   - All reads must be encapsulated in an `IQuery<Result<T>>` with a corresponding `IQueryHandler`.
   - Always dispatch through `IDispatcher.SendAsync()` or `IDispatcher.QueryAsync()`.
2. **Result Pattern**:
   - Do not throw business exceptions for predictable failure modes.
   - Return `Result.Success(data)` or `Result.Failure(DomainError)`.
   - Convert to HTTP responses via `.ToHttpResult()`.
3. **Database Write vs. Read Segregation**:
   - Write operations use EF Core with tracked domain entities and explicit Unit of Work (`IUnitOfWork.SaveChangesAsync()`).
   - Read operations use Dapper or raw SQL read repositories for maximum performance.
4. **Multi-Tenancy & Security**:
   - Every entity belongs to a Tenant / Workspace.
   - Always validate `tenantId` / `workspaceId` headers and claim context.
   - All synthesized SQL execution must be executed through read-only database connections with strict row-count and timeout constraints.
5. **Frontend State & Dark Mode**:
   - The application strictly uses permanent dark mode (`dark` class on root `<html>`).
   - Never store tokens in cookies that bypass the `api-client.ts` bearer headers unless configured with HTTP-only flags.
   - Use Zustand for client state (`auth-store.ts`) and React Query for server cache invalidation.
6. **File Formatting & Hyperlinks**:
   - When referencing files in documentation or agent interactions, always use clickable markdown links with forward slashes: `[FileName](file:///absolute/path/to/file)`.
