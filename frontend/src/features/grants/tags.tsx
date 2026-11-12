import type { GrantListItemDto, TaskPriority, TaskStatus } from './types'

export function GrantStatusTag({ status }: { status: GrantListItemDto['status'] }) {
  return <span className={status === 'Open' ? 'tag tag-open' : 'tag tag-closed'}>{status}</span>
}

export function TaskStatusTag({ status }: { status: TaskStatus }) {
  return status === 'Completed' ? (
    <span className="tag tag-completed">Completed</span>
  ) : (
    <span className="tag tag-open">Pending</span>
  )
}

export function PriorityTag({ priority }: { priority: TaskPriority }) {
  if (priority === 'High') {
    return <span className="tag tag-high">High priority</span>
  }

  if (priority === 'Medium') {
    return <span className="tag tag-medium">Medium priority</span>
  }

  return <span className="tag">Low priority</span>
}
