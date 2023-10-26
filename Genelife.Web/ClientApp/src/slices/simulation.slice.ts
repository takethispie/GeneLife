import { createSlice, PayloadAction } from "@reduxjs/toolkit";

interface simulationState {
}

const initialState: simulationState = {
};

const simulationSlice = createSlice({
  name: "app",
  initialState,
  reducers: {
    CREATE_SMALL_CITY: (state) => {}
  },
});

export const { CREATE_SMALL_CITY } = simulationSlice.actions;
export default simulationSlice.reducer;
