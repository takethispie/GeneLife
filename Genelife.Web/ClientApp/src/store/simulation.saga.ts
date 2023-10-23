import { PayloadAction } from "@reduxjs/toolkit";
import { call, delay, put, select, takeEvery } from "redux-saga/effects";

import { RootState } from "../store/store";
import { SET_SIM_STATE, SIM_UPDATE, START_SIM } from "../app.slice";
import { initSimulation, simulationState } from "../services/simulation.service";

export default function* SimulationSaga() {
    yield takeEvery(SIM_UPDATE, UpdateSaga);
    yield takeEvery(START_SIM, StartSimSaga)
}

function* UpdateSaga(): any {
    const state: RootState = yield select();
    while(state.appSlice.simulationRunning) {
        let simData: any = yield call(simulationState);
        yield put(SET_SIM_STATE(simData));
        yield delay(2000);
    }
}


function* StartSimSaga() {
    yield call(initSimulation);
    yield delay(2000);
    yield put(SIM_UPDATE());
}