"use client";

import { useCallback, useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { Subject } from "@/lib/structure-types";
import {
  Button,
  CheckboxField,
  EmptyState,
  ErrorBanner,
  LoadingBlock,
  Modal,
  PageHeader,
  StatusBadge,
  TextField
} from "@/components/ui";

const subjectSchema = z.object({
  code: z.string().min(1).max(20),
  nameEn: z.string().min(1).max(150),
  nameAr: z.string().min(1).max(150),
  isActive: z.boolean()
});

type SubjectFormValues = z.infer<typeof subjectSchema>;

export default function SubjectsPage() {
  const t = useTranslations("structure");
  const [items, setItems] = useState<Subject[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Subject | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      setItems(await apiJson<Subject[]>("/api/subjects"));
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
        title={t("subjects.title")}
        description={t("subjects.description")}
        actions={
          <Button
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
          >
            {t("subjects.create")}
          </Button>
        }
      />

      {loadError ? <ErrorBanner message={loadError} /> : null}

      {items === null ? (
        <LoadingBlock message={t("common.loading")} />
      ) : items.length === 0 ? (
        <EmptyState message={t("subjects.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("subjects.code")}</th>
                <th className="px-4 py-3 text-start">{t("subjects.nameEn")}</th>
                <th className="px-4 py-3 text-start">{t("subjects.nameAr")}</th>
                <th className="px-4 py-3 text-start">{t("subjects.status")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {items.map((subject) => (
                <tr key={subject.id}>
                  <td className="px-4 py-3 font-mono text-xs text-slate-600">{subject.code}</td>
                  <td className="px-4 py-3 font-medium">{subject.nameEn}</td>
                  <td className="px-4 py-3" dir="rtl">
                    {subject.nameAr}
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge
                      tone={subject.isActive ? "active" : "muted"}
                      label={subject.isActive ? t("common.active") : t("common.inactive")}
                    />
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end">
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setEditing(subject);
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

      <SubjectFormModal
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

function SubjectFormModal({
  open,
  editing,
  onClose,
  onSaved
}: {
  open: boolean;
  editing: Subject | null;
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
  } = useForm<SubjectFormValues>({
    resolver: zodResolver(subjectSchema),
    defaultValues: { code: "", nameEn: "", nameAr: "", isActive: true }
  });

  useEffect(() => {
    if (open) {
      reset(
        editing
          ? {
              code: editing.code,
              nameEn: editing.nameEn,
              nameAr: editing.nameAr,
              isActive: editing.isActive
            }
          : { code: "", nameEn: "", nameAr: "", isActive: true }
      );
      setServerError(null);
    }
  }, [open, editing, reset]);

  const onSubmit = async (values: SubjectFormValues) => {
    setServerError(null);
    try {
      if (editing) {
        await apiJson(`/api/subjects/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify({ id: editing.id, ...values })
        });
      } else {
        await apiJson("/api/subjects", { method: "POST", body: JSON.stringify(values) });
      }
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  return (
    <Modal
      open={open}
      title={editing ? t("subjects.editTitle") : t("subjects.createTitle")}
      onClose={onClose}
    >
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <TextField
          label={t("subjects.code")}
          error={errors.code ? t("common.required") : undefined}
          {...register("code")}
        />
        <TextField
          label={t("subjects.nameEn")}
          error={errors.nameEn ? t("common.required") : undefined}
          {...register("nameEn")}
        />
        <TextField
          label={t("subjects.nameAr")}
          error={errors.nameAr ? t("common.required") : undefined}
          {...register("nameAr")}
        />
        <CheckboxField label={t("common.active")} {...register("isActive")} />
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
