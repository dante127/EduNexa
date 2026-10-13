import { redirect } from "@/i18n/navigation";

export default async function StructureIndexPage({
  params
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  redirect({ href: "/structure/academic-years", locale });
}
