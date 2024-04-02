import { Modal, Box, Typography, Button } from "@mui/material";
import { Human } from "../models/Human";

interface NpcDetailProps {
    npc: Human;
    handleClose: () => void;
    open: boolean;
}

export const NpcDetail = ({ npc, handleClose, open }: NpcDetailProps) => {
    return (
        <Modal open={open} aria-labelledby="modal-modal-title" aria-describedby="modal-modal-description">
            <Box>
                <Typography id="modal-modal-title" variant="h6" component="h2">
                    Text in a modal
                </Typography>
                <Typography id="modal-modal-description" sx={{ mt: 2 }}>
                    Duis mollis, est non commodo luctus, nisi erat porttitor ligula.
                </Typography>
                <Button onClick={() => handleClose()}></Button>
            </Box>
        </Modal>
    );
};
