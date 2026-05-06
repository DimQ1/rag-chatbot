# Authentication Flow

The API supports two sign-in modes:

- Email and password through /api/auth/register and /api/auth/login
- Google ID token exchange through /api/auth/google

JWT token contains user id, email, name, and role claims.
The frontend stores the token and sends it in Authorization headers.

The RAG endpoint requires authentication with [Authorize].
Without a valid token, requests to /api/rag/query are rejected.
