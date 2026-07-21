using UnityEngine;

public enum StatType
{
    Damage, MagCapacity, FireRate, ReloadSpeed,
    MaxHealth, MoveSpeed, ReviveSpeed, BleedOutTime
}

public static class PlayerStatsConfig
{
    public const int MaxPerStat = 3;
    private const int ClaveXOR = 1986;

    // --- SISTEMA DE NIVELES Y XP ---
    public static int GetXP()
    {
        // Si no existe la partida guardada, devolvemos 0 directamente sin desencriptar nada
        if (!PlayerPrefs.HasKey("experiencia_agente"))
        {
            return 0;
        }

        // Si existe, lo leemos y lo desencriptamos
        return PlayerPrefs.GetInt("experiencia_agente") ^ ClaveXOR;
    }

    public static void SetXP(int xp)
    {
        PlayerPrefs.SetInt("experiencia_agente", xp ^ ClaveXOR);
        Save();
    }

    public static int GetLevel()
    {
        int currentXP = GetXP();
        // Cada 100 XP es un nivel. Empezás en Nivel 1 (0 XP).
        return (currentXP / 100) + 1;
    }

    // LA CORRECCIÓN: Nivel 1 (1+9) = 10 puntos. Nivel 2 (2+9) = 11 puntos.
    public static int TotalPointsAvailable() => GetLevel() + 9;

    // ---------------------------------------

    public static float PerPoint(StatType s)
    {
        switch (s)
        {
            case StatType.Damage: return 10f;
            case StatType.MagCapacity: return 5f;
            case StatType.FireRate: return 0.05f;
            case StatType.ReloadSpeed: return 0.3f;
            case StatType.MaxHealth: return 25f;
            case StatType.MoveSpeed: return 0.5f;
            case StatType.ReviveSpeed: return 1f;
            case StatType.BleedOutTime: return 3f;
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

    public static int GetLevel(StatType s)
    {
        int valorCrudo = PlayerPrefs.GetInt("stat_" + s, 0);
        // Retrocompatibilidad (si el valor grabado no está encriptado)
        if (valorCrudo <= 10) return valorCrudo;
        else return valorCrudo ^ ClaveXOR;
    }

    public static void SetLevel(StatType s, int level)
    {
        int valorEncriptado = level ^ ClaveXOR;
        PlayerPrefs.SetInt("stat_" + s, valorEncriptado);
    }

    public static void Save() => PlayerPrefs.Save();

    public static int TotalSpent()
    {
        int sum = 0;
        foreach (StatType s in System.Enum.GetValues(typeof(StatType)))
            sum += GetLevel(s);
        return sum;
    }

    // Calcula restando los gastados a tu total dinámico base 10
    public static int PointsLeft() => TotalPointsAvailable() - TotalSpent();
}