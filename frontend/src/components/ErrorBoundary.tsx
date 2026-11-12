import { Component, type ErrorInfo, type ReactNode } from 'react'

interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  hasError: boolean
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false }

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true }
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Uncaught rendering error', error, errorInfo)
  }

  render() {
    if (this.state.hasError) {
      return (
        <main className="alert" role="alert">
          <h1 className="alert-title">Something went wrong on this page.</h1>
          <p>Reload the page to continue.</p>
          <button
            className="btn btn-primary"
            type="button"
            onClick={() => window.location.reload()}
          >
            Reload page
          </button>
        </main>
      )
    }

    return this.props.children
  }
}
