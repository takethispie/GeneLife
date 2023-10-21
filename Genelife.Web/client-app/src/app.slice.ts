import { createSlice, PayloadAction } from "@reduxjs/toolkit";

interface pyramidState {
}

const initialState: pyramidState = {
};

const appSlice = createSlice({
  name: "app",
  initialState,
  reducers: {
    
  },
});

export const { AWAIT_PLAYER_INPUT, SET_CURRENT_PLAYER, CONTINUE } = appSlice.actions;
export default appSlice.reducer;
