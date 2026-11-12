import { useEffect, useRef, useState } from 'react'
import { toApiError, type ApiError } from '../../api/apiError'
import { getInlineErrorMessage } from '../../api/errorMessages'
import { Reference } from '../../components/Reference'
import { TaskFormDialog } from './TaskFormDialog'
import { CloseGrantButton } from './CloseGrantButton'
import { filterAndSortTasks, formatDate, overdueLabel, type TaskFilters } from './format'
import { useCompleteTaskMutation, useGetGrantQuery } from './grantsApi'
import { GrantStatusTag, PriorityTag, TaskStatusTag } from './tags'
import type { GrantDetailDto, TaskDto } from './types'

const statusOptions: TaskFilters['status'][] = ['All', 'Pending', 'Completed']
const priorityOptions: TaskFilters['priority'][] = ['All', 'High', 'Medium', 'Low']

interface GrantTasksPageProps {
  grantId: string
  onBack(): void
}

interface TaskActionsProps {
  task: TaskDto
  grantId: string
  grantClosed: boolean
  onEdit(task: TaskDto): void
}

function TaskActions({ task, grantId, grantClosed, onEdit }: TaskActionsProps) {
  const [completeTask, { isLoading }] = useCompleteTaskMutation()
  const [refusal, setRefusal] = useState<ApiError | null>(null)
  const reasonId = `complete-reason-${task.id}`

  if (task.status === 'Completed') {
    return null
  }

  async function handleComplete() {
    setRefusal(null)
    try {
      await completeTask({ taskId: task.id, grantId }).unwrap()
    } catch (requestError: unknown) {
      const error = toApiError(requestError)
      if (error.kind === 'problem') {
        setRefusal(error)
      }
    }
  }

  return (
    <div className="table-actions">
      <button
        className="btn btn-secondary"
        type="button"
        aria-label={`Complete ${task.title}`}
        disabled={grantClosed || isLoading}
        aria-describedby={grantClosed ? reasonId : undefined}
        onClick={handleComplete}
      >
        {isLoading ? 'Completing…' : 'Complete'}
      </button>
      <button
        className="btn btn-secondary"
        type="button"
        aria-label={`Edit ${task.title}`}
        disabled={grantClosed}
        aria-describedby={grantClosed ? reasonId : undefined}
        onClick={() => onEdit(task)}
      >
        Edit
      </button>
      {grantClosed && (
        <span className="meta" id={reasonId}>
          This grant is closed.
        </span>
      )}
      {refusal && refusal.kind === 'problem' && (
        <div className="alert" role="alert">
          <p className="alert-title">{getInlineErrorMessage(refusal)}</p>
          {refusal.traceId && <Reference traceId={refusal.traceId} />}
        </div>
      )}
    </div>
  )
}

function TasksTable({
  grant,
  filters,
  onEdit,
}: {
  grant: GrantDetailDto
  filters: TaskFilters
  onEdit(task: TaskDto): void
}) {
  if (grant.tasks.length === 0) {
    return <p className="empty">No task</p>
  }

  const visible = filterAndSortTasks(grant.tasks, filters)
  if (visible.length === 0) {
    return <p className="empty">No task matches the filters</p>
  }

  const grantClosed = grant.status === 'Closed'

  return (
    <div className="table-wrap">
      <table className="table">
        <caption className="visually-hidden">Tasks for {grant.referenceNumber}</caption>
        <thead>
          <tr>
            <th scope="col">Title</th>
            <th scope="col">Priority</th>
            <th scope="col">Due date</th>
            <th scope="col">Status</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          {visible.map((task) => (
            <tr key={task.id} className={task.status === 'Completed' ? 'row-completed' : undefined}>
              <td>{task.title}</td>
              <td>
                <PriorityTag priority={task.priority} />
              </td>
              <td className="num">
                {formatDate(task.dueDate)}
                {task.isOverdue && <span className="tag tag-overdue">{overdueLabel(task.daysOverdue)}</span>}
              </td>
              <td>
                <TaskStatusTag status={task.status} />
              </td>
              <td>
                <TaskActions task={task} grantId={grant.id} grantClosed={grantClosed} onEdit={onEdit} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function GrantTasksPage({ grantId, onBack }: GrantTasksPageProps) {
  const { data: grant, isLoading, isError, error, refetch } = useGetGrantQuery(grantId, {
    refetchOnMountOrArgChange: true,
  })
  const headingRef = useRef<HTMLHeadingElement>(null)
  const headingFocused = useRef(false)
  const [filters, setFilters] = useState<TaskFilters>({ status: 'Pending', priority: 'All', search: '' })
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<TaskDto | null>(null)

  async function openAddTask() {
    await refetch()
    setAdding(true)
  }

  useEffect(() => {
    if (grant && !headingFocused.current) {
      headingRef.current?.focus()
      headingFocused.current = true
    }
  }, [grant])

  const loadError = isError ? toApiError(error) : null
  const grantMissing = loadError?.kind === 'problem' && loadError.code === 'NOT_FOUND'
  const grantClosed = grant?.status === 'Closed'

  return (
    <section>
      <div className="page-header">
        <div>
          {grant && (
            <>
              <h1 tabIndex={-1} ref={headingRef}>
                {grant.referenceNumber} · {grant.organisationName}
              </h1>
              <p className="meta">
                Applied {formatDate(grant.applicationDate)} <GrantStatusTag status={grant.status} />
              </p>
            </>
          )}
        </div>
        <div className="page-actions">
          <button className="btn btn-quiet" type="button" onClick={onBack}>
            Back to grants
          </button>
          {grant && <CloseGrantButton grant={grant} />}
          {grant && (
            <>
              <button
                className="btn btn-primary"
                type="button"
                disabled={grantClosed}
                aria-describedby={grantClosed ? 'add-task-reason' : undefined}
                onClick={openAddTask}
              >
                Add task
              </button>
              {grantClosed && (
                <p className="meta" id="add-task-reason">
                  This grant is closed.
                </p>
              )}
            </>
          )}
        </div>
      </div>

      {isLoading && <p role="status">Loading tasks…</p>}
      {grantMissing && <p role="alert">This grant no longer exists.</p>}

      {grant && (
        <div>
          <div className="filter-bar">
            <div className="field">
              <label className="field-label" htmlFor="task-status-filter">
                Status
              </label>
              <select
                id="task-status-filter"
                className="select"
                value={filters.status}
                onChange={(event) => {
                  const next = statusOptions.find((option) => option === event.target.value)
                  if (next) {
                    setFilters((current) => ({ ...current, status: next }))
                  }
                }}
              >
                {statusOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label className="field-label" htmlFor="task-priority-filter">
                Priority
              </label>
              <select
                id="task-priority-filter"
                className="select"
                value={filters.priority}
                onChange={(event) => {
                  const next = priorityOptions.find((option) => option === event.target.value)
                  if (next) {
                    setFilters((current) => ({ ...current, priority: next }))
                  }
                }}
              >
                {priorityOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label className="field-label" htmlFor="task-search">
                Search by title
              </label>
              <input
                id="task-search"
                className="input"
                type="search"
                value={filters.search}
                onChange={(event) => setFilters((current) => ({ ...current, search: event.target.value }))}
              />
            </div>
          </div>

          <TasksTable grant={grant} filters={filters} onEdit={setEditing} />
        </div>
      )}

      {grant && (
        <TaskFormDialog
          open={adding}
          mode="add"
          grant={{ id: grant.id, applicationDate: grant.applicationDate, today: grant.today }}
          onClose={() => setAdding(false)}
        />
      )}
      {grant && editing && (
        <TaskFormDialog
          open
          mode="edit"
          task={editing}
          grant={{ id: grant.id, applicationDate: grant.applicationDate, today: grant.today }}
          onClose={() => setEditing(null)}
        />
      )}
    </section>
  )
}
