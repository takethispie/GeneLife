export class HumanStats {
    public Stamina: string = ""
    public Thirst: string = ""
    public Hunger: string = ""
    public Damage: string = ""
}

export class Human {
    Wallet: string = ""
    Identity: string = ""
    Stats?: HumanStats
}