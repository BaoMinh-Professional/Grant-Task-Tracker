import { baseApi } from '../../api/baseApi'
import type { UserDto } from './sessionSlice'

export const authApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    login: build.mutation<UserDto, { email: string }>({
      query: ({ email }) => ({
        url: '/auth/login',
        method: 'POST',
        body: { email },
      }),
    }),
  }),
})

export const { useLoginMutation } = authApi
