import { useAppDispatch } from '../../app/hooks'
import { signedOut } from '../session/sessionSlice'
import type { UserDto } from '../session/sessionSlice'
import './MainPage.css'

interface MainPageProps {
  user: UserDto
}

export function MainPage({ user }: MainPageProps) {
  const dispatch = useAppDispatch()

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
        <h1 className="visually-hidden">Grant Task Tracker</h1>
      </main>
    </>
  )
}
