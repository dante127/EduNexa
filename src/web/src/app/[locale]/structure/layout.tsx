"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { apiFetch } from "@/lib/api-client";

const navItems = [
  { href: "/structure/academic-years", key: "academicYears" },
  { href: "/structure/grade-levels", key: "gradeLevels" },
  { href: "/structure/subjects", key: "subjects" },
  { href: "/structure/classrooms", key: "classrooms" },
  { href: "/structure/sections", key: "sections" },
  { href: "/structure/settings", key: "settings" }
] as const;

export default function StructureLayout({ children }: { children: React.ReactNode }) {
  const t = useTranslations("structure");
  const router = useRouter();
  const [state, setState] = useState<"loading" | "ready">("loading");

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

      setState("ready");
    })();

    return () => {
      cancelled = true;
    };
  }, [router]);

  if (state !== "ready") {
    return (
      <main className="flex min-h-screen items-center justify-center text-slate-500">
        {t("common.loading")}
      </main>
    );
  }

  return (
    <div className="mx-auto flex max-w-7xl flex-wrap gap-8 px-4 py-8">
      <aside className="w-56 shrink-0">
        <Link href="/dashboard" className="text-sm text-slate-500 hover:text-slate-900">
          ← {t("nav.dashboard")}
        </Link>
        <h1 className="mt-4 text-lg font-semibold text-slate-900">{t("nav.title")}</h1>
        <nav className="mt-4 space-y-1">
          {navItems.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="block rounded-lg px-3 py-2 text-sm text-slate-700 hover:bg-slate-100"
            >
              {t(`nav.${item.key}`)}
            </Link>
          ))}
        </nav>
      </aside>
      <section className="min-w-0 flex-1">{children}</section>
    </div>
  );
}
