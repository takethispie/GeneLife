import { Accordion, AccordionDetails, AccordionSummary, Card, CardContent, Grid, Typography } from "@mui/material";
import { Human } from "../models/Human";
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';

interface NpcListProps {
    npcs: Human[];
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
                            <Accordion>
                                <AccordionSummary expandIcon={<ExpandMoreIcon />} aria-controls="panel1-content" id="panel1-header">
                                    Planning
                                </AccordionSummary>
                                <AccordionDetails>
                                    {npc.objectives?.map(x => <Typography>{x}</Typography>)}
                                </AccordionDetails>
                            </Accordion>
                        </CardContent>
                    </Card>
                </Grid>
            ))}
        </Grid>
    );
};
