import * as React from "react";
import { useDispatch } from "react-redux";
import { Button, Container, Grid } from "@mui/material";
import NavMenu from "./NavMenu";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Card from "@mui/material/Card";
import CardActions from "@mui/material/CardActions";
import CardContent from "@mui/material/CardContent";
import Typography from "@mui/material/Typography";
import CardHeader from "@mui/material/CardHeader";
import { CREATE_SMALL_CITY } from "../slices/simulation.slice";
import { useSelector } from "react-redux";
import { RootState } from "../store/store";

export default function Home() {
    const dispatch = useDispatch();
    const state = useSelector((state: RootState) => state.appSlice);

    return (
        <React.Fragment>
            <NavMenu />
            <Container>
                <Grid container spacing={1} style={{ margin: 5 }}>
                    <Grid item xs={8}>
                        <Card style={{ minHeight: 500 }}>
                            <CardHeader title="Main logs" style={{ paddingTop: 0, paddingBottom: 0 }}></CardHeader>
                            <CardContent style={{ paddingTop: 0, paddingBottom: 0 }}>
                                <List>
                                    {state.logs.map((log, id) => (
                                        <ListItem key={id} style={{ paddingTop: 0, paddingBottom: 0 }}>
                                            <ListItemText primary={log} style={{ marginTop: 0, marginBottom: 0 }}></ListItemText>
                                        </ListItem>
                                    ))}
                                </List>
                            </CardContent>
                        </Card>
                    </Grid>
                    <Grid item xs={4}>
                        <Card style={{ minHeight: 500 }}>
                            <CardHeader title="Simulation Options"></CardHeader>
                            <CardContent>
                                <Typography variant="h6">Info</Typography>
                                <Typography variant="body2" style={{ paddingLeft: 10 }}>
                                    {state.totalTicks} Ticks
                                </Typography>
                                <Typography variant="h6">Create</Typography>
                                <Button onClick={() => dispatch(CREATE_SMALL_CITY())}>Create Small City</Button>
                                <Typography variant="h6">Ticks Per Day</Typography>
                                <Button>5 Ticks</Button>
                                <Button>10 Ticks</Button>
                                <Button>30 Ticks</Button>
                            </CardContent>
                        </Card>
                    </Grid>
                </Grid>
            </Container>
        </React.Fragment>
    );
}
