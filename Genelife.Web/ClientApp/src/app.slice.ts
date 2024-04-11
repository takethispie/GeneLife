import { createSlice, PayloadAction } from "@reduxjs/toolkit";

interface genelifeState {
    simulationRunning: boolean
    logs: string[]
    initialized: boolean,
    millisecondsPerTick: number
}

const initialState: genelifeState = {
    simulationRunning: false,
    logs: [],
    initialized: false,
    millisecondsPerTick: 1000
};

const appSlice = createSlice({
  name: "app",
  initialState,
  reducers: {
    SIM_UPDATE: (state) => {},
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
    SET_TICKS_PER_DAY: (state, action: PayloadAction<number>) => {},
    SET_MILLISECONDS_PER_TICK: (state, action: PayloadAction<number>) => {
        state.millisecondsPerTick = action.payload;
    }
  },
});

export const { SIM_UPDATE, START_SIM, STOP_SIM, SET_INITIALIZED_FLAG, SET_TICKS_PER_DAY, SET_MILLISECONDS_PER_TICK } = appSlice.actions;
export default appSlice.reducer;
