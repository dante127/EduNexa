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

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly errors?: Record<string, string[]>
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * Fetch + parse JSON with ProblemDetails-aware errors (`code`, `errors`).
 * Automatically attaches the access token and retries once after a silent refresh.
 */
export async function apiJson<T>(input: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await apiFetch(input, { ...init, headers });

  if (!response.ok) {
    type ProblemPayload = {
      code?: string;
      title?: string;
      errors?: Record<string, string[]>;
    };

    let problem: ProblemPayload | null = null;

    try {
      problem = (await response.json()) as ProblemPayload;
    } catch {
      problem = null;
    }

    throw new ApiError(
      response.status,
      problem?.code ?? "unknown",
      problem?.title ?? `Request failed with status ${response.status}.`,
      problem?.errors
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

type Translator = ((key: string) => string) & { has?: (key: string) => boolean };

/** Maps an ApiError to a localized message when a translation for its code exists. */
export function formatApiError(t: Translator, error: unknown): string {
  if (error instanceof ApiError) {
    const key = `errors.${error.code}`;
    if (t.has?.(key)) {
      return t(key);
    }

    return error.status >= 500 ? t("errors.serverError") : t("errors.generic");
  }

  return t("errors.generic");
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
