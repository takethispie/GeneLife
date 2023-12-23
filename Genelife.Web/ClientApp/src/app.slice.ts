import { createSlice, PayloadAction } from "@reduxjs/toolkit";

interface genelifeState {
    simulationRunning: boolean
    logs: string[]
    totalTicks: number,
    initialized: boolean
}

const initialState: genelifeState = {
    simulationRunning: false,
    logs: [],
    totalTicks: 0,
    initialized: false
};

const appSlice = createSlice({
  name: "app",
  initialState,
  reducers: {
    SIM_UPDATE: (state) => {},
    UPDATE_TOTAL_TICK: (state) => {
        state.totalTicks++;
    },
    START_SIM: (state) => {
        state.simulationRunning = true;
        if(!state.initialized) state.logs = [];
    },
    SET_INITIALIZED_FLAG: (state) => {
        if(!state.initialized) state.initialized = true;
    },
    STOP_SIM: (state) => {
        state.simulationRunning = false;
    },
    SET_TICKS_PER_DAY: (state, action: PayloadAction<number>) => {}
  },
});

export const { SIM_UPDATE, START_SIM, STOP_SIM, UPDATE_TOTAL_TICK, SET_INITIALIZED_FLAG, SET_TICKS_PER_DAY } = appSlice.actions;
export default appSlice.reducer;
