The project currently contains:

* Multi-tenancy
* Authentication
* Workspace management
* Database connectivity
* Schema discovery
* Metadata synchronization

Continue from the existing solution.

Do not recreate previous work.

Build the AI Engine.

Technology:

* Microsoft Agent Framework (latest) 
* Azure OpenAI (Azure.AI.OpenAI Version 2.1.0)
* PgVector
* PostgreSQL

Create these agents:

1. Schema Discovery Agent
2. Intent Understanding Agent
3. SQL Generation Agent
4. SQL Validation Agent
5. Data Analyst Agent
6. Visualization Recommendation Agent

Implement a hybrid retrieval architecture.

Requirements:

DO NOT send the entire schema to the LLM.

Instead:

1. Generate embeddings for:

   * Tables
   * Columns
   * Relationships
   * Business terms
   * Sample queries

2. Store embeddings in PgVector.

3. Retrieve only relevant schema context.

Workflow:

User Question

→ Intent Agent

→ Vector Search

→ Relevant Schema Retrieval

→ SQL Generation Agent

→ SQL Validation Agent

→ Query Execution

→ Data Analysis Agent

→ Visualization Agent

→ Final Response

Create entities:

EmbeddingRecord

QueryHistory

PromptTemplate

ConversationSession

SavedReport

GeneratedInsight

Security Requirements:

Generated SQL MUST be validated.

Block:

* DELETE
* UPDATE
* INSERT
* DROP
* TRUNCATE
* ALTER
* EXEC

Allow:

SELECT only

Implement:

* Query cost estimation
* Execution timeout
* Row limits
* Query auditing

Response Model:

Question

Generated SQL

Results

Insights

Recommended Chart

Confidence Score

Agent Framework Requirements:

Each agent must be isolated.

Use:

* Agent orchestration
* Tool calling
* Retry strategies
* Structured outputs

Create APIs:

POST /chat/query

POST /chat/analyze

GET /reports

POST /reports/save

GET /query-history

Provide:

1. Full Agent Architecture
2. Sequence Diagrams
3. Prompt Templates
4. Tool Definitions
5. PgVector Design
6. Azure OpenAI Integration

This is Phase 3.
Build production-ready enterprise code.
