"use client";

import { useCallback, useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { GradeLevel } from "@/lib/structure-types";
import {
  Button,
  EmptyState,
  ErrorBanner,
  LoadingBlock,
  Modal,
  PageHeader,
  TextField
} from "@/components/ui";

const gradeSchema = z.object({
  sequence: z.number().int().min(1),
  nameEn: z.string().min(1).max(100),
  nameAr: z.string().min(1).max(100)
});

type GradeFormValues = z.infer<typeof gradeSchema>;

export default function GradeLevelsPage() {
  const t = useTranslations("structure");
  const [items, setItems] = useState<GradeLevel[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<GradeLevel | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      setItems(await apiJson<GradeLevel[]>("/api/grade-levels"));
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div>
      <PageHeader
        title={t("gradeLevels.title")}
        description={t("gradeLevels.description")}
        actions={
          <Button
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
          >
            {t("gradeLevels.create")}
          </Button>
        }
      />

      {loadError ? <ErrorBanner message={loadError} /> : null}

      {items === null ? (
        <LoadingBlock message={t("common.loading")} />
      ) : items.length === 0 ? (
        <EmptyState message={t("gradeLevels.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("gradeLevels.sequence")}</th>
                <th className="px-4 py-3 text-start">{t("gradeLevels.nameEn")}</th>
                <th className="px-4 py-3 text-start">{t("gradeLevels.nameAr")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {items.map((level) => (
                <tr key={level.id}>
                  <td className="px-4 py-3 text-slate-600">{level.sequence}</td>
                  <td className="px-4 py-3 font-medium">{level.nameEn}</td>
                  <td className="px-4 py-3" dir="rtl">
                    {level.nameAr}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end">
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setEditing(level);
                          setFormOpen(true);
                        }}
                      >
                        {t("common.edit")}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <GradeFormModal
        open={formOpen}
        editing={editing}
        onClose={() => setFormOpen(false)}
        onSaved={async () => {
          setFormOpen(false);
          await load();
        }}
      />
    </div>
  );
}

function GradeFormModal({
  open,
  editing,
  onClose,
  onSaved
}: {
  open: boolean;
  editing: GradeLevel | null;
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
  } = useForm<GradeFormValues>({
    resolver: zodResolver(gradeSchema),
    defaultValues: { sequence: 1, nameEn: "", nameAr: "" }
  });

  useEffect(() => {
    if (open) {
      reset(
        editing
          ? { sequence: editing.sequence, nameEn: editing.nameEn, nameAr: editing.nameAr }
          : { sequence: 1, nameEn: "", nameAr: "" }
      );
      setServerError(null);
    }
  }, [open, editing, reset]);

  const onSubmit = async (values: GradeFormValues) => {
    setServerError(null);
    try {
      if (editing) {
        await apiJson(`/api/grade-levels/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify({ id: editing.id, ...values })
        });
      } else {
        await apiJson("/api/grade-levels", { method: "POST", body: JSON.stringify(values) });
      }
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  return (
    <Modal
      open={open}
      title={editing ? t("gradeLevels.editTitle") : t("gradeLevels.createTitle")}
      onClose={onClose}
    >
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <TextField
          type="number"
          label={t("gradeLevels.sequence")}
          error={errors.sequence ? t("common.invalidNumber") : undefined}
          {...register("sequence", { valueAsNumber: true })}
        />
        <TextField
          label={t("gradeLevels.nameEn")}
          error={errors.nameEn ? t("common.required") : undefined}
          {...register("nameEn")}
        />
        <TextField
          label={t("gradeLevels.nameAr")}
          error={errors.nameAr ? t("common.required") : undefined}
          {...register("nameAr")}
        />
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
