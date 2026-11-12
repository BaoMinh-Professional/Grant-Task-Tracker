import { useState } from 'react'
import { toApiError, type ApiError } from '../../api/apiError'
import { getInlineErrorMessage } from '../../api/errorMessages'
import { Reference } from '../../components/Reference'
import { useCloseGrantMutation } from './grantsApi'
import type { GrantListItemDto } from './types'

interface CloseGrantButtonProps {
  grant: Pick<GrantListItemDto, 'id' | 'referenceNumber' | 'status' | 'canClose' | 'pendingHighTasks'>
}

function closeReason(grant: CloseGrantButtonProps['grant']): string | null {
  if (grant.canClose) {
    return null
  }

  if (grant.status === 'Closed') {
    return 'Already closed.'
  }

  const count = grant.pendingHighTasks
  return `${count} High priority ${count === 1 ? 'task' : 'tasks'} pending.`
}

export function CloseGrantButton({ grant }: CloseGrantButtonProps) {
  const [closeGrant, { isLoading }] = useCloseGrantMutation()
  const [refusal, setRefusal] = useState<ApiError | null>(null)
  const reason = closeReason(grant)
  const reasonId = `close-reason-${grant.id}`

  async function handleClick() {
    setRefusal(null)
    try {
      await closeGrant(grant.id).unwrap()
    } catch (requestError: unknown) {
      const error = toApiError(requestError)
      if (error.kind === 'problem') {
        setRefusal(error)
      }
    }
  }

  return (
    <>
      <button
        className="btn btn-secondary"
        type="button"
        disabled={!grant.canClose || isLoading}
        aria-label={`Close grant ${grant.referenceNumber}`}
        aria-describedby={reason ? reasonId : undefined}
        onClick={handleClick}
      >
        {isLoading ? 'Closing…' : 'Close grant'}
      </button>
      {reason && (
        <p className="meta" id={reasonId}>
          {reason}
        </p>
      )}
      {refusal && refusal.kind === 'problem' && (
        <div className="alert" role="alert">
          <p className="alert-title">{getInlineErrorMessage(refusal)}</p>
          {refusal.traceId && <Reference traceId={refusal.traceId} />}
        </div>
      )}
    </>
  )
}
