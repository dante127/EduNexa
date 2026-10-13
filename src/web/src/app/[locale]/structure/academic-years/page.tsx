"use client";

import { useCallback, useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Link } from "@/i18n/navigation";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { AcademicYear } from "@/lib/structure-types";
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

const yearSchema = z
  .object({
    name: z.string().min(1).max(20),
    startDate: z.string().min(1),
    endDate: z.string().min(1)
  })
  .refine((values) => values.endDate > values.startDate, { path: ["endDate"] });

type YearFormValues = z.infer<typeof yearSchema>;

export default function AcademicYearsPage() {
  const t = useTranslations("structure");
  const [years, setYears] = useState<AcademicYear[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<AcademicYear | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [confirmAction, setConfirmAction] = useState<{ year: AcademicYear; action: "activate" | "close" } | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      setYears(await apiJson<AcademicYear[]>("/api/academic-years"));
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const runStatusAction = async () => {
    if (!confirmAction) {
      return;
    }

    const { year, action } = confirmAction;
    setBusyId(year.id);
    setActionError(null);
    try {
      await apiJson(`/api/academic-years/${year.id}/${action}`, { method: "POST" });
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
      <PageHeader
        title={t("years.title")}
        description={t("years.description")}
        actions={
          <Button
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
          >
            {t("years.create")}
          </Button>
        }
      />

      {loadError ? <ErrorBanner message={loadError} /> : null}
      {actionError ? <ErrorBanner message={actionError} /> : null}

      {years === null ? (
        <LoadingBlock message={t("common.loading")} />
      ) : years.length === 0 ? (
        <EmptyState message={t("years.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-start text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("years.name")}</th>
                <th className="px-4 py-3 text-start">{t("years.dates")}</th>
                <th className="px-4 py-3 text-start">{t("years.status")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {years.map((year) => (
                <tr key={year.id}>
                  <td className="px-4 py-3 font-medium">
                    <Link href={`/structure/academic-years/${year.id}`} className="hover:underline">
                      {year.name}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-slate-600" dir="ltr">
                    {year.startDate} — {year.endDate}
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge tone={statusTone(year.status)} label={t(`common.statuses.${year.status}`)} />
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-2">
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setEditing(year);
                          setFormOpen(true);
                        }}
                      >
                        {t("common.edit")}
                      </Button>
                      {year.status !== "Active" ? (
                        <Button
                          disabled={busyId === year.id}
                          onClick={() => setConfirmAction({ year, action: "activate" })}
                        >
                          {t("common.activate")}
                        </Button>
                      ) : (
                        <Button
                          variant="danger"
                          disabled={busyId === year.id}
                          onClick={() => setConfirmAction({ year, action: "close" })}
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

      <YearFormModal
        open={formOpen}
        editing={editing}
        onClose={() => setFormOpen(false)}
        onSaved={async () => {
          setFormOpen(false);
          await load();
        }}
      />

      <ConfirmDialog
        open={confirmAction !== null}
        title={confirmAction?.action === "close" ? t("years.closeTitle") : t("years.activateTitle")}
        message={
          confirmAction
            ? t(confirmAction.action === "close" ? "years.closeConfirm" : "years.activateConfirm", {
                name: confirmAction.year.name
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

function YearFormModal({
  open,
  editing,
  onClose,
  onSaved
}: {
  open: boolean;
  editing: AcademicYear | null;
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
  } = useForm<YearFormValues>({
    resolver: zodResolver(yearSchema),
    defaultValues: { name: "", startDate: "", endDate: "" }
  });

  useEffect(() => {
    if (open) {
      reset(
        editing
          ? { name: editing.name, startDate: editing.startDate, endDate: editing.endDate }
          : { name: "", startDate: "", endDate: "" }
      );
      setServerError(null);
    }
  }, [open, editing, reset]);

  const onSubmit = async (values: YearFormValues) => {
    setServerError(null);
    try {
      if (editing) {
        await apiJson(`/api/academic-years/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify({ id: editing.id, ...values })
        });
      } else {
        await apiJson("/api/academic-years", { method: "POST", body: JSON.stringify(values) });
      }
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  return (
    <Modal open={open} title={editing ? t("years.editTitle") : t("years.createTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <TextField
          label={t("years.name")}
          error={errors.name ? t("common.required") : undefined}
          {...register("name")}
        />
        <div className="grid grid-cols-2 gap-3">
          <TextField
            type="date"
            label={t("years.startDate")}
            error={errors.startDate ? t("common.required") : undefined}
            {...register("startDate")}
          />
          <TextField
            type="date"
            label={t("years.endDate")}
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
