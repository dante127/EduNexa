"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { Paged, StudentSummary } from "@/lib/students-types";
import {
  Button,
  EmptyState,
  ErrorBanner,
  LoadingBlock,
  PageHeader,
  SelectField,
  StatusBadge,
  TextField,
  type StatusTone
} from "@/components/ui";

const statusOptions = ["Applicant", "Active", "Transferred", "Withdrawn", "Graduated"] as const;

function studentTone(status: string): StatusTone {
  if (status === "Active") return "active";
  if (status === "Applicant") return "planned";
  return "muted";
}

export default function StudentsPage() {
  const t = useTranslations("students");
  const [data, setData] = useState<Paged<StudentSummary> | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [loadError, setLoadError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const params = new URLSearchParams();
      if (search) {
        params.set("search", search);
      }
      if (status) {
        params.set("status", status);
      }
      params.set("page", String(page));
      params.set("pageSize", "20");
      setData(await apiJson<Paged<StudentSummary>>(`/api/students?${params.toString()}`));
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t, search, status, page]);

  useEffect(() => {
    void load();
  }, [load]);

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <div>
      <PageHeader
        title={t("list.title")}
        description={t("list.description")}
        actions={
          <Link
            href="/admissions"
            className="rounded-lg bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-700"
          >
            {t("list.newAdmission")}
          </Link>
        }
      />

      <form
        className="mb-4 flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          setPage(1);
          setSearch(searchInput.trim());
        }}
      >
        <div className="min-w-56 flex-1">
          <TextField
            label={t("list.searchPlaceholder")}
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
          />
        </div>
        <SelectField
          label={t("list.filterStatus")}
          value={status}
          onChange={(event) => {
            setStatus(event.target.value);
            setPage(1);
          }}
        >
          <option value="">{t("common.all")}</option>
          {statusOptions.map((option) => (
            <option key={option} value={option}>
              {t(`profile.statuses.${option}`)}
            </option>
          ))}
        </SelectField>
        <Button type="submit" variant="secondary">
          {t("common.search")}
        </Button>
      </form>

      {loadError ? <ErrorBanner message={loadError} /> : null}

      {data === null ? (
        <LoadingBlock message={t("common.loading")} />
      ) : data.items.length === 0 ? (
        <EmptyState message={t("list.empty")} />
      ) : (
        <>
          <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3 text-start">{t("list.colNumber")}</th>
                  <th className="px-4 py-3 text-start">{t("list.colName")}</th>
                  <th className="px-4 py-3 text-start">{t("list.colStatus")}</th>
                  <th className="px-4 py-3 text-end">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.items.map((student) => (
                  <tr key={student.id}>
                    <td className="px-4 py-3 font-mono text-xs">{student.studentNumber}</td>
                    <td className="px-4 py-3">
                      <div className="font-medium">
                        {student.firstNameEn} {student.lastNameEn}
                      </div>
                      <div className="text-xs text-slate-500">
                        {student.firstNameAr} {student.lastNameAr}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge tone={studentTone(student.status)} label={t(`profile.statuses.${student.status}`)} />
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end">
                        <Link
                          href={`/students/${student.id}`}
                          className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-100"
                        >
                          {t("list.view")}
                        </Link>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex flex-wrap items-center justify-between gap-3 text-sm text-slate-600">
            <span>{t("list.total", { total: data.total })}</span>
            <div className="flex items-center gap-2">
              <Button
                variant="secondary"
                disabled={page <= 1}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                {t("list.prev")}
              </Button>
              <span>{t("list.page", { page: data.page, pages })}</span>
              <Button
                variant="secondary"
                disabled={page >= pages}
                onClick={() => setPage((current) => current + 1)}
              >
                {t("list.next")}
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
