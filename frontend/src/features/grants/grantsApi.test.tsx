import { beforeEach, describe, expect, it, vi } from 'vitest'
import { store } from '../../app/store'
import { mockApi } from '../../test/mockApi'
import { signInTestUser, testUser } from '../../test/renderWithStore'
import { grantsApi } from './grantsApi'
import type { GrantListItemDto, TaskDto } from './types'

const grantId = '00000000-0000-0000-0000-000000000101'

const grantItem: GrantListItemDto = {
  id: grantId,
  referenceNumber: 'GR-100001',
  organisationName: 'Riverside Community Garden Inc.',
  applicationDate: '2026-01-09',
  status: 'Open',
  totalTasks: 3,
  completedTasks: 1,
  pendingHighTasks: 1,
  canClose: false,
}

const newTask = { title: 'Call applicant', dueDate: '2026-03-10', priority: 'Low' as const }

const createdTask: TaskDto = {
  id: '00000000-0000-0000-0000-000000000301',
  grantId,
  title: 'Call applicant',
  priority: 'Low',
  dueDate: '2026-03-10',
  status: 'Pending',
  completedAtUtc: null,
  isOverdue: false,
  daysOverdue: 0,
  version: 1,
}

describe('grantsApi', () => {
  beforeEach(() => {
    signInTestUser()
  })

  it('requests open grants with the signed-in officer id', async () => {
    const { calls } = mockApi([{ method: 'GET', path: '/grants?status=Open', body: [grantItem] }])
    const subscription = store.dispatch(grantsApi.endpoints.getGrants.initiate('Open'))
    await subscription
    subscription.unsubscribe()

    expect(calls[0].path).toBe('/grants?status=Open')
    expect(calls[0].headers.get('X-User-Id')).toBe(testUser.id)
  })

  it('requests all grants without a status filter', async () => {
    const { calls } = mockApi([{ method: 'GET', path: '/grants', body: [] }])
    const subscription = store.dispatch(grantsApi.endpoints.getGrants.initiate('All'))
    await subscription
    subscription.unsubscribe()

    expect(calls[0].path).toBe('/grants')
  })

  it('sends the idempotency key and the task as JSON when adding a task', async () => {
    const { calls } = mockApi([
      { method: 'POST', path: `/grants/${grantId}/tasks`, status: 201, body: createdTask },
    ])
    await store.dispatch(
      grantsApi.endpoints.addTask.initiate({ grantId, task: newTask, idempotencyKey: 'key-1' }),
    )

    expect(calls[0].method).toBe('POST')
    expect(calls[0].headers.get('Idempotency-Key')).toBe('key-1')
    expect(calls[0].body).toEqual(newTask)
  })

  it('refetches the cached open list after a successful close', async () => {
    const { calls } = mockApi([
      { method: 'GET', path: '/grants?status=Open', body: [grantItem] },
      {
        method: 'PUT',
        path: `/grants/${grantId}/close`,
        body: { ...grantItem, status: 'Closed', canClose: false },
      },
    ])
    const list = store.dispatch(grantsApi.endpoints.getGrants.initiate('Open'))
    await list

    await store.dispatch(grantsApi.endpoints.closeGrant.initiate(grantId))

    await vi.waitFor(() =>
      expect(calls.filter((call) => call.path === '/grants?status=Open')).toHaveLength(2),
    )
    list.unsubscribe()
  })
})
