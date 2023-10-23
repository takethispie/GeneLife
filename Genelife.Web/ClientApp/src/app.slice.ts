import { createSlice, PayloadAction } from "@reduxjs/toolkit";

interface genelifeState {
    simulationRunning: boolean
    simulationState: {}
}

const initialState: genelifeState = {
    simulationRunning: true,
    simulationState: {}
};

const appSlice = createSlice({
  name: "app",
  initialState,
  reducers: {
    SIM_UPDATE: (state) => {},
    SET_SIM_STATE: (state, action) => {
        state.simulationState = action.payload;
    },
    START_SIM: (state) => {
        state.simulationRunning = true;
    },
    STOP_SIM: (state) => {
        state.simulationRunning = false;
    }
  },
});

export const { SIM_UPDATE, SET_SIM_STATE, START_SIM, STOP_SIM } = appSlice.actions;
export default appSlice.reducer;
