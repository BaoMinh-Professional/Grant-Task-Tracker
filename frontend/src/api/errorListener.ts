import { isRejectedWithValue } from '@reduxjs/toolkit'
import type { AppStartListening } from '../app/store'
import { baseApi } from './baseApi'
import { toApiError } from './apiError'
import { getErrorToastTitle } from './errorMessages'
import { notify } from '../components/notify'
import { setServerUnreachable } from '../features/connection/connectionSlice'
import { signedOut } from '../features/session/sessionSlice'

function getEndpointName(action: unknown): string | undefined {
  if (
    typeof action !== 'object' ||
    action === null ||
    !('type' in action) ||
    typeof action.type !== 'string' ||
    !action.type.startsWith(`${baseApi.reducerPath}/`) ||
    !('meta' in action) ||
    typeof action.meta !== 'object' ||
    action.meta === null ||
    !('arg' in action.meta) ||
    typeof action.meta.arg !== 'object' ||
    action.meta.arg === null ||
    !('endpointName' in action.meta.arg) ||
    typeof action.meta.arg.endpointName !== 'string'
  ) {
    return undefined
  }

  return action.meta.arg.endpointName
}

export function registerErrorListeners(startListening: AppStartListening) {
  startListening({
    matcher: isRejectedWithValue(),
    effect: (action, listenerApi) => {
      const endpointName = getEndpointName(action)
      if (!endpointName) {
        return
      }

      if (!('payload' in action)) {
        return
      }

      const error = toApiError(action.payload)

      if (error.kind === 'unreachable') {
        listenerApi.dispatch(setServerUnreachable(true))
        return
      }

      if (error.status === 401) {
        if (endpointName !== 'login') {
          listenerApi.dispatch(signedOut())
          notify.info(
            "You've been signed out. Sign in again to continue.",
          )
        }
        return
      }

      if (
        error.kind === 'problem' &&
        (error.code === 'VALIDATION_FAILED' ||
          error.code === 'NOT_FOUND' ||
          error.status === 409)
      ) {
        return
      }

      notify.error({
        id: `${endpointName}:${error.kind === 'problem' ? error.code : 'unexpected'}`,
        title: getErrorToastTitle(error, endpointName),
        traceId: error.traceId,
      })
    },
  })

  startListening({
    predicate: (action) =>
      action.type.startsWith(`${baseApi.reducerPath}/execute`) &&
      action.type.endsWith('/fulfilled'),
    effect: (_action, listenerApi) => {
      listenerApi.dispatch(setServerUnreachable(false))
    },
  })
}
