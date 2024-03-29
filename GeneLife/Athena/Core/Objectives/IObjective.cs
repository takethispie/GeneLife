﻿namespace GeneLife.Athena.Core.Objectives;

public interface IObjective
{
    public int Priority { get; set; }
    public string Name { get; }

    public static string GetName() => "IObjective";
}