import type { ApiError } from './apiError'

const codeMessages: Record<string, string> = {
  MALFORMED_REQUEST: 'The request could not be understood.',
  IDEMPOTENCY_KEY_REQUIRED: 'The request is missing a safety key.',
  IDEMPOTENCY_KEY_MISMATCH: 'The request does not match the earlier attempt.',
}

const endpointActionPhrases: Record<string, string> = {
  login: 'Sign-in',
}

export function getActionPhrase(endpointName: string): string {
  return endpointActionPhrases[endpointName] ?? 'That action'
}

export function getErrorToastTitle(
  error: ApiError,
  endpointName: string,
): string {
  const action = getActionPhrase(endpointName)

  if (
    error.kind === 'unexpected' ||
    (error.kind === 'problem' &&
      (error.status >= 500 || error.code === 'UNEXPECTED_ERROR'))
  ) {
    return `${action} failed because of a server error.`
  }

  if (error.kind === 'problem') {
    const message = codeMessages[error.code]
    return message ? `${action} failed. ${message}` : 'That didn’t work.'
  }

  return `${action} failed.`
}
