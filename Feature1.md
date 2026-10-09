# IntelliQueryAI — FULL-STACK PREMIUM INTELLIGENCE UPGRADE

## ROLE

Act as a:

- Principal ASP.NET Core Architect
- Principal AI Engineer
- Microsoft Agent Framework Specialist
- Enterprise SaaS Architect
- PostgreSQL / SQL Server Expert
- Enterprise Security Architect
- Principal Next.js Engineer
- Product-focused AI Engineer

You are working on an EXISTING application called:

# IntelliQueryAI

IntelliQueryAI is an AI-powered enterprise analytics and decision-intelligence platform.

This is NOT a greenfield project.

Your responsibility is to EXTEND the existing application with premium enterprise intelligence capabilities WITHOUT breaking, unnecessarily rewriting, duplicating, or destabilizing existing functionality.

The application is already being prepared for demonstrations to potential enterprise customers.

Production quality, stability, security, explainability, and demo readiness are therefore critical.

---

# CRITICAL FULL-STACK REQUIREMENT

This task requires changes to BOTH:

1. The existing ASP.NET Core backend
2. The existing Next.js frontend

A feature is NOT considered complete simply because its backend endpoint exists.

For EVERY feature implemented, complete the entire vertical slice:

Backend Domain/Application
↓
Infrastructure
↓
Database Migration
↓
API Contract
↓
Authorization
↓
Frontend API Client
↓
TanStack Query Integration
↓
Frontend UI
↓
Loading/Error/Empty States
↓
Permission Handling
↓
Backend Tests
↓
Frontend Tests
↓
Backend Build
↓
Frontend Build
↓
Regression Verification

Do NOT stop after backend implementation.

Do NOT create disconnected frontend mockups.

Do NOT create a new frontend application.

Locate and modify the EXISTING Next.js application.

If backend and frontend are in different directories/projects, locate BOTH before implementation begins.

---

# CRITICAL RULE — EXISTING CODEBASE IS THE SOURCE OF TRUTH

Before writing implementation code:

1. Inspect the complete existing solution/repository.
2. Locate the ASP.NET Core backend.
3. Locate the Next.js frontend.
4. Understand the current architecture.
5. Identify existing modules.
6. Identify existing entities.
7. Identify existing interfaces.
8. Identify existing API endpoints.
9. Identify existing services.
10. Identify existing Microsoft Agent Framework agents/tools.
11. Identify existing database migrations.
12. Identify existing frontend routes.
13. Identify existing components.
14. Identify existing TanStack Query hooks.
15. Identify existing Zustand stores.
16. Identify existing API client abstractions.
17. Identify existing authentication implementation.
18. Identify existing authorization/RBAC implementation.
19. Identify existing tenant/workspace resolution.
20. Identify existing testing frameworks.
21. Identify functionality that can be reused.
22. Identify safe extension points.
23. Identify potential regression risks.

Then produce a concise implementation plan before implementing.

The actual existing codebase overrides assumptions in this prompt.

If this prompt mentions functionality that already exists under another name, REUSE or EXTEND the existing implementation.

DO NOT create duplicates.

---

# NON-NEGOTIABLE BACKWARD-COMPATIBILITY RULES

DO NOT:

- Rebuild the application
- Replace working architecture
- Rename existing entities unnecessarily
- Rename existing routes
- Rename existing API endpoints
- Break existing API contracts
- Change authentication behavior
- Change tenant resolution behavior
- Replace existing RBAC
- Replace existing AI orchestration
- Replace existing HTTP/API clients
- Replace existing state management
- Replace existing UI libraries
- Replace existing validation frameworks
- Introduce another ORM
- Duplicate existing abstractions
- Modify unrelated modules
- Perform cosmetic refactoring unrelated to this task
- Remove existing functionality
- Create fake APIs
- Use mock data where real APIs exist
- Leave TODO placeholders
- Leave incomplete implementations
- Introduce unnecessary dependencies
- Change working code simply because another implementation is preferred

All changes must be:

- Additive
- Modular
- Backward-compatible
- Testable
- Auditable
- Multi-tenant safe
- Production-ready

---

# CURRENT PRODUCT CAPABILITIES

The application may already contain some or all of:

- Authentication
- User registration
- Multi-tenancy
- Workspace management
- RBAC
- Data source management
- SQL Server connectivity
- PostgreSQL connectivity
- MySQL connectivity
- Schema discovery
- Schema metadata
- Vector embeddings
- Natural Language to SQL
- SQL validation
- Safe query execution
- Microsoft Agent Framework
- AI-generated insights
- Data visualization
- Query history
- Saved reports
- Executive intelligence
- Monitoring
- Alerts
- Anomaly detection
- Scheduled reports
- Audit logging
- Subscription management
- Next.js frontend
- TanStack Query
- Zustand
- Existing design system

VERIFY what exists before modifying anything.

---

# PRODUCT EVOLUTION

Evolve IntelliQueryAI from:

Ask
→ SQL
→ Results
→ Insights

into:

Understand
→ Monitor
→ Detect
→ Investigate
→ Explain
→ Recommend
→ Forecast
→ Decide

The long-term product positioning is:

# "Your AI Business Analyst"

The system should help enterprise users understand:

- What happened?
- Why did it happen?
- What changed?
- What requires attention?
- What could happen next?
- What actions should be considered?

---

# IMPLEMENTATION PRIORITY

Do NOT attempt all features simultaneously.

Implement incrementally in this order:

## PHASE A
Business Knowledge / Semantic Layer

## PHASE B
AI Root-Cause Investigation

## PHASE C
Contextual "Ask AI About This"

## PHASE D
Morning Executive Brief

## PHASE E
AI Dashboard Generator

## PHASE F
Enterprise Data Governance

## PHASE G
What-If / Scenario Analysis

## PHASE H
Recommendation Engine

## PHASE I
Forecasting Foundation

Complete, test, build, and verify each phase before proceeding.

The highest priority for enterprise demos is:

A → B → C → D

Do not sacrifice stability in order to reach later phases.

---

# ============================================================
# PHASE A — BUSINESS KNOWLEDGE / SEMANTIC LAYER
# ============================================================

The AI must understand the organization's business language, not just database table and column names.

Create or extend a Business Knowledge Layer.

Potential concepts:

BusinessTerm

MetricDefinition

KpiDefinition

BusinessRule

BusinessAlias

DataClassification

DataOwner

Before creating these entities, search for existing equivalents.

Do not duplicate existing concepts.

---

# EXAMPLE BUSINESS TERM

Business Term:

Revenue

Definition:

Total value of successfully completed payments.

Aliases:

Revenue
Collections
Income
Total Collections

Underlying data definition:

Payments.Amount

Condition:

Payments.Status = Successful

---

# ANOTHER EXAMPLE

Business Term:

Active Customer

Definition:

A customer with at least one successful transaction within the last 90 days.

The system must use these definitions when interpreting business questions.

---

# BUSINESS DICTIONARY CAPABILITIES

Provide backend and frontend functionality to:

- Create business terms
- Edit business terms
- Delete/deactivate terms
- Search terms
- Define aliases
- Define metrics
- Define KPIs
- Define calculation rules
- Associate terms with schema objects
- Associate data owners
- Classify sensitive data
- Enable/disable definitions
- Version definitions where appropriate

The AI Query Engine and Investigation Engine must retrieve relevant business definitions.

DO NOT inject the entire dictionary into prompts.

Retrieve only relevant definitions.

---

# BUSINESS KNOWLEDGE FRONTEND

Create or extend administration pages such as:

/knowledge

/knowledge/terms

/knowledge/metrics

/knowledge/kpis

Use existing routing conventions.

Provide:

- Search
- Filtering
- Pagination
- Create
- Edit
- View
- Enable/Disable

Reuse existing forms, tables, dialogs, badges, and layouts.

---

# ============================================================
# PHASE B — AI ROOT-CAUSE INVESTIGATION
# ============================================================

Build an Investigation Engine.

Example user question:

"Why did revenue decline last month?"

The system must NOT simply generate one SQL statement and provide a generic LLM explanation.

It should perform a controlled, evidence-driven investigation.

Example:

Question

↓

Understand metric/business definition

↓

Establish baseline

↓

Analyze historical trend

↓

Determine relevant dimensions

↓

Break down performance

↓

Identify major contributors

↓

Investigate anomalies

↓

Collect supporting evidence

↓

Generate root-cause explanation

↓

Generate recommendations

↓

Suggest follow-up questions

Potential dimensions include:

- Region
- Customer
- Product
- Channel
- Transaction type
- Business unit
- Revenue source
- Date/time

DO NOT hardcode these.

Discover dimensions from:

- Schema metadata
- Relationships
- Business Knowledge Layer

---

# INVESTIGATION OUTPUT

Use structured output.

Example:

{
  "summary": "...",
  "confidence": 0.91,
  "contributors": [],
  "evidence": [],
  "recommendations": [],
  "queriesExecuted": [],
  "followUpQuestions": []
}

Every major conclusion must be supported by evidence.

Users should be able to inspect:

- Supporting metrics
- Evidence
- Charts
- Contributors
- AI explanation
- Generated SQL where authorized

---

# MICROSOFT AGENT FRAMEWORK

Use the EXISTING Microsoft Agent Framework architecture.

DO NOT introduce another AI orchestration framework.

Potential capabilities:

Investigation Agent

Responsibilities:

- Coordinate investigation
- Decide next analytical step
- Determine when sufficient evidence exists

Schema Tool

Responsibilities:

- Retrieve relevant schema context

Business Knowledge Tool

Responsibilities:

- Retrieve relevant definitions and metrics

Analysis Tool

Responsibilities:

- Execute safe analytical queries

Evidence Tool

Responsibilities:

- Store and organize evidence

Recommendation capability

Responsibilities:

- Generate recommendations from verified findings

Do not create agents merely for architectural aesthetics.

Prefer deterministic services/tools for:

- Security
- SQL validation
- Authorization
- Calculations
- Data access
- Policy enforcement

Use agents for:

- Reasoning
- Investigation planning
- Interpretation
- Summarization
- Recommendations

---

# AGENT SAFETY LIMITS

Prevent uncontrolled agent loops.

Implement:

- Maximum investigation steps
- Maximum query count
- Execution timeout
- Token/cost budget where possible
- Cancellation
- Retry limits
- Maximum result sizes

---

# INVESTIGATION FRONTEND

Create an Investigation experience.

Suggested route:

/investigations

and:

/investigations/[id]

Adapt to existing routing conventions.

Display:

Question

Status

Executive Summary

Root Causes

Contributors

Evidence

Charts

Recommendations

Confidence

Follow-up Questions

Provide actions:

- Ask Follow-up
- View Evidence
- Explore Contributor
- Save Investigation
- Ask AI About This

Do NOT expose chain-of-thought or private model reasoning.

Display only user-facing explanations and supporting evidence.

---

# ============================================================
# PHASE C — CONTEXTUAL "ASK AI ABOUT THIS"
# ============================================================

AI should not exist only on one chat screen.

Integrate AI contextually throughout the application.

Examples:

KPI:

"Why did this change?"

Alert:

"Explain this alert."

Anomaly:

"Investigate this anomaly."

Chart:

"Explain this trend."

Dashboard:

"Analyze this dashboard."

Report:

"Summarize this report."

Customer metric:

"Analyze this customer."

Recommendation:

"Show supporting evidence."

Forecast:

"Explain this forecast."

---

# AI CONTEXT MODEL

Create or reuse a generic context model.

Example:

AiContext

- WorkspaceId
- DataSourceId
- ContextType
- EntityId
- Metric
- DateRange
- AdditionalMetadata

Adapt naming to existing conventions.

Backend must validate ALL context.

Never trust IDs supplied by frontend without:

- Tenant validation
- Authorization validation
- Entity validation

---

# CONTEXTUAL AI FRONTEND

Create a reusable frontend action/component:

AskAiAboutThis

Attach it where appropriate to:

- KPI cards
- Charts
- Alerts
- Anomalies
- Reports
- Dashboard widgets
- Recommendations
- Forecasts

Reuse the existing AI Query Workspace.

DO NOT build another separate chatbot.

Where appropriate, navigate/open the existing AI experience with validated context.

---

# ============================================================
# PHASE D — MORNING EXECUTIVE BRIEF
# ============================================================

Create a personalized Executive Brief.

When an authorized executive opens IntelliQueryAI:

"Good morning, {Name}.

Here's what changed in your business since yesterday."

Potential information:

- Revenue
- Transactions
- Customers
- Collections
- Failed payments
- Major anomalies
- Important alerts
- Emerging risks
- Opportunities
- Recommendations

DO NOT hardcode business metrics.

Determine relevant metrics using:

- Workspace configuration
- Business Knowledge Layer
- Saved KPIs
- Active monitors
- User preferences

---

# EXECUTIVE BRIEF PERIODS

Support configurable periods such as:

- Last 24 hours
- Since yesterday
- This week
- Week-to-date
- Month-to-date

---

# PRIORITIZATION

Do not overwhelm executives.

Rank findings using factors such as:

- Severity
- Financial impact
- Percentage change
- Statistical significance
- Business priority
- Confidence

Display the most important findings first.

---

# EXECUTIVE HOME FRONTEND

Enhance the EXISTING dashboard.

Do not replace it.

Add:

Good morning, {User}

Here's what changed in your business.

Display:

- Key KPIs
- Changes
- Critical alerts
- Important anomalies
- Risks
- Opportunities
- Recommendations

Provide contextual actions:

Ask AI

Investigate

View Evidence

View Details

---

# ============================================================
# PHASE E — AI DASHBOARD GENERATOR
# ============================================================

Allow users to create dashboards using natural language.

Example:

"Create an executive revenue dashboard comparing this year with last year."

AI should determine:

- KPIs
- Metrics
- Dimensions
- Visualizations
- Date ranges
- Comparisons

IMPORTANT:

DO NOT allow the LLM to generate arbitrary React/JavaScript code.

Generate a SAFE STRUCTURED DASHBOARD DEFINITION.

Example:

{
  "title": "Executive Revenue Dashboard",
  "widgets": [
    {
      "type": "metric",
      "metric": "TotalRevenue"
    },
    {
      "type": "lineChart",
      "metric": "Revenue",
      "dimension": "Month"
    }
  ]
}

Validate definitions against supported widget types.

---

# DASHBOARD CAPABILITIES

Support:

- Generate with AI
- Save
- Edit
- Rename
- Delete
- Duplicate
- Refresh
- Date filtering
- Ask AI
- Share according to permissions
- Export where supported

---

# DASHBOARD FRONTEND

Create or extend dashboard management.

Suggested:

/dashboards

/dashboards/[id]

Provide:

"Create Dashboard with AI"

User enters:

"Create an executive dashboard showing revenue, transactions, top customers and regional performance."

Backend returns validated structured definition.

Frontend renders using EXISTING chart/UI components where possible.

Allow authorized users to arrange widgets if compatible with current architecture.

---

# ============================================================
# PHASE F — ENTERPRISE DATA GOVERNANCE
# ============================================================

AI must NEVER bypass user permissions.

Implement or extend data governance.

Potential hierarchy:

Data Source
↓
Schema
↓
Table
↓
Column
↓
Row

Do not create parallel security if existing RBAC/authorization can be extended.

---

# DATA ACCESS POLICIES

Potential concepts:

DataAccessPolicy

ColumnAccessPolicy

RowAccessPolicy

DataMaskingPolicy

SensitiveDataClassification

Search for existing equivalents first.

Example:

Finance Manager

Allowed:

Finance.*
Payments.*
Invoices.*

Restricted:

HR.Salary
Customer.BVN
Customer.NIN

---

# DATA CLASSIFICATION

Support concepts such as:

- Public
- Internal
- Confidential
- Restricted
- PII
- Financial
- Secret/Credential

---

# MASKING

Example:

Original BVN:

22345678901

Displayed:

*******8901

Support appropriate masking strategies.

---

# CRITICAL AI DATA LEAKAGE RULE

Unauthorized or restricted values must NOT be sent to the AI provider.

Authorization and masking must occur before sensitive information enters LLM context wherever technically possible.

This applies to:

- AI Query
- Investigation
- Reports
- Monitoring
- Alerts
- Dashboards
- Recommendations
- Forecasts
- Contextual AI
- Exports

Audit sensitive data access.

---

# GOVERNANCE FRONTEND

Create authorized administration screens for:

- Data access policies
- Sensitive fields
- Data classifications
- Masking policies
- Role access

Hide these screens from unauthorized users.

Frontend authorization is NOT the security boundary.

Backend remains authoritative.

---

# ============================================================
# PHASE G — WHAT-IF / SCENARIO ANALYSIS
# ============================================================

Create Scenario Analysis.

Examples:

"What happens if revenue grows by 8% while customer churn increases by 3%?"

"If current collections continue at the same rate, what could year-end revenue look like?"

Potential concepts:

Scenario

ScenarioVariable

ScenarioAssumption

ScenarioResult

ScenarioComparison

Reuse existing equivalents.

---

# SCENARIO TYPES

Support:

Baseline

Expected Case

Best Case

Worst Case

Custom Assumptions

Every projection must identify:

- Historical data
- Assumptions
- Calculation methodology
- Uncertainty/confidence where appropriate

NEVER present speculation as fact.

Use deterministic/statistical calculations wherever possible.

AI explains the result.

AI must NOT fabricate numerical projections.

---

# SCENARIO FRONTEND

Create a scenario workspace.

Suggested:

/scenarios

/scenarios/[id]

Display:

Baseline

Expected

Best Case

Worst Case

Assumptions

Charts

AI Explanation

Allow users to modify assumptions and rerun.

Clearly label projections as estimates.

---

# ============================================================
# PHASE H — RECOMMENDATION ENGINE
# ============================================================

Create evidence-backed recommendations.

Example:

HIGH PRIORITY

Investigate Enterprise Customer Churn

Reason:

Revenue from the enterprise segment declined by 17%.

Evidence:

...

Suggested next steps:

...

Recommendations should be linked where applicable to:

- Investigation
- Evidence
- Alert
- Metric
- Monitor
- Anomaly
- Forecast

Do NOT automatically perform destructive business actions.

---

# RECOMMENDATION FRONTEND

Create reusable Recommendation components.

Display:

- Priority
- Title
- Explanation
- Supporting evidence
- Related metric
- Related investigation
- Suggested actions

Provide:

View Evidence

Investigate

Ask AI

Dismiss

where supported.

---

# ============================================================
# PHASE I — FORECASTING FOUNDATION
# ============================================================

Create an extensible forecasting architecture.

Start with practical statistical/deterministic approaches.

Examples:

- Moving averages
- Growth-rate projection
- Trend extrapolation
- Seasonal comparisons

Design extension points for future ML forecasting.

Do NOT introduce heavy ML infrastructure unless justified.

---

# FORECAST OUTPUT

Include:

- Forecast period
- Historical period
- Forecast values
- Confidence interval where available
- Method
- Assumptions
- Generated timestamp

---

# FORECAST FRONTEND

Create forecast views.

Suggested:

/forecasts

Display:

Historical Data

Forecast

Confidence Range

Method

Assumptions

AI Explanation

Provide:

Ask AI About This

Create Scenario

Investigate Historical Change

where appropriate.

---

# ============================================================
# BACKEND ARCHITECTURE
# ============================================================

Follow the EXISTING solution architecture.

If compatible, new capabilities may be organized around:

- BusinessKnowledge
- Investigation
- Dashboards
- Governance
- ScenarioAnalysis
- ExecutiveBrief
- Recommendations
- Forecasting

DO NOT create microservices merely because features are logically separate.

Respect current deployment architecture.

---

# CLEAN ARCHITECTURE RULES

Follow existing project conventions.

Where consistent with the current architecture:

- Domain owns business concepts
- Application owns use cases
- Infrastructure owns external implementations
- API owns HTTP concerns

Do not inject DbContext directly into application handlers if the current architecture uses abstractions.

Do not introduce repository abstractions merely for ceremony if the existing architecture intentionally uses another pattern.

Consistency with the real project is the priority.

---

# DATABASE CHANGES

All schema changes must use migrations.

Before creating a table/entity:

SEARCH for existing equivalents.

Do not create duplicates such as:

Metric

BusinessMetric

MetricDefinition

if the same concept already exists.

Use appropriate:

- Primary keys
- Foreign keys
- Unique constraints
- Indexes
- Workspace/Tenant indexes
- Concurrency controls
- Auditing fields

Every tenant-owned record must preserve tenant isolation.

---

# SQL QUERY SECURITY

ALL AI-generated analytical queries must pass through the EXISTING safe query pipeline.

Never create a special bypass for:

- Investigations
- Dashboards
- Executive Brief
- Recommendations
- Scenarios
- Forecasting

Maintain protections such as:

- Read-only credentials
- SELECT-only policy
- SQL validation
- Timeout
- Row limits
- Query complexity/cost controls
- Cancellation
- Audit logging

If a required protection is missing, document the security gap before changing the sensitive path.

---

# BACKGROUND PROCESSING

Reuse existing Quartz.NET/background processing architecture.

Potential jobs:

ExecutiveBriefGenerationJob

ForecastGenerationJob

DashboardRefreshJob

RecommendationRefreshJob

Do not duplicate existing monitor/anomaly jobs.

Jobs must be:

- Multi-tenant aware
- Idempotent where possible
- Retry-safe
- Observable
- Cancellable
- Distributed-deployment safe

---

# AUDITING

Reuse existing audit infrastructure.

Audit:

- Investigations
- Queries
- Sensitive data access
- Business term changes
- Metric changes
- Policy changes
- Dashboard generation
- Scenario execution
- Forecast generation
- Executive brief generation
- Recommendation generation

---

# OBSERVABILITY

Integrate with existing logging/telemetry.

Capture useful operational metrics:

- Investigation duration
- Investigation steps
- Queries per investigation
- AI requests
- Token usage where available
- Query execution duration
- Forecast execution duration
- Failed investigations
- Policy denials
- Dashboard generation duration
- Background job duration

NEVER log:

- Passwords
- Connection strings
- API keys
- JWTs
- Refresh tokens
- Secrets
- Unmasked sensitive data

---

# ============================================================
# NEXT.JS FRONTEND REQUIREMENTS
# ============================================================

The frontend implementation is MANDATORY.

Do not consider a feature complete without its corresponding frontend implementation where user interaction is required.

Use the EXISTING Next.js application.

Preserve:

- Authentication
- Existing routes
- Existing layout
- Existing navigation conventions
- Existing design system
- Existing theme
- Existing API client
- Existing TanStack Query setup
- Existing Zustand setup
- Existing React Hook Form usage
- Existing Zod usage
- Existing error handling
- Existing route protection

Do NOT globally redesign the product.

New screens must visually belong to the existing application.

---

# FRONTEND SERVER STATE

Use TanStack Query for remote/server state.

Potential hooks:

useBusinessTerms

useMetrics

useKpis

useInvestigations

useInvestigation

useStartInvestigation

useExecutiveBrief

useDashboards

useGenerateDashboard

useScenario

useRunScenario

useRecommendations

useForecasts

useGovernancePolicies

Adapt naming to existing conventions.

Do NOT store server state in Zustand.

Use Zustand only for genuine client-side state where appropriate.

---

# FRONTEND API CLIENT

Reuse the existing HTTP/API client abstraction.

Do not create an independent fetch/axios layer if one already exists.

Create strongly typed contracts.

Ensure backend/frontend contracts remain synchronized.

Handle existing authentication/refresh behavior automatically through the current infrastructure.

---

# FRONTEND ASYNC STATES

Every new asynchronous screen/component must implement:

- Loading state
- Skeleton where appropriate
- Empty state
- Error state
- Retry mechanism where appropriate

Never show blank screens.

---

# FRONTEND SECURITY

Respect permissions when rendering:

- Buttons
- Navigation
- Forms
- Sensitive values
- Administration features

However:

FRONTEND AUTHORIZATION IS NOT SECURITY.

All security must be enforced by backend APIs.

---

# FRONTEND RESPONSIVENESS

Maintain existing responsive behavior.

Prioritize enterprise desktop usage.

Support:

- Desktop
- Tablet
- Reasonable mobile experience

Avoid unnecessary horizontal overflow.

---

# FRONTEND ACCESSIBILITY

Maintain:

- Keyboard navigation
- ARIA labels
- Accessible forms
- Accessible tables
- Sufficient contrast
- Screen-reader-friendly status indicators

---

# ============================================================
# TESTING
# ============================================================

Use the EXISTING testing frameworks.

Do not introduce another test framework unnecessarily.

BACKEND TESTS SHOULD COVER:

- Tenant isolation
- Authorization
- SQL safety
- Investigation limits
- Evidence linkage
- Business definition resolution
- Sensitive data masking
- Policy enforcement
- Scenario calculations
- Forecast calculations
- Executive brief generation
- Dashboard definition validation

FRONTEND TESTS SHOULD COVER:

- Business term management
- Investigation experience
- Executive brief
- Contextual Ask AI
- Dashboard generation
- Scenario analysis
- Recommendations
- Governance permissions
- Forecast display
- Loading states
- Empty states
- Error states
- Permission-based visibility

---

# ============================================================
# REGRESSION PROTECTION
# ============================================================

After EACH PHASE verify existing functionality.

At minimum:

- Login
- Registration
- Password reset if existing
- Workspace switching
- Data source management
- Connection testing
- Schema discovery
- Schema explorer
- Natural Language to SQL
- SQL validation
- Query execution
- Query results
- Charts
- AI insights
- Query history
- Saved reports
- Monitoring
- Alerts
- Anomaly detection
- Executive intelligence
- Scheduled reports
- Administration
- Existing subscription functionality

Do not claim verification unless actual tests/build/runtime checks were performed.

---

# BUILD VERIFICATION

After every phase:

BACKEND:

- Restore
- Build
- Run relevant tests

FRONTEND:

- Install/use existing dependencies
- Type-check
- Lint if configured
- Build
- Run relevant tests

Fix regressions caused by the implementation before proceeding.

Do not suppress errors merely to make builds pass.

---

# GIT / CHANGE SAFETY

Keep changes logically scoped.

Do not combine unrelated refactoring with feature implementation.

Before modifying widely used abstractions:

Identify their consumers.

Prefer extension over breaking modification.

If a breaking change appears unavoidable:

STOP.

Explain:

1. Why the breaking change appears necessary
2. Which existing features are affected
3. Available backward-compatible alternatives
4. Recommended approach

Do NOT silently implement a breaking change.

---

# IMPLEMENTATION WORKFLOW FOR EVERY PHASE

For each phase:

1. Inspect existing relevant backend code.
2. Inspect existing relevant frontend code.
3. Identify reusable functionality.
4. Identify regression risks.
5. Produce a concise plan.
6. Implement domain/application changes.
7. Implement infrastructure changes.
8. Add migrations where necessary.
9. Implement/extend API.
10. Add backend authorization.
11. Add backend tests.
12. Build backend.
13. Implement frontend API contracts.
14. Implement TanStack Query integration.
15. Build frontend components.
16. Build/update frontend pages.
17. Add frontend permission handling.
18. Add loading/error/empty states.
19. Add frontend tests.
20. Build frontend.
21. Run relevant regression tests.
22. Document what changed.
23. Proceed only when the phase is stable.

---

# FEATURE COMPLETION DEFINITION

A feature is NOT complete if only this exists:

Backend Endpoint ✓

A feature is complete when:

Backend Domain/Application ✓

Infrastructure ✓

Migration ✓

API ✓

Authorization ✓

Frontend API Integration ✓

TanStack Query ✓

Frontend UI ✓

Loading/Error/Empty States ✓

Permission Handling ✓

Tests ✓

Backend Build ✓

Frontend Build ✓

Regression Verification ✓

---

# DEMO-READINESS REQUIREMENTS

Because IntelliQueryAI will be demonstrated to enterprise customers, prioritize polished end-to-end workflows.

The following demo should eventually work smoothly:

EXECUTIVE OPENS APPLICATION

↓

Morning Brief:

"Revenue decreased 14.2% compared with the previous period."

↓

Executive clicks:

"Investigate"

↓

IntelliQueryAI performs controlled investigation.

↓

Result:

"Three factors account for approximately 81% of the decline."

↓

Executive views:

- Contributors
- Evidence
- Charts
- Explanation

↓

Executive clicks:

"Ask AI About This"

↓

Asks:

"Which customers contributed most?"

↓

Receives contextual answer.

↓

Executive clicks:

"Create Dashboard"

↓

AI creates a validated executive dashboard.

↓

Executive asks:

"What happens if this trend continues?"

↓

Scenario/forecast capability provides an evidence-based projection.

This workflow should feel like ONE integrated product.

Not multiple disconnected AI features.

---

# UX PRINCIPLE

Avoid turning every feature into another chat window.

Use the appropriate UI for each problem:

Questions → Conversational interface

Investigation → Evidence-driven investigation UI

KPIs → Cards

Trends → Charts

Alerts → Alert Center

Business Definitions → Administrative forms

Scenarios → Interactive analysis workspace

Dashboards → Dashboard UI

Executive Brief → Concise executive experience

Recommendations → Action-oriented cards

Forecasts → Historical + projected visualization

AI should enhance the product rather than dominate every screen.

---

# FINAL ACCEPTANCE CRITERIA

The upgrade is successful only if:

1. Existing functionality remains operational.

2. Existing API contracts remain backward-compatible.

3. Existing frontend routes remain operational.

4. New features respect workspace/tenant boundaries.

5. AI cannot bypass authorization.

6. Sensitive information is protected.

7. Restricted data is not unnecessarily sent to AI providers.

8. Investigations are evidence-driven.

9. Recommendations reference evidence.

10. Scenarios clearly expose assumptions.

11. Forecasts clearly identify methodology and uncertainty.

12. AI dashboard definitions are validated before rendering.

13. Background jobs are tenant-safe.

14. New functionality is auditable.

15. New UI has loading/error/empty states.

16. Backend builds successfully.

17. Frontend builds successfully.

18. Relevant automated tests pass.

19. Existing workflows continue to work.

20. New functionality feels integrated rather than bolted on.

---

# FINAL IMPLEMENTATION REPORT

After each phase provide:

## Phase Implemented

What was implemented.

## Existing Architecture Reused

List important existing functionality reused.

## Backend Changes

- Entities
- Services
- Handlers
- Interfaces
- Agents
- Tools
- Jobs

## Database Changes

- Tables
- Columns
- Indexes
- Migrations

## API Changes

- New endpoints
- Extended endpoints

## Frontend Changes

- Routes
- Pages
- Components
- Hooks
- API clients

## Security Changes

- Authorization
- Tenant isolation
- Data masking
- Governance

## Tests

List tests added/updated.

## Build Results

Backend:

PASS / FAIL

Frontend:

PASS / FAIL

Tests:

PASS / FAIL

Only report PASS when actually verified.

## Regression Verification

List existing features checked.

## Known Limitations

Document real limitations.

## Next Phase

State the next recommended phase.

---

# FINAL INSTRUCTION

Do NOT treat this prompt as permission to rewrite IntelliQueryAI.

Treat the existing application as a valuable working product.

Your job is to carefully evolve it.

Prefer:

EXTEND over REPLACE.

REUSE over DUPLICATE.

DETERMINISTIC CODE over AI for security/calculations.

EVIDENCE over AI speculation.

BACKWARD COMPATIBILITY over architectural perfection.

SECURITY over convenience.

INCREMENTAL DELIVERY over massive rewrites.

For every user-facing capability:

BACKEND + API + FRONTEND + SECURITY + TESTING + BUILD VERIFICATION

must be completed together.

Start by inspecting the existing backend AND frontend.

Produce a concise architecture assessment and Phase A implementation plan.

Then begin Phase A.

Do not begin later phases until Phase A is implemented, integrated into both backend and frontend, tested, built, and regression-checked successfully.