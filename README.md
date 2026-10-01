# CqrsModulerMonolithApp2026

A modular monolith sample focused on a `Products` module, implemented with a CQRS flow on .NET.

This repository demonstrates:

- Clear separation between `Domain`, `Application`, `Infrastructure`, and API layers
- Command/Query handling with MediatR
- FastEndpoints-based HTTP API
- EF Core + Sqlite persistence
- Standard response envelope with `ApiResult<T>`
- Soft-delete and restore behavior
- Plan-based create quota checks

## Table Of Contents

1. [What This Project Is](#what-this-project-is)
2. [Architecture Overview](#architecture-overview)
3. [Repository Structure](#repository-structure)
4. [Technology Stack](#technology-stack)
5. [Request Pipeline](#request-pipeline)
6. [Domain Rules](#domain-rules)
7. [Data Model](#data-model)
8. [API Contract](#api-contract)
9. [Run Locally](#run-locally)
10. [Frontend Notes](#frontend-notes)
11. [Shared Type Contracts](#shared-type-contracts)
12. [How To Extend The System](#how-to-extend-the-system)
13. [Troubleshooting](#troubleshooting)
14. [Current Status](#current-status)

## What This Project Is

This codebase is a learning/reference implementation of a modular monolith where:

- Modules contain business concerns (`Products` in this repo)
- Shared projects host reusable abstractions and infrastructure
- HTTP endpoints stay thin, while business logic flows through handlers and rules

It is intentionally structured to make feature growth predictable without immediately splitting into microservices.

## Architecture Overview

High-level flow:

`HTTP Endpoint -> Service Facade -> MediatR Command/Query -> Handler -> BusinessRules -> Repository -> EF Core`

Key design choices:

- Endpoints are transport adapters, not business logic centers
- Commands/queries are explicit and testable application use-cases
- Repositories abstract EF Core data access
- Shared contracts (`ApiResult<T>`, base entities, repository abstractions) keep consistency across modules

## Repository Structure

```text
.
|-- ECommerce.App.sln
|-- src
|   |-- API
|   |   `-- ECommerce.API
|   |-- Modules
|   |   `-- Products
|   |       |-- Products.Domain
|   |       |-- Products.Application
|   |       `-- Products.Infrastructure
|   |-- Services
|   |   `-- Products
|   |       `-- Products.Service
|   `-- Shared
|       |-- Shared.Kernel
|       |-- Shared.Application
|       `-- Shared.Infrastructure
|-- frontend
|   |-- web
|   `-- mobile
`-- shared
    `-- types
```

Layer responsibilities:

- `Shared.Kernel`: core abstractions (`EntityBase`, `IRepository<T>`, `ISoftDeletable`)
- `Shared.Application`: cross-cutting app contracts (`ApiResult<T>`, standard messages, migration/seeding abstractions)
- `Shared.Infrastructure`: EF Core `AppDbContext`, generic repository, db bootstrap services
- `Products.Domain`: pure domain model and repository interface
- `Products.Application`: CQRS commands/queries, handlers, business rules, DTOs
- `Products.Infrastructure`: EF configuration and repository implementation
- `Products.Service`: application-facing facade over MediatR
- `ECommerce.API`: HTTP entry point, middleware, endpoint definitions, DI wiring

## Technology Stack

Backend:

- .NET `10.0` (target framework: `net10.0`)
- FastEndpoints `6.1.0`
- FastEndpoints.Swagger `6.1.0`
- MediatR `13.1.0`
- EF Core `10.0.0`
- EF Core Sqlite `10.0.0`

Frontend:

- Web: Next.js `15`, React `19`, React Query `5`, Zustand `5`
- Mobile: Expo `53`, React Native `0.79`, React Query `5`, Zustand `5`

## Request Pipeline

The API startup (`Program.cs`) builds this sequence:

1. Service registration
2. Middleware registration
3. FastEndpoints route configuration (global prefix `v1`)
4. OpenAPI + Swagger UI registration
5. Database migration/creation and seeding on startup

Middlewares:

- `UserIdPreProcessor`
  - Reads `x-user-id` header
  - Converts a valid GUID into an authenticated principal
  - Stores parsed user id in `HttpContext.Items["CurrentUserId"]`
- `SubscriptionPreProcessor`
  - Reads `x-user-plan` header
  - Defaults to `free` when missing
  - Stores value in `HttpContext.Items["UserPlan"]`

Important detail:

- Endpoints currently use `AllowAnonymous()` at FastEndpoints level.
- Authorization is enforced manually inside endpoint handlers by checking resolved user id.

## Domain Rules

Product creation validation rules:

- Name is required and trimmed
- Price must be greater than 0
- Stock cannot be negative
- Duplicate active product name is rejected

Soft-delete behavior:

- `DELETE /products/{id}` sets `DeletedAtUtc` (logical delete)
- Active queries exclude soft-deleted rows via EF query filter
- `POST /products/{id}/restore` reactivates logically deleted rows

Quota behavior (`ISubscriptionQuotaService`):

- `free` plan: max 3 active products
- `basic` plan: max 20 active products
- Other plan values: no limit

## Data Model

### Product Entity

Fields:

- `Id: Guid`
- `Name: string` (required, max 150)
- `Price: decimal(18,2)` (required)
- `Stock: int` (required)
- `DeletedAtUtc: DateTime?` (soft-delete marker)
- `CreatedAtUtc: DateTime`
- `UpdatedAtUtc: DateTime?`

EF mapping:

- Table: `products`
- Index on `Name`
- Global filter: `DeletedAtUtc == null`

## API Contract

Global route prefix: `/v1`

All endpoints return:

```json
{
  "success": true,
  "message": "string",
  "data": {},
  "statusCode": 200
}
```

### Required Headers

Every product endpoint expects:

- `x-user-id`: GUID (required in practice)
- `x-user-plan`: optional (`free` by default)

### Endpoints

#### 1) Get Products

- Method: `GET`
- Path: `/v1/products`
- Success: `200`
- Error: `401` when user id is missing/invalid

Sample request:

```bash
curl -X GET "http://localhost:5031/v1/products" \
  -H "x-user-id: 11111111-1111-1111-1111-111111111111" \
  -H "x-user-plan: free"
```

Sample response:

```json
{
  "success": true,
  "message": "OK",
  "data": [
    {
      "id": "8dc50e80-6f6d-4d16-98eb-3f9bc15cc2d9",
      "name": "Keyboard",
      "price": 99.9,
      "stock": 15,
      "deletedAtUtc": null
    }
  ],
  "statusCode": 200
}
```

#### 2) Create Product

- Method: `POST`
- Path: `/v1/products`
- Success: `201`
- Errors:
  - `400` invalid input
  - `401` missing/invalid user id
  - `403` quota exceeded
  - `409` duplicate name

Sample request:

```bash
curl -X POST "http://localhost:5031/v1/products" \
  -H "Content-Type: application/json" \
  -H "x-user-id: 11111111-1111-1111-1111-111111111111" \
  -H "x-user-plan: free" \
  -d '{
    "name": "Keyboard",
    "price": 99.90,
    "stock": 15
  }'
```

#### 3) Delete Product (Soft Delete)

- Method: `DELETE`
- Path: `/v1/products/{productId}`
- Success: `200`
- Errors:
  - `401` missing/invalid user id
  - `404` product not found

Sample request:

```bash
curl -X DELETE "http://localhost:5031/v1/products/{productId}" \
  -H "x-user-id: 11111111-1111-1111-1111-111111111111"
```

#### 4) Restore Product

- Method: `POST`
- Path: `/v1/products/{productId}/restore`
- Success: `200`
- Errors:
  - `401` missing/invalid user id
  - `404` product not found
  - `400` product already active

Sample request:

```bash
curl -X POST "http://localhost:5031/v1/products/{productId}/restore" \
  -H "x-user-id: 11111111-1111-1111-1111-111111111111"
```

### Standard Message Catalog

Product-related messages include:

- `Product name is required.`
- `Product price must be greater than zero.`
- `Product stock cannot be negative.`
- `A product with the same name already exists.`
- `Product not found.`
- `Product is already active.`
- `Created`
- `Deleted`
- `Restored`

Authorization/subscription messages include:

- `Unauthorized`
- `Create quota exceeded for current plan.`

## Run Locally

### Prerequisites

- .NET SDK `10.0.x`
- Node.js `20+`
- npm

### Backend

From repo root:

```bash
dotnet restore ECommerce.App.sln
dotnet build ECommerce.App.sln
dotnet run --project src/API/ECommerce.API
```

Default local URLs:

- HTTP: `http://localhost:5031`
- HTTPS profile also exists in launch settings

Swagger UI:

- `http://localhost:5031/swagger`

Database:

- Connection string key: `ConnectionStrings:DefaultConnection`
- Default value: `Data Source=ecommerce.db`
- Db file is created in the API runtime working directory

Startup DB behavior:

- If EF migrations exist, app runs `Database.MigrateAsync()`
- If migrations do not exist, app checks expected vs existing tables and applies `EnsureCreated()` fallback when needed

### Useful Local Commands

Build all:

```bash
dotnet build ECommerce.App.sln
```

Run API only:

```bash
dotnet run --project src/API/ECommerce.API
```

## Frontend Notes

This repository currently contains frontend infrastructure and API integration helpers, not full production UI pages/screens.

### Shared Frontend-Backend Contracts

- File: `shared/types/api.types.ts`
- Contains:
  - `ApiResult<T>`
  - `ProductDto`
  - `CreateProductRequest`

### Web (`frontend/web`)

Contains:

- API helper functions (`src/lib/api.ts`)
- Product query/mutation hooks (`src/queries/hooks/useProducts.ts`)
- Query key factory (`src/queries/queryKeys.ts`)
- Auth context + Zustand sync (`src/auth`, `src/stores`)

Run commands:

```bash
cd frontend/web
npm install
npm run dev
```

Environment variable:

- `NEXT_PUBLIC_API_BASE_URL` (default in code: `http://localhost:5000/v1`)

Important mismatch to fix for full integration:

- Web API wrapper sends `Authorization: Bearer ...`
- Current backend resolves identity from `x-user-id`
- For end-to-end calls, either update frontend headers or backend auth strategy

### Mobile (`frontend/mobile`)

Contains:

- API helper functions (`src/api.ts`)
- Product query hook (`src/queries/hooks/useProducts.ts`)
- Auth context (`src/auth-context.tsx`)

Run commands:

```bash
cd frontend/mobile
npm install
npm run start
```

Environment variable:

- `EXPO_PUBLIC_API_BASE_URL` (default in code: `http://localhost:5000/v1`)

Same auth-header mismatch applies to mobile.

## Shared Type Contracts

`shared/types/api.types.ts` is designed as the canonical transport contract shared by web and mobile clients.

Benefits:

- Keeps DTO shape aligned with backend responses
- Reduces contract drift between clients
- Makes API changes visible in one place

## How To Extend The System

Recommended workflow for a new module:

1. Create module projects:
   - `<Feature>.Domain`
   - `<Feature>.Application`
   - `<Feature>.Infrastructure`
2. Add domain entity and repository interface in `Domain`.
3. Add EF mapping + repository implementation in `Infrastructure`.
4. Add commands/queries + handlers + business rules in `Application`.
5. Add service facade in `src/Services/<Feature>`.
6. Add endpoint classes in `src/API/ECommerce.API/Endpoints/<Feature>`.
7. Register dependencies in:
   - `ModuleServiceExtensions`
   - `InfrastructureServiceExtensions` (include EF config assembly)
8. Return `ApiResult<T>` consistently from endpoint handlers.

CQRS consistency checklist:

- Command/Query objects are small and explicit.
- Handlers own orchestration, not transport concerns.
- Business rules return fail-fast results before persistence.
- Repositories hide EF details from application layer.

## Troubleshooting

### 401 Unauthorized

Cause:

- Missing or invalid `x-user-id`.

Fix:

- Send a valid GUID in `x-user-id`.

### 403 Create Quota Exceeded

Cause:

- Reached plan limit (`free=3`, `basic=20`).

Fix:

- Use another plan value or delete/restore data as needed.

### Frontend Cannot Reach API

Cause:

- Frontend defaults to `http://localhost:5000/v1`, API runs on `http://localhost:5031`.

Fix:

- Set `NEXT_PUBLIC_API_BASE_URL` or `EXPO_PUBLIC_API_BASE_URL` to `http://localhost:5031/v1`.

### Frontend Requests Still Unauthorized

Cause:

- Frontend uses bearer token header, backend expects `x-user-id`.

Fix:

- Align auth strategy on both sides.

## Current Status

- Solution build verified with:
  - `dotnet build ECommerce.App.sln`
- Build result:
  - Success
  - 0 warnings
  - 0 errors
- Test projects:
  - None in this repository at the moment

---

If you want, the next step can be:

1. Add a Postman collection and include it in this repo.
2. Update web/mobile API wrappers to send `x-user-id` for local development.
3. Add automated integration tests for product endpoints.
