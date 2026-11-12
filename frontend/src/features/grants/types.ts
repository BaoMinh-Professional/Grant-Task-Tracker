export type GrantStatus = 'Open' | 'Closed'
export type GrantStatusFilter = 'All' | GrantStatus
export type TaskPriority = 'Low' | 'Medium' | 'High'
export type TaskStatus = 'Pending' | 'Completed'

export interface TaskDto {
  id: string
  grantId: string
  title: string
  priority: TaskPriority
  dueDate: string
  status: TaskStatus
  completedAtUtc: string | null
  isOverdue: boolean
  daysOverdue: number
  version: number
}

export interface GrantListItemDto {
  id: string
  referenceNumber: string
  organisationName: string
  applicationDate: string
  status: GrantStatus
  totalTasks: number
  completedTasks: number
  pendingHighTasks: number
  canClose: boolean
}

export interface GrantDetailDto {
  id: string
  referenceNumber: string
  organisationName: string
  applicationDate: string
  status: GrantStatus
  today: string
  pendingHighTasks: number
  canClose: boolean
  tasks: TaskDto[]
}

export interface NewTask {
  title: string
  dueDate: string
  priority: TaskPriority
}
