"use client";

import { useCallback, useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { AcademicYear, ClassSection, GradeLevel } from "@/lib/structure-types";
import {
  Button,
  EmptyState,
  ErrorBanner,
  LoadingBlock,
  Modal,
  PageHeader,
  SelectField,
  TextField
} from "@/components/ui";

const sectionSchema = z.object({
  academicYearId: z.string().min(1),
  gradeLevelId: z.string().min(1),
  name: z.string().min(1).max(20),
  capacity: z.number().int().min(1)
});

type SectionFormValues = z.infer<typeof sectionSchema>;

export default function SectionsPage() {
  const t = useTranslations("structure");
  const [sections, setSections] = useState<ClassSection[] | null>(null);
  const [years, setYears] = useState<AcademicYear[]>([]);
  const [gradeLevels, setGradeLevels] = useState<GradeLevel[]>([]);
  const [yearFilter, setYearFilter] = useState("");
  const [gradeFilter, setGradeFilter] = useState("");
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<ClassSection | null>(null);

  const loadReferenceData = useCallback(async () => {
    try {
      const [yearData, gradeData] = await Promise.all([
        apiJson<AcademicYear[]>("/api/academic-years"),
        apiJson<GradeLevel[]>("/api/grade-levels")
      ]);
      setYears(yearData);
      setGradeLevels(gradeData);
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t]);

  const loadSections = useCallback(async () => {
    setLoadError(null);
    try {
      const params = new URLSearchParams();
      if (yearFilter) params.set("academicYearId", yearFilter);
      if (gradeFilter) params.set("gradeLevelId", gradeFilter);
      const query = params.toString();
      setSections(await apiJson<ClassSection[]>(`/api/sections${query ? `?${query}` : ""}`));
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t, yearFilter, gradeFilter]);

  useEffect(() => {
    void loadReferenceData();
  }, [loadReferenceData]);

  useEffect(() => {
    void loadSections();
  }, [loadSections]);

  const gradeName = (id: string) => {
    const level = gradeLevels.find((item) => item.id === id);
    return level ? `${level.nameEn} / ${level.nameAr}` : id;
  };

  const yearName = (id: string) => years.find((item) => item.id === id)?.name ?? id;

  return (
    <div>
      <PageHeader
        title={t("sections.title")}
        description={t("sections.description")}
        actions={
          <Button
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
            disabled={years.length === 0 || gradeLevels.length === 0}
          >
            {t("sections.create")}
          </Button>
        }
      />

      <div className="mb-4 flex flex-wrap gap-3">
        <SelectField label={t("sections.filterYear")} value={yearFilter} onChange={(event) => setYearFilter(event.target.value)}>
          <option value="">{t("common.all")}</option>
          {years.map((year) => (
            <option key={year.id} value={year.id}>
              {year.name}
            </option>
          ))}
        </SelectField>
        <SelectField
          label={t("sections.filterGrade")}
          value={gradeFilter}
          onChange={(event) => setGradeFilter(event.target.value)}
        >
          <option value="">{t("common.all")}</option>
          {gradeLevels.map((level) => (
            <option key={level.id} value={level.id}>
              {level.nameEn}
            </option>
          ))}
        </SelectField>
      </div>

      {loadError ? <ErrorBanner message={loadError} /> : null}

      {sections === null ? (
        <LoadingBlock message={t("common.loading")} />
      ) : sections.length === 0 ? (
        <EmptyState message={t("sections.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("sections.name")}</th>
                <th className="px-4 py-3 text-start">{t("sections.gradeLevel")}</th>
                <th className="px-4 py-3 text-start">{t("sections.year")}</th>
                <th className="px-4 py-3 text-start">{t("sections.capacity")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {sections.map((section) => (
                <tr key={section.id}>
                  <td className="px-4 py-3 font-medium">{section.name}</td>
                  <td className="px-4 py-3 text-slate-600">{gradeName(section.gradeLevelId)}</td>
                  <td className="px-4 py-3 text-slate-600">{yearName(section.academicYearId)}</td>
                  <td className="px-4 py-3 text-slate-600">{section.capacity}</td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end">
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setEditing(section);
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

      <SectionFormModal
        open={formOpen}
        editing={editing}
        years={years}
        gradeLevels={gradeLevels}
        onClose={() => setFormOpen(false)}
        onSaved={async () => {
          setFormOpen(false);
          await loadSections();
        }}
      />
    </div>
  );
}

function SectionFormModal({
  open,
  editing,
  years,
  gradeLevels,
  onClose,
  onSaved
}: {
  open: boolean;
  editing: ClassSection | null;
  years: AcademicYear[];
  gradeLevels: GradeLevel[];
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
  } = useForm<SectionFormValues>({
    resolver: zodResolver(sectionSchema),
    defaultValues: { academicYearId: "", gradeLevelId: "", name: "", capacity: 25 }
  });

  useEffect(() => {
    if (open) {
      reset(
        editing
          ? {
              academicYearId: editing.academicYearId,
              gradeLevelId: editing.gradeLevelId,
              name: editing.name,
              capacity: editing.capacity
            }
          : { academicYearId: "", gradeLevelId: "", name: "", capacity: 25 }
      );
      setServerError(null);
    }
  }, [open, editing, reset]);

  const onSubmit = async (values: SectionFormValues) => {
    setServerError(null);
    try {
      if (editing) {
        await apiJson(`/api/sections/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify({ id: editing.id, name: values.name, capacity: values.capacity })
        });
      } else {
        await apiJson("/api/sections", { method: "POST", body: JSON.stringify(values) });
      }
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  return (
    <Modal
      open={open}
      title={editing ? t("sections.editTitle") : t("sections.createTitle")}
      onClose={onClose}
    >
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        {!editing ? (
          <>
            <SelectField
              label={t("sections.year")}
              error={errors.academicYearId ? t("common.required") : undefined}
              {...register("academicYearId")}
            >
              <option value="">{t("common.select")}</option>
              {years.map((year) => (
                <option key={year.id} value={year.id}>
                  {year.name}
                </option>
              ))}
            </SelectField>
            <SelectField
              label={t("sections.gradeLevel")}
              error={errors.gradeLevelId ? t("common.required") : undefined}
              {...register("gradeLevelId")}
            >
              <option value="">{t("common.select")}</option>
              {gradeLevels.map((level) => (
                <option key={level.id} value={level.id}>
                  {level.nameEn}
                </option>
              ))}
            </SelectField>
          </>
        ) : null}
        <TextField
          label={t("sections.name")}
          error={errors.name ? t("common.required") : undefined}
          {...register("name")}
        />
        <TextField
          type="number"
          label={t("sections.capacity")}
          error={errors.capacity ? t("common.invalidNumber") : undefined}
          {...register("capacity", { valueAsNumber: true })}
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
