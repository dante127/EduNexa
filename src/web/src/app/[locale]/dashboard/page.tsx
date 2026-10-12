"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { apiFetch, logout, type MeResponse } from "@/lib/api-client";

export default function DashboardPage() {
  const t = useTranslations("dashboard");
  const router = useRouter();
  const [status, setStatus] = useState<"loading" | "ready">("loading");
  const [me, setMe] = useState<MeResponse | null>(null);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      const response = await apiFetch("/api/auth/me");
      if (cancelled) {
        return;
      }

      if (!response.ok) {
        router.replace("/login");
        return;
      }

      setMe((await response.json()) as MeResponse);
      setStatus("ready");
    })();

    return () => {
      cancelled = true;
    };
  }, [router]);

  const onLogout = async () => {
    await logout();
    router.replace("/login");
  };

  if (status !== "ready" || !me) {
    return (
      <main className="flex min-h-screen items-center justify-center text-slate-500">
        {t("loading")}
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-2xl px-4 py-12">
      <div className="rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-semibold">{t("title")}</h1>
          <button
            onClick={onLogout}
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-100"
          >
            {t("logout")}
          </button>
        </div>

        <p className="mt-4 text-slate-600">
          {t("welcome")}, <span className="font-medium">{me.email}</span>
        </p>

        <dl className="mt-6 grid grid-cols-2 gap-4 text-sm">
          <div>
            <dt className="text-slate-500">{t("roles")}</dt>
            <dd className="mt-1 font-medium">{me.roles.join(", ") || "—"}</dd>
          </div>
          <div>
            <dt className="text-slate-500">{t("permissions")}</dt>
            <dd className="mt-1 font-medium">{me.permissions.length}</dd>
          </div>
        </dl>
      </div>
    </main>
  );
}
