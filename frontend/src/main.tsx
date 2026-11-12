import '@fontsource-variable/atkinson-hyperlegible-next'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { Toaster } from 'sonner'
import App from './App'
import { store } from './app/store'
import { ErrorBoundary } from './components/ErrorBoundary'
import './styles/theme.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provider store={store}>
      <ErrorBoundary>
        <App />
        <Toaster
          position="bottom-right"
          visibleToasts={3}
          closeButton
          theme="light"
          toastOptions={{
            unstyled: true,
            classNames: {
              toast: 'toast',
              title: 'toast-title',
              description: 'toast-description',
              error: 'toast-error',
              success: 'toast-success',
              closeButton: 'toast-close',
            },
          }}
        />
      </ErrorBoundary>
    </Provider>
  </StrictMode>,
)
