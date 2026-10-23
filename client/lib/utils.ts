import { createCn } from "cn/config"

/**
 * Joins class names and resolves Tailwind conflicts (clsx + tailwind-merge semantics). It is told about
 * the custom theme keys in app/globals.css: without that, `text-13` would be read as a text colour and
 * silently dropped next to `text-ink`.
 */
export const cn = createCn({
  extend: {
    theme: {
      text: ["12", "13", "14", "16", "20", "24", "30"],
      spacing: ["topbar", "sidebar", "sidebar-collapsed", "row", "row-compact"],
      radius: ["pill"],
      container: ["form"],
    },
  },
})
