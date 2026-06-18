using UnityEngine;

public enum StatType
{
    Damage,        // arma
    MagCapacity,   // arma
    FireRate,      // arma
    ReloadSpeed,   // arma
    MaxHealth,     // agente
    MoveSpeed,     // agente
    ReviveSpeed,   // agente
    BleedOutTime   // agente
}

public static class PlayerStatsConfig
{
    public const int TotalPoints = 10;
    public const int MaxPerStat = 3;

    // Cuánto suma CADA punto invertido, por stat.
    // (los de "menor tiempo" van en negativo porque reducen)
    public static float PerPoint(StatType s)
    {
        switch (s)
        {
            case StatType.Damage: return 10f;   // +10 daño por punto
            case StatType.MagCapacity: return 5f;    // +5 balas
            case StatType.FireRate: return 0.05f; // -0.05s entre tiros
            case StatType.ReloadSpeed: return 0.3f;  // -0.3s de recarga
            case StatType.MaxHealth: return 25f;   // +25 vida
            case StatType.MoveSpeed: return 0.5f;  // +0.5 velocidad
            case StatType.ReviveSpeed: return 0.5f;  // -0.5s revivir
            case StatType.BleedOutTime: return 3f;    // +3s desangrado
        }
        return 0f;
    }

    public static string DisplayName(StatType s)
    {
        switch (s)
        {
            case StatType.Damage: return "Daño";
            case StatType.MagCapacity: return "Cargador";
            case StatType.FireRate: return "Vel. Disparo";
            case StatType.ReloadSpeed: return "Vel. Recarga";
            case StatType.MaxHealth: return "Salud Máx.";
            case StatType.MoveSpeed: return "Vel. Mov.";
            case StatType.ReviveSpeed: return "Vel. Revivir";
            case StatType.BleedOutTime: return "Desangrado";
        }
        return s.ToString();
    }

    public static int GetLevel(StatType s) => PlayerPrefs.GetInt("stat_" + s, 0);
    public static void SetLevel(StatType s, int level) => PlayerPrefs.SetInt("stat_" + s, level);
    public static void Save() => PlayerPrefs.Save();

    public static int TotalSpent()
    {
        int sum = 0;
        foreach (StatType s in System.Enum.GetValues(typeof(StatType)))
            sum += GetLevel(s);
        return sum;
    }

    public static int PointsLeft() => TotalPoints - TotalSpent();
}