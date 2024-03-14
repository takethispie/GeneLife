import * as React from "react";
import { useDispatch } from "react-redux";
import { Box, Button, Container, Grid, Tab, Tabs } from "@mui/material";
import NavMenu from "./NavMenu";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Typography from "@mui/material/Typography";
import CardHeader from "@mui/material/CardHeader";
import { CREATE_SMALL_CITY } from "../slices/simulation.slice";
import { useSelector } from "react-redux";
import { RootState } from "../store/store";
import { match } from "ts-pattern";
import { NpcList } from "./NpcList";
import { SET_TICKS_PER_DAY } from "../app.slice";
import { StructureList } from "./StructureList";

export default function Home() {
    const dispatch = useDispatch();
    const state = useSelector((state: RootState) => state);
    const [tabId, setTabId] = React.useState(0);

    return (
        <React.Fragment>
            <NavMenu />
            <Container>
                <Grid container spacing={1} style={{ margin: 5 }}>
                    <Grid item xs={8}>
                        <Box sx={{ width: "100%" }}>
                            <Box sx={{ borderBottom: 1, borderColor: "divider" }}>
                                <Tabs value={tabId} onChange={(e, v) => setTabId(v)} aria-label="basic tabs example">
                                    <Tab label="Main Logs" />
                                    <Tab label="NPCs" />
                                    <Tab label="Structures" />
                                </Tabs>
                            </Box>
                            {match(tabId)
                                .with(0, () => (
                                    <Card style={{ minHeight: 500 }}>
                                        <CardContent style={{ paddingTop: 0, paddingBottom: 0 }}>
                                            <List>
                                                {state.simulationSlice.simulationState.logs.map((log, id) => (
                                                    <ListItem key={id} style={{ paddingTop: 0, paddingBottom: 0 }}>
                                                        <ListItemText
                                                            primary={log}
                                                            style={{ marginTop: 0, marginBottom: 0 }}
                                                        ></ListItemText>
                                                    </ListItem>
                                                ))}
                                            </List>
                                        </CardContent>
                                    </Card>
                                ))
                                .with(1, () => (
                                    <Card style={{ minHeight: 500 }}>
                                        <CardContent style={{ paddingTop: 0, paddingBottom: 0 }}>
                                            <NpcList npcs={state.simulationSlice.simulationState.npcs}/>
                                        </CardContent>
                                    </Card>
                                ))
                                .otherwise(() => (
                                    <Card style={{ minHeight: 500 }}>
                                        <CardContent style={{ paddingTop: 0, paddingBottom: 0 }}>
                                            <StructureList buildings={state.simulationSlice.simulationState.buildings}/>
                                        </CardContent>
                                    </Card>
                                ))}
                        </Box>
                    </Grid>
                    <Grid item xs={4} style={{ marginTop: 48 }}>
                        <Card style={{ minHeight: 500 }}>
                            <CardHeader title="Simulation Options"></CardHeader>
                            <CardContent>
                                <Typography variant="h6">Info</Typography>
                                <Typography variant="body2" style={{ paddingLeft: 10 }}>
                                    {state.appSlice.totalTicks} Ticks
                                </Typography>
                                <Typography variant="h6">Create</Typography>
                                <Button onClick={() => dispatch(CREATE_SMALL_CITY())}>Create Small City</Button>
                                <Typography variant="h6">Ticks Per Day</Typography>
                                <Button onClick={() => dispatch(SET_TICKS_PER_DAY(24))}>24 Ticks</Button>
                                <Button onClick={() => dispatch(SET_TICKS_PER_DAY(48))}>48 Ticks</Button>
                                <Button onClick={() => dispatch(SET_TICKS_PER_DAY(192))}>192 Ticks</Button>
                                <Button onClick={() => dispatch(SET_TICKS_PER_DAY(240))}>240 Ticks</Button>
                            </CardContent>
                        </Card>
                    </Grid>
                </Grid>
            </Container>
        </React.Fragment>
    );
}
