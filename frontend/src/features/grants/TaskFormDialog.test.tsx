import { fireEvent, screen, waitFor } from '@testing-library/react'
import { Provider } from 'react-redux'
import { describe, expect, it, vi } from 'vitest'
import { store } from '../../app/store'
import { notify } from '../../components/notify'
import { mockApi } from '../../test/mockApi'
import { renderWithStore } from '../../test/renderWithStore'
import { TaskFormDialog } from './TaskFormDialog'
import type { TaskDto } from './types'

const grantId = '00000000-0000-0000-0000-000000000101'
const taskId = '00000000-0000-0000-0000-000000000201'
const grant = { id: grantId, applicationDate: '2026-03-15', today: '2026-03-10' }
const oldGrant = { id: grantId, applicationDate: '2026-01-09', today: '2026-03-10' }
const createdTask = {
  id: '00000000-0000-0000-0000-000000000301',
  grantId,
  title: 'Call applicant',
  priority: 'High',
  dueDate: '2026-03-20',
  status: 'Pending',
  completedAtUtc: null,
  isOverdue: false,
  daysOverdue: 0,
  version: 1,
}
const verify: TaskDto = {
  id: taskId,
  grantId,
  title: 'Verify bank details',
  priority: 'High',
  dueDate: '2026-03-07',
  status: 'Pending',
  completedAtUtc: null,
  isOverdue: true,
  daysOverdue: 3,
  version: 1,
}

function fill(title: string, dueDate: string, priority?: string) {
  fireEvent.change(screen.getByLabelText('Title', { exact: true }), { target: { value: title } })
  fireEvent.change(screen.getByLabelText('Due date'), { target: { value: dueDate } })
  if (priority) {
    fireEvent.change(screen.getByLabelText('Priority'), { target: { value: priority } })
  }
}

function submit() {
  fireEvent.click(screen.getByRole('button', { name: /^(Add task|Save changes)$/ }))
}

function renderAdd(onClose = vi.fn(), open = true) {
  return renderWithStore(<TaskFormDialog open={open} mode="add" grant={grant} onClose={onClose} />)
}

function renderEdit(onClose = vi.fn(), dialogGrant = oldGrant) {
  return renderWithStore(<TaskFormDialog open mode="edit" task={verify} grant={dialogGrant} onClose={onClose} />)
}

describe('TaskFormDialog (add)', () => {
  it('sets the due date minimum to the later of today and the application date', () => {
    renderAdd()
    expect((screen.getByLabelText('Due date') as HTMLInputElement).min).toBe('2026-03-15')
  })

  it('reports blank and too-long input without sending a request', () => {
    const { calls } = mockApi([])
    renderAdd()

    submit()
    expect(screen.getByText('Enter a task title.')).toBeTruthy()
    expect(screen.getByText('Enter a due date.')).toBeTruthy()

    fill('x'.repeat(101), '2026-03-20')
    submit()
    expect(screen.getByText('The title must be 100 characters or fewer.')).toBeTruthy()
    expect(calls).toHaveLength(0)
  })

  it('sends the task with an idempotency key and closes on success', async () => {
    const { calls } = mockApi([
      { method: 'POST', path: `/grants/${grantId}/tasks`, status: 201, body: createdTask },
    ])
    const onClose = vi.fn()
    renderAdd(onClose)

    fill('Call applicant', '2026-03-20', 'High')
    submit()

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(calls[0].body).toEqual({ title: 'Call applicant', dueDate: '2026-03-20', priority: 'High' })
    expect(calls[0].headers.get('Idempotency-Key')).toBeTruthy()
  })

  it('reuses the key for an unchanged retry and replaces it after an edit', async () => {
    const { calls } = mockApi([
      {
        method: 'POST',
        path: `/grants/${grantId}/tasks`,
        status: 400,
        body: { code: 'BR-2', title: 'The task is invalid.', errors: { dueDate: ["The due date can't be in the past."] } },
      },
    ])
    renderAdd()

    fill('Call applicant', '2026-03-20')
    submit()
    await waitFor(() => expect(calls).toHaveLength(1))
    submit()
    await waitFor(() => expect(calls).toHaveLength(2))
    expect(calls[1].headers.get('Idempotency-Key')).toBe(calls[0].headers.get('Idempotency-Key'))

    fireEvent.change(screen.getByLabelText('Title', { exact: true }), { target: { value: 'Call the applicant' } })
    submit()
    await waitFor(() => expect(calls).toHaveLength(3))
    expect(calls[2].headers.get('Idempotency-Key')).not.toBe(calls[0].headers.get('Idempotency-Key'))
  })

  it('shows a clear message when the server is unreachable, then retries with the same key', async () => {
    let attempt = 0
    const { calls } = mockApi([
      {
        method: 'POST',
        path: `/grants/${grantId}/tasks`,
        handler: () => {
          attempt += 1
          if (attempt === 1) {
            throw new TypeError('Failed to fetch')
          }
          return { status: 201, body: createdTask }
        },
      },
    ])
    const onClose = vi.fn()
    renderAdd(onClose)

    fill('Call applicant', '2026-03-20', 'High')
    submit()

    expect(await screen.findByText(/Can't reach the server/)).toBeTruthy()
    expect(onClose).not.toHaveBeenCalled()

    submit()
    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(calls).toHaveLength(2)
    expect(calls[1].headers.get('Idempotency-Key')).toBe(calls[0].headers.get('Idempotency-Key'))
  })

  it('shows a server due-date refusal beside the field without a toast', async () => {
    const errorToast = vi.spyOn(notify, 'error')
    mockApi([
      {
        method: 'POST',
        path: `/grants/${grantId}/tasks`,
        status: 400,
        body: { code: 'BR-2', title: 'The task is invalid.', errors: { dueDate: ["The due date can't be in the past."] } },
      },
    ])
    renderAdd()

    fill('Call applicant', '2026-03-20')
    submit()

    const message = await screen.findByText("The due date can't be in the past.")
    expect(message.id).toBe('task-form-due-error')
    expect(errorToast).not.toHaveBeenCalled()
  })

  it('shows a closed-grant refusal as an alert inside the dialog', async () => {
    mockApi([
      {
        method: 'POST',
        path: `/grants/${grantId}/tasks`,
        status: 409,
        body: { code: 'BR-1', title: "This grant is closed, so tasks can't be added." },
      },
    ])
    renderAdd()

    fill('Call applicant', '2026-03-20')
    submit()

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain("This grant is closed, so tasks can't be added.")
  })

  it('explains a key mismatch and uses a new key on the next submit', async () => {
    const { calls } = mockApi([
      {
        method: 'POST',
        path: `/grants/${grantId}/tasks`,
        status: 422,
        body: { code: 'IDEMPOTENCY_KEY_MISMATCH', title: 'The key was used for another request.' },
      },
    ])
    renderAdd()

    fill('Call applicant', '2026-03-20')
    submit()

    expect(
      await screen.findByText(
        "This request doesn't match your earlier attempt. Check the details, then select Add task again.",
      ),
    ).toBeTruthy()
    submit()
    await waitFor(() => expect(calls).toHaveLength(2))
    expect(calls[1].headers.get('Idempotency-Key')).not.toBe(calls[0].headers.get('Idempotency-Key'))
  })

  it('shows Adding task and disables the button while the request is unresolved', async () => {
    mockApi([{ method: 'POST', path: `/grants/${grantId}/tasks`, handler: () => new Promise(() => {}) }])
    renderAdd()

    fill('Call applicant', '2026-03-20')
    submit()

    const button = (await screen.findByRole('button', { name: 'Adding task…' })) as HTMLButtonElement
    expect(button.disabled).toBe(true)
  })

  it('clears values and errors when the dialog is opened again', () => {
    mockApi([])
    const onClose = vi.fn()
    const view = renderAdd(onClose)

    fill('Draft title', '')
    submit()
    expect(screen.getByText('Enter a due date.')).toBeTruthy()

    view.rerender(
      <Provider store={store}>
        <TaskFormDialog open={false} mode="add" grant={grant} onClose={onClose} />
      </Provider>,
    )
    view.rerender(
      <Provider store={store}>
        <TaskFormDialog open mode="add" grant={grant} onClose={onClose} />
      </Provider>,
    )

    expect((screen.getByLabelText('Title', { exact: true }) as HTMLInputElement).value).toBe('')
    expect(screen.queryByText('Enter a due date.')).toBeNull()
  })
})

describe('TaskFormDialog (edit)', () => {
  it('prefills the task and labels the form as an edit', () => {
    renderEdit()
    expect(screen.getByRole('heading', { name: 'Edit task' })).toBeTruthy()
    expect((screen.getByLabelText('Title', { exact: true }) as HTMLInputElement).value).toBe('Verify bank details')
    expect((screen.getByLabelText('Priority') as HTMLSelectElement).value).toBe('High')
    expect((screen.getByLabelText('Due date') as HTMLInputElement).value).toBe('2026-03-07')
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeTruthy()
  })

  it('sends If-Match with the task version and an idempotency key, then closes', async () => {
    const { calls } = mockApi([
      {
        method: 'PUT',
        path: `/tasks/${taskId}`,
        body: { ...verify, title: 'Renamed', version: 2 },
      },
    ])
    const onClose = vi.fn()
    renderEdit(onClose)

    fireEvent.change(screen.getByLabelText('Title', { exact: true }), { target: { value: 'Renamed' } })
    submit()

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(calls[0].method).toBe('PUT')
    expect(calls[0].headers.get('If-Match')).toBe('"1"')
    expect(calls[0].headers.get('Idempotency-Key')).toBeTruthy()
    expect(calls[0].body).toEqual({ title: 'Renamed', dueDate: '2026-03-07', priority: 'High' })
  })

  it('does not apply the date minimum while the due date is unchanged', async () => {
    const { calls } = mockApi([{ method: 'PUT', path: `/tasks/${taskId}`, body: { ...verify, version: 2 } }])
    renderEdit()

    expect((screen.getByLabelText('Due date') as HTMLInputElement).min).toBe('')
    submit()

    await waitFor(() => expect(calls).toHaveLength(1))
  })

  it('refuses a moved due date that is before today on the client', () => {
    const { calls } = mockApi([])
    renderEdit()

    fireEvent.change(screen.getByLabelText('Due date'), { target: { value: '2026-03-01' } })
    submit()

    expect(screen.getByText("The due date can't be before 10-Mar-2026.")).toBeTruthy()
    expect(calls).toHaveLength(0)
  })

  it('shows the version conflict inside the dialog after a 412', async () => {
    mockApi([
      {
        method: 'PUT',
        path: `/tasks/${taskId}`,
        status: 412,
        body: {
          code: 'VERSION_CONFLICT',
          title:
            'This task was changed by someone else before your edit was saved. Reload to see the latest version, then edit again.',
        },
      },
    ])
    const onClose = vi.fn()
    renderEdit(onClose)

    fireEvent.change(screen.getByLabelText('Title', { exact: true }), { target: { value: 'Renamed' } })
    submit()

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('This task was changed by someone else before your edit was saved.')
    expect(onClose).not.toHaveBeenCalled()
  })

  it('shows Saving while the request is unresolved', async () => {
    mockApi([{ method: 'PUT', path: `/tasks/${taskId}`, handler: () => new Promise(() => {}) }])
    renderEdit()

    fireEvent.change(screen.getByLabelText('Title', { exact: true }), { target: { value: 'Renamed' } })
    submit()

    const button = (await screen.findByRole('button', { name: 'Saving…' })) as HTMLButtonElement
    expect(button.disabled).toBe(true)
  })
})
