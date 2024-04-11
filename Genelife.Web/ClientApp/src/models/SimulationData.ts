import { Building } from "./Building";
import { Human } from "./Human";

export interface SimulationData {
    npcs: Human[]
    buildings: Building[]
    logs: string[]
    time: string
}