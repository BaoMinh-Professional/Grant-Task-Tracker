import {
  configureStore,
  createListenerMiddleware,
  type TypedStartListening,
} from '@reduxjs/toolkit'
import { setupListeners } from '@reduxjs/toolkit/query'
import { baseApi } from '../api/baseApi'
import { registerErrorListeners } from '../api/errorListener'
import { notify } from '../components/notify'
import { connectionReducer } from '../features/connection/connectionSlice'
import {
  sessionReducer,
  sessionStorageKey,
  signedIn,
  signedOut,
} from '../features/session/sessionSlice'

const listenerMiddleware = createListenerMiddleware()

export const store = configureStore({
  reducer: {
    [baseApi.reducerPath]: baseApi.reducer,
    connection: connectionReducer,
    session: sessionReducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware().concat(
      baseApi.middleware,
      listenerMiddleware.middleware,
    ),
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
export type AppStartListening = TypedStartListening<RootState, AppDispatch>

const startAppListening =
  listenerMiddleware.startListening.withTypes<RootState, AppDispatch>()

registerErrorListeners(startAppListening)
setupListeners(store.dispatch)

startAppListening({
  actionCreator: signedIn,
  effect: (action) => {
    try {
      window.localStorage.setItem(
        sessionStorageKey,
        JSON.stringify(action.payload),
      )
    } catch (error) {
      console.error('Unable to save the sign-in in this browser.', error)
      notify.error({
        id: 'session:save',
        title:
          "This browser couldn't save your sign-in. You'll need to sign in again after reloading.",
      })
    }
  },
})

startAppListening({
  actionCreator: signedOut,
  effect: (_action, listenerApi) => {
    try {
      window.localStorage.removeItem(sessionStorageKey)
    } catch (error) {
      console.error('Unable to clear the saved sign-in in this browser.', error)
      notify.error({
        id: 'session:clear',
        title:
          "This browser couldn't clear the saved sign-in. Clear its saved data before another officer uses this device.",
      })
    }

    listenerApi.dispatch(baseApi.util.resetApiState())
  },
})
