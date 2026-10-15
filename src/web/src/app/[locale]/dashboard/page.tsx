"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { apiFetch, logout, type MeResponse } from "@/lib/api-client";

export default function DashboardPage() {
  const t = useTranslations("dashboard");
  const tStructure = useTranslations("structure");
  const tStudents = useTranslations("students");
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
    <main className="mx-auto max-w-3xl px-4 py-12">
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

      <section className="mt-8">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
          {t("structureSection")}
        </h2>
        <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3">
          <Link
            href="/structure/academic-years"
            className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-800 hover:border-slate-400"
          >
            {tStructure("nav.academicYears")}
          </Link>
          <Link
            href="/structure/grade-levels"
            className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-800 hover:border-slate-400"
          >
            {tStructure("nav.gradeLevels")}
          </Link>
          <Link
            href="/structure/subjects"
            className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-800 hover:border-slate-400"
          >
            {tStructure("nav.subjects")}
          </Link>
          <Link
            href="/structure/classrooms"
            className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-800 hover:border-slate-400"
          >
            {tStructure("nav.classrooms")}
          </Link>
          <Link
            href="/structure/sections"
            className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-800 hover:border-slate-400"
          >
            {tStructure("nav.sections")}
          </Link>
          <Link
            href="/structure/settings"
            className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-800 hover:border-slate-400"
          >
            {tStructure("nav.settings")}
          </Link>
        </div>
      </section>

      <section className="mt-8">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
          {t("studentsSection")}
        </h2>
        <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3">
          <Link
            href="/students"
            className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-800 hover:border-slate-400"
          >
            {tStudents("nav.students")}
          </Link>
          <Link
            href="/admissions"
            className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-800 hover:border-slate-400"
          >
            {tStudents("nav.admissions")}
          </Link>
        </div>
      </section>
    </main>
  );
}
