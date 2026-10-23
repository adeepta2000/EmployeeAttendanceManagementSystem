/**
 * Validation messages are keys into the string table (messages/en.json), not English text, so the same
 * schema can validate in the browser and in a Route Handler and still be translated where it's shown.
 * Values travel after a `|` as JSON: `validation.tooLong|{"max":30}`.
 *
 * Messages from the API are plain sentences; `isMessageKey` tells the two apart.
 */
export function m(key: string, values?: Record<string, string | number>): string {
  return values ? `${key}|${JSON.stringify(values)}` : key
}

const KEY = /^[a-z][a-zA-Z]*(\.[a-zA-Z0-9]+)+$/

export function parseMessage(message: string): { key: string; values?: Record<string, string | number> } | null {
  const separator = message.indexOf("|")
  const key = separator === -1 ? message : message.slice(0, separator)
  if (!KEY.test(key)) return null
  if (separator === -1) return { key }
  try {
    return { key, values: JSON.parse(message.slice(separator + 1)) }
  } catch {
    return null
  }
}
