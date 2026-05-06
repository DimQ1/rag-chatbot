# Contributing

## Workflow

- Keep changes focused and small.
- Match the existing project structure and naming conventions.
- Avoid unrelated refactors in feature or bug-fix pull requests.

## Local Setup

### Frontend

```powershell
cd rag-chatbot
npm install
npm start
```

### Backend

```powershell
cd rag-chatbot-api
dotnet build
dotnet watch run --launch-profile http
```

## Before Opening a Pull Request

- Run `npm test` in `rag-chatbot/` when frontend code changes.
- Run `dotnet build` in `rag-chatbot-api/` when backend code changes.
- Do not commit real secrets, local database files, or IDE-specific files.
- Update documentation when setup, behavior, or public APIs change.

## Coding Notes

- Angular code in this repo uses standalone components.
- Backend changes should follow the existing controller/DTO/service structure.
- Error payloads in the API should keep the current `{ message = "..." }` pattern.
