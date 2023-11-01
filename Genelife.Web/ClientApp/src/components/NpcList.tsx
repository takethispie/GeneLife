import { List, ListItem, ListItemText } from "@mui/material";
import { Human } from "../models/Human";

export const NpcList = (npcs: Human[]) => {
    return (
        <List>
            {npcs.map((npc, id) => (
                <ListItem key={id}>
                    <ListItemText primary={npc.identity}></ListItemText>
                </ListItem>
            ))}
        </List>
    );
};
