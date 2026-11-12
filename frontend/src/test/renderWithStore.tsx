import type { ReactElement } from 'react'
import { render } from '@testing-library/react'
import { Provider } from 'react-redux'
import { baseApi } from '../api/baseApi'
import { store } from '../app/store'
import { signedIn, type UserDto } from '../features/session/sessionSlice'

export const testUser: UserDto = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Alex Nguyen',
  email: 'alex.nguyen@example.org',
}

export function signInTestUser() {
  store.dispatch(baseApi.util.resetApiState())
  store.dispatch(signedIn(testUser))
}

export function renderWithStore(ui: ReactElement) {
  signInTestUser()
  return render(<Provider store={store}>{ui}</Provider>)
}
