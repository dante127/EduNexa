"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Link, useRouter } from "@/i18n/navigation";
import { login } from "@/lib/api-client";

const loginSchema = z.object({
  emailOrUserName: z.string().min(1),
  password: z.string().min(1)
});

type LoginFormValues = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const t = useTranslations("login");
  const router = useRouter();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting }
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema)
  });

  const onSubmit = async (values: LoginFormValues) => {
    setServerError(null);
    const result = await login(values.emailOrUserName, values.password);
    if (result.ok) {
      router.push("/dashboard");
      return;
    }

    setServerError(
      result.error === "invalid_credentials"
        ? t("invalidCredentials")
        : t("genericError")
    );
  };

  return (
    <main className="flex min-h-screen items-center justify-center px-4">
      <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <h1 className="text-2xl font-semibold">{t("title")}</h1>
        <p className="mt-1 text-sm text-slate-500">{t("subtitle")}</p>

        <form className="mt-6 space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <div>
            <label className="block text-sm font-medium" htmlFor="emailOrUserName">
              {t("emailOrUserName")}
            </label>
            <input
              id="emailOrUserName"
              type="text"
              autoComplete="username"
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-500"
              {...register("emailOrUserName")}
            />
            {errors.emailOrUserName ? (
              <p className="mt-1 text-xs text-red-600">{t("required")}</p>
            ) : null}
          </div>

          <div>
            <label className="block text-sm font-medium" htmlFor="password">
              {t("password")}
            </label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-500"
              {...register("password")}
            />
            {errors.password ? (
              <p className="mt-1 text-xs text-red-600">{t("required")}</p>
            ) : null}
          </div>

          {serverError ? (
            <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {serverError}
            </div>
          ) : null}

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700 disabled:opacity-60"
          >
            {isSubmitting ? t("submitting") : t("submit")}
          </button>
        </form>

        <div className="mt-6 text-center text-sm">
          <Link href="/login" locale="ar" className="text-slate-500 hover:text-slate-900">
            العربية
          </Link>
          <span className="mx-2 text-slate-300">|</span>
          <Link href="/login" locale="en" className="text-slate-500 hover:text-slate-900">
            English
          </Link>
        </div>
      </div>
    </main>
  );
}
