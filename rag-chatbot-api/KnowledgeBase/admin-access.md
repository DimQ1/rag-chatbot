# Admin Access

Admin endpoint: GET /api/admin/users

Role rules:

- Default role is User
- Admin-only endpoints require role claim Admin

User records are stored in SQLite through Entity Framework Core.
Email is unique and normalized to lower case.
