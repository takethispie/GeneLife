import { PayloadAction } from "@reduxjs/toolkit";
import { call, delay, put, select, takeEvery } from "redux-saga/effects";

import { RootState } from "../store/store";
import { ADD_LOG, SET_INITIALIZED_FLAG, SET_TICKS_PER_DAY, SIM_UPDATE, START_SIM, UPDATE_TOTAL_TICK } from "../app.slice";
import { createSmallCity, initSimulation, setTicksPerDay, simulationState } from "../services/simulation.service";
import { CREATE_SMALL_CITY, SET_SIM_STATE } from "../slices/simulation.slice";

export default function* SimulationSaga() {
    yield takeEvery(SIM_UPDATE, UpdateSaga);
    yield takeEvery(START_SIM, StartSimSaga);
    yield takeEvery(CREATE_SMALL_CITY, CreateSmallCitySaga);
    yield takeEvery(SET_TICKS_PER_DAY, SetTicksPerDaySaga);
}

function* UpdateSaga(): any {
    const state: RootState = yield select();
    while(state.appSlice.simulationRunning) {
        let simData: any = yield call(simulationState);
        yield put(SET_SIM_STATE(simData));
        yield put(UPDATE_TOTAL_TICK());
        yield delay(2000);
        const newState: RootState = yield select();
        if(newState.appSlice.simulationRunning == false) break;
    }
}


function* StartSimSaga(): any {
    const state: RootState = yield select();
    if(state.appSlice.initialized == false) {
        var res = yield call(initSimulation);
        if(res == 200) {
            yield put(SET_INITIALIZED_FLAG());
        }
    }
    yield delay(2000);
    yield put(SIM_UPDATE());
}

function* CreateSmallCitySaga(): any {
    yield call(createSmallCity);
}

function* SetTicksPerDaySaga(action: PayloadAction<number>): any {
    yield call(setTicksPerDay, action.payload);
}