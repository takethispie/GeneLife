import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { SimulationData } from "../models/SimulationData";

interface simulationState {
    simulationState: SimulationData
}

const initialState: simulationState = {
    simulationState: { npcs: [], buildings: [], logs: [], time: "" }
};

const simulationSlice = createSlice({
    name: "app",
    initialState,
    reducers: {
        CREATE_SMALL_CITY: (state) => {},
        SET_SIM_STATE: (state, action) => {
            state.simulationState = action.payload;
        },
    },
});

export const { CREATE_SMALL_CITY, SET_SIM_STATE } = simulationSlice.actions;
export default simulationSlice.reducer;
