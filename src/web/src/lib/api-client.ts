export interface MeResponse {
  id: string;
  email: string;
  name: string;
  roles: string[];
  permissions: string[];
}

let accessToken: string | null = null;
let refreshInFlight: Promise<string | null> | null = null;

export function setAccessToken(token: string | null): void {
  accessToken = token;
}

export function getAccessToken(): string | null {
  return accessToken;
}

async function refreshAccessToken(): Promise<string | null> {
  refreshInFlight ??= (async () => {
    try {
      const response = await fetch("/api/auth/refresh", {
        method: "POST",
        credentials: "include"
      });
      if (!response.ok) {
        accessToken = null;
        return null;
      }

      const data = (await response.json()) as { accessToken: string };
      accessToken = data.accessToken;
      return data.accessToken;
    } catch {
      accessToken = null;
      return null;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

export async function apiFetch(
  input: string,
  init: RequestInit = {},
  allowRetry = true
): Promise<Response> {
  const headers = new Headers(init.headers);
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  const response = await fetch(input, {
    ...init,
    headers,
    credentials: "include"
  });

  if (response.status === 401 && allowRetry) {
    const newToken = await refreshAccessToken();
    if (newToken) {
      return apiFetch(input, init, false);
    }
  }

  return response;
}

export type LoginResult = { ok: true } | { ok: false; error: string };

export async function login(emailOrUserName: string, password: string): Promise<LoginResult> {
  const response = await fetch("/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ emailOrUserName, password }),
    credentials: "include"
  });

  if (response.ok) {
    const data = (await response.json()) as { accessToken: string };
    accessToken = data.accessToken;
    return { ok: true };
  }

  let code = "unknown";
  try {
    const problem = (await response.json()) as { code?: string };
    if (problem.code) {
      code = problem.code;
    }
  } catch {
    // Response was not JSON; keep the generic code.
  }

  return { ok: false, error: code };
}

export async function logout(): Promise<void> {
  try {
    await fetch("/api/auth/logout", { method: "POST", credentials: "include" });
  } finally {
    accessToken = null;
  }
}
