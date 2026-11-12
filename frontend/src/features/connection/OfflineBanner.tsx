import { useAppSelector } from '../../app/hooks'

export function OfflineBanner() {
  const serverUnreachable = useAppSelector(
    (state) => state.connection.serverUnreachable,
  )

  if (!serverUnreachable) {
    return null
  }

  return (
    <div className="banner" role="alert">
      <p className="banner-text">
        <strong>Can&apos;t reach the server.</strong> Check your connection, then
        try again.
      </p>
    </div>
  )
}
