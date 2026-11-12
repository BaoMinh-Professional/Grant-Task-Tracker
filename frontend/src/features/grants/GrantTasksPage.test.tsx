import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { mockApi } from '../../test/mockApi'
import { renderWithStore } from '../../test/renderWithStore'
import { GrantTasksPage } from './GrantTasksPage'
import type { GrantDetailDto, TaskDto } from './types'

const grantId = '00000000-0000-0000-0000-000000000101'
const verifyId = '00000000-0000-0000-0000-000000000201'
const quoteId = '00000000-0000-0000-0000-000000000202'
const surveyId = '00000000-0000-0000-0000-000000000204'
const eligibilityId = '00000000-0000-0000-0000-000000000203'

function task(overrides: Partial<TaskDto> & Pick<TaskDto, 'id' | 'title'>): TaskDto {
  return {
    grantId,
    priority: 'Low',
    dueDate: '2026-03-20',
    status: 'Pending',
    completedAtUtc: null,
    isOverdue: false,
    daysOverdue: 0,
    version: 1,
    ...overrides,
  }
}

const verify = task({
  id: verifyId,
  title: 'Verify bank details',
  priority: 'High',
  dueDate: '2026-03-07',
  isOverdue: true,
  daysOverdue: 3,
})
const quote = task({ id: quoteId, title: 'Request quote for tools', priority: 'Medium', dueDate: '2026-03-15' })
const survey = task({ id: surveyId, title: 'Schedule site visit', priority: 'Low', dueDate: '2026-03-25' })
const eligibility = task({
  id: eligibilityId,
  title: 'Check eligibility documents',
  priority: 'High',
  dueDate: '2026-02-01',
  status: 'Completed',
  completedAtUtc: '2026-02-02T09:00:00Z',
})

function detail(overrides: Partial<GrantDetailDto> = {}): GrantDetailDto {
  return {
    id: grantId,
    referenceNumber: 'GR-100001',
    organisationName: 'Riverside Community Garden Inc.',
    applicationDate: '2026-01-09',
    status: 'Open',
    today: '2026-03-10',
    pendingHighTasks: 1,
    canClose: false,
    tasks: [verify, quote, survey, eligibility],
    ...overrides,
  }
}

function renderTasks(onBack = vi.fn()) {
  return renderWithStore(<GrantTasksPage grantId={grantId} onBack={onBack} />)
}

function rowTitles(): string[] {
  return screen
    .getAllByRole('row')
    .slice(1)
    .map((row) => row.querySelector('td')?.textContent ?? '')
}

describe('GrantTasksPage', () => {
  it('shows priority words and the overdue badge only for overdue pending tasks', async () => {
    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    renderTasks()

    expect(await screen.findByText('Verify bank details')).toBeTruthy()
    expect(screen.getByText('High priority', { selector: '.tag' })).toBeTruthy()
    expect(screen.getByText('Medium priority', { selector: '.tag' })).toBeTruthy()
    expect(screen.getByText('Low priority', { selector: '.tag' })).toBeTruthy()
    expect(screen.getByText('Overdue by 3 days')).toBeTruthy()

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'All' } })
    expect(screen.getAllByText(/Overdue by/)).toHaveLength(1)
  })

  it('lists only pending tasks by default and supports All and Completed', async () => {
    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    renderTasks()
    await screen.findByText('Verify bank details')
    expect(screen.queryByText('Check eligibility documents')).toBeNull()

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'All' } })
    expect(screen.getByText('Check eligibility documents')).toBeTruthy()

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Completed' } })
    expect(rowTitles()).toEqual(['Check eligibility documents'])
  })

  it('filters by priority and by a case-insensitive title search', async () => {
    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    renderTasks()
    await screen.findByText('Verify bank details')

    fireEvent.change(screen.getByLabelText('Priority'), { target: { value: 'High' } })
    expect(rowTitles()).toEqual(['Verify bank details'])

    fireEvent.change(screen.getByLabelText('Priority'), { target: { value: 'All' } })
    fireEvent.change(screen.getByLabelText('Search by title'), { target: { value: 'BANK' } })
    expect(rowTitles()).toEqual(['Verify bank details'])
  })

  it('orders pending before completed, then priority, then due date', async () => {
    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    renderTasks()
    await screen.findByText('Verify bank details')

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'All' } })
    expect(rowTitles()).toEqual([
      'Verify bank details',
      'Request quote for tools',
      'Schedule site visit',
      'Check eligibility documents',
    ])
  })

  it('shows empty messages for a grant without tasks and for filters that match nothing', async () => {
    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail({ tasks: [], pendingHighTasks: 0, canClose: true }) }])
    const { unmount } = renderTasks()
    expect(await screen.findByText('No task')).toBeTruthy()
    unmount()

    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    renderTasks()
    await screen.findByText('Verify bank details')
    fireEvent.change(screen.getByLabelText('Search by title'), { target: { value: 'zzz' } })
    expect(screen.getByText('No task matches the filters')).toBeTruthy()
  })

  it('completes a task and shows the refreshed row', async () => {
    let completed = false
    const { calls } = mockApi([
      {
        method: 'GET',
        path: `/grants/${grantId}`,
        handler: () => ({
          body: detail({
            pendingHighTasks: completed ? 0 : 1,
            canClose: completed,
            tasks: completed ? [{ ...verify, status: 'Completed', completedAtUtc: '2026-03-10T09:00:00Z', isOverdue: false, daysOverdue: 0, version: 2 }, quote, survey, eligibility] : [verify, quote, survey, eligibility],
          }),
        }),
      },
      {
        method: 'PUT',
        path: `/tasks/${verifyId}/complete`,
        handler: () => {
          completed = true
          return { body: { ...verify, status: 'Completed', completedAtUtc: '2026-03-10T09:00:00Z', version: 2 } }
        },
      },
    ])
    renderTasks()
    await screen.findByText('Verify bank details')
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'All' } })

    fireEvent.click(screen.getByRole('button', { name: 'Complete Verify bank details' }))

    await waitFor(() => expect(calls.some((call) => call.method === 'PUT')).toBe(true))
    await waitFor(() => {
      const row = screen.getByText('Verify bank details').closest('tr')!
      expect(within(row).getByText('Completed')).toBeTruthy()
    })
  })

  it('shows Completing and disables the button while the request is unresolved', async () => {
    mockApi([
      { method: 'GET', path: `/grants/${grantId}`, body: detail() },
      { method: 'PUT', path: `/tasks/${verifyId}/complete`, handler: () => new Promise(() => {}) },
    ])
    renderTasks()
    await screen.findByText('Verify bank details')

    fireEvent.click(screen.getByRole('button', { name: 'Complete Verify bank details' }))

    const button = (await screen.findByRole('button', { name: 'Complete Verify bank details' })) as HTMLButtonElement
    await waitFor(() => expect(button.textContent).toBe('Completing…'))
    expect(button.disabled).toBe(true)
  })

  it('makes Add task and every Complete button read-only on a closed grant', async () => {
    mockApi([
      {
        method: 'GET',
        path: `/grants/${grantId}`,
        body: detail({ status: 'Closed', pendingHighTasks: 0, canClose: false }),
      },
    ])
    renderTasks()
    await screen.findByText('Verify bank details')
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'All' } })

    const add = (await screen.findByRole('button', { name: 'Add task' })) as HTMLButtonElement
    expect(add.disabled).toBe(true)
    const completeButtons = screen.getAllByRole('button', { name: /^Complete / }) as HTMLButtonElement[]
    expect(completeButtons).toHaveLength(3)
    expect(completeButtons.every((button) => button.disabled)).toBe(true)
    const editButtons = screen.getAllByRole('button', { name: /^Edit / }) as HTMLButtonElement[]
    expect(editButtons).toHaveLength(3)
    expect(editButtons.every((button) => button.disabled)).toBe(true)
    expect(screen.getAllByText('This grant is closed.').length).toBeGreaterThan(0)
  })

  it('opens the edit dialog prefilled with the chosen task', async () => {
    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    renderTasks()
    await screen.findByText('Verify bank details')

    fireEvent.click(screen.getByRole('button', { name: 'Edit Verify bank details' }))

    expect(await screen.findByRole('heading', { name: 'Edit task' })).toBeTruthy()
    expect((screen.getByLabelText('Title', { exact: true }) as HTMLInputElement).value).toBe('Verify bank details')
  })

  it('explains why Close is disabled from the grant detail', async () => {
    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    renderTasks()

    const close = (await screen.findByRole('button', { name: 'Close grant GR-100001' })) as HTMLButtonElement
    expect(close.disabled).toBe(true)
    expect(screen.getByText('1 High priority task pending.')).toBeTruthy()
  })

  it('moves focus to the grant heading once the grant has loaded', async () => {
    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    renderTasks()

    const heading = await screen.findByRole('heading', { level: 1 })
    await waitFor(() => expect(document.activeElement).toBe(heading))
  })

  it('fetches the grant again when Add task is chosen', async () => {
    const { calls } = mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    renderTasks()
    await screen.findByText('Verify bank details')
    expect(calls[0].path).toBe(`/grants/${grantId}`)

    fireEvent.click(screen.getByRole('button', { name: 'Add task' }))

    await waitFor(() => expect(calls.filter((call) => call.path === `/grants/${grantId}`)).toHaveLength(2))
  })

  it('calls onBack from Back to grants', async () => {
    mockApi([{ method: 'GET', path: `/grants/${grantId}`, body: detail() }])
    const onBack = vi.fn()
    renderTasks(onBack)
    await screen.findByText('Verify bank details')

    fireEvent.click(screen.getByRole('button', { name: 'Back to grants' }))

    expect(onBack).toHaveBeenCalledOnce()
  })
})
