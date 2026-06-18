using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StatAllocatorUI : MonoBehaviour
{
    [Header("Dónde se construye (un panel vacío del Canvas)")]
    [SerializeField] private RectTransform container;

    private TMP_Text pointsLabel;
    private readonly Dictionary<StatType, TMP_Text> levelLabels = new Dictionary<StatType, TMP_Text>();
    private readonly Dictionary<StatType, Button> plusButtons = new Dictionary<StatType, Button>();
    private readonly Dictionary<StatType, Button> minusButtons = new Dictionary<StatType, Button>();

    void Start()
    {
        if (container == null) container = (RectTransform)transform;
        BuildUI();
        RefreshAll();
    }

    void BuildUI()
    {
        // Layout vertical en el contenedor
        var layout = container.gameObject.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(20, 20, 20, 20);

        // Encabezado de puntos restantes
        pointsLabel = MakeText(container, "", 28, FontStyles.Bold);
        pointsLabel.alignment = TextAlignmentOptions.Center;

        // Una fila por stat
        foreach (StatType s in System.Enum.GetValues(typeof(StatType)))
        {
            BuildRow(s);
        }
    }

    void BuildRow(StatType stat)
    {
        // contenedor horizontal de la fila
        GameObject row = new GameObject("Row_" + stat, typeof(RectTransform));
        row.transform.SetParent(container, false);
        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10;
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childForceExpandWidth = false;
        var le = row.AddComponent<LayoutElement>();
        le.minHeight = 25;

        // nombre del stat
        var name = MakeText(row.transform, PlayerStatsConfig.DisplayName(stat), 18, FontStyles.Normal);
        name.alignment = TextAlignmentOptions.MidlineLeft;
        var nameLE = name.gameObject.AddComponent<LayoutElement>();
        nameLE.preferredWidth = 200;

        // botón −
        var minus = MakeButton(row.transform, "−", () => Change(stat, -1));
        minusButtons[stat] = minus;

        // nivel (ej "●●○")
        var lvl = MakeText(row.transform, "", 18, FontStyles.Bold);
        lvl.alignment = TextAlignmentOptions.Center;
        var lvlLE = lvl.gameObject.AddComponent<LayoutElement>();
        lvlLE.preferredWidth = 120;
        levelLabels[stat] = lvl;

        // botón +
        var plus = MakeButton(row.transform, "+", () => Change(stat, 1));
        plusButtons[stat] = plus;
    }

    void Change(StatType stat, int dir)
    {
        int current = PlayerStatsConfig.GetLevel(stat);
        int next = current + dir;

        if (next < 0 || next > PlayerStatsConfig.MaxPerStat) return;          // tope por stat
        if (dir > 0 && PlayerStatsConfig.PointsLeft() <= 0) return;           // sin puntos

        PlayerStatsConfig.SetLevel(stat, next);
        PlayerStatsConfig.Save();
        RefreshAll();
    }

    void RefreshAll()
    {
        pointsLabel.text = $"Puntos restantes: {PlayerStatsConfig.PointsLeft()} / {PlayerStatsConfig.TotalPoints}";

        bool noPointsLeft = PlayerStatsConfig.PointsLeft() <= 0;

        foreach (StatType s in System.Enum.GetValues(typeof(StatType)))
        {
            int lvl = PlayerStatsConfig.GetLevel(s);

            // puntitos: llenos = nivel, vacíos = lo que falta hasta el máximo
            string dots = new string('●', lvl) + new string('○', PlayerStatsConfig.MaxPerStat - lvl);
            levelLabels[s].text = dots;

            minusButtons[s].interactable = lvl > 0;
            plusButtons[s].interactable = lvl < PlayerStatsConfig.MaxPerStat && !noPointsLeft;
        }
    }

    // ---------- helpers de creación ----------
    TMP_Text MakeText(Transform parent, string text, float size, FontStyles style)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = Color.white;
        return t;
    }

    Button MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.3f, 1f);

        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 220;
        le.preferredHeight = 20;

        var txt = MakeText(go.transform, label, 16, FontStyles.Bold);
        txt.alignment = TextAlignmentOptions.Center;
        var txtRT = (RectTransform)txt.transform;
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        return btn;
    }
}