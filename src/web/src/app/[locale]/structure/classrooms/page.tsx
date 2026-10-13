"use client";

import { useCallback, useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { Classroom } from "@/lib/structure-types";
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

const classroomSchema = z.object({
  name: z.string().min(1).max(50),
  capacity: z.number().int().min(0),
  isActive: z.boolean()
});

type ClassroomFormValues = z.infer<typeof classroomSchema>;

export default function ClassroomsPage() {
  const t = useTranslations("structure");
  const [items, setItems] = useState<Classroom[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Classroom | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      setItems(await apiJson<Classroom[]>("/api/classrooms"));
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
        title={t("classrooms.title")}
        description={t("classrooms.description")}
        actions={
          <Button
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
          >
            {t("classrooms.create")}
          </Button>
        }
      />

      {loadError ? <ErrorBanner message={loadError} /> : null}

      {items === null ? (
        <LoadingBlock message={t("common.loading")} />
      ) : items.length === 0 ? (
        <EmptyState message={t("classrooms.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("classrooms.name")}</th>
                <th className="px-4 py-3 text-start">{t("classrooms.capacity")}</th>
                <th className="px-4 py-3 text-start">{t("classrooms.status")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {items.map((room) => (
                <tr key={room.id}>
                  <td className="px-4 py-3 font-medium">{room.name}</td>
                  <td className="px-4 py-3 text-slate-600">{room.capacity}</td>
                  <td className="px-4 py-3">
                    <StatusBadge
                      tone={room.isActive ? "active" : "muted"}
                      label={room.isActive ? t("common.active") : t("common.inactive")}
                    />
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end">
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setEditing(room);
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

      <ClassroomFormModal
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

function ClassroomFormModal({
  open,
  editing,
  onClose,
  onSaved
}: {
  open: boolean;
  editing: Classroom | null;
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
  } = useForm<ClassroomFormValues>({
    resolver: zodResolver(classroomSchema),
    defaultValues: { name: "", capacity: 0, isActive: true }
  });

  useEffect(() => {
    if (open) {
      reset(
        editing
          ? { name: editing.name, capacity: editing.capacity, isActive: editing.isActive }
          : { name: "", capacity: 0, isActive: true }
      );
      setServerError(null);
    }
  }, [open, editing, reset]);

  const onSubmit = async (values: ClassroomFormValues) => {
    setServerError(null);
    try {
      if (editing) {
        await apiJson(`/api/classrooms/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify({ id: editing.id, ...values })
        });
      } else {
        await apiJson("/api/classrooms", { method: "POST", body: JSON.stringify(values) });
      }
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  return (
    <Modal
      open={open}
      title={editing ? t("classrooms.editTitle") : t("classrooms.createTitle")}
      onClose={onClose}
    >
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <TextField
          label={t("classrooms.name")}
          error={errors.name ? t("common.required") : undefined}
          {...register("name")}
        />
        <TextField
          type="number"
          label={t("classrooms.capacity")}
          error={errors.capacity ? t("common.invalidNumber") : undefined}
          {...register("capacity", { valueAsNumber: true })}
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
