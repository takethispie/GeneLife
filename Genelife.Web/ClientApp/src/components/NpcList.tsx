import { Card, CardContent, CardHeader, Grid, List, ListItem, ListItemText, Typography } from "@mui/material";
import { Human } from "../models/Human";

interface NpcListProps {
    npcs: Human[]
}

export const NpcList = ({ npcs }: NpcListProps) => {

    return (
        <Grid container>
            {npcs.map((npc, id) => (
                <Grid xs={4} style={{ padding: 10 }} key={id}>
                    <Card>
                       <CardContent>
                            <Typography variant="h6">{npc.identity}</Typography>
                            <Typography>{"Hunger: " + npc.stats?.hunger}</Typography>
                            <Typography>{"Thirst: " + npc.stats?.thirst}</Typography>
                            <Typography>{"Damage: " + npc.stats?.damage}</Typography>
                            <Typography>{"Stamina: " + npc.stats?.stamina}</Typography>
                            <Typography>{"Money: " + npc.wallet}</Typography>
                            <Typography>{"Position: " + npc.position}</Typography>
                       </CardContent>
                    </Card>
                </Grid>
            ))}
        </Grid>
    );
};
