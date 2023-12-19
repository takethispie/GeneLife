import { Card, CardContent, CardHeader, Grid, List, ListItem, ListItemText, Typography } from "@mui/material";
import { Building } from "../models/Building";

interface StructureListProps {
    buildings: Building[]
}

export const StructureList = ({ buildings }: StructureListProps) => {

    return (
        <Grid container>
            {buildings?.map((building, id) => (
                <Grid xs={4} style={{ padding: 10 }} key={id}>
                    <Card>
                       <CardContent>
                            <Typography variant="h6">{building.type + " " + building.id}</Typography>
                            <Typography>Adress: {building.adress}</Typography>
                            <Typography>Position: {building.position}</Typography>
                       </CardContent>
                    </Card>
                </Grid>
            ))}
        </Grid>
    );
};
