"use client";

import { useCallback, useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Link } from "@/i18n/navigation";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { AdmissionApplication } from "@/lib/students-types";
import type { AcademicYear, GradeLevel } from "@/lib/structure-types";
import {
  Button,
  EmptyState,
  ErrorBanner,
  LoadingBlock,
  Modal,
  PageHeader,
  SelectField,
  StatusBadge,
  TextField,
  type StatusTone
} from "@/components/ui";

const statusOptions = ["Submitted", "UnderReview", "Approved", "Rejected", "Enrolled"] as const;

function admissionTone(status: string): StatusTone {
  if (status === "Approved" || status === "Enrolled") return "active";
  if (status === "Rejected") return "danger";
  if (status === "UnderReview") return "planned";
  return "muted";
}

const applicationSchema = z.object({
  applicantFirstNameEn: z.string().min(1).max(100),
  applicantLastNameEn: z.string().min(1).max(100),
  applicantFirstNameAr: z.string().min(1).max(100),
  applicantLastNameAr: z.string().min(1).max(100),
  dateOfBirth: z.string().min(1),
  gender: z.string().min(1),
  nationality: z.string().max(100),
  academicYearId: z.string().min(1),
  desiredGradeLevelId: z.string().min(1),
  guardianName: z.string().min(1).max(150),
  guardianPhone: z.string().max(30),
  guardianEmail: z.union([z.literal(""), z.string().email().max(200)])
});

type ApplicationFormValues = z.infer<typeof applicationSchema>;

export default function AdmissionsPage() {
  const t = useTranslations("admissions");
  const [applications, setApplications] = useState<AdmissionApplication[] | null>(null);
  const [years, setYears] = useState<AcademicYear[]>([]);
  const [grades, setGrades] = useState<GradeLevel[]>([]);
  const [status, setStatus] = useState("");
  const [loadError, setLoadError] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const query = status ? `?status=${encodeURIComponent(status)}` : "";
      setApplications(await apiJson<AdmissionApplication[]>(`/api/admission-applications${query}`));
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t, status]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const [yearData, gradeData] = await Promise.all([
          apiJson<AcademicYear[]>("/api/academic-years"),
          apiJson<GradeLevel[]>("/api/grade-levels")
        ]);
        if (!cancelled) {
          setYears(yearData);
          setGrades(gradeData);
        }
      } catch {
        // Reference data failure keeps the list readable for the rows that load.
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const yearName = (id: string) => years.find((year) => year.id === id)?.name ?? id;
  const gradeName = (id: string) => {
    const grade = grades.find((level) => level.id === id);
    return grade ? `${grade.nameEn} / ${grade.nameAr}` : id;
  };

  return (
    <div>
      <PageHeader
        title={t("list.title")}
        description={t("list.description")}
        actions={<Button onClick={() => setCreateOpen(true)}>{t("list.create")}</Button>}
      />

      <div className="mb-4 max-w-xs">
        <SelectField label={t("list.filterStatus")} value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">{t("common.all")}</option>
          {statusOptions.map((option) => (
            <option key={option} value={option}>
              {t(`statuses.${option}`)}
            </option>
          ))}
        </SelectField>
      </div>

      {loadError ? <ErrorBanner message={loadError} /> : null}

      {applications === null ? (
        <LoadingBlock message={t("common.loading")} />
      ) : applications.length === 0 ? (
        <EmptyState message={t("list.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("list.colApplicant")}</th>
                <th className="px-4 py-3 text-start">{t("list.colGuardian")}</th>
                <th className="px-4 py-3 text-start">{t("list.colDesired")}</th>
                <th className="px-4 py-3 text-start">{t("list.colStatus")}</th>
                <th className="px-4 py-3 text-start">{t("list.colSubmitted")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {applications.map((application) => (
                <tr key={application.id}>
                  <td className="px-4 py-3">
                    <div className="font-medium">
                      {application.applicantFirstNameEn} {application.applicantLastNameEn}
                    </div>
                    <div className="text-xs text-slate-500">
                      {application.applicantFirstNameAr} {application.applicantLastNameAr}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-slate-600">
                    <div>{application.guardianName}</div>
                    <div className="text-xs text-slate-500">{application.guardianPhone ?? "—"}</div>
                  </td>
                  <td className="px-4 py-3 text-slate-600">
                    <div className="text-xs">{yearName(application.academicYearId)}</div>
                    <div>{gradeName(application.desiredGradeLevelId)}</div>
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge tone={admissionTone(application.status)} label={t(`statuses.${application.status}`)} />
                  </td>
                  <td className="px-4 py-3 text-slate-600">{application.submittedAt.slice(0, 10)}</td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end">
                      <Link
                        href={`/admissions/${application.id}`}
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
      )}

      <CreateApplicationModal
        open={createOpen}
        years={years}
        grades={grades}
        onClose={() => setCreateOpen(false)}
        onSaved={async () => {
          setCreateOpen(false);
          await load();
        }}
      />
    </div>
  );
}

function CreateApplicationModal({
  open,
  years,
  grades,
  onClose,
  onSaved
}: {
  open: boolean;
  years: AcademicYear[];
  grades: GradeLevel[];
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("admissions");
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<ApplicationFormValues>({
    resolver: zodResolver(applicationSchema),
    defaultValues: {
      applicantFirstNameEn: "",
      applicantLastNameEn: "",
      applicantFirstNameAr: "",
      applicantLastNameAr: "",
      dateOfBirth: "",
      gender: "Male",
      nationality: "",
      academicYearId: "",
      desiredGradeLevelId: "",
      guardianName: "",
      guardianPhone: "",
      guardianEmail: ""
    }
  });

  useEffect(() => {
    if (open) {
      reset();
      setServerError(null);
    }
  }, [open, reset]);

  const emptyToNull = (value: string) => (value.trim() === "" ? null : value.trim());

  const onSubmit = async (values: ApplicationFormValues) => {
    setServerError(null);
    try {
      await apiJson("/api/admission-applications", {
        method: "POST",
        body: JSON.stringify({
          applicantFirstNameEn: values.applicantFirstNameEn.trim(),
          applicantLastNameEn: values.applicantLastNameEn.trim(),
          applicantFirstNameAr: values.applicantFirstNameAr.trim(),
          applicantLastNameAr: values.applicantLastNameAr.trim(),
          dateOfBirth: values.dateOfBirth,
          gender: values.gender,
          nationality: emptyToNull(values.nationality),
          academicYearId: values.academicYearId,
          desiredGradeLevelId: values.desiredGradeLevelId,
          guardianName: values.guardianName.trim(),
          guardianPhone: emptyToNull(values.guardianPhone),
          guardianEmail: emptyToNull(values.guardianEmail)
        })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  return (
    <Modal open={open} title={t("list.createTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className="grid grid-cols-2 gap-3">
          <TextField
            label={t("form.firstNameEn")}
            error={errors.applicantFirstNameEn ? t("common.required") : undefined}
            {...register("applicantFirstNameEn")}
          />
          <TextField
            label={t("form.lastNameEn")}
            error={errors.applicantLastNameEn ? t("common.required") : undefined}
            {...register("applicantLastNameEn")}
          />
          <TextField
            label={t("form.firstNameAr")}
            error={errors.applicantFirstNameAr ? t("common.required") : undefined}
            {...register("applicantFirstNameAr")}
          />
          <TextField
            label={t("form.lastNameAr")}
            error={errors.applicantLastNameAr ? t("common.required") : undefined}
            {...register("applicantLastNameAr")}
          />
          <TextField
            type="date"
            label={t("form.dob")}
            error={errors.dateOfBirth ? t("common.required") : undefined}
            {...register("dateOfBirth")}
          />
          <SelectField label={t("form.gender")} {...register("gender")}>
            <option value="Male">{t("form.male")}</option>
            <option value="Female">{t("form.female")}</option>
          </SelectField>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <SelectField
            label={t("form.year")}
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
            label={t("form.grade")}
            error={errors.desiredGradeLevelId ? t("common.required") : undefined}
            {...register("desiredGradeLevelId")}
          >
            <option value="">{t("common.select")}</option>
            {grades.map((grade) => (
              <option key={grade.id} value={grade.id}>
                {grade.nameEn}
              </option>
            ))}
          </SelectField>
        </div>
        <TextField label={t("form.nationality")} {...register("nationality")} />
        <div className="border-t border-slate-200 pt-4">
          <TextField
            label={t("form.guardianName")}
            error={errors.guardianName ? t("common.required") : undefined}
            {...register("guardianName")}
          />
          <div className="mt-3 grid grid-cols-2 gap-3">
            <TextField label={t("form.guardianPhone")} {...register("guardianPhone")} />
            <TextField label={t("form.guardianEmail")} {...register("guardianEmail")} />
          </div>
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
