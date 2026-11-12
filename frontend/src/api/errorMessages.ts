import type { ApiError } from './apiError'

const codeMessages: Record<string, string> = {
  MALFORMED_REQUEST: 'The request could not be understood.',
  IDEMPOTENCY_KEY_REQUIRED: 'The request is missing a safety key.',
  IDEMPOTENCY_KEY_MISMATCH: 'The request does not match the earlier attempt.',
}

const endpointActionPhrases: Record<string, string> = {
  login: 'Sign-in',
  getGrants: 'Loading grants',
  getGrant: 'Loading the grant',
  addTask: 'Adding the task',
  completeTask: 'Completing the task',
  closeGrant: 'Closing the grant',
}

export function getActionPhrase(endpointName: string): string {
  return endpointActionPhrases[endpointName] ?? 'That action'
}

export function getInlineErrorMessage(error: ApiError): string {
  if (error.kind === 'unreachable') {
    return "Can't reach the server. Check your connection, then try again. Trying again is safe: the same request won't be saved twice."
  }

  if (error.kind === 'problem' && error.code === 'IDEMPOTENCY_KEY_MISMATCH') {
    return "This request doesn't match your earlier attempt. Check the details, then select Add task again."
  }

  if (error.kind === 'problem') {
    return error.title
  }

  return "That didn't work. Try again."
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
