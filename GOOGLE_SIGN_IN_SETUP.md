# Google sign-in setup

Google sign-in is already wired into the login page and API. It becomes usable
after a Google OAuth 2.0 **Web application** client ID is configured.

## Create a client ID

In Google Cloud Console, configure the OAuth consent screen, then create a Web
application OAuth client. Add each UI origin under **Authorized JavaScript
origins**. For local development, add:

```text
http://localhost:5173
```

Use the production UI origin as well when deploying. No redirect URI is needed
for this Google Identity Services button flow.

## Configure the UI

Create `library-management-ui/.env.local` (or edit `.env`) and set:

```env
VITE_GOOGLE_CLIENT_ID=your-client-id.apps.googleusercontent.com
```

Restart the Vite dev server after changing the environment file.

## Configure the API

From the repository root, set the same client ID using .NET user secrets for
local development:

```powershell
dotnet user-secrets set "GoogleAuth:ClientId" "your-client-id.apps.googleusercontent.com" --project LibraryManagement/LibraryManagement
```

Restart the API. In a deployment, set the environment variable
`GoogleAuth__ClientId` to the same value.

The OAuth client ID is public and can be used in the browser. Do not put a
Google client secret in the frontend. The existing email and password sign-in
continues to work without Google configuration.
