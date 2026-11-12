import { describe, expect, it } from 'vitest'
import { toApiError } from './apiError'

describe('toApiError', () => {
  it('maps a problem response and lower-cases field keys', () => {
    const error = toApiError({
      status: 400,
      data: { code: 'BR-2', title: 'x', errors: { dueDate: ['a'] } },
    })

    expect(error).toMatchObject({ kind: 'problem', code: 'BR-2' })
    expect(error).toHaveProperty('fieldErrors.duedate', ['a'])
  })

  it('maps a network failure to unreachable', () => {
    expect(toApiError({ status: 'FETCH_ERROR' })).toEqual({ kind: 'unreachable', timedOut: false })
  })
})
