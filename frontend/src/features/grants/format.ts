import type { TaskDto, TaskPriority, TaskStatus } from './types'

const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

export function formatDate(iso: string): string {
  const [year, month, day] = iso.split('-')
  return `${day}-${months[Number(month) - 1]}-${year}`
}

export function overdueLabel(days: number): string {
  return `Overdue by ${days} ${days === 1 ? 'day' : 'days'}`
}

export function minimumDueDate(today: string, applicationDate: string): string {
  return today > applicationDate ? today : applicationDate
}

export interface TaskFilters {
  status: 'All' | TaskStatus
  priority: 'All' | TaskPriority
  search: string
}

const statusOrder: Record<TaskStatus, number> = { Pending: 0, Completed: 1 }
const priorityOrder: Record<TaskPriority, number> = { High: 0, Medium: 1, Low: 2 }

export function filterAndSortTasks(tasks: TaskDto[], filters: TaskFilters): TaskDto[] {
  const search = filters.search.trim().toLowerCase()

  return tasks
    .filter((task) => filters.status === 'All' || task.status === filters.status)
    .filter((task) => filters.priority === 'All' || task.priority === filters.priority)
    .filter((task) => task.title.toLowerCase().includes(search))
    .sort(
      (a, b) =>
        statusOrder[a.status] - statusOrder[b.status] ||
        priorityOrder[a.priority] - priorityOrder[b.priority] ||
        (a.dueDate < b.dueDate ? -1 : a.dueDate > b.dueDate ? 1 : 0) ||
        a.title.localeCompare(b.title),
    )
}
