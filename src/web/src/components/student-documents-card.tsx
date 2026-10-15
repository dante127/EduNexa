"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { ApiError, apiFetch, apiJson, formatApiError } from "@/lib/api-client";
import type { StudentDocument } from "@/lib/students-types";
import {
  Button,
  ConfirmDialog,
  EmptyState,
  ErrorBanner,
  LoadingBlock,
  Modal,
  SelectField,
  StatusBadge,
  TextField,
  type StatusTone
} from "@/components/ui";

const documentTypes = [
  "BirthCertificate",
  "NationalId",
  "Passport",
  "PreviousSchoolRecord",
  "MedicalRecord",
  "Photo",
  "Other"
] as const;

function documentTone(status: string): StatusTone {
  if (status === "Verified") return "active";
  if (status === "Rejected") return "danger";
  return "planned";
}

const formatSize = (bytes: number): string =>
  bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;

export default function StudentDocumentsCard({ studentId }: { studentId: string }) {
  const t = useTranslations("students");
  const [documents, setDocuments] = useState<StudentDocument[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [documentType, setDocumentType] = useState<string>("BirthCertificate");
  const [uploadNotes, setUploadNotes] = useState("");
  const [uploading, setUploading] = useState(false);
  const [verifying, setVerifying] = useState<StudentDocument | null>(null);
  const [deleting, setDeleting] = useState<StudentDocument | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      setDocuments(await apiJson<StudentDocument[]>(`/api/students/${studentId}/documents`));
    } catch (error) {
      setLoadError(formatApiError(t, error));
    }
  }, [t, studentId]);

  useEffect(() => {
    void load();
  }, [load]);

  const upload = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!file) {
      setActionError(t("documents.fileRequired"));
      return;
    }

    setUploading(true);
    setActionError(null);
    try {
      const form = new FormData();
      form.append("file", file);
      form.append("documentType", documentType);
      if (uploadNotes.trim()) {
        form.append("notes", uploadNotes.trim());
      }

      const response = await apiFetch(`/api/students/${studentId}/documents`, { method: "POST", body: form });
      if (!response.ok) {
        let code = "unknown";
        let title = "";
        let errors: Record<string, string[]> | undefined;
        try {
          const problem = (await response.json()) as {
            code?: string;
            title?: string;
            errors?: Record<string, string[]>;
          };
          code = problem.code ?? code;
          title = problem.title ?? "";
          errors = problem.errors;
        } catch {
          // Keep the defaults when the body is not JSON.
        }

        throw new ApiError(response.status, code, title || `Request failed with status ${response.status}.`, errors);
      }

      setFile(null);
      setUploadNotes("");
      setDocumentType("BirthCertificate");
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }

      await load();
    } catch (error) {
      setActionError(formatApiError(t, error));
    } finally {
      setUploading(false);
    }
  };

  const download = async (document: StudentDocument) => {
    setBusyId(document.id);
    setActionError(null);
    try {
      const response = await apiFetch(`/api/students/${studentId}/documents/${document.id}/download`);
      if (!response.ok) {
        throw new ApiError(response.status, "download_failed", `Download failed with status ${response.status}.`);
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = window.document.createElement("a");
      anchor.href = url;
      anchor.download = document.fileName;
      window.document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      setActionError(formatApiError(t, error));
    } finally {
      setBusyId(null);
    }
  };

  const confirmDelete = async () => {
    if (!deleting) {
      return;
    }

    setBusyId(deleting.id);
    setActionError(null);
    try {
      await apiJson(`/api/students/${studentId}/documents/${deleting.id}`, { method: "DELETE" });
      await load();
    } catch (error) {
      setActionError(formatApiError(t, error));
    } finally {
      setBusyId(null);
      setDeleting(null);
    }
  };

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{t("documents.title")}</h3>
        <span className="text-xs text-slate-500">{t("documents.sizeLimitHint")}</span>
      </div>

      <form className="mb-4 flex flex-wrap items-end gap-3 rounded-xl border border-slate-200 bg-white p-4" onSubmit={upload}>
        <div className="min-w-56 flex-1">
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">{t("documents.file")}</span>
            <input
              ref={fileInputRef}
              type="file"
              accept=".pdf,.jpg,.jpeg,.png,.webp,application/pdf,image/jpeg,image/png,image/webp"
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
              className="w-full rounded-lg border border-slate-300 px-3 py-1.5 text-sm file:me-3 file:rounded file:border-0 file:bg-slate-100 file:px-2 file:py-1 file:text-sm"
            />
          </label>
        </div>
        <div className="w-48">
          <SelectField label={t("documents.type")} value={documentType} onChange={(event) => setDocumentType(event.target.value)}>
            {documentTypes.map((type) => (
              <option key={type} value={type}>
                {t(`documents.types.${type}`)}
              </option>
            ))}
          </SelectField>
        </div>
        <div className="min-w-40 flex-1">
          <TextField label={t("documents.notes")} value={uploadNotes} onChange={(event) => setUploadNotes(event.target.value)} />
        </div>
        <Button type="submit" disabled={uploading}>
          {uploading ? t("common.saving") : t("documents.upload")}
        </Button>
      </form>

      {actionError ? (
        <div className="mb-3">
          <ErrorBanner message={actionError} />
        </div>
      ) : null}
      {loadError ? (
        <div className="mb-3">
          <ErrorBanner message={loadError} />
        </div>
      ) : null}

      {documents === null ? (
        <LoadingBlock message={t("common.loading")} />
      ) : documents.length === 0 ? (
        <EmptyState message={t("documents.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("documents.colType")}</th>
                <th className="px-4 py-3 text-start">{t("documents.colFile")}</th>
                <th className="px-4 py-3 text-start">{t("documents.colSize")}</th>
                <th className="px-4 py-3 text-start">{t("documents.colUploaded")}</th>
                <th className="px-4 py-3 text-start">{t("documents.colStatus")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {documents.map((document) => (
                <tr key={document.id}>
                  <td className="px-4 py-3 text-slate-600">{t(`documents.types.${document.documentType}`)}</td>
                  <td className="px-4 py-3">
                    <div className="font-medium">{document.fileName}</div>
                    {document.notes ? <div className="text-xs text-slate-500">{document.notes}</div> : null}
                  </td>
                  <td className="px-4 py-3 text-slate-600">{formatSize(document.sizeBytes)}</td>
                  <td className="px-4 py-3 text-slate-600">{document.uploadedAt.slice(0, 10)}</td>
                  <td className="px-4 py-3">
                    <StatusBadge
                      tone={documentTone(document.verificationStatus)}
                      label={t(`documents.statuses.${document.verificationStatus}`)}
                    />
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap justify-end gap-2">
                      <Button
                        variant="secondary"
                        disabled={busyId === document.id}
                        onClick={() => void download(document)}
                      >
                        {t("documents.download")}
                      </Button>
                      {document.verificationStatus === "Pending" ? (
                        <Button variant="secondary" onClick={() => setVerifying(document)}>
                          {t("documents.verify")}
                        </Button>
                      ) : null}
                      <Button variant="danger" onClick={() => setDeleting(document)}>
                        {t("documents.deleteTitle")}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {verifying ? (
        <VerifyDocumentModal
          open
          studentId={studentId}
          document={verifying}
          onClose={() => setVerifying(null)}
          onSaved={async () => {
            setVerifying(null);
            await load();
          }}
        />
      ) : null}

      <ConfirmDialog
        open={deleting !== null}
        title={t("documents.deleteTitle")}
        message={t("documents.deleteConfirm")}
        confirmLabel={t("common.delete")}
        cancelLabel={t("common.cancel")}
        danger
        onConfirm={() => void confirmDelete()}
        onCancel={() => setDeleting(null)}
      />
    </div>
  );
}

function VerifyDocumentModal({
  open,
  studentId,
  document,
  onClose,
  onSaved
}: {
  open: boolean;
  studentId: string;
  document: StudentDocument;
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [status, setStatus] = useState("Verified");
  const [notes, setNotes] = useState("");
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSaving(true);
    setServerError(null);
    try {
      await apiJson(`/api/students/${studentId}/documents/${document.id}/verify`, {
        method: "POST",
        body: JSON.stringify({ status, notes: notes.trim() === "" ? null : notes.trim() })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} title={t("documents.verifyTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <p className="text-sm text-slate-600">{document.fileName}</p>
        <SelectField label={t("documents.status")} value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="Verified">{t("documents.verifiedLabel")}</option>
          <option value="Rejected">{t("documents.rejectedLabel")}</option>
        </SelectField>
        <TextField label={t("documents.verifyNotes")} value={notes} onChange={(event) => setNotes(event.target.value)} />
        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
