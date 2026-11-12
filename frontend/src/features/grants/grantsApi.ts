import { baseApi } from '../../api/baseApi'
import type {
  GrantDetailDto,
  GrantListItemDto,
  GrantStatusFilter,
  NewTask,
  TaskDto,
} from './types'

export const grantsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getGrants: build.query<GrantListItemDto[], GrantStatusFilter>({
      query: (status) => (status === 'All' ? '/grants' : `/grants?status=${status}`),
      providesTags: (result) => [
        { type: 'Grant', id: 'LIST' },
        ...(result ?? []).map(({ id }) => ({ type: 'Grant' as const, id })),
      ],
    }),
    getGrant: build.query<GrantDetailDto, string>({
      query: (id) => `/grants/${id}`,
      providesTags: (_result, _error, id) => [{ type: 'Grant', id }],
    }),
    addTask: build.mutation<TaskDto, { grantId: string; task: NewTask; idempotencyKey: string }>({
      query: ({ grantId, task, idempotencyKey }) => ({
        url: `/grants/${grantId}/tasks`,
        method: 'POST',
        body: task,
        headers: { 'Idempotency-Key': idempotencyKey },
      }),
      invalidatesTags: (_result, error, { grantId }) =>
        error && error.status !== 409 ? [] : [{ type: 'Grant', id: grantId }],
    }),
    completeTask: build.mutation<TaskDto, { taskId: string; grantId: string }>({
      query: ({ taskId }) => ({ url: `/tasks/${taskId}/complete`, method: 'PUT' }),
      invalidatesTags: (_result, error, { grantId }) =>
        error && error.status !== 409 ? [] : [{ type: 'Grant', id: grantId }],
    }),
    closeGrant: build.mutation<GrantListItemDto, string>({
      query: (id) => ({ url: `/grants/${id}/close`, method: 'PUT' }),
      invalidatesTags: (_result, error, id) =>
        error && error.status !== 409
          ? []
          : [
              { type: 'Grant', id },
              { type: 'Grant', id: 'LIST' },
            ],
    }),
    editTask: build.mutation<
      TaskDto,
      { taskId: string; grantId: string; version: number; task: NewTask; idempotencyKey: string }
    >({
      query: ({ taskId, version, task, idempotencyKey }) => ({
        url: `/tasks/${taskId}`,
        method: 'PUT',
        body: task,
        headers: { 'If-Match': `"${version}"`, 'Idempotency-Key': idempotencyKey },
      }),
      invalidatesTags: (_result, error, { grantId }) =>
        error && error.status !== 409 && error.status !== 412 ? [] : [{ type: 'Grant', id: grantId }],
    }),
  }),
})

export const {
  useGetGrantsQuery,
  useGetGrantQuery,
  useAddTaskMutation,
  useCompleteTaskMutation,
  useCloseGrantMutation,
  useEditTaskMutation,
} = grantsApi
