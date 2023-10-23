import { all } from 'redux-saga/effects'
import SimulationSaga from './simulation.saga'

export default function* rootSaga() {
    yield all([
        SimulationSaga()
    ])
}