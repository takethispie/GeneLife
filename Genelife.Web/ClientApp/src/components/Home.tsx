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

export default function Home() {
    const dispatch = useDispatch();
    return (
        <React.Fragment>
            <NavMenu />
            <Container>
                <Grid container spacing={1} style={{ margin: 5 }}>
                    <Grid item xs={8}>
                        <Card style={{ minHeight: 500 }}>
                            <CardHeader title="Main logs"></CardHeader>
                            <CardContent>
                                <List>
                                    <ListItem>
                                        <ListItemText>test</ListItemText>
                                    </ListItem>
                                </List>
                            </CardContent>
                        </Card>
                    </Grid>
                    <Grid item xs={4}>
                        <Card style={{ minHeight: 500 }}>
                            <CardHeader title="Simulation Options"></CardHeader>
                            <CardContent>
                                <Typography>Create</Typography>
                                <Button onClick={() => dispatch(CREATE_SMALL_CITY())}>Create Small City</Button>
                                <Typography>Ticks Per Day</Typography>
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
