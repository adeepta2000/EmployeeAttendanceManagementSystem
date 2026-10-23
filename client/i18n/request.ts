import { getRequestConfig } from "next-intl/server"

/**
 * Strings come from messages/<locale>.json (spec §2.4: Bangla-ready). English is the only table today;
 * adding Bangla means adding messages/bn.json and choosing the locale here, e.g. from a cookie. URLs carry
 * no locale prefix, so bookmarks and shared filter links work in either language.
 */
export default getRequestConfig(async () => {
  const locale = "en"
  return {
    locale,
    messages: (await import(`../messages/${locale}.json`)).default,
    timeZone: "Asia/Dhaka",
  }
})
