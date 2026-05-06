# RAG Chatbot Workspace

Full-stack Retrieval-Augmented Generation chatbot workspace built with Angular 21 and ASP.NET Core .NET 10.

The repository contains:

- `rag-chatbot/`: Angular standalone SPA with login, register, account, chat, and admin screens.
- `rag-chatbot-api/`: ASP.NET Core Web API with JWT auth, Google sign-in exchange, admin endpoints, knowledge-base indexing, and SQLite-backed vector retrieval.
- `AngularTestApp.sln`: solution file for the backend workspace and related tooling.

## Features

- Email/password registration and login.
- Google sign-in flow via frontend ID token exchange.
- Protected chat experience with JWT authentication.
- Admin panel for users, markdown knowledge-base documents, and RAG configuration.
- SQLite + `SqliteVec` vector store for document indexing and retrieval.
- OpenAI-compatible chat and embedding configuration stored in the database.

## Stack

- Frontend: Angular 21, Angular Material, Angular CDK, RxJS.
- Backend: ASP.NET Core .NET 10, EF Core, SQLite, Semantic Kernel.
- Auth: JWT bearer auth and Google token validation.
- Retrieval: Semantic Kernel embeddings + `Microsoft.SemanticKernel.Connectors.SqliteVec`.

## Repository Layout

```text
.
|-- AngularTestApp.sln
|-- rag-chatbot/
|   |-- src/
|   `-- package.json
`-- rag-chatbot-api/
    |-- Controllers/
    |-- Data/
    |-- Dtos/
    |-- KnowledgeBase/
    |-- Models/
    |-- Options/
    |-- Services/
    `-- rag-chatbot-api.csproj
```

## Prerequisites

- Node.js 20.19+.
- npm 11+.
- .NET SDK 10.0.x.

## Quick Start

### 1. Frontend

```powershell
cd rag-chatbot
npm install
npm start
```

The Angular app runs on `http://localhost:4200`.

### 2. Backend

```powershell
cd rag-chatbot-api
dotnet build
dotnet watch run --launch-profile http
```

The API runs on `http://localhost:5024`.

### 3. Full stack in VS Code

The workspace includes tasks for:

- `frontend: ng serve`
- `backend: dotnet watch`
- `start: fullstack`

## Configuration

### Frontend

Update these placeholders before using real auth or deployment:

- `rag-chatbot/src/app/app.config.ts`: Google OAuth client ID.
- `rag-chatbot/src/environments/environment.ts`: production API URL.
- `rag-chatbot/src/environments/environment.development.ts`: local API URL if you change ports.

### Backend

Check `rag-chatbot-api/appsettings.Development.json` and replace placeholders before real use:

- `Jwt:SecretKey`
- `GoogleAuth:ClientId`
- `Rag:OpenAIApiKey`

The backend creates a local SQLite database in the API project folder during development.

## Development Notes

- The API uses `EnsureCreated()` rather than migrations.
- Knowledge-base documents live in `rag-chatbot-api/KnowledgeBase/`.
- RAG configuration can be edited through the admin UI.
- If you change the embedding model, reprocess the knowledge base from the admin panel.

## Public Repository Notes

- Do not commit real secrets, API keys, or production OAuth client IDs.
- Do not commit local SQLite database files, `bin/`, `obj/`, or IDE user files.
- Review the placeholder development admin credentials before publishing.

## Validation Commands

```powershell
cd rag-chatbot
npm test
```

```powershell
cd rag-chatbot-api
dotnet build
```

## Documentation

- See `rag-chatbot/README.md` for Angular-specific notes.
- See `AGENTS.md` for a concise workspace map and development conventions.

## Publishing Checklist

- Replace placeholder secrets and sample credentials.
- Confirm the Google OAuth client configuration.
- Choose and add a license before publishing.
- Verify `.gitignore` covers all generated and local-only files.
