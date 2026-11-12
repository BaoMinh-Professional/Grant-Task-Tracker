import type { FetchBaseQueryError } from '@reduxjs/toolkit/query'

export type ApiError =
  | { kind: 'unreachable'; timedOut: boolean }
  | {
      kind: 'problem'
      status: number
      code: string
      title: string
      traceId?: string
      fieldErrors: Record<string, string[]>
    }
  | { kind: 'unexpected'; status?: number; traceId?: string }

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function getStatus(error: Record<string, unknown>): number | undefined {
  if (typeof error.status === 'number') {
    return error.status
  }

  if (
    error.status === 'PARSING_ERROR' &&
    typeof error.originalStatus === 'number'
  ) {
    return error.originalStatus
  }

  return undefined
}

function getFieldErrors(data: Record<string, unknown>): Record<string, string[]> {
  if (!isRecord(data.errors)) {
    return {}
  }

  const fieldErrors = new Map<string, string[]>()

  for (const [field, messages] of Object.entries(data.errors)) {
    if (!Array.isArray(messages)) {
      continue
    }

    const normalizedField = field.toLowerCase()
    const validMessages = messages.filter(
      (message): message is string => typeof message === 'string',
    )
    fieldErrors.set(normalizedField, [
      ...(fieldErrors.get(normalizedField) ?? []),
      ...validMessages,
    ])
  }

  return Object.fromEntries(fieldErrors)
}

export function toApiError(error: unknown): ApiError {
  if (!isRecord(error)) {
    return { kind: 'unexpected' }
  }

  if (error.status === 'FETCH_ERROR') {
    return { kind: 'unreachable', timedOut: false }
  }

  if (error.status === 'TIMEOUT_ERROR') {
    return { kind: 'unreachable', timedOut: true }
  }

  const status = getStatus(error)
  const data = error.data

  if (
    status !== undefined &&
    isRecord(data) &&
    typeof data.code === 'string'
  ) {
    return {
      kind: 'problem',
      status,
      code: data.code,
      title: typeof data.title === 'string' ? data.title : 'The request failed.',
      ...(typeof data.traceId === 'string' ? { traceId: data.traceId } : {}),
      fieldErrors: getFieldErrors(data),
    }
  }

  return {
    kind: 'unexpected',
    ...(status !== undefined ? { status } : {}),
    ...(isRecord(data) && typeof data.traceId === 'string'
      ? { traceId: data.traceId }
      : {}),
  }
}

export function shortReference(traceId: string): string {
  return traceId.slice(0, 8)
}

export type FetchApiError = FetchBaseQueryError
