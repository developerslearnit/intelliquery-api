You are a Principal .NET Architect, Enterprise SaaS Architect, PostgreSQL Expert, and Microsoft Agent Framework specialist.

Build the backend foundation for an Enterprise AI Analytics Platform that allows users to connect databases, ask questions in natural language, automatically generate SQL, execute queries safely, and receive AI-generated insights.

The application is a multi-tenant SaaS platform.

Technology Stack:

* ASP.NET Core 10
* PostgreSQL
* Entity Framework Core
* Dapper
* Dispatcher Instead of MediatR
* FluentValidation
* Scrutor
* JWT Authentication
* Microsoft Agent Framework
* Azure OpenAI
* Redis
* Docker
* OpenTelemetry
* Serilog

Architecture Requirements:

* Clean Architecture
* DDD
* CQRS
* Vertical Slice Architecture
* Repository Pattern NOT allowed
* DbContext MUST NOT be injected into handlers
* Domain interfaces defined in Domain
* Implementations in Infrastructure
* Handlers consume interfaces only
* Dispatcher for all commands and queries
* FluentValidation pipeline
* Result pattern for responses

Project Structure:

src/

* Api
* Application
* Domain
* Infrastructure
* SharedKernel

Modules:

1. Identity
2. Workspace
3. Tenant
4. Audit
5. Subscription

Build:

1. Multi-tenant architecture
2. Workspace creation
3. Tenant resolution
4. User registration
5. User login
6. JWT authentication
7. Role-based access control
8. Refresh tokens
9. Email verification
10. Password reset

Tenant Model:

Workspace

* Id
* Name
* Slug
* SubscriptionPlan
* IsActive

Example:

acme.aiquery.com

globex.aiquery.com

The slug must uniquely identify tenants.

Build:

* Domain entities
* Aggregate roots
* Configurations
* Migrations
* Handlers
* Endpoints
* Validators
* DTOs
* OpenAPI documentation

Requirements:

* Full XML comments
* Production-ready code
* Unit-test-ready design
* No mock implementations
* No TODO comments
* No incomplete code

At the end provide:

1. Architecture diagram
2. Database schema
3. API endpoint documentation
4. Folder structure
5. Next implementation roadmap

Assume this is Phase 1 only.
Do not build AI, schema discovery, embeddings, or SQL generation yet.
