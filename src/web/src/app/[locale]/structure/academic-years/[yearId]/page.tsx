"use client";

import { useCallback, useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useParams, useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Link } from "@/i18n/navigation";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { AcademicTerm, AcademicYear } from "@/lib/structure-types";
import {
  Button,
  ConfirmDialog,
  EmptyState,
  ErrorBanner,
  LoadingBlock,
  Modal,
  PageHeader,
  StatusBadge,
  TextField,
  statusTone
} from "@/components/ui";

const termSchema = z
  .object({
    name: z.string().min(1).max(50),
    sequence: z.number().int().min(1),
    startDate: z.string().min(1),
    endDate: z.string().min(1)
  })
  .refine((values) => values.endDate > values.startDate, { path: ["endDate"] });

type TermFormValues = z.infer<typeof termSchema>;

export default function AcademicYearDetailPage() {
  const t = useTranslations("structure");
  const params = useParams<{ yearId: string }>();
  const router = useRouter();
  const yearId = params.yearId;

  const [year, setYear] = useState<AcademicYear | null>(null);
  const [terms, setTerms] = useState<AcademicTerm[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<AcademicTerm | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [confirmAction, setConfirmAction] = useState<{ term: AcademicTerm; action: "activate" | "close" } | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const [yearData, termData] = await Promise.all([
        apiJson<AcademicYear>(`/api/academic-years/${yearId}`),
        apiJson<AcademicTerm[]>(`/api/academic-years/${yearId}/terms`)
      ]);
      setYear(yearData);
      setTerms(termData);
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t, yearId]);

  useEffect(() => {
    void load();
  }, [load]);

  const runStatusAction = async () => {
    if (!confirmAction) {
      return;
    }

    const { term, action } = confirmAction;
    setBusyId(term.id);
    setActionError(null);
    try {
      await apiJson(`/api/terms/${term.id}/${action}`, { method: "POST" });
      await load();
    } catch (error) {
      setActionError(formatApiError(t, error));
    } finally {
      setBusyId(null);
      setConfirmAction(null);
    }
  };

  return (
    <div>
      {year ? (
        <PageHeader
          title={`${t("terms.title")} · ${year.name}`}
          description={`${year.startDate} — ${year.endDate}`}
          actions={
            <Button
              onClick={() => {
                setEditing(null);
                setFormOpen(true);
              }}
            >
              {t("terms.create")}
            </Button>
          }
        />
      ) : (
        <PageHeader title={t("terms.title")} />
      )}

      <div className="mb-4 flex items-center gap-3 text-sm">
        <Link href="/structure/academic-years" className="text-slate-500 hover:text-slate-900">
          ← {t("years.title")}
        </Link>
        {year ? (
          <span className="text-slate-400">
            {t("years.status")}:{" "}
            <StatusBadge tone={statusTone(year.status)} label={t(`common.statuses.${year.status}`)} />
          </span>
        ) : null}
        <Button
          variant="secondary"
          className="ms-auto"
          onClick={() => router.refresh()}
        >
          {t("common.refresh")}
        </Button>
      </div>

      {loadError ? <ErrorBanner message={loadError} /> : null}
      {actionError ? <ErrorBanner message={actionError} /> : null}

      {terms === null ? (
        <LoadingBlock message={t("common.loading")} />
      ) : terms.length === 0 ? (
        <EmptyState message={t("terms.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("terms.sequenceShort")}</th>
                <th className="px-4 py-3 text-start">{t("terms.name")}</th>
                <th className="px-4 py-3 text-start">{t("terms.dates")}</th>
                <th className="px-4 py-3 text-start">{t("terms.status")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {terms.map((term) => (
                <tr key={term.id}>
                  <td className="px-4 py-3 text-slate-600">{term.sequence}</td>
                  <td className="px-4 py-3 font-medium">{term.name}</td>
                  <td className="px-4 py-3 text-slate-600" dir="ltr">
                    {term.startDate} — {term.endDate}
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge tone={statusTone(term.status)} label={t(`common.statuses.${term.status}`)} />
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-2">
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setEditing(term);
                          setFormOpen(true);
                        }}
                      >
                        {t("common.edit")}
                      </Button>
                      {term.status !== "Active" ? (
                        <Button
                          disabled={busyId === term.id}
                          onClick={() => setConfirmAction({ term, action: "activate" })}
                        >
                          {t("common.activate")}
                        </Button>
                      ) : (
                        <Button
                          variant="danger"
                          disabled={busyId === term.id}
                          onClick={() => setConfirmAction({ term, action: "close" })}
                        >
                          {t("common.close")}
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <TermFormModal
        open={formOpen}
        yearId={yearId}
        editing={editing}
        onClose={() => setFormOpen(false)}
        onSaved={async () => {
          setFormOpen(false);
          await load();
        }}
      />

      <ConfirmDialog
        open={confirmAction !== null}
        title={confirmAction?.action === "close" ? t("terms.closeTitle") : t("terms.activateTitle")}
        message={
          confirmAction
            ? t(confirmAction.action === "close" ? "terms.closeConfirm" : "terms.activateConfirm", {
                name: confirmAction.term.name
              })
            : ""
        }
        confirmLabel={confirmAction?.action === "close" ? t("common.close") : t("common.activate")}
        cancelLabel={t("common.cancel")}
        danger={confirmAction?.action === "close"}
        onConfirm={() => void runStatusAction()}
        onCancel={() => setConfirmAction(null)}
      />
    </div>
  );
}

function TermFormModal({
  open,
  yearId,
  editing,
  onClose,
  onSaved
}: {
  open: boolean;
  yearId: string;
  editing: AcademicTerm | null;
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("structure");
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<TermFormValues>({
    resolver: zodResolver(termSchema),
    defaultValues: { name: "", sequence: 1, startDate: "", endDate: "" }
  });

  useEffect(() => {
    if (open) {
      reset(
        editing
          ? {
              name: editing.name,
              sequence: editing.sequence,
              startDate: editing.startDate,
              endDate: editing.endDate
            }
          : { name: "", sequence: 1, startDate: "", endDate: "" }
      );
      setServerError(null);
    }
  }, [open, editing, reset]);

  const onSubmit = async (values: TermFormValues) => {
    setServerError(null);
    try {
      if (editing) {
        await apiJson(`/api/terms/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify(values)
        });
      } else {
        await apiJson(`/api/academic-years/${yearId}/terms`, {
          method: "POST",
          body: JSON.stringify(values)
        });
      }
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  return (
    <Modal open={open} title={editing ? t("terms.editTitle") : t("terms.createTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <TextField
          label={t("terms.name")}
          error={errors.name ? t("common.required") : undefined}
          {...register("name")}
        />
        <TextField
          type="number"
          label={t("terms.sequence")}
          error={errors.sequence ? t("common.invalidNumber") : undefined}
          {...register("sequence", { valueAsNumber: true })}
        />
        <div className="grid grid-cols-2 gap-3">
          <TextField
            type="date"
            label={t("terms.startDate")}
            error={errors.startDate ? t("common.required") : undefined}
            {...register("startDate")}
          />
          <TextField
            type="date"
            label={t("terms.endDate")}
            error={errors.endDate ? t("common.invalidDateRange") : undefined}
            {...register("endDate")}
          />
        </div>
        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
