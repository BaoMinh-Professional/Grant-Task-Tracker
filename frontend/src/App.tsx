import { useAppSelector } from './app/hooks'
import { OfflineBanner } from './features/connection/OfflineBanner'
import { MainPage } from './features/main/MainPage'
import { LoginPage } from './features/session/LoginPage'

function App() {
  const user = useAppSelector((state) => state.session.user)

  if (user) {
    return (
      <>
        <OfflineBanner />
        <MainPage user={user} />
      </>
    )
  }

  return (
    <div className="login-page">
      <OfflineBanner />
      <LoginPage />
    </div>
  )
}

export default App
