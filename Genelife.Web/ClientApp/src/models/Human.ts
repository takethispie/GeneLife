export class HumanStats {
    public stamina: string = ""
    public thirst: string = ""
    public hunger: string = ""
    public damage: string = ""
}

export class Human {
    wallet: string = ""
    identity: string = ""
    stats?: HumanStats
    objectives: string[] = []
}