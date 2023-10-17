import React, { useState } from 'react';
import './App.css';
import Container from '@mui/material/Container';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import { Button } from '@mui/material';
import { GetCurrentSimulationState } from './Services/SimulationService';

function App() {
    const [state, setState] = useState({});
    return (
    <Container maxWidth="lg">
        <Box sx={{ my: 4 }}>
            <Typography variant="h4" component="h1" gutterBottom>
                Genelife Web Interface
                </Typography>
                <Button onClick={() => {
                    setState(await GetCurrentSimulationState())
                }}>Refresh</Button>
        </Box>
    </Container>
    );
}

export default App;
