import { useEffect, useRef, useState, type FormEvent } from 'react'
import { toApiError, type ApiError } from '../../api/apiError'
import { getInlineErrorMessage } from '../../api/errorMessages'
import { Reference } from '../../components/Reference'
import { formatDate, minimumDueDate } from './format'
import { useAddTaskMutation, useEditTaskMutation } from './grantsApi'
import type { TaskDto, TaskPriority } from './types'

const priorityOptions: TaskPriority[] = ['High', 'Medium', 'Low']
const titleMaxLength = 100

type FieldName = 'title' | 'dueDate' | 'priority'
type FieldErrors = Partial<Record<FieldName, string>>

interface GrantRef {
  id: string
  applicationDate: string
  today: string
}

export type TaskFormDialogProps = {
  open: boolean
  grant: GrantRef
  onClose(): void
} & ({ mode: 'add'; task?: undefined } | { mode: 'edit'; task: TaskDto })

interface TaskFormProps {
  mode: 'add' | 'edit'
  grant: GrantRef
  task?: TaskDto
  onCancel(): void
  onSaved(): void
}

function TaskForm({ mode, grant, task, onCancel, onSaved }: TaskFormProps) {
  const [addTask, addState] = useAddTaskMutation()
  const [editTask, editState] = useEditTaskMutation()
  const isLoading = mode === 'add' ? addState.isLoading : editState.isLoading
  const [version] = useState(task?.version)
  const [title, setTitle] = useState(task?.title ?? '')
  const [dueDate, setDueDate] = useState(task?.dueDate ?? '')
  const [priority, setPriority] = useState<TaskPriority>(task?.priority ?? 'Medium')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [alertError, setAlertError] = useState<ApiError | null>(null)
  const keyRef = useRef(crypto.randomUUID())
  const titleRef = useRef<HTMLInputElement>(null)
  const dueDateRef = useRef<HTMLInputElement>(null)
  const priorityRef = useRef<HTMLSelectElement>(null)
  const minDate = minimumDueDate(grant.today, grant.applicationDate)
  // Edit mode enforces the date minimum only when the officer moves the date.
  const dueMin = mode === 'add' || dueDate !== task?.dueDate ? minDate : undefined

  function fieldChanged(field: FieldName) {
    keyRef.current = crypto.randomUUID()
    setFieldErrors((current) => ({ ...current, [field]: undefined }))
  }

  function focusFirstError(errors: FieldErrors) {
    const first = (['title', 'dueDate', 'priority'] as const).find((field) => errors[field])
    if (first === 'title') {
      titleRef.current?.focus()
    } else if (first === 'dueDate') {
      dueDateRef.current?.focus()
    } else if (first === 'priority') {
      priorityRef.current?.focus()
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (isLoading) {
      return
    }

    setAlertError(null)
    const errors: FieldErrors = {}
    const trimmedTitle = title.trim()
    if (!trimmedTitle) {
      errors.title = 'Enter a task title.'
    } else if (trimmedTitle.length > titleMaxLength) {
      errors.title = `The title must be ${titleMaxLength} characters or fewer.`
    }
    if (!dueDate) {
      errors.dueDate = 'Enter a due date.'
    } else if (dueMin && dueDate < dueMin) {
      errors.dueDate = `The due date can't be before ${formatDate(minDate)}.`
    }
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      focusFirstError(errors)
      return
    }

    const values = { title: trimmedTitle, dueDate, priority }
    try {
      if (mode === 'add') {
        await addTask({ grantId: grant.id, task: values, idempotencyKey: keyRef.current }).unwrap()
      } else if (task && version !== undefined) {
        await editTask({
          taskId: task.id,
          grantId: grant.id,
          version,
          task: values,
          idempotencyKey: keyRef.current,
        }).unwrap()
      }
      onSaved()
    } catch (requestError: unknown) {
      const error = toApiError(requestError)
      if (error.kind === 'unreachable') {
        setAlertError(error)
        return
      }

      if (error.kind !== 'problem') {
        return
      }

      if (error.status === 400) {
        const serverErrors: FieldErrors = {
          title: error.fieldErrors.title?.[0],
          dueDate: error.fieldErrors.duedate?.[0],
          priority: error.fieldErrors.priority?.[0],
        }
        setFieldErrors(serverErrors)
        focusFirstError(serverErrors)
        return
      }

      if (error.status === 409 || error.status === 412 || error.status === 422) {
        setAlertError(error)
        if (error.status === 422) {
          keyRef.current = crypto.randomUUID()
        }
      }
    }
  }

  const isAdd = mode === 'add'

  return (
    <form className="dialog-form" noValidate onSubmit={handleSubmit}>
      <h2 className="dialog-title" id="task-form-title">
        {isAdd ? 'Add task' : 'Edit task'}
      </h2>

      {alertError && (
        <div className="alert" role="alert">
          <p className="alert-title">{getInlineErrorMessage(alertError)}</p>
          {alertError.kind === 'problem' && alertError.traceId && <Reference traceId={alertError.traceId} />}
        </div>
      )}

      <div className="field">
        <label className="field-label" htmlFor="task-form-title-input">
          Title
        </label>
        <input
          ref={titleRef}
          className="input"
          id="task-form-title-input"
          name="title"
          value={title}
          aria-invalid={fieldErrors.title !== undefined}
          aria-describedby={fieldErrors.title ? 'task-form-title-error' : 'task-form-title-hint'}
          onChange={(event) => {
            setTitle(event.target.value)
            fieldChanged('title')
          }}
        />
        <p className="field-hint" id="task-form-title-hint">
          Up to 100 characters.
        </p>
        {fieldErrors.title && (
          <p className="field-error" id="task-form-title-error" role="alert">
            {fieldErrors.title}
          </p>
        )}
      </div>

      <div className="field">
        <label className="field-label" htmlFor="task-form-priority">
          Priority
        </label>
        <select
          ref={priorityRef}
          className="select"
          id="task-form-priority"
          name="priority"
          value={priority}
          aria-invalid={fieldErrors.priority !== undefined}
          aria-describedby={fieldErrors.priority ? 'task-form-priority-error' : undefined}
          onChange={(event) => {
            const next = priorityOptions.find((option) => option === event.target.value)
            if (next) {
              setPriority(next)
              fieldChanged('priority')
            }
          }}
        >
          {priorityOptions.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
        {fieldErrors.priority && (
          <p className="field-error" id="task-form-priority-error" role="alert">
            {fieldErrors.priority}
          </p>
        )}
      </div>

      <div className="field">
        <label className="field-label" htmlFor="task-form-due-date">
          Due date
        </label>
        <input
          ref={dueDateRef}
          className="input"
          id="task-form-due-date"
          name="dueDate"
          type="date"
          min={dueMin}
          value={dueDate}
          aria-invalid={fieldErrors.dueDate !== undefined}
          aria-describedby={fieldErrors.dueDate ? 'task-form-due-error' : undefined}
          onChange={(event) => {
            setDueDate(event.target.value)
            fieldChanged('dueDate')
          }}
        />
        {fieldErrors.dueDate && (
          <p className="field-error" id="task-form-due-error" role="alert">
            {fieldErrors.dueDate}
          </p>
        )}
      </div>

      <div className="dialog-actions">
        <button className="btn btn-secondary" type="button" onClick={onCancel}>
          Cancel
        </button>
        <button className="btn btn-primary" type="submit" disabled={isLoading}>
          {isAdd
            ? isLoading
              ? 'Adding task…'
              : 'Add task'
            : isLoading
              ? 'Saving…'
              : 'Save changes'}
        </button>
      </div>
    </form>
  )
}

export function TaskFormDialog({ open, mode, grant, task, onClose }: TaskFormDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) {
      return
    }

    if (open && !dialog.open) {
      dialog.showModal()
    }
    if (!open && dialog.open) {
      dialog.close()
    }
  }, [open])

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) {
      return
    }

    const handleClose = () => onClose()
    dialog.addEventListener('close', handleClose)
    return () => dialog.removeEventListener('close', handleClose)
  }, [onClose])

  return (
    <dialog className="dialog" ref={dialogRef} aria-labelledby="task-form-title">
      {open && (
        <TaskForm
          mode={mode}
          grant={grant}
          task={task}
          onCancel={() => dialogRef.current?.close()}
          onSaved={() => dialogRef.current?.close()}
        />
      )}
    </dialog>
  )
}
