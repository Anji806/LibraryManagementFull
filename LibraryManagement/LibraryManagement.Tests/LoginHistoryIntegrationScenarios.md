# Login history integration scenarios

Run the API against an isolated SQL Server database after applying `Database/LoginHistory.sql`.

1. Submit valid credentials to `POST /api/auth/login`. Verify the existing response shape and a `Success` row with the authenticated `UserId`, UTC timestamp, and request metadata.
2. Submit an unknown email and a known user's incorrect password. Verify the existing login error response and `Failed` rows; unknown users have `UserId = NULL`, known users retain their backend-resolved ID, and reasons are generic.
3. Attempt login for an inactive user. Verify the existing inactive-account error and a failed row tied to that user.
4. Make the `LoginHistory` table unavailable in an isolated environment. Verify successful and failed authentication responses remain governed by the existing auth checks, and the server logs an audit write error without including credentials.
5. Call `GET /api/login-history` without a token, as a Member, as a Librarian, and as an Admin. Expect 401, 403, 403, and 200 respectively.
6. As Admin, test username/status/date filters, invalid page size/status/date range rejection, both sort directions, stable pagination, and empty results.
7. Verify the admin API exposes no password, token, or secret fields and has no update/delete endpoint. Test a configured trusted proxy and an untrusted forwarded header; only the trusted proxy's client address should be used.
