import { createSlice, type PayloadAction } from '@reduxjs/toolkit'

interface ConnectionState {
  serverUnreachable: boolean
}

const initialState: ConnectionState = {
  serverUnreachable: false,
}

const connectionSlice = createSlice({
  name: 'connection',
  initialState,
  reducers: {
    setServerUnreachable(state, action: PayloadAction<boolean>) {
      state.serverUnreachable = action.payload
    },
  },
})

export const { setServerUnreachable } = connectionSlice.actions
export const connectionReducer = connectionSlice.reducer
