import { useState } from 'react'
import { useAppDispatch } from '../../app/hooks'
import { GrantsPage } from '../grants/GrantsPage'
import { GrantTasksPage } from '../grants/GrantTasksPage'
import type { GrantStatusFilter } from '../grants/types'
import { signedOut } from '../session/sessionSlice'
import type { UserDto } from '../session/sessionSlice'
import './MainPage.css'

interface MainPageProps {
  user: UserDto
}

type MainView = { kind: 'list' } | { kind: 'tasks'; grantId: string }

export function MainPage({ user }: MainPageProps) {
  const dispatch = useAppDispatch()
  const [view, setView] = useState<MainView>({ kind: 'list' })
  const [statusFilter, setStatusFilter] = useState<GrantStatusFilter>('Open')
  const [returnFocusGrantId, setReturnFocusGrantId] = useState<string>()

  return (
    <>
      <header className="main-header">
        <div className="main-header-inner">
          <span className="wordmark">
            <span className="wordmark-mark" aria-hidden="true" />
            Grant Task Tracker
          </span>
          <div className="main-account">
            <span>Signed in as {user.name}</span>
            <button
              className="btn btn-quiet"
              type="button"
              onClick={() => dispatch(signedOut())}
            >
              Sign out
            </button>
          </div>
        </div>
      </header>
      <main className="main-content">
        <div className="page">
          {view.kind === 'list' ? (
            <>
              <h1 className="visually-hidden">Grant Task Tracker</h1>
              <GrantsPage
                statusFilter={statusFilter}
                onStatusFilterChange={setStatusFilter}
                onOpenTasks={(grantId) => setView({ kind: 'tasks', grantId })}
                focusGrantId={returnFocusGrantId}
              />
            </>
          ) : (
            <GrantTasksPage
              grantId={view.grantId}
              onBack={() => {
                setReturnFocusGrantId(view.grantId)
                setView({ kind: 'list' })
              }}
            />
          )}
        </div>
      </main>
    </>
  )
}
