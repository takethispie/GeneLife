import * as React from "react";
import AppBar from "@mui/material/AppBar";
import Toolbar from "@mui/material/Toolbar";
import Typography from "@mui/material/Typography";
import Button from "@mui/material/Button";
import IconButton from "@mui/material/IconButton";
import MenuIcon from "@mui/icons-material/Menu";
import { useDispatch } from "react-redux";
import { START_SIM, STOP_SIM } from "../app.slice";

export default function NavMenu() {
  const dispatch = useDispatch();

  return (
    <AppBar position="static">
      <Toolbar>
        <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
          Genelife Web
        </Typography>
        <Button color="inherit" onClick={() => dispatch(START_SIM())}>Start Sim</Button>
        <Button color="inherit" onClick={() => dispatch(STOP_SIM())}>Stop Sim</Button>
      </Toolbar>
    </AppBar>
  );
}
