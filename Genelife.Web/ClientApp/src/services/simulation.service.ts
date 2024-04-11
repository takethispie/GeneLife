import axios from 'axios';
import { SimulationData } from '../models/SimulationData';


export async function simulationState(): Promise<SimulationData> {
    var res = await axios.get('/api/simulation/state')
    return res.data as SimulationData;
}


export async function initSimulation(): Promise<any> {
    var res = await axios.get('/api/simulation/init')
    return res.status;
}

export async function createSmallCity(): Promise<any> {
    var res = await axios.get('/api/simulation/generate/smallcity')
    return res.status;
}

export async function setTicksPerDay(ticks: number): Promise<any> {
    var res = await axios.get('/api/simulation/set/ticks/day/' + ticks);
    return res.status;
}

export async function setTickInterval(ticks: number): Promise<any> {
    var res = await axios.get('/api/simulation/set/ticks/interval/' + ticks);
    return res.status;
}