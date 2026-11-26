using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TP.Core;
using TP.Gameplay;

namespace TP.UI
{
    // Cada pantalla tiene su propio canvas (anidado en el principal)
    public abstract class UIScreen
    {
        protected readonly RectTransform Root;
        private readonly Canvas canvas;
        private readonly GraphicRaycaster raycaster;

        protected UIScreen(string name, Transform parent, bool interactive)
        {
            Root = UIFactory.Stretch(UIFactory.Rect(name, parent));
            canvas = UIFactory.AddCanvas(Root);
            if (interactive) raycaster = Root.gameObject.AddComponent<GraphicRaycaster>();
            SetVisible(false);
        }

        public bool Visible => canvas.enabled;

        public virtual void Show() => SetVisible(true);
        public virtual void Hide() => SetVisible(false);

        private void SetVisible(bool value)
        {
            // apago el componente Canvas en vez del GameObject, asi no reconstruye las mallas al volver
            canvas.enabled = value;
            if (raycaster == null) return;

            raycaster.enabled = value;
            // si queda un boton seleccionado se puede apretar con Enter/Espacio aunque no se vea
            if (!value && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        protected RectTransform CenteredColumn(string name, float width, float height, float spacing, bool withPanel)
        {
            RectTransform column;
            if (withPanel)
            {
                Image panel = UIFactory.MakePanel(name, Root, UIFactory.PanelColor);
                column = panel.rectTransform;
            }
            else
            {
                column = UIFactory.Rect(name, Root);
            }

            UIFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            VerticalLayoutGroup layout = UIFactory.VerticalLayout(column, spacing, TextAnchor.MiddleCenter);
            layout.padding = new RectOffset(30, 30, 30, 30);
            return column;
        }

        protected static TextMeshProUGUI ColumnText(RectTransform column, string text, float size, Color color,
                                                    float height, FontStyles style = FontStyles.Normal)
        {
            TextMeshProUGUI label = UIFactory.Label("Text", column, text, size, color, TextAlignmentOptions.Center, style);
            label.rectTransform.sizeDelta = new Vector2(column.sizeDelta.x - 60f, height);
            return label;
        }
    }

    public sealed class MenuScreen : UIScreen
    {
        private readonly IProgressionService progression;
        private readonly List<string> modelNames;
        private readonly TextMeshProUGUI profileText;
        private readonly Button characterButton;

        public MenuScreen(Transform canvas, Texture background, IGameFlow flow, IProgressionService progression,
                          IAudioService audio, List<string> modelNames, System.Action openUpgrades)
            : base("MenuScreen", canvas, true)
        {
            this.progression = progression;
            this.modelNames = modelNames;

            UIFactory.Background(Root, background);
            RectTransform column = CenteredColumn("Column", 760f, 860f, 22f, true);

            ColumnText(column, "LA ORDEN", 96f, UIFactory.Amber, 120f, FontStyles.Bold);
            ColumnText(column, "Supervivencia por oleadas\nTrabajo Final - Optimización de Videojuegos", 26f, UIFactory.Cream, 80f);
            profileText = ColumnText(column, "", 28f, UIFactory.Cream, 50f);

            UIFactory.MakeButton("Play", column, "JUGAR", () => { audio.Play(SfxType.UiClick); flow.StartGame(); }, 520f, 86f, 42f);
            UIFactory.MakeButton("Upgrades", column, "MEJORAS", () => { audio.Play(SfxType.UiClick); openUpgrades(); }, 520f, 74f);
            characterButton = UIFactory.MakeButton("Character", column, "", () => { audio.Play(SfxType.UiClick); NextCharacter(); }, 520f, 74f, 28f);
            UIFactory.MakeButton("Quit", column, "SALIR", () => { audio.Play(SfxType.UiClick); flow.Quit(); }, 520f, 74f);

            ColumnText(column, "WASD mover - Mouse apuntar - Click disparar - Click der. patada\nR recargar - G granada (mantener) - Shift correr - Esc pausa",
                       20f, new Color(1f, 1f, 1f, 0.6f), 70f);
        }

        public override void Show()
        {
            base.Show();
            Refresh();
        }

        private void NextCharacter()
        {
            if (modelNames.Count == 0) return;
            progression.SelectedModel = (progression.SelectedModel + 1) % modelNames.Count;
            Refresh();
        }

        private void Refresh()
        {
            profileText.text = $"Nivel {progression.Level} | {progression.Xp} XP | Puntos libres: {progression.PointsLeft}";

            if (modelNames.Count == 0)
            {
                characterButton.gameObject.SetActive(false);
                return;
            }
            int index = Mathf.Clamp(progression.SelectedModel, 0, modelNames.Count - 1);
            UIFactory.SetLabel(characterButton, $"PERSONAJE: {modelNames[index]} ({index + 1}/{modelNames.Count})");
        }
    }

    public sealed class UpgradesScreen : UIScreen
    {
        private readonly IProgressionService progression;
        private readonly TextMeshProUGUI header;
        private readonly Dictionary<StatType, Image[]> pips = new Dictionary<StatType, Image[]>();
        private readonly Dictionary<StatType, Button> plus = new Dictionary<StatType, Button>();
        private readonly Dictionary<StatType, Button> minus = new Dictionary<StatType, Button>();

        public UpgradesScreen(Transform canvas, Texture background, IProgressionService progression,
                              IAudioService audio, System.Action onBack)
            : base("UpgradesScreen", canvas, true)
        {
            this.progression = progression;

            UIFactory.Background(Root, background);
            RectTransform column = CenteredColumn("Column", 900f, 860f, 14f, true);

            ColumnText(column, "MEJORAS DEL AGENTE", 56f, UIFactory.Amber, 80f, FontStyles.Bold);
            header = ColumnText(column, "", 30f, UIFactory.Cream, 50f);

            foreach (StatType stat in ProgressionService.Stats)
            {
                RectTransform row = UIFactory.Rect("Row_" + stat, column);
                row.sizeDelta = new Vector2(780f, 62f);
                UIFactory.HorizontalLayout(row, 14f, TextAnchor.MiddleCenter);

                TextMeshProUGUI name = UIFactory.Label("Name", row, ProgressionService.DisplayName(stat), 30f,
                                                       UIFactory.Cream, TextAlignmentOptions.MidlineLeft);
                name.rectTransform.sizeDelta = new Vector2(280f, 60f);

                StatType captured = stat;
                minus[stat] = UIFactory.MakeButton("Minus", row, "-", () => Change(captured, -1, audio), 70f, 56f, 40f);

                RectTransform pipRow = UIFactory.Rect("Pips", row);
                pipRow.sizeDelta = new Vector2(200f, 40f);
                UIFactory.HorizontalLayout(pipRow, 10f, TextAnchor.MiddleCenter);
                var statPips = new Image[progression.MaxPerStat];
                for (int i = 0; i < statPips.Length; i++)
                {
                    statPips[i] = UIFactory.MakePanel("Pip", pipRow, Color.gray);
                    statPips[i].rectTransform.sizeDelta = new Vector2(48f, 26f);
                }
                pips[stat] = statPips;

                plus[stat] = UIFactory.MakeButton("Plus", row, "+", () => Change(captured, 1, audio), 70f, 56f, 40f);
            }

            UIFactory.MakeButton("Back", column, "VOLVER", () => { audio.Play(SfxType.UiClick); onBack(); }, 420f, 70f);
        }

        public override void Show()
        {
            base.Show();
            Refresh();
        }

        private void Change(StatType stat, int delta, IAudioService audio)
        {
            if (progression.TryChangeStat(stat, delta)) audio.Play(SfxType.UiClick);
            Refresh();
        }

        private void Refresh()
        {
            int left = progression.PointsLeft;
            header.text = $"Nivel {progression.Level} | Puntos: {left} / {progression.TotalPoints}";

            foreach (StatType stat in ProgressionService.Stats)
            {
                int level = progression.GetStatLevel(stat);
                Image[] statPips = pips[stat];
                for (int i = 0; i < statPips.Length; i++)
                    statPips[i].color = i < level ? UIFactory.Amber : new Color(0.43f, 0.35f, 0.18f, 1f);

                minus[stat].interactable = level > 0;
                plus[stat].interactable = level < progression.MaxPerStat && left > 0;
            }
        }
    }

    public sealed class PauseScreen : UIScreen
    {
        public PauseScreen(Transform canvas, IGameFlow flow, IAudioService audio) : base("PauseScreen", canvas, true)
        {
            Image dim = UIFactory.MakePanel("Dim", Root, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(dim.rectTransform);

            RectTransform column = CenteredColumn("Column", 620f, 520f, 22f, true);
            ColumnText(column, "PAUSA", 72f, UIFactory.Amber, 100f, FontStyles.Bold);
            UIFactory.MakeButton("Resume", column, "REANUDAR", () => { audio.Play(SfxType.UiClick); flow.TogglePause(); });
            UIFactory.MakeButton("Restart", column, "REINICIAR", () => { audio.Play(SfxType.UiClick); flow.Restart(); });
            UIFactory.MakeButton("Menu", column, "MENÚ PRINCIPAL", () => { audio.Play(SfxType.UiClick); flow.BackToMenu(); });
        }
    }

    public sealed class EndScreen : UIScreen
    {
        private readonly IMatchStats stats;
        private readonly IProgressionService progression;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI summary;

        public EndScreen(Transform canvas, IGameFlow flow, IMatchStats stats, IProgressionService progression,
                         IAudioService audio) : base("EndScreen", canvas, true)
        {
            this.stats = stats;
            this.progression = progression;

            Image dim = UIFactory.MakePanel("Dim", Root, new Color(0f, 0f, 0f, 0.7f));
            UIFactory.Stretch(dim.rectTransform);

            RectTransform column = CenteredColumn("Column", 760f, 680f, 22f, true);
            title = ColumnText(column, "", 84f, UIFactory.Amber, 110f, FontStyles.Bold);
            summary = ColumnText(column, "", 32f, UIFactory.Cream, 230f);
            UIFactory.MakeButton("Again", column, "JUGAR DE NUEVO", () => { audio.Play(SfxType.UiClick); flow.Restart(); });
            UIFactory.MakeButton("Menu", column, "MENÚ PRINCIPAL", () => { audio.Play(SfxType.UiClick); flow.BackToMenu(); });
        }

        public void Show(bool victory)
        {
            base.Show();
            title.text = victory ? "¡VICTORIA!" : "DERROTA";
            title.color = victory ? new Color(0.45f, 0.9f, 0.35f) : UIFactory.Danger;

            int minutes = Mathf.FloorToInt(stats.Duration / 60f);
            int seconds = Mathf.FloorToInt(stats.Duration % 60f);
            summary.text = $"Oleadas superadas: {stats.WavesCleared}\nZombies eliminados: {stats.Kills}\n" +
                           $"Tiempo: {minutes:00}:{seconds:00}\nXP ganada: +{stats.XpEarned} (Nivel {progression.Level})";
        }
    }
}
