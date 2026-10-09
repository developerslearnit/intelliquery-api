The project already contains:

* Authentication
* Multi-tenancy
* Workspace management
* RBAC
* Audit logging
* Subscription management

Continue from the existing solution.

DO NOT recreate previous work.

Build the Database Connectivity and Schema Discovery module.

Supported databases:

1. PostgreSQL
2. SQL Server
3. MySQL

Create a new module:

DataSource

Capabilities:

* Register database connection
* Validate connection
* Encrypt connection strings
* Test connectivity
* Enable/Disable data source

Entities:

DataSource

* Id
* WorkspaceId
* Name
* DatabaseType
* EncryptedConnectionString
* IsActive
* LastSyncAt

SchemaMetadata

* Id
* WorkspaceId
* DataSourceId
* TableName
* ColumnName
* DataType
* IsNullable
* Description

Relationships

* ParentTable
* ParentColumn
* ChildTable
* ChildColumn

Build a Schema Discovery Service that:

1. Connects to supported databases
2. Reads tables
3. Reads columns
4. Reads indexes
5. Reads foreign keys
6. Reads views
7. Reads stored procedures
8. Generates schema metadata

Create background synchronization using:

* Hosted Services
* Quartz.NET

Requirements:

Store:

* Table descriptions
* Column descriptions
* Relationships
* Business-friendly aliases

Example:

tbl_customer

should become

Customer

The system should automatically create human-readable metadata.

Create APIs:

POST /datasources

GET /datasources

POST /datasources/test

POST /datasources/sync

GET /schema

GET /schema/tables

GET /schema/relationships

Requirements:

* Support millions of metadata records
* Optimized PostgreSQL indexes
* Dapper for read-heavy operations
* EF Core for writes

Provide:

1. Database schema
2. API contracts
3. Sequence diagrams
4. Synchronization architecture

Do not build embeddings or AI yet.

This is Phase 2.
