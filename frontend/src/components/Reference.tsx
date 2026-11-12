import { useState } from 'react'
import { shortReference } from '../api/apiError'

interface ReferenceProps {
  traceId: string
}

export function Reference({ traceId }: ReferenceProps) {
  const [copyState, setCopyState] = useState<'idle' | 'copied' | 'failed'>('idle')

  async function copyReference() {
    try {
      if (!navigator.clipboard) {
        throw new Error('Clipboard access is unavailable.')
      }

      await navigator.clipboard.writeText(traceId)
      setCopyState('copied')
    } catch {
      setCopyState('failed')
    }
  }

  return (
    <div className="reference">
      <span>Reference</span>
      <code className="reference-code num">{shortReference(traceId)}</code>
      <button className="btn btn-quiet" type="button" onClick={copyReference}>
        {copyState === 'copied' ? 'Copied' : 'Copy full reference'}
      </button>
      {copyState === 'failed' && (
        <span className="alert-title" role="alert">
          Could not copy. Select this reference: <code>{traceId}</code>
        </span>
      )}
    </div>
  )
}
