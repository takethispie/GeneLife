import * as React from "react";
import AppBar from "@mui/material/AppBar";
import Toolbar from "@mui/material/Toolbar";
import Typography from "@mui/material/Typography";
import Button from "@mui/material/Button";
import { useDispatch } from "react-redux";
import { START_SIM, STOP_SIM } from "../app.slice";
import { useSelector } from "react-redux";
import { RootState } from "../store/store";

export default function NavMenu() {
    const dispatch = useDispatch();
    const state = useSelector((root: RootState) => root.appSlice);

    return (
        <AppBar position="static">
            <Toolbar>
                <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
                    Genelife Web
                </Typography>
                <Button
                    color="inherit"
                    onClick={() => {
                        if(state.simulationRunning) return;
                        dispatch(START_SIM());
                    }}
                >
                    Start Sim
                </Button>
                <Button color="inherit" onClick={() => dispatch(STOP_SIM())}>
                    Stop Sim
                </Button>
            </Toolbar>
        </AppBar>
    );
}
