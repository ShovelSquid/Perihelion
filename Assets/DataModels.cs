using System;
using System.Collections.Generic;

[Serializable]
public class StatData
{
    public string statName;
    public float baseValue;
}

[Serializable]
public class MobData
{
    public int reference_number;
    public string mobName;
    public List<StatData> stats;
}

[Serializable]
public class MobDataCollection
{
    public List<MobData> mobs;
}