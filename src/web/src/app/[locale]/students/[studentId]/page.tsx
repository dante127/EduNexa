"use client";

import { useCallback, useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Link } from "@/i18n/navigation";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { StudentDetail, StudentSummary } from "@/lib/students-types";
import type { AcademicYear, ClassSection, GradeLevel } from "@/lib/structure-types";
import StudentDocumentsCard from "@/components/student-documents-card";
import StudentEnrollmentsTab from "@/components/student-enrollments-tab";
import StudentGuardiansTab from "@/components/student-guardians-tab";
import {
  Button,
  ErrorBanner,
  LoadingBlock,
  Modal,
  PageHeader,
  SelectField,
  StatusBadge,
  TextField,
  type StatusTone
} from "@/components/ui";

const profileSchema = z.object({
  firstNameEn: z.string().min(1).max(100),
  lastNameEn: z.string().min(1).max(100),
  firstNameAr: z.string().min(1).max(100),
  lastNameAr: z.string().min(1).max(100),
  dateOfBirth: z.string().min(1),
  gender: z.string().min(1),
  nationality: z.string().max(100),
  phone: z.string().max(30),
  email: z.union([z.literal(""), z.string().email().max(200)]),
  address: z.string().max(300)
});

type ProfileFormValues = z.infer<typeof profileSchema>;

function studentTone(status: string): StatusTone {
  if (status === "Active") return "active";
  if (status === "Applicant") return "planned";
  return "muted";
}

export default function StudentDetailPage() {
  const t = useTranslations("students");
  const params = useParams<{ studentId: string }>();
  const studentId = params.studentId;

  const [detail, setDetail] = useState<StudentDetail | null>(null);
  const [years, setYears] = useState<AcademicYear[]>([]);
  const [grades, setGrades] = useState<GradeLevel[]>([]);
  const [sections, setSections] = useState<ClassSection[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [tab, setTab] = useState<"guardians" | "enrollments" | "documents">("guardians");
  const [editOpen, setEditOpen] = useState(false);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const detailData = await apiJson<StudentDetail>(`/api/students/${studentId}`);
      setDetail(detailData);
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t, studentId]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const [yearData, gradeData, sectionData] = await Promise.all([
          apiJson<AcademicYear[]>("/api/academic-years"),
          apiJson<GradeLevel[]>("/api/grade-levels"),
          apiJson<ClassSection[]>("/api/sections")
        ]);
        if (!cancelled) {
          setYears(yearData);
          setGrades(gradeData);
          setSections(sectionData);
        }
      } catch {
        // Reference data failure keeps the page usable for the profile portion.
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  if (loadError && !detail) {
    return (
      <div>
        <ErrorBanner message={loadError} />
      </div>
    );
  }

  if (!detail) {
    return <LoadingBlock message={t("common.loading")} />;
  }

  const student = detail.student;

  const tabButton = (key: "guardians" | "enrollments" | "documents", label: string) => (
    <button
      key={key}
      type="button"
      onClick={() => setTab(key)}
      className={`rounded-lg px-3 py-1.5 text-sm font-medium ${
        tab === key ? "bg-slate-900 text-white" : "border border-slate-300 bg-white text-slate-700 hover:bg-slate-100"
      }`}
    >
      {label}
    </button>
  );

  return (
    <div>
      <div className="mb-3 text-sm">
        <Link href="/students" className="text-slate-500 hover:text-slate-900">
          {t("profile.backToList")}
        </Link>
      </div>

      <PageHeader
        title={`${student.firstNameEn} ${student.lastNameEn}`}
        description={`${student.firstNameAr} ${student.lastNameAr}`}
        actions={
          <Button variant="secondary" onClick={() => setEditOpen(true)}>
            {t("common.edit")}
          </Button>
        }
      />

      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <div className="flex flex-wrap items-center gap-3">
          <span className="font-mono text-xs text-slate-500">{student.studentNumber}</span>
          <StatusBadge tone={studentTone(student.status)} label={t(`profile.statuses.${student.status}`)} />
        </div>
        <dl className="mt-4 grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
          <div>
            <dt className="text-slate-500">{t("profile.dob")}</dt>
            <dd className="mt-1 font-medium">{student.dateOfBirth}</dd>
          </div>
          <div>
            <dt className="text-slate-500">{t("profile.gender")}</dt>
            <dd className="mt-1 font-medium">
              {student.gender === "Male" ? t("profile.male") : t("profile.female")}
            </dd>
          </div>
          <div>
            <dt className="text-slate-500">{t("profile.nationality")}</dt>
            <dd className="mt-1 font-medium">{student.nationality ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-slate-500">{t("profile.phone")}</dt>
            <dd className="mt-1 font-medium">{student.phone ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-slate-500">{t("profile.email")}</dt>
            <dd className="mt-1 font-medium">{student.email ?? "—"}</dd>
          </div>
          <div className="col-span-2 sm:col-span-3">
            <dt className="text-slate-500">{t("profile.address")}</dt>
            <dd className="mt-1 font-medium">{student.address ?? "—"}</dd>
          </div>
        </dl>
      </div>

      <div className="mt-6 flex flex-wrap gap-2">
        {tabButton("guardians", t("tabs.guardians"))}
        {tabButton("enrollments", t("tabs.enrollments"))}
        {tabButton("documents", t("tabs.documents"))}
      </div>

      <div className="mt-4">
        {tab === "guardians" ? (
          <StudentGuardiansTab studentId={studentId} guardians={detail.guardians} onChanged={load} />
        ) : null}
        {tab === "enrollments" ? (
          <StudentEnrollmentsTab
            studentId={studentId}
            enrollments={detail.enrollments}
            years={years}
            grades={grades}
            sections={sections}
            onChanged={load}
          />
        ) : null}
        {tab === "documents" ? <StudentDocumentsCard studentId={studentId} /> : null}
      </div>

      <ProfileEditModal
        open={editOpen}
        student={student}
        onClose={() => setEditOpen(false)}
        onSaved={async () => {
          setEditOpen(false);
          await load();
        }}
      />
    </div>
  );
}

function ProfileEditModal({
  open,
  student,
  onClose,
  onSaved
}: {
  open: boolean;
  student: StudentSummary;
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      firstNameEn: "",
      lastNameEn: "",
      firstNameAr: "",
      lastNameAr: "",
      dateOfBirth: "",
      gender: "Male",
      nationality: "",
      phone: "",
      email: "",
      address: ""
    }
  });

  useEffect(() => {
    if (open) {
      reset({
        firstNameEn: student.firstNameEn,
        lastNameEn: student.lastNameEn,
        firstNameAr: student.firstNameAr,
        lastNameAr: student.lastNameAr,
        dateOfBirth: student.dateOfBirth,
        gender: student.gender,
        nationality: student.nationality ?? "",
        phone: student.phone ?? "",
        email: student.email ?? "",
        address: student.address ?? ""
      });
      setServerError(null);
    }
  }, [open, student, reset]);

  const emptyToNull = (value: string) => (value.trim() === "" ? null : value.trim());

  const onSubmit = async (values: ProfileFormValues) => {
    setServerError(null);
    try {
      await apiJson(`/api/students/${student.id}`, {
        method: "PUT",
        body: JSON.stringify({
          id: student.id,
          firstNameEn: values.firstNameEn.trim(),
          lastNameEn: values.lastNameEn.trim(),
          firstNameAr: values.firstNameAr.trim(),
          lastNameAr: values.lastNameAr.trim(),
          dateOfBirth: values.dateOfBirth,
          gender: values.gender,
          nationality: emptyToNull(values.nationality),
          phone: emptyToNull(values.phone),
          email: emptyToNull(values.email),
          address: emptyToNull(values.address),
          photoPath: student.photoPath ?? null
        })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  return (
    <Modal open={open} title={t("profile.editTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className="grid grid-cols-2 gap-3">
          <TextField
            label={t("profile.firstNameEn")}
            error={errors.firstNameEn ? t("common.required") : undefined}
            {...register("firstNameEn")}
          />
          <TextField
            label={t("profile.lastNameEn")}
            error={errors.lastNameEn ? t("common.required") : undefined}
            {...register("lastNameEn")}
          />
          <TextField
            label={t("profile.firstNameAr")}
            error={errors.firstNameAr ? t("common.required") : undefined}
            {...register("firstNameAr")}
          />
          <TextField
            label={t("profile.lastNameAr")}
            error={errors.lastNameAr ? t("common.required") : undefined}
            {...register("lastNameAr")}
          />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <TextField
            type="date"
            label={t("profile.dob")}
            error={errors.dateOfBirth ? t("common.required") : undefined}
            {...register("dateOfBirth")}
          />
          <SelectField label={t("profile.gender")} {...register("gender")}>
            <option value="Male">{t("profile.male")}</option>
            <option value="Female">{t("profile.female")}</option>
          </SelectField>
        </div>
        <TextField label={t("profile.nationality")} {...register("nationality")} />
        <div className="grid grid-cols-2 gap-3">
          <TextField label={t("profile.phone")} {...register("phone")} />
          <TextField label={t("profile.email")} error={errors.email ? t("common.invalidEmail") : undefined} {...register("email")} />
        </div>
        <TextField label={t("profile.address")} {...register("address")} />
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
