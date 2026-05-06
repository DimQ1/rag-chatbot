# RagChatbot

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.2.9.

## Project Goal

Build a chatbot UI for a custom RAG (Retrieval-Augmented Generation) system with:

- User registration and login
- Google authentication support
- Protected chat area
- Backend integration for auth and RAG query endpoints

## Project Structure

```text
src/
	app/
		app.config.ts
		app.routes.ts
		app.ts
		core/
			guards/
				auth-guard.ts
			interceptors/
				auth-interceptor.ts
			services/
				auth.ts
				chat.ts
				rag.ts
		features/
			auth/
				login/
					login.ts
					login.html
					login.scss
				register/
					register.ts
					register.html
					register.scss
			chat/
				chat/
					chat.ts
					chat.html
					chat.scss
	environments/
		environment.ts
		environment.development.ts
	styles.scss
```

## Architecture Overview

### Application Composition

- Standalone Angular components (no NgModule-based feature modules)
- Route-based lazy loading for feature pages
- Central app-level providers configured in `app.config.ts`

### Routing and Access Control

- `app.routes.ts` defines routes for:
	- `/login`
	- `/register`
	- `/chat`
- `authGuard` protects `/chat` and redirects unauthenticated users to `/login`

### Core Services

- `AuthService`
	- Handles register/login requests
	- Integrates Google sign-in via `@abacritt/angularx-social-login`
	- Persists user token in local storage
- `RagService`
	- Sends user questions to backend RAG endpoint
	- Receives answer and source citations
- `ChatService`
	- Maintains in-memory chat history as reactive state

### HTTP Pipeline

- `authInterceptor` appends `Authorization: Bearer <token>` to outgoing API calls when token exists
- HTTP client providers are registered globally in `app.config.ts`

### UI Layer

- Angular Material-based UI
- Auth screens:
	- Email/password forms
	- Google sign-in button
- Chat screen:
	- User/assistant message bubbles
	- Source links for RAG responses
	- Auto-scroll and "thinking" state

## Integration Contracts (Frontend Expectations)

- `POST /api/auth/register` -> returns user payload with token
- `POST /api/auth/login` -> returns user payload with token
- `POST /api/rag/query` -> returns:
	- `answer: string`
	- `sources: { title: string; url: string }[]`

## Configuration

- Update Google OAuth client ID in `src/app/app.config.ts`
- Update API base URL in:
	- `src/environments/environment.development.ts`
	- `src/environments/environment.ts`

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
