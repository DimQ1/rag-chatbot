# AGENTS.md — RAG Chatbot Workspace Guide

> Quick-start reference for AI agents working in this repo. Read this before making changes.

---

## Project Overview

A fullstack RAG (Retrieval-Augmented Generation) chatbot scaffold with:
- **Frontend:** Angular 21 SPA (`rag-chatbot/`)
- **Backend:** .NET 10 Web API (`rag-chatbot-api/`)
- The Angular app authenticates via JWT obtained from the API, then sends user questions to the RAG endpoint and renders answers with source citations.

---

## Tech Stack

| Layer | Technology | Version |
|---|---|---|
| Frontend framework | Angular (standalone components) | 21.2.x |
| UI library | Angular Material + CDK | 21.2.x |
| Social login | @abacritt/angularx-social-login | 2.6.x |
| Backend framework | ASP.NET Core Web API | .NET 10 |
| ORM / DB | EF Core + SQLite | 10.0.7 |
| Auth (API) | JWT Bearer + Google.Apis.Auth | JwtBearer 10.0.7 |
| OpenAPI | Microsoft.AspNetCore.OpenApi | 10.0.7 |
| TypeScript | ~5.9.2 | |
| Test runner (FE) | Vitest | 4.x |

---

## Build & Run Commands

### Frontend (`rag-chatbot/`)

```bash
cd rag-chatbot
npm start          # ng serve — dev server on http://localhost:4200
npm run build      # production build
npm run watch      # build --watch (dev)
npm test           # vitest
```

### Backend (`rag-chatbot-api/`)

```bash
cd rag-chatbot-api
dotnet watch run --launch-profile http   # dev server on http://localhost:5024
dotnet run --launch-profile http         # without hot reload
dotnet build
```

### Both together (VS Code tasks)

- **`start: fullstack`** — runs both in parallel via `.vscode/tasks.json`
- **`frontend: ng serve`** — frontend only
- **`backend: dotnet watch`** — backend only

---

## Architecture

```
Browser (Angular)
    │
    ├─ POST /api/auth/register|login|google  → AuthController
    ├─ POST /api/rag/query  [JWT required]   → RagController
    └─ GET  /api/admin/users [Admin role]    → AdminController
```

- Angular calls `http://localhost:5024/api` in dev (see `environment.development.ts`).
- CORS is locked to `http://localhost:4200` in dev.
- The API uses **EnsureCreated()** at startup — no migrations, schema auto-created from model.
- The RAG endpoint is a **stub** — returns a demo answer. The real vector store / LLM pipeline is not yet integrated.

---

## Configuration & Secrets

### Backend — `appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=rag-chatbot.dev.db"
  },
  "Jwt": {
    "Issuer": "rag-chatbot-api",
    "Audience": "rag-chatbot-client",
    "SecretKey": "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_KEY_32+",
    "ExpirationMinutes": 120
  },
  "GoogleAuth": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID"
  }
}
```

Options are bound via the **Options pattern**:
- `JwtOptions` ← `"Jwt"` section (`rag-chatbot-api/Options/JwtOptions.cs`)
- `GoogleAuthOptions` ← `"GoogleAuth"` section (`rag-chatbot-api/Options/GoogleAuthOptions.cs`)

### Frontend — `src/environments/`

| File | Use |
|---|---|
| `environment.development.ts` | `apiUrl: 'http://localhost:5024/api'` |
| `environment.ts` | `apiUrl: 'https://your-backend-api.com/api'` (prod placeholder) |

Google Client ID is hardcoded in `app.config.ts` → `GoogleLoginProvider('YOUR_GOOGLE_CLIENT_ID')`. Must be replaced for Google sign-in to work.

---

## .NET API Conventions

### Controller pattern
- Primary constructor injection, stored in `readonly` fields.
- Route: `[Route("api/[controller]")]`
- Return `ActionResult<T>` with typed DTOs.
- Conflict/Unauthorized/BadRequest returned with `new { message = "..." }` objects.

### Auth approach
1. **Email/password** — PBKDF2-SHA256 with random salt (`PasswordService.cs`). Hash and salt stored as Base64 strings.
2. **Google OAuth** — Frontend gets Google ID token via `angularx-social-login`, posts to `POST /api/auth/google`. API validates with `GoogleJsonWebSignature.ValidateAsync`. A local `AppUser` is upserted.
3. **JWT** — `TokenService.CreateToken(user)` issues HS256 tokens with claims: `sub` (userId), `email`, `name`, `role`. Token lifetime: `ExpirationMinutes` from config.

### Roles
- Default: `"User"`
- Admin: `"Admin"` — required for `GET /api/admin/users`.
- Roles are embedded in JWT claims (`ClaimTypes.Role`).

### Database
- `AppDbContext` (`Data/AppDbContext.cs`) — one `DbSet<AppUser>`.
- Email has a unique index. Role has default value `"User"`.
- **No EF migrations** — `dbContext.Database.EnsureCreated()` called at startup. To change schema, drop the `.db` file and restart.
- SQLite file: `rag-chatbot.dev.db` in the API project root (dev).

### DTOs
Located in `rag-chatbot-api/Dtos/`:
- `Auth/AuthResponse.cs` — `{Id, Email, Name, Token, Role}`
- `Auth/LoginRequest.cs`, `RegisterRequest.cs`, `GoogleLoginRequest.cs`
- `Rag/RagQueryRequest.cs` — `{Question}`
- `Rag/RagQueryResponse.cs` — `{Answer, Sources[]}`

---

## Angular Conventions

### Component structure
- All components are **standalone** (`standalone: true`).
- One component per folder: `feature-name.ts`, `feature-name.html`, `feature-name.scss`, `feature-name.spec.ts`.
- Named exports match the filename in PascalCase (e.g. `export class Login`).
- Dependencies injected with `inject()` function, stored as `private readonly`.

### Routing
- `app.routes.ts` — lazy-loaded via `loadComponent()`.
- Redirect: `''` → `login`, wildcard → `login`.
- `chat` route is protected by `authGuard`.

### Auth guard (`core/guards/auth-guard.ts`)
- Functional guard (`CanActivateFn`).
- Checks `authService.isLoggedIn()`, redirects to `/login` if false.

### Auth interceptor (`core/interceptors/auth-interceptor.ts`)
- Functional interceptor (`HttpInterceptorFn`).
- Attaches `Authorization: Bearer <token>` to every outbound request if token exists.
- Registered in `app.config.ts` via `withInterceptors([authInterceptor])`.

### Services
- All `providedIn: 'root'` singletons.
- `AuthService` — state via `BehaviorSubject<AuthUser | null>`. Persists to `localStorage` under key `auth_token`. Listens to Google `authState` observable, exchanges ID token with backend.
- `ChatService` — in-memory message store via `BehaviorSubject<ChatMessage[]>`. Exposed as `readonly messages` observable.
- `RagService` — thin HTTP wrapper around `POST /api/rag/query`.

### Forms
- **Reactive forms** (`ReactiveFormsModule`) everywhere.
- Use `fb.nonNullable.group({...})` for login/register.
- Validate before submit: `if (this.form.invalid) return;`
- Errors surfaced via `errorMessage: string` component field, displayed in template.

### Error handling pattern
```ts
this.service.call().subscribe({
  next: () => { /* success */ },
  error: (err) => {
    this.errorMessage = err?.error?.message ?? 'Fallback message.';
  },
});
```

### UI
- Angular Material throughout (mat-card, mat-form-field, mat-button, mat-icon, mat-toolbar, mat-progress-spinner).
- `provideAnimationsAsync()` used in `app.config.ts`.

---

## Key File Map

| File | Purpose |
|---|---|
| `rag-chatbot/src/app/app.config.ts` | Angular bootstrap config, providers, interceptors |
| `rag-chatbot/src/app/app.routes.ts` | Route definitions |
| `rag-chatbot/src/app/core/services/auth.ts` | Auth state, login, register, Google, logout |
| `rag-chatbot/src/app/core/services/chat.ts` | Chat message state |
| `rag-chatbot/src/app/core/services/rag.ts` | RAG API calls |
| `rag-chatbot/src/app/core/guards/auth-guard.ts` | Route protection |
| `rag-chatbot/src/app/core/interceptors/auth-interceptor.ts` | JWT injection |
| `rag-chatbot/src/app/features/auth/login/login.ts` | Login component |
| `rag-chatbot/src/app/features/auth/register/register.ts` | Register component |
| `rag-chatbot/src/app/features/chat/chat/chat.ts` | Chat component |
| `rag-chatbot/src/environments/environment.development.ts` | Dev API URL |
| `rag-chatbot-api/Program.cs` | DI setup, middleware pipeline |
| `rag-chatbot-api/Controllers/AuthController.cs` | Register / Login / Google endpoints |
| `rag-chatbot-api/Controllers/RagController.cs` | RAG query endpoint (stub) |
| `rag-chatbot-api/Controllers/AdminController.cs` | Admin user list |
| `rag-chatbot-api/Data/AppDbContext.cs` | EF Core context |
| `rag-chatbot-api/Models/AppUser.cs` | User entity |
| `rag-chatbot-api/Services/TokenService.cs` | JWT creation |
| `rag-chatbot-api/Services/PasswordService.cs` | PBKDF2 hash/verify |
| `rag-chatbot-api/Options/JwtOptions.cs` | JWT config binding |
| `rag-chatbot-api/Options/GoogleAuthOptions.cs` | Google config binding |
| `rag-chatbot-api/appsettings.Development.json` | Dev secrets (DB, JWT, Google) |

---

## Known Placeholders / Next Steps

- **RAG pipeline not implemented** — `RagController.Query` returns a hardcoded demo string. Needs vector store + LLM wiring.
- **Google Client ID** — must be set in both `appsettings.Development.json` (`GoogleAuth:ClientId`) and `app.config.ts` (`GoogleLoginProvider`).
- **JWT Secret** — change `SecretKey` in `appsettings.Development.json` before any real use.
- **Production `environment.ts`** — `apiUrl` is a placeholder; update before deploying frontend.
- **No EF migrations** — schema changes require dropping the SQLite file.

## Agent Guidance

- Before adding a new API endpoint: add the DTO in `Dtos/`, add a method to the relevant controller, follow the `{ message }` error object convention.
- Before adding an Angular feature: create a folder under `features/`, use standalone component, register lazy route in `app.routes.ts`.
- Do not add NgModules — this project uses fully standalone Angular components.
- Do not add EF Core migrations — modify the model and restart (EnsureCreated).
- Google auth errors are surfaced via `AuthService.googleAuthError` observable; subscribe in the login component.
- Email/password conflicts return `409 Conflict`; Google-vs-password conflicts return `401` or `409` depending on direction.
