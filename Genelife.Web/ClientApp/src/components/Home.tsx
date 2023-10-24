import * as React from "react";
import { useDispatch } from "react-redux";
import { Button, Container, Grid } from "@mui/material";
import NavMenu from "./NavMenu";
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';

export default function Home() {
  return (
    <React.Fragment>
      <NavMenu />
      <Container>
        <Grid container spacing={2}>
          <Grid item xs={8}>
            <List>
                <ListItem>
                    Test
                </ListItem>
            </List>
          </Grid>
          <Grid item xs={4}>
          </Grid>
          <Grid item xs={4}>
          </Grid>
          <Grid item xs={8}>
          </Grid>
        </Grid>
      </Container>
    </React.Fragment>
  );
}
