import { useEffect, useRef } from 'react'
import { toApiError } from '../../api/apiError'
import { CloseGrantButton } from './CloseGrantButton'
import { formatDate } from './format'
import { useGetGrantsQuery } from './grantsApi'
import { GrantStatusTag } from './tags'
import type { GrantStatusFilter } from './types'

const statusOptions: GrantStatusFilter[] = ['All', 'Open', 'Closed']

interface GrantsPageProps {
  statusFilter: GrantStatusFilter
  onStatusFilterChange(status: GrantStatusFilter): void
  onOpenTasks(grantId: string): void
  focusGrantId?: string
}

export function GrantsPage({ statusFilter, onStatusFilterChange, onOpenTasks, focusGrantId }: GrantsPageProps) {
  const { data: grants, isLoading, isError, error } = useGetGrantsQuery(statusFilter)
  const pageRef = useRef<HTMLDivElement>(null)
  const focusDone = useRef(false)

  useEffect(() => {
    if (!focusGrantId || !grants || focusDone.current) {
      return
    }

    const button = pageRef.current?.querySelector<HTMLButtonElement>(`[data-grant-id="${focusGrantId}"]`)
    if (button) {
      button.focus()
      focusDone.current = true
    }
  }, [grants, focusGrantId])

  const loadFailed = isError && toApiError(error).kind !== 'unreachable'

  return (
    <div ref={pageRef}>
      <div className="filter-bar">
        <div className="field">
          <label className="field-label" htmlFor="grant-status-filter">
            Status
          </label>
          <select
            id="grant-status-filter"
            className="select"
            value={statusFilter}
            onChange={(event) => {
              const next = statusOptions.find((option) => option === event.target.value)
              if (next) {
                onStatusFilterChange(next)
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
      </div>

      {isLoading && <p role="status">Loading grants…</p>}
      {loadFailed && <p role="alert">Grants couldn&apos;t be loaded.</p>}
      {grants && grants.length === 0 && <p className="empty">No grants match this filter.</p>}

      {grants && grants.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <caption className="visually-hidden">Grants</caption>
            <thead>
              <tr>
                <th scope="col">Reference</th>
                <th scope="col">Organisation</th>
                <th scope="col">Application date</th>
                <th scope="col">Status</th>
                <th scope="col">Tasks</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {grants.map((grant) => (
                <tr key={grant.id}>
                  <td>{grant.referenceNumber}</td>
                  <td>{grant.organisationName}</td>
                  <td className="num">{formatDate(grant.applicationDate)}</td>
                  <td>
                    <GrantStatusTag status={grant.status} />
                  </td>
                  <td className="num">
                    {grant.completedTasks} / {grant.totalTasks} completed
                  </td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="btn btn-secondary"
                        type="button"
                        data-grant-id={grant.id}
                        aria-label={`Tasks for ${grant.referenceNumber}`}
                        onClick={() => onOpenTasks(grant.id)}
                      >
                        Tasks
                      </button>
                      <CloseGrantButton grant={grant} />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
