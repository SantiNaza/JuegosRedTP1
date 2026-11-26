using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TP.UI
{
    // La UI se arma por codigo para no tener MonoBehaviours ni referencias del Inspector
    public static class UIFactory
    {
        public static readonly Color Amber = new Color32(0xF5, 0xA8, 0x28, 0xFF);
        public static readonly Color Cream = new Color32(0xEB, 0xE1, 0xCD, 0xFF);
        public static readonly Color Danger = new Color32(0xD6, 0x3A, 0x3A, 0xFF);
        public static readonly Color PanelColor = new Color(0.06f, 0.05f, 0.04f, 0.88f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.18f, 0.12f, 1f);
        public static readonly Color ButtonHover = new Color(0.36f, 0.27f, 0.12f, 1f);

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // el raycaster va solo en las pantallas que tienen botones (ver UIScreen)
            return canvas;
        }

        public static Canvas AddCanvas(RectTransform rt)
        {
            return rt.gameObject.AddComponent<Canvas>();
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetAsLastSibling();
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image MakePanel(string name, Transform parent, Color color)
        {
            RectTransform rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static RawImage Background(Transform parent, Texture texture)
        {
            RectTransform rt = Stretch(Rect("Background", parent));
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.color = texture != null ? Color.white : new Color(0.03f, 0.03f, 0.03f, 0.85f);
            raw.raycastTarget = false;
            return raw;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color,
                                           TextAlignmentOptions alignment = TextAlignmentOptions.Center,
                                           FontStyles style = FontStyles.Normal)
        {
            RectTransform rt = Rect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        public static Button MakeButton(string name, Transform parent, string label, UnityAction onClick,
                                        float width = 420f, float height = 70f, float fontSize = 34f)
        {
            RectTransform rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(width, height);

            var image = rt.gameObject.AddComponent<Image>();
            image.color = Color.white;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHover;
            colors.selectedColor = ButtonColor;
            colors.pressedColor = Amber;
            colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;

            TextMeshProUGUI text = Label("Label", rt, label, fontSize, Amber, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform);
            return button;
        }

        public static VerticalLayoutGroup VerticalLayout(RectTransform rt, float spacing, TextAnchor alignment)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup HorizontalLayout(RectTransform rt, float spacing, TextAnchor alignment)
        {
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        // devuelve el relleno, se llena moviendo anchorMax.x
        public static RectTransform MakeBar(string name, Transform parent, Vector2 size, Color background, Color fill)
        {
            Image bg = MakePanel(name, parent, background);
            bg.rectTransform.sizeDelta = size;

            Image fillImage = MakePanel("Fill", bg.rectTransform, fill);
            return Stretch(fillImage.rectTransform);
        }

        public static void SetBar(RectTransform fill, float normalized)
        {
            Vector2 max = fill.anchorMax;
            max.x = Mathf.Clamp01(normalized);
            fill.anchorMax = max;
        }

        public static void SetLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;
        }
    }
}
