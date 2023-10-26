import axios from 'axios';
import { Human } from '../models/Human';


export async function simulationState(): Promise<Human[]> {
    var res = await axios.get('/api/simulation/state')
    return res.data as Human[];
}


export async function initSimulation(): Promise<any> {
    var res = await axios.get('/api/simulation/init')
    return res.status;
}

export async function createSmallCity(): Promise<any> {
    var res = await axios.get('/api/simulation/generate/smallcity')
    return res.status;
}