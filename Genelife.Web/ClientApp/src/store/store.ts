import { combineReducers } from "redux";
import createSagaMiddleware from "redux-saga";
import { configureStore } from '@reduxjs/toolkit'
import rootSaga from "./root.saga";
import appSlice from "../app.slice";

const sagaMiddleware = createSagaMiddleware();

export const rootReducer = combineReducers({
    appSlice,
});

export const store = configureStore({
    reducer: rootReducer,
    middleware: (getDefaultMiddleware: any) =>
        getDefaultMiddleware().concat([sagaMiddleware]),
    devTools: process.env.NODE_ENV !== "production",
});

sagaMiddleware.run(rootSaga);

export type RootState = ReturnType<typeof rootReducer>;
