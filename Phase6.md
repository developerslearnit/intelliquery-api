You are a Principal Frontend Architect specializing in:

* Next.js 15 App Router
* React 19
* TypeScript
* TailwindCSS
* shadcn/ui
* TanStack Query
* Zustand
* React Hook Form
* Zod
* Recharts
* Enterprise SaaS UX
* AI Analytics Platforms

The existing application is already functional and includes:

* Authentication
* Multi-tenancy
* Workspace management
* Dashboard
* Data source management
* Schema explorer
* Natural Language to SQL
* AI query workspace
* Query history
* Saved reports
* AI-generated insights
* Role-based access control
* Subscription management
* Existing API client abstractions
* Existing route protection
* Existing layouts
* Existing state management

IMPORTANT:

This is an enhancement project.

DO NOT:

* Rebuild the existing application
* Rename existing routes
* Replace existing API clients without a strong reason
* Change working authentication flows
* Change working state management unnecessarily
* Rewrite unrelated components
* Remove existing functionality
* Break existing API contracts
* Introduce unnecessary new dependencies
* Modify existing styling globally in a way that breaks current pages

All new functionality must be additive, modular, reusable, backward-compatible, and consistent with the existing design system.

Before making changes:

1. Inspect the existing project structure.
2. Identify current conventions for:

   * routing
   * feature folders
   * API clients
   * TanStack Query hooks
   * Zustand stores
   * shadcn/ui usage
   * error handling
   * authentication
   * tenant/workspace resolution
3. Reuse existing patterns wherever possible.
4. Avoid duplicating utilities or components that already exist.

========================================================

GOAL

========================================================

Extend the frontend to support:

1. AI Monitoring
2. Alerts
3. Anomaly Detection
4. Executive Intelligence
5. Scheduled Reports
6. Recommendations
7. Monitoring History

The application should evolve from:

Ask Question → Get SQL → Get Result → View Insight

into:

Monitor Business → Detect Issues → Generate Insight → Alert User → Recommend Action → Create Executive Intelligence

========================================================

NEW NAVIGATION

========================================================

Extend the authenticated navigation with:

* Overview
* Ask Data
* Data Sources
* Schema Explorer
* Monitors
* Alerts
* Executive Intelligence
* Reports
* Query History
* Administration

Preserve all existing routes.

Do not rename existing navigation destinations.

Add new routes only.

Suggested routes:

/monitors
/monitors/new
/monitors/[id]
/monitors/[id]/history

/alerts

/intelligence
/intelligence/reports
/intelligence/reports/[id]

/anomalies

/reports/scheduled

========================================================

EXECUTIVE DASHBOARD UPDATE

========================================================

Enhance the existing dashboard without replacing it.

Add an Executive Intelligence section.

Display:

* Active Monitors
* Open Alerts
* Critical Alerts
* Detected Anomalies
* Latest Executive Summary
* Key Risks
* Key Opportunities
* Recent Recommendations

Create reusable dashboard cards.

Example cards:

Active Monitors
12

Open Alerts
5

Critical Issues
2

Anomalies Detected
7

Do not hardcode these values.

Use backend APIs.

========================================================

EXECUTIVE SUMMARY

========================================================

Add a prominent AI-generated executive summary component.

Example:

"This week's revenue increased by 8.4%, primarily driven by growth in enterprise customers.

However, failed payments increased by 14%, and the West region experienced a 9% decline.

Two anomalies require attention."

Display:

* Summary
* Key Findings
* Risks
* Opportunities
* Recommended Actions
* Generated Date
* Reporting Period

Add:

Refresh Summary

button where supported by the backend.

========================================================

MONITORS FEATURE

========================================================

Create a full Monitor Management experience.

Route:

/monitors

Display monitors using:

* Table view
* Optional card view

Columns:

* Name
* Type
* Schedule
* Status
* Last Run
* Last Result
* Alerts Generated
* Actions

Actions:

* View
* Edit
* Enable
* Disable
* Run Now
* Delete

Provide:

* Search
* Filtering
* Pagination
* Status filter
* Monitor Type filter

========================================================

CREATE MONITOR

========================================================

Route:

/monitors/new

Create a multi-step monitor creation experience.

STEP 1

Choose Data Source

STEP 2

Define What to Monitor

Allow two methods:

A. Natural Language

Example:

"Monitor daily revenue and alert me when revenue drops more than 15% compared to the previous 7-day average."

B. Structured Rule Builder

Fields:

* Metric
* Aggregation
* Time period
* Comparison
* Threshold

STEP 3

Schedule

Options:

* Hourly
* Daily
* Weekly
* Monthly
* Custom cron where supported

STEP 4

Alert Settings

Allow:

* Information
* Warning
* Critical

STEP 5

Review

Show:

* Monitor name
* Data source
* Monitoring rule
* Schedule
* Alert settings

Then:

Create Monitor

Use:

* React Hook Form
* Zod validation

========================================================

MONITOR DETAILS

========================================================

Route:

/monitors/[id]

Display:

* Monitor Name
* Status
* Description
* Schedule
* Data Source
* Last Execution
* Next Execution

Show trend visualization.

Display:

* Historical executions
* Generated findings
* Detected anomalies
* Alerts generated
* AI recommendations

Provide:

Run Monitor Now

Enable / Disable

Edit Monitor

========================================================

MONITOR HISTORY

========================================================

Route:

/monitors/[id]/history

Display historical executions.

Columns:

* Execution Date
* Status
* Result
* Anomalies
* Alerts
* Duration

Clicking execution should show:

* Metrics analyzed
* Findings
* AI-generated explanation
* Anomalies
* Recommended actions

========================================================

ALERT CENTER

========================================================

Create:

/alerts

This must act as an enterprise Alert Center.

Display:

* All Alerts
* Open Alerts
* Acknowledged Alerts
* Critical Alerts

Provide filtering by:

* Severity
* Monitor
* Date
* Status
* Data Source

Severity:

Information
Warning
Critical

Use visually distinct but accessible badges.

Alert card/table fields:

* Severity
* Title
* Description
* Source Monitor
* Detected At
* Status

Actions:

Acknowledge

View Details

Open Monitor

========================================================

ALERT DETAILS

========================================================

Create a drawer, modal, or details page consistent with the current UI architecture.

Display:

* Alert title
* Severity
* What happened
* Expected behavior
* Actual behavior
* Difference
* Related data
* AI explanation
* Recommended actions
* Related monitor

Example:

Revenue decreased by 18.4%.

Expected:
₦28,400,000

Actual:
₦23,170,000

Difference:
-₦5,230,000

AI Explanation:

"The decline was primarily driven by reduced collections from three enterprise customers."

========================================================

ANOMALY EXPLORER

========================================================

Create:

/anomalies

Display detected anomalies.

Support:

* Time series graph
* Anomaly markers
* Severity
* Confidence
* Metric
* Data Source
* Detection Date

Users should be able to:

* Open anomaly details
* View related monitor
* View related query
* View AI explanation

========================================================

EXECUTIVE INTELLIGENCE

========================================================

Create:

/intelligence

This should feel different from the technical Query Workspace.

Target users:

* Executives
* Finance leaders
* Managers
* Business users

Keep technical SQL information hidden by default.

Sections:

Executive Summary

Key Metrics

Risks

Opportunities

Anomalies

Recommendations

Business Trends

Latest Reports

========================================================

AI RECOMMENDATIONS

========================================================

Create reusable Recommendation components.

Each recommendation should contain:

* Title
* Description
* Priority
* Reason
* Related Metric
* Related Alert
* Related Monitor

Example:

High Priority

Investigate Enterprise Customer Churn

"Revenue from the top 10 enterprise customers decreased by 17%."

Allow actions such as:

* Investigate
* Ask AI
* View Data
* Dismiss

Do not implement destructive automated actions unless backend support explicitly exists.

========================================================

ASK AI CONTEXTUAL INTEGRATION

========================================================

Integrate the existing AI Query Workspace into the new features.

Examples:

From an alert:

Ask AI:
"Why did this happen?"

From an anomaly:

Ask AI:
"Which customers contributed most to this anomaly?"

From an executive report:

Ask AI:
"Explain the West region decline."

The existing AI Query feature must be reused.

Do not create a duplicate AI chat system.

Pass context such as:

* MonitorId
* AlertId
* DataSourceId
* Date range
* Metric

where supported by existing APIs.

========================================================

EXECUTIVE REPORTS

========================================================

Create:

/intelligence/reports

Display:

* Weekly Reports
* Monthly Reports
* Quarterly Reports

Fields:

* Report Name
* Period
* Generated Date
* Status

Report Details:

* Executive Summary
* Major Findings
* Risks
* Opportunities
* Recommendations
* Supporting charts

Provide:

* View
* Download
* Regenerate

Only expose actions supported by backend APIs.

========================================================

SCHEDULED REPORTS

========================================================

Create:

/reports/scheduled

Allow users to configure:

* Report type
* Data source
* Schedule
* Reporting period
* Recipient configuration if backend supports it

Examples:

Weekly Revenue Summary

Monthly Executive Summary

Quarterly Tax Performance

========================================================

VISUALIZATION

========================================================

Use Recharts for:

* Line charts
* Area charts
* Bar charts
* Pie charts only where appropriate

Preferred usage:

Trend charts:
LineChart

Revenue over time:
AreaChart

Comparisons:
BarChart

Do not overuse charts.

Charts must:

* Be responsive
* Handle empty states
* Handle loading states
* Format currency correctly
* Format large numbers
* Format dates according to user locale

========================================================

NIGERIAN BUSINESS SUPPORT

========================================================

The product should support Nigerian enterprise customers.

Do not hardcode Nigeria globally because this is intended as a global SaaS product.

Provide reusable formatting utilities for:

* Currency
* Number
* Date

Support:

NGN

and other currencies based on workspace settings.

Example:

₦245.8M

instead of:

245800000

========================================================

API INTEGRATION

========================================================

Create strongly typed API clients for:

Monitoring API

Alerts API

Executive Intelligence API

Anomaly API

Scheduled Reports API

Reuse the existing HTTP client infrastructure.

Create TanStack Query hooks such as:

useMonitors

useMonitor

useCreateMonitor

useUpdateMonitor

useRunMonitor

useMonitorHistory

useAlerts

useAcknowledgeAlert

useAnomalies

useExecutiveSummary

useExecutiveReports

useScheduledReports

Use appropriate:

queryKey structures

cache invalidation

staleTime

retry behavior

loading state handling

========================================================

STATE MANAGEMENT

========================================================

Do not place server state in Zustand.

Use:

TanStack Query

for remote/server state.

Use Zustand only for true client state such as:

* Sidebar state
* Temporary UI preferences
* Workspace UI selection where appropriate
* Chart configuration
* Local filters that need global sharing

========================================================

LOADING STATES

========================================================

Every asynchronous screen must implement:

* Loading skeleton
* Empty state
* Error state
* Retry mechanism

Do not display blank screens.

Examples:

No monitors exist

"Create your first monitor to start proactively analyzing your data."

No alerts

"No active alerts. Your monitored metrics are currently within expected ranges."

========================================================

ERROR HANDLING

========================================================

Reuse the existing application error handling.

Handle:

401

403

404

409

422

429

500

Display user-friendly messages.

Do not expose:

* Stack traces
* Internal database errors
* AI provider errors
* Raw backend exceptions

========================================================

SECURITY

========================================================

Respect existing:

* Authentication
* Workspace isolation
* RBAC
* Permission handling

Hide UI capabilities that users are not authorized to use.

However:

Frontend authorization must NOT be considered the security boundary.

The backend remains authoritative.

========================================================

ACCESSIBILITY

========================================================

Ensure:

* Keyboard navigation
* ARIA labels
* Proper button labels
* Accessible forms
* Accessible tables
* Sufficient contrast
* Screen-reader-friendly status indicators

========================================================

RESPONSIVE DESIGN

========================================================

Support:

Desktop

Tablet

Mobile

Prioritize desktop experience because this is an enterprise analytics application.

On mobile:

* Collapse large tables intelligently
* Use drawers where appropriate
* Preserve key actions
* Avoid horizontal overflow where possible

========================================================

DARK MODE

========================================================

Preserve existing dark mode implementation.

All new components must support:

Light mode

Dark mode

Do not introduce hardcoded colors that break theming.

========================================================

COMPONENT ARCHITECTURE

========================================================

Create reusable components where appropriate.

Examples:

MetricCard

MonitorStatusBadge

AlertSeverityBadge

ExecutiveSummaryCard

RecommendationCard

AnomalyChart

MonitorTrendChart

EmptyState

ErrorState

DataTable

DateRangeSelector

DataSourceSelector

WorkspaceSelector

Reuse existing equivalents instead of creating duplicates.

========================================================

FEATURE STRUCTURE

========================================================

Follow the project's existing feature organization.

If compatible with the current architecture, use:

features/

monitoring/

components/
hooks/
api/
schemas/
types/

alerts/

components/
hooks/
api/
types/

intelligence/

components/
hooks/
api/
types/

anomalies/

components/
hooks/
api/
types/

Do not force this structure if the existing application already uses another consistent feature architecture.

Consistency with the existing codebase is more important.

========================================================

TESTING

========================================================

Add tests for new functionality.

Test:

* Monitor creation
* Monitor validation
* Alert acknowledgement
* Executive report display
* Permission-based UI
* Error states
* Empty states

Use the testing tools already installed in the project.

Do not introduce a second testing framework unnecessarily.

========================================================

BACKWARD COMPATIBILITY

========================================================

This requirement is critical.

After implementation:

Verify existing functionality still works:

* Login
* Registration
* Workspace switching
* Data Sources
* Schema Explorer
* AI Query Workspace
* Text-to-SQL
* Query Results
* AI Insights
* Saved Reports
* Query History
* Administration

Do not change existing behavior unless required to fix a confirmed defect.

========================================================

IMPLEMENTATION APPROACH

========================================================

Work incrementally.

For every major feature:

1. Inspect existing implementation patterns.
2. Define types.
3. Create API integration.
4. Create TanStack Query hooks.
5. Build UI components.
6. Build page.
7. Add validation.
8. Add loading/error/empty states.
9. Add authorization.
10. Add tests.
11. Verify no regression to existing functionality.

Do not generate disconnected sample code.

Modify the real application.

========================================================

FINAL DELIVERABLE

========================================================

After implementation provide a concise technical report containing:

1. New routes created
2. New components created
3. New API integrations
4. New TanStack Query hooks
5. Existing files modified
6. Any database/backend assumptions made
7. Authorization requirements
8. Test coverage added
9. Backward compatibility verification
10. Recommended next frontend enhancements

IMPORTANT FINAL RULES:

* Preserve existing features.
* Extend instead of rewrite.
* Reuse before creating.
* Do not use mock data when real APIs exist.
* Do not introduce breaking changes.
* Do not replace working architecture unnecessarily.
* Do not leave TODO placeholders.
* Do not create fake APIs.
* Do not silently change existing contracts.
* Keep the implementation production-ready.
