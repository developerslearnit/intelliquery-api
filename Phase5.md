You are a Principal Enterprise Architect, Microsoft Agent Framework specialist, ASP.NET Core Architect, and SaaS Platform Engineer.

The existing platform is already in production and contains:

* Multi-tenancy
* Authentication
* RBAC
* Workspace Management
* Data Source Management
* Schema Discovery
* PgVector Embeddings
* Microsoft Agent Framework Orchestration
* Natural Language to SQL
* SQL Validation
* Query Execution
* AI Generated Insights
* Saved Reports
* Audit Logging
* Subscription Management

IMPORTANT:

This is an enhancement project.

DO NOT:

* Rebuild existing modules
* Refactor unrelated functionality
* Rename existing entities
* Change existing API contracts
* Modify existing database structures unless absolutely required
* Break backward compatibility
* Introduce breaking changes

All new functionality must be implemented using extension modules and feature-based architecture.

The goal is to transform the platform from:

Question → SQL → Results

into

Question → Results → Insights → Monitoring → Recommendations → Executive Intelligence

========================================================

PHASE 4

AI MONITORING & EXECUTIVE INTELLIGENCE

========================================================

Create the following new modules:

1. Monitoring
2. Alerts
3. Executive Intelligence
4. Scheduled Reports
5. Anomaly Detection

========================================================

MONITORING MODULE

========================================================

Users must be able to create AI Monitors.

Examples:

* Revenue Monitor
* Tax Collection Monitor
* Customer Churn Monitor
* Failed Payment Monitor
* Subscription Growth Monitor
* Expense Monitor

Entities:

Monitor

* Id
* WorkspaceId
* Name
* Description
* MonitorType
* IsActive
* ScheduleType
* CreatedAt

MonitorRule

* Id
* MonitorId
* RuleType
* ThresholdValue
* ComparisonOperator

MonitorExecution

* Id
* MonitorId
* StartedAt
* CompletedAt
* Status
* Findings
* GeneratedInsights

========================================================

ALERTS MODULE

========================================================

Create intelligent alerts.

Examples:

Revenue dropped 20%

Top customer stopped transacting

Collections declined compared to last month

Expenses increased unexpectedly

Entities:

Alert

* Id
* WorkspaceId
* MonitorId
* AlertType
* Severity
* Title
* Description
* CreatedAt
* IsAcknowledged

AlertSeverity

* Information
* Warning
* Critical

Provide APIs:

GET /alerts

POST /alerts/acknowledge

GET /alerts/open

========================================================

ANOMALY DETECTION MODULE

========================================================

Create anomaly detection services.

Capabilities:

* Trend analysis
* Statistical anomaly detection
* Pattern recognition
* Historical comparisons

Detect:

* Sudden revenue drops
* Revenue spikes
* Customer inactivity
* Tax collection anomalies
* Payment abnormalities

Architecture:

Use dedicated services.

No AI model required initially.

Implement statistical detection first.

Provide extension points for future ML models.

========================================================

EXECUTIVE INTELLIGENCE MODULE

========================================================

Create AI-generated executive summaries.

Examples:

Monthly Business Summary

Weekly Revenue Summary

Quarterly Tax Performance Summary

AI should automatically generate:

* Executive Summary
* Key Findings
* Risks
* Opportunities
* Recommended Actions

Entity:

ExecutiveReport

* Id
* WorkspaceId
* ReportPeriod
* Summary
* Findings
* Recommendations
* GeneratedAt

========================================================

MICROSOFT AGENT FRAMEWORK

========================================================

Create additional agents:

Monitoring Agent

Responsibilities:

* Evaluate monitors
* Analyze trends
* Generate findings

Anomaly Agent

Responsibilities:

* Detect unusual patterns
* Classify severity

Executive Summary Agent

Responsibilities:

* Generate executive reports
* Create recommendations

Recommendation Agent

Responsibilities:

* Suggest corrective actions

Examples:

Revenue dropped 18%

Recommendations:

* Review top customer churn
* Investigate regional performance
* Launch retention campaign

========================================================

BACKGROUND PROCESSING

========================================================

Use:

* Quartz.NET

Create jobs:

MonitorExecutionJob

AnomalyDetectionJob

ExecutiveReportJob

AlertGenerationJob

Requirements:

* Distributed-safe
* Retry capable
* Multi-tenant aware
* Fully auditable

========================================================

DASHBOARD APIs

========================================================

Provide endpoints:

GET /dashboard/executive-summary

GET /dashboard/alerts

GET /dashboard/monitoring

GET /dashboard/trends

GET /dashboard/anomalies

========================================================

PERFORMANCE REQUIREMENTS

========================================================

Support:

* 10,000+ monitors
* Millions of executions
* Multi-tenant isolation
* PostgreSQL partitioning where appropriate

Implement:

* Caching
* Pagination
* Background processing
* Query optimization

========================================================

DELIVERABLES

========================================================

Generate:

1. Architecture Diagram
2. Sequence Diagrams
3. Database Schema Changes
4. Agent Architecture
5. Quartz Job Architecture
6. API Documentation
7. Migration Scripts
8. Folder Structure
9. Unit Testing Strategy

IMPORTANT:

Preserve all existing functionality.

The current Text-to-SQL platform must continue to operate exactly as before.

All new functionality must be additive, modular, and production-ready.

No placeholders.

No mock implementations.

No TODO comments.

Generate enterprise-grade code suitable for SaaS deployment on Azure Kubernetes Service.
