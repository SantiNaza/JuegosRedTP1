using System;
using TMPro;
using UnityEngine;
using TP.Core;

namespace TP.UI
{
    /// <summary>
    /// HUD in-game. Se actualiza por eventos (vida, munición, granadas, waves) y solo reescribe un texto
    /// cuando el valor cambia; usa TMP SetText con números para no generar strings (GC) por frame.
    /// </summary>
    public sealed class HudScreen : UIScreen, IUpdatable, IDisposable
    {
        private readonly EventBus events;
        private readonly IPlayerService player;
        private readonly bool showFps;

        private readonly RectTransform healthFill;
        private readonly TextMeshProUGUI healthText;
        private readonly RectTransform staminaFill;
        private readonly TextMeshProUGUI waveText;
        private readonly TextMeshProUGUI remainingText;
        private readonly RectTransform waveFill;
        private readonly TextMeshProUGUI ammoText;
        private readonly TextMeshProUGUI magazinesText;
        private readonly TextMeshProUGUI grenadesText;
        private readonly TextMeshProUGUI killsText;
        private readonly TextMeshProUGUI fpsText;
        private readonly TextMeshProUGUI messageText;

        private float messageTimer;
        private float lastStamina = -1f;
        private int kills;
        private float fpsTimer;
        private int fpsFrames;

        public HudScreen(Transform canvas, EventBus events, IPlayerService player, bool showFps) : base("HudScreen", canvas)
        {
            this.events = events;
            this.player = player;
            this.showFps = showFps;

            // --- Vida y stamina (arriba a la izquierda) ---
            RectTransform vitals = UIFactory.Place(UIFactory.Rect("Vitals", Root), new Vector2(0f, 1f), new Vector2(0f, 1f),
                                                   new Vector2(40f, -30f), new Vector2(520f, 120f));
            healthFill = UIFactory.MakeBar("Health", vitals, new Vector2(460f, 34f), new Color(0f, 0f, 0f, 0.6f), UIFactory.Danger);
            UIFactory.Place((RectTransform)healthFill.parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(460f, 34f));
            healthText = UIFactory.Label("HealthText", healthFill.parent, "100", 24f, Color.white);
            UIFactory.Stretch(healthText.rectTransform);

            staminaFill = UIFactory.MakeBar("Stamina", vitals, new Vector2(460f, 12f), new Color(0f, 0f, 0f, 0.6f), new Color(0.3f, 0.8f, 1f));
            UIFactory.Place((RectTransform)staminaFill.parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -42f), new Vector2(460f, 12f));

            killsText = UIFactory.Label("Kills", vitals, "Bajas: 0", 26f, UIFactory.Cream, TextAlignmentOptions.MidlineLeft);
            UIFactory.Place(killsText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -62f), new Vector2(460f, 36f));

            // --- Wave (arriba al centro) ---
            RectTransform wave = UIFactory.Place(UIFactory.Rect("Wave", Root), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                 new Vector2(0f, -24f), new Vector2(600f, 120f));
            waveText = UIFactory.Label("WaveText", wave, "PREPARATE", 44f, UIFactory.Amber, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Place(waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(600f, 56f));
            remainingText = UIFactory.Label("Remaining", wave, "", 26f, UIFactory.Cream);
            UIFactory.Place(remainingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(600f, 34f));
            waveFill = UIFactory.MakeBar("WaveProgress", wave, new Vector2(420f, 10f), new Color(0f, 0f, 0f, 0.6f), UIFactory.Amber);
            UIFactory.Place((RectTransform)waveFill.parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(420f, 10f));
            UIFactory.SetBar(waveFill, 0f);

            // --- Munición y granadas (abajo a la derecha) ---
            RectTransform weapon = UIFactory.Place(UIFactory.Rect("Weapon", Root), new Vector2(1f, 0f), new Vector2(1f, 0f),
                                                   new Vector2(-40f, 30f), new Vector2(420f, 170f));
            ammoText = UIFactory.Label("Ammo", weapon, "15 / 15", 64f, UIFactory.Amber, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            UIFactory.Place(ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 80f), new Vector2(420f, 80f));
            magazinesText = UIFactory.Label("Mags", weapon, "", 28f, UIFactory.Cream, TextAlignmentOptions.BottomRight);
            UIFactory.Place(magazinesText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(420f, 36f));
            grenadesText = UIFactory.Label("Grenades", weapon, "", 28f, new Color(1f, 0.6f, 0.2f), TextAlignmentOptions.BottomRight);
            UIFactory.Place(grenadesText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(420f, 36f));

            // --- FPS (arriba a la derecha) y mensajes (centro) ---
            fpsText = UIFactory.Label("Fps", Root, "", 24f, new Color(0.6f, 1f, 0.6f), TextAlignmentOptions.TopRight);
            UIFactory.Place(fpsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(300f, 34f));
            fpsText.gameObject.SetActive(showFps);

            messageText = UIFactory.Label("Message", Root, "", 54f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Place(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1200f, 80f));

            events.Subscribe<PlayerHealthChangedEvent>(OnHealth);
            events.Subscribe<AmmoChangedEvent>(OnAmmo);
            events.Subscribe<GrenadesChangedEvent>(OnGrenades);
            events.Subscribe<WaveStartedEvent>(OnWaveStarted);
            events.Subscribe<WaveProgressEvent>(OnWaveProgress);
            events.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            events.Subscribe<HudMessageEvent>(OnMessage);
        }

        public void Dispose()
        {
            events.Unsubscribe<PlayerHealthChangedEvent>(OnHealth);
            events.Unsubscribe<AmmoChangedEvent>(OnAmmo);
            events.Unsubscribe<GrenadesChangedEvent>(OnGrenades);
            events.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            events.Unsubscribe<WaveProgressEvent>(OnWaveProgress);
            events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            events.Unsubscribe<HudMessageEvent>(OnMessage);
        }

        public void Tick(float deltaTime)
        {
            if (!Visible) return;

            float stamina = player.Stamina01;
            if (Mathf.Abs(stamina - lastStamina) > 0.005f)
            {
                lastStamina = stamina;
                UIFactory.SetBar(staminaFill, stamina);
            }

            if (messageTimer > 0f)
            {
                messageTimer -= deltaTime;
                Color c = messageText.color;
                c.a = Mathf.Clamp01(messageTimer * 2f);
                messageText.color = c;
            }

            if (!showFps) return;
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fpsText.SetText("{0:0} FPS", fpsFrames / fpsTimer);
                fpsFrames = 0;
                fpsTimer = 0f;
            }
        }

        private void OnHealth(PlayerHealthChangedEvent e)
        {
            UIFactory.SetBar(healthFill, e.Max > 0f ? e.Current / e.Max : 0f);
            healthText.SetText("{0:0} / {1:0}", e.Current, e.Max);
        }

        private void OnAmmo(AmmoChangedEvent e)
        {
            if (e.Reloading) ammoText.SetText("RECARGANDO");
            else ammoText.SetText("{0} / {1}", e.InMagazine, e.MagazineSize);
            magazinesText.SetText("Cargadores: {0}", e.SpareMagazines);
        }

        private void OnGrenades(GrenadesChangedEvent e)
        {
            if (e.Cooking) grenadesText.SetText("Granadas: {0}  ·  {1:0.0}s", e.Count, e.CookTimeLeft);
            else grenadesText.SetText("Granadas: {0}", e.Count);
        }

        private void OnWaveStarted(WaveStartedEvent e)
        {
            waveText.SetText("OLEADA {0} / {1}", e.Wave, e.TotalWaves);
            ShowMessage(e.HasElite ? "¡OLEADA DE ÉLITE!" : "¡NUEVA OLEADA!", e.HasElite ? UIFactory.Danger : UIFactory.Amber, 2f);
        }

        private void OnWaveProgress(WaveProgressEvent e)
        {
            remainingText.SetText("Enemigos restantes: {0}", e.Remaining);
            UIFactory.SetBar(waveFill, e.Total > 0 ? 1f - (float)e.Remaining / e.Total : 0f);
        }

        private void OnEnemyKilled(EnemyKilledEvent e)
        {
            kills++;
            killsText.SetText("Bajas: {0}", kills);
        }

        private void OnMessage(HudMessageEvent e) => ShowMessage(e.Text, e.Color, e.Duration);

        private void ShowMessage(string text, Color color, float duration)
        {
            messageText.text = text;
            messageText.color = color;
            messageTimer = duration;
        }
    }
}
