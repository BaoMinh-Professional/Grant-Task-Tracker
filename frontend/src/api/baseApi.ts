import { createApi, fetchBaseQuery, retry } from '@reduxjs/toolkit/query/react'
import type { FetchBaseQueryError } from '@reduxjs/toolkit/query'
import type { RootState } from '../app/store'

function getFetchErrorStatus(
  error: unknown,
): FetchBaseQueryError['status'] | undefined {
  if (
    typeof error !== 'object' ||
    error === null ||
    !('status' in error)
  ) {
    return undefined
  }

  const { status } = error
  return typeof status === 'number' ||
    status === 'FETCH_ERROR' ||
    status === 'PARSING_ERROR' ||
    status === 'TIMEOUT_ERROR' ||
    status === 'CUSTOM_ERROR'
    ? status
    : undefined
}

const baseQuery = retry(
  fetchBaseQuery({
    baseUrl: import.meta.env.VITE_API_BASE_URL,
    timeout: 10_000,
    prepareHeaders(headers, { endpoint, getState }) {
      if (endpoint !== 'login') {
        const user = (getState() as RootState).session.user
        if (user) {
          headers.set('X-User-Id', user.id)
        }
      }

      return headers
    },
  }),
  {
    retryCondition: (error, _args, { attempt, baseQueryApi }) => {
      if (baseQueryApi.type !== 'query' || attempt > 3) {
        return false
      }

      const status = getFetchErrorStatus(error)
      return (
        status === 'FETCH_ERROR' ||
        status === 'TIMEOUT_ERROR' ||
        (typeof status === 'number' && status >= 500)
      )
    },
  },
)

export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery,
  tagTypes: ['Grant'],
  // Data can be stale between refetches (a High task added by another officer, or the business day
  // passing midnight). The server still enforces the rules, and the screen shows the refusal.
  refetchOnFocus: true,
  refetchOnReconnect: true,
  endpoints: () => ({}),
})
