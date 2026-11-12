import { useRef, useState, type FormEvent } from 'react'
import { toApiError } from '../../api/apiError'
import { useAppDispatch } from '../../app/hooks'
import { signedIn } from './sessionSlice'
import { useLoginMutation } from './authApi'
import './LoginPage.css'

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function LoginPage() {
  const dispatch = useAppDispatch()
  const emailInput = useRef<HTMLInputElement>(null)
  const [email, setEmail] = useState('')
  const [fieldError, setFieldError] = useState<string | null>(null)
  const [login, { isLoading }] = useLoginMutation()

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (isLoading) {
      return
    }

    const normalizedEmail = email.trim()
    setEmail(normalizedEmail)
    setFieldError(null)

    if (!normalizedEmail) {
      setFieldError('Enter your email.')
      emailInput.current?.focus()
      return
    }

    if (!emailPattern.test(normalizedEmail)) {
      setFieldError('Enter an email in the format name@example.org.')
      emailInput.current?.focus()
      return
    }

    try {
      const user = await login({ email: normalizedEmail }).unwrap()
      dispatch(signedIn(user))
    } catch (requestError: unknown) {
      const error = toApiError(requestError)

      if (error.kind !== 'problem') {
        return
      }

      if (error.code === 'UNKNOWN_USER' && error.status === 401) {
        setFieldError(
          'No officer uses this email. Check the spelling, or ask an administrator to add you.',
        )
        emailInput.current?.focus()
        return
      }

      if (error.code === 'USER_REQUIRED' && error.status === 401) {
        setFieldError('Enter your email.')
        emailInput.current?.focus()
        return
      }

      if (error.code === 'VALIDATION_FAILED') {
        setFieldError(error.fieldErrors.email?.[0] ?? error.title)
        emailInput.current?.focus()
      }
    }
  }

  function handleEmailChange(value: string) {
    setEmail(value)
    if (fieldError) {
      setFieldError(null)
    }
  }

  return (
    <main className="login-main">
      <h1 className="login-title">Sign in</h1>

      <form className="login-form" noValidate onSubmit={handleSubmit}>
        <div className="field">
          <label className="field-label" htmlFor="email">
            Email
          </label>
          <input
            ref={emailInput}
            className="input input-lg"
            id="email"
            name="email"
            type="email"
            autoComplete="email"
            autoFocus
            spellCheck={false}
            value={email}
            disabled={isLoading}
            aria-invalid={fieldError !== null}
            aria-describedby={fieldError ? 'email-error' : undefined}
            onChange={(event) => handleEmailChange(event.target.value)}
          />
          {fieldError && (
            <p className="field-error" id="email-error" role="alert">
              {fieldError}
            </p>
          )}
        </div>

        <button
          className="btn btn-primary"
          type="submit"
          disabled={isLoading}
        >
          {isLoading ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  )
}
