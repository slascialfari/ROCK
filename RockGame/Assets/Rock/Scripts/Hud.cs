using UnityEngine;

// Prototype HUD drawn with IMGUI: zero setup, fine for Build 001. Replaced with polished UI in Build 002.
public class Hud : MonoBehaviour
{
    Rock rock;
    GUIStyle big, small;
    float fps = 60f;

    public void Init(Rock rock) => this.rock = rock;

    void Update() => fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);

    void OnGUI()
    {
        if (rock == null) return;
        float unit = Mathf.Min(Screen.width, Screen.height) / 100f;
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }
        big.fontSize = Mathf.RoundToInt(unit * 4f);
        small.fontSize = Mathf.RoundToInt(unit * 2.8f);

        Rect safe = Screen.safeArea; // origin bottom-left; GUI origin is top-left
        float left = safe.xMin + unit * 3f;
        float top = Screen.height - safe.yMax + unit * 3f;

        Label(new Rect(left, top, unit * 80f, unit * 6f), $"KICKS  {rock.Kicks:N0}", big);
        Label(new Rect(left, top + unit * 5.5f, unit * 80f, unit * 6f), $"DISTANCE  {FormatDistance(rock.Distance)}", big);
        float roundness = rock.Erosion.Roundness * 100f;
        Label(new Rect(left, top + unit * 11f, unit * 80f, unit * 6f),
            roundness < 99f ? $"ROUNDNESS  {roundness:0.0}%" : $"ROUNDNESS  {roundness:0.00}%", big);

        if (rock.Kicks == 0)
            Label(new Rect(0f, Screen.height - safe.yMin - unit * 16f, Screen.width, unit * 6f), "swipe up to kick", small);

        if (Debug.isDebugBuild)
        {
            small.alignment = TextAnchor.UpperRight;
            Label(new Rect(safe.xMax - unit * 23f, top + unit * 9f, unit * 20f, unit * 4f), $"{fps:0} fps", small);
            small.alignment = TextAnchor.MiddleCenter;
        }
    }

    static string FormatDistance(float meters) =>
        meters < 1000f ? $"{meters:0.0} m" : $"{meters / 1000f:0.00} km";

    static void Label(Rect rect, string text, GUIStyle style)
    {
        style.normal.textColor = new Color(0f, 0f, 0f, 0.45f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = Color.white;
        GUI.Label(rect, text, style);
    }
}
