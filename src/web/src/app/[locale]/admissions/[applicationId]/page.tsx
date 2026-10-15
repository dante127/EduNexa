"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { useParams } from "next/navigation";
import { Link } from "@/i18n/navigation";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { AdmissionApplication, AdmissionEnrollmentResult } from "@/lib/students-types";
import type { AcademicYear, ClassSection, GradeLevel } from "@/lib/structure-types";
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

function admissionTone(status: string): StatusTone {
  if (status === "Approved" || status === "Enrolled") return "active";
  if (status === "Rejected") return "danger";
  if (status === "UnderReview") return "planned";
  return "muted";
}

const today = () => new Date().toISOString().slice(0, 10);

export default function AdmissionDetailPage() {
  const t = useTranslations("admissions");
  const params = useParams<{ applicationId: string }>();
  const applicationId = params.applicationId;

  const [application, setApplication] = useState<AdmissionApplication | null>(null);
  const [years, setYears] = useState<AcademicYear[]>([]);
  const [grades, setGrades] = useState<GradeLevel[]>([]);
  const [sections, setSections] = useState<ClassSection[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [approveOpen, setApproveOpen] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [enrollOpen, setEnrollOpen] = useState(false);
  const [enrolled, setEnrolled] = useState<AdmissionEnrollmentResult | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      setApplication(await apiJson<AdmissionApplication>(`/api/admission-applications/${applicationId}`));
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t, applicationId]);

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
        // Reference data failure keeps the page readable.
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const review = async () => {
    setBusy(true);
    setActionError(null);
    try {
      await apiJson(`/api/admission-applications/${applicationId}/review`, { method: "POST" });
      await load();
    } catch (error) {
      setActionError(formatApiError(t, error));
    } finally {
      setBusy(false);
    }
  };

  if (loadError && !application) {
    return <ErrorBanner message={loadError} />;
  }

  if (!application) {
    return <LoadingBlock message={t("common.loading")} />;
  }

  const yearName = years.find((year) => year.id === application.academicYearId)?.name ?? application.academicYearId;
  const grade = grades.find((level) => level.id === application.desiredGradeLevelId);
  const gradeLabel = grade ? `${grade.nameEn} / ${grade.nameAr}` : application.desiredGradeLevelId;
  const availableSections = sections.filter(
    (section) =>
      section.academicYearId === application.academicYearId && section.gradeLevelId === application.desiredGradeLevelId
  );

  return (
    <div>
      <div className="mb-3 text-sm">
        <Link href="/admissions" className="text-slate-500 hover:text-slate-900">
          {t("common.back")}
        </Link>
      </div>

      <PageHeader
        title={`${application.applicantFirstNameEn} ${application.applicantLastNameEn}`}
        description={`${application.applicantFirstNameAr} ${application.applicantLastNameAr}`}
        actions={
          <>
            {application.status === "Submitted" ? (
              <Button disabled={busy} onClick={() => void review()}>
                {t("detail.review")}
              </Button>
            ) : null}
            {application.status === "UnderReview" ? (
              <>
                <Button disabled={busy} onClick={() => setApproveOpen(true)}>
                  {t("detail.approve")}
                </Button>
                <Button variant="danger" disabled={busy} onClick={() => setRejectOpen(true)}>
                  {t("detail.reject")}
                </Button>
              </>
            ) : null}
            {application.status === "Approved" ? (
              <Button disabled={busy || availableSections.length === 0} onClick={() => setEnrollOpen(true)}>
                {t("detail.enroll")}
              </Button>
            ) : null}
            {application.status === "Enrolled" && application.convertedStudentId && !enrolled ? (
              <Link
                href={`/students/${application.convertedStudentId}`}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium hover:bg-slate-100"
              >
                {t("detail.viewStudent")}
              </Link>
            ) : null}
          </>
        }
      />

      {enrolled ? (
        <div className="mb-4 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
          {t("detail.enrolledInfo", { number: enrolled.student.studentNumber })}{" "}
          <Link href={`/students/${enrolled.student.id}`} className="font-medium underline">
            {t("detail.viewStudent")}
          </Link>
        </div>
      ) : null}

      {actionError ? (
        <div className="mb-4">
          <ErrorBanner message={actionError} />
        </div>
      ) : null}

      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <div className="flex items-center gap-3">
          <StatusBadge tone={admissionTone(application.status)} label={t(`statuses.${application.status}`)} />
          <span className="text-xs text-slate-500">
            {t("detail.submittedAt")}: {application.submittedAt.slice(0, 10)}
          </span>
          {application.reviewedAt ? (
            <span className="text-xs text-slate-500">
              {t("detail.reviewedAt")}: {application.reviewedAt.slice(0, 10)}
            </span>
          ) : null}
        </div>

        <dl className="mt-4 grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
          <div>
            <dt className="text-slate-500">{t("form.dob")}</dt>
            <dd className="mt-1 font-medium">{application.dateOfBirth}</dd>
          </div>
          <div>
            <dt className="text-slate-500">{t("form.gender")}</dt>
            <dd className="mt-1 font-medium">
              {application.gender === "Male" ? t("form.male") : t("form.female")}
            </dd>
          </div>
          <div>
            <dt className="text-slate-500">{t("form.nationality")}</dt>
            <dd className="mt-1 font-medium">{application.nationality ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-slate-500">{t("detail.requested")}</dt>
            <dd className="mt-1 font-medium">
              {yearName} — {gradeLabel}
            </dd>
          </div>
        </dl>

        <div className="mt-6 border-t border-slate-100 pt-4">
          <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{t("detail.guardian")}</h3>
          <dl className="mt-3 grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
            <div>
              <dt className="text-slate-500">{t("form.guardianName")}</dt>
              <dd className="mt-1 font-medium">{application.guardianName}</dd>
            </div>
            <div>
              <dt className="text-slate-500">{t("form.guardianPhone")}</dt>
              <dd className="mt-1 font-medium">{application.guardianPhone ?? "—"}</dd>
            </div>
            <div>
              <dt className="text-slate-500">{t("form.guardianEmail")}</dt>
              <dd className="mt-1 font-medium">{application.guardianEmail ?? "—"}</dd>
            </div>
          </dl>
        </div>

        {application.decisionNotes ? (
          <div className="mt-6 border-t border-slate-100 pt-4">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{t("detail.decision")}</h3>
            <p className="mt-2 text-sm text-slate-700">{application.decisionNotes}</p>
          </div>
        ) : null}
      </div>

      <ApproveModal
        open={approveOpen}
        applicationId={applicationId}
        onClose={() => setApproveOpen(false)}
        onSaved={async () => {
          setApproveOpen(false);
          await load();
        }}
      />

      <RejectModal
        open={rejectOpen}
        applicationId={applicationId}
        onClose={() => setRejectOpen(false)}
        onSaved={async () => {
          setRejectOpen(false);
          await load();
        }}
      />

      {enrollOpen ? (
        <EnrollModal
          open
          applicationId={applicationId}
          sections={availableSections}
          onClose={() => setEnrollOpen(false)}
          onSaved={async (result) => {
            setEnrollOpen(false);
            setEnrolled(result);
            await load();
          }}
        />
      ) : null}
    </div>
  );
}

function ApproveModal({
  open,
  applicationId,
  onClose,
  onSaved
}: {
  open: boolean;
  applicationId: string;
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("admissions");
  const [notes, setNotes] = useState("");
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSaving(true);
    setServerError(null);
    try {
      await apiJson(`/api/admission-applications/${applicationId}/approve`, {
        method: "POST",
        body: JSON.stringify({ notes: notes.trim() === "" ? null : notes.trim() })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} title={t("detail.approveTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <TextField label={t("detail.approveNotes")} value={notes} onChange={(event) => setNotes(event.target.value)} />
        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? t("common.saving") : t("detail.approve")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function RejectModal({
  open,
  applicationId,
  onClose,
  onSaved
}: {
  open: boolean;
  applicationId: string;
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("admissions");
  const [reason, setReason] = useState("");
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (reason.trim() === "") {
      setServerError(t("common.required"));
      return;
    }

    setSaving(true);
    setServerError(null);
    try {
      await apiJson(`/api/admission-applications/${applicationId}/reject`, {
        method: "POST",
        body: JSON.stringify({ notes: reason.trim() })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} title={t("detail.rejectTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <TextField label={t("detail.rejectReason")} value={reason} onChange={(event) => setReason(event.target.value)} />
        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="danger" disabled={saving}>
            {saving ? t("common.saving") : t("detail.reject")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function EnrollModal({
  open,
  applicationId,
  sections,
  onClose,
  onSaved
}: {
  open: boolean;
  applicationId: string;
  sections: ClassSection[];
  onClose: () => void;
  onSaved: (result: AdmissionEnrollmentResult) => void | Promise<void>;
}) {
  const t = useTranslations("admissions");
  const [sectionId, setSectionId] = useState("");
  const [startDate, setStartDate] = useState(today());
  const [notes, setNotes] = useState("");
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSaving(true);
    setServerError(null);
    try {
      const result = await apiJson<AdmissionEnrollmentResult>(`/api/admission-applications/${applicationId}/enroll`, {
        method: "POST",
        body: JSON.stringify({
          classSectionId: sectionId,
          startDate,
          notes: notes.trim() === "" ? null : notes.trim()
        })
      });
      await onSaved(result);
    } catch (error) {
      setServerError(formatApiError(t, error));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} title={t("detail.enrollTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <SelectField label={t("detail.section")} value={sectionId} onChange={(event) => setSectionId(event.target.value)}>
          <option value="">{t("common.select")}</option>
          {sections.map((section) => (
            <option key={section.id} value={section.id}>
              {section.name}
            </option>
          ))}
        </SelectField>
        <TextField
          type="date"
          label={t("detail.startDate")}
          value={startDate}
          onChange={(event) => setStartDate(event.target.value)}
        />
        <TextField label={t("detail.notes")} value={notes} onChange={(event) => setNotes(event.target.value)} />
        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={saving || !sectionId || !startDate}>
            {saving ? t("common.saving") : t("detail.enroll")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
