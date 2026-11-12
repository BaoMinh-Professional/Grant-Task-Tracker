import { createSlice, type PayloadAction } from '@reduxjs/toolkit'

export interface UserDto {
  id: string
  name: string
  email: string
}

interface SessionState {
  user: UserDto | null
}

export const sessionStorageKey = 'grant-tracker.user'

function isUserDto(value: unknown): value is UserDto {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return false
  }

  return (
    'id' in value &&
    typeof value.id === 'string' &&
    value.id.length > 0 &&
    'name' in value &&
    typeof value.name === 'string' &&
    value.name.length > 0 &&
    'email' in value &&
    typeof value.email === 'string' &&
    value.email.length > 0
  )
}

function readSavedUser(): UserDto | null {
  try {
    const savedUser = window.localStorage.getItem(sessionStorageKey)
    if (savedUser === null) {
      return null
    }

    const parsed: unknown = JSON.parse(savedUser)
    return isUserDto(parsed) ? parsed : null
  } catch (error) {
    console.error('Unable to read the saved sign-in from this browser.', error)
    return null
  }
}

const initialState: SessionState = {
  user: readSavedUser(),
}

const sessionSlice = createSlice({
  name: 'session',
  initialState,
  reducers: {
    signedIn(state, action: PayloadAction<UserDto>) {
      state.user = action.payload
    },
    signedOut(state) {
      state.user = null
    },
  },
})

export const { signedIn, signedOut } = sessionSlice.actions
export const sessionReducer = sessionSlice.reducer
