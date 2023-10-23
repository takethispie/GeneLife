import axios from 'axios';


export async function simulationState(): Promise<any> {
    var res = await axios.get('/api/simulation/state')
    return res.data;
}


export async function initSimulation(): Promise<any> {
    var res = await axios.get('/api/simulation/init')
    return res.status;
}