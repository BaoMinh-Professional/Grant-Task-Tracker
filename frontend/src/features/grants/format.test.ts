import { describe, expect, it } from 'vitest'
import type { TaskDto } from './types'
import { filterAndSortTasks, formatDate, minimumDueDate, overdueLabel } from './format'

function task(overrides: Partial<TaskDto> & Pick<TaskDto, 'title'>): TaskDto {
  return {
    id: overrides.title,
    grantId: 'grant',
    priority: 'Low',
    dueDate: '2026-03-10',
    status: 'Pending',
    completedAtUtc: null,
    isOverdue: false,
    daysOverdue: 0,
    version: 1,
    ...overrides,
  }
}

describe('format helpers', () => {
  it('formats an ISO date without the browser time zone', () => {
    expect(formatDate('2026-03-07')).toBe('07-Mar-2026')
  })

  it('pluralises the overdue label', () => {
    expect(overdueLabel(1)).toBe('Overdue by 1 day')
    expect(overdueLabel(3)).toBe('Overdue by 3 days')
  })

  it('returns the later of today and the application date', () => {
    expect(minimumDueDate('2026-03-10', '2026-03-15')).toBe('2026-03-15')
    expect(minimumDueDate('2026-03-15', '2026-03-10')).toBe('2026-03-15')
  })
})

describe('filterAndSortTasks', () => {
  const tasks = [
    task({ title: 'Completed High', priority: 'High', status: 'Completed', dueDate: '2026-01-01' }),
    task({ title: 'Pending Low', priority: 'Low', dueDate: '2026-03-01' }),
    task({ title: 'Pending High late', priority: 'High', dueDate: '2026-04-01' }),
    task({ title: 'Pending High early', priority: 'High', dueDate: '2026-02-01' }),
    task({ title: 'Pending Medium', priority: 'Medium', dueDate: '2026-02-01' }),
  ]

  it('keeps only pending tasks when the status filter is Pending, in order', () => {
    const result = filterAndSortTasks(tasks, { status: 'Pending', priority: 'All', search: '' })
    expect(result.map((t) => t.title)).toEqual([
      'Pending High early',
      'Pending High late',
      'Pending Medium',
      'Pending Low',
    ])
  })

  it('places completed tasks after pending ones when all statuses are shown', () => {
    const result = filterAndSortTasks(tasks, { status: 'All', priority: 'All', search: '' })
    expect(result[result.length - 1].title).toBe('Completed High')
  })

  it('filters by priority and by a case-insensitive title search', () => {
    const high = filterAndSortTasks(tasks, { status: 'All', priority: 'High', search: '' })
    expect(high.every((t) => t.priority === 'High')).toBe(true)

    const search = filterAndSortTasks(tasks, { status: 'All', priority: 'All', search: '  PENDING LOW ' })
    expect(search.map((t) => t.title)).toEqual(['Pending Low'])
  })

  it('does not change the input array', () => {
    const copy = [...tasks]
    filterAndSortTasks(tasks, { status: 'All', priority: 'All', search: '' })
    expect(tasks).toEqual(copy)
  })
})
