﻿namespace GeneLife.Athena.Core.Objectives;

public interface IObjective
{
    public int Priority { get; init; }
    public string Name { get; init; }
}