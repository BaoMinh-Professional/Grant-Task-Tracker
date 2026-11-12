import { useState } from 'react'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { notify } from '../../components/notify'
import { mockApi } from '../../test/mockApi'
import { renderWithStore } from '../../test/renderWithStore'
import { GrantsPage } from './GrantsPage'
import type { GrantListItemDto, GrantStatusFilter } from './types'

const reference1 = '00000000-0000-0000-0000-000000000101'
const reference2 = '00000000-0000-0000-0000-000000000102'
const closedId = '00000000-0000-0000-0000-000000000103'

const riverside: GrantListItemDto = {
  id: reference1,
  referenceNumber: 'GR-100001',
  organisationName: 'Riverside Community Garden Inc.',
  applicationDate: '2026-01-09',
  status: 'Open',
  totalTasks: 3,
  completedTasks: 1,
  pendingHighTasks: 1,
  canClose: false,
}

const northside: GrantListItemDto = {
  id: reference2,
  referenceNumber: 'GR-100002',
  organisationName: 'Northside Youth Sports Club',
  applicationDate: '2026-02-18',
  status: 'Open',
  totalTasks: 2,
  completedTasks: 0,
  pendingHighTasks: 0,
  canClose: true,
}

const closedGrant: GrantListItemDto = {
  id: closedId,
  referenceNumber: 'GR-100003',
  organisationName: 'Harbour Arts Collective',
  applicationDate: '2025-08-01',
  status: 'Closed',
  totalTasks: 1,
  completedTasks: 1,
  pendingHighTasks: 0,
  canClose: false,
}

function Harness({ onOpenTasks = () => {} }: { onOpenTasks?: (id: string) => void }) {
  const [status, setStatus] = useState<GrantStatusFilter>('Open')
  return <GrantsPage statusFilter={status} onStatusFilterChange={setStatus} onOpenTasks={onOpenTasks} />
}

describe('GrantsPage', () => {
  it('renders reference, organisation, date, status words and task counts', async () => {
    mockApi([{ method: 'GET', path: '/grants?status=Open', body: [riverside, northside] }])
    renderWithStore(<Harness />)

    expect(await screen.findByText('GR-100001')).toBeTruthy()
    expect(screen.getByText('Riverside Community Garden Inc.')).toBeTruthy()
    expect(screen.getByText('09-Jan-2026')).toBeTruthy()
    expect(screen.getAllByText('Open', { selector: '.tag' })).toHaveLength(2)
    expect(screen.getByText('1 / 3 completed')).toBeTruthy()
  })

  it('requests open grants by default and all grants after choosing All', async () => {
    const { calls } = mockApi([
      { method: 'GET', path: '/grants?status=Open', body: [riverside] },
      { method: 'GET', path: '/grants', body: [riverside, closedGrant] },
    ])
    renderWithStore(<Harness />)
    await screen.findByText('GR-100001')
    expect(calls[0].path).toBe('/grants?status=Open')

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'All' } })

    await waitFor(() => expect(calls.some((call) => call.path === '/grants')).toBe(true))
    expect(await screen.findByText('GR-100003')).toBeTruthy()
  })

  it('disables Close and explains a grant with a pending High task', async () => {
    mockApi([{ method: 'GET', path: '/grants?status=Open', body: [riverside] }])
    renderWithStore(<Harness />)

    const button = (await screen.findByRole('button', { name: 'Close grant GR-100001' })) as HTMLButtonElement
    expect(button.disabled).toBe(true)
    const reasonId = button.getAttribute('aria-describedby')
    expect(reasonId).toBeTruthy()
    expect(document.getElementById(reasonId!)?.textContent).toBe('1 High priority task pending.')
  })

  it('says a closed grant is already closed', async () => {
    mockApi([{ method: 'GET', path: '/grants', body: [closedGrant] }])
    renderWithStore(
      <GrantsPage statusFilter="All" onStatusFilterChange={() => {}} onOpenTasks={() => {}} />,
    )

    const button = (await screen.findByRole('button', { name: 'Close grant GR-100003' })) as HTMLButtonElement
    expect(button.disabled).toBe(true)
    expect(screen.getByText('Already closed.')).toBeTruthy()
  })

  it('closes an eligible grant and refetches the list', async () => {
    const { calls } = mockApi([
      { method: 'GET', path: '/grants?status=Open', body: [northside] },
      { method: 'PUT', path: `/grants/${reference2}/close`, body: { ...northside, status: 'Closed', canClose: false } },
    ])
    renderWithStore(<Harness />)

    fireEvent.click(await screen.findByRole('button', { name: 'Close grant GR-100002' }))

    await waitFor(() => expect(calls.some((call) => call.method === 'PUT')).toBe(true))
    await waitFor(() =>
      expect(calls.filter((call) => call.path === '/grants?status=Open')).toHaveLength(2),
    )
  })

  it('shows a refused close inline and does not raise an error toast', async () => {
    const errorToast = vi.spyOn(notify, 'error')
    mockApi([
      { method: 'GET', path: '/grants?status=Open', body: [northside] },
      {
        method: 'PUT',
        path: `/grants/${reference2}/close`,
        status: 409,
        body: {
          code: 'BR-5',
          title: "This grant can't be closed while it has High priority tasks pending.",
          status: 409,
          traceId: '0af7651916cd43dd8448eb211c80319c',
        },
      },
    ])
    renderWithStore(<Harness />)

    fireEvent.click(await screen.findByRole('button', { name: 'Close grant GR-100002' }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain(
      "This grant can't be closed while it has High priority tasks pending.",
    )
    expect(errorToast).not.toHaveBeenCalled()
  })

  it('says no grants match when the list is empty', async () => {
    mockApi([{ method: 'GET', path: '/grants?status=Open', body: [] }])
    renderWithStore(<Harness />)
    expect(await screen.findByText('No grants match this filter.')).toBeTruthy()
  })

  it('opens the tasks for the chosen grant', async () => {
    mockApi([{ method: 'GET', path: '/grants?status=Open', body: [riverside] }])
    const onOpenTasks = vi.fn()
    renderWithStore(<Harness onOpenTasks={onOpenTasks} />)

    fireEvent.click(await screen.findByRole('button', { name: 'Tasks for GR-100001' }))

    expect(onOpenTasks).toHaveBeenCalledWith(reference1)
  })
})
