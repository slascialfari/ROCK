using System;
using UnityEngine;

// Temporary developer options: erosion speed and "new rock". Handles its own touch input
// (IMGUI is used only for drawing), so it works the same in the editor and on the iPhone.
public class DevMenu : MonoBehaviour
{
    const string SpeedKey = "dev.erosionSpeed";
    const float ConfirmWindow = 3f;

    enum Control { None, Toggle, Slider, SpeedOne, Reset, Close, Outside }

    Rock rock;
    Action onReset;
    bool open, wasPressed;
    Control active;
    float confirmResetUntil;
    GUIStyle label, button, title;
    Rect toggle, panel, slider, speedOne, reset, close;

    public void Init(Rock rock, Action onReset)
    {
        this.rock = rock;
        this.onReset = onReset;
        rock.ErosionSpeed = PlayerPrefs.GetFloat(SpeedKey, 1f);
    }

    // For SwipeInput: touches on the menu button or anywhere while the menu is open are not kicks.
    public bool Captures(Vector2 screenPosition)
    {
        Layout();
        return open || toggle.Contains(ToGui(screenPosition));
    }

    void Update()
    {
        if (rock == null || !TouchPointer.Read(out bool pressed, out Vector2 position)) return;
        Layout();
        Vector2 p = ToGui(position);
        bool began = pressed && !wasPressed;
        bool ended = !pressed && wasPressed;
        wasPressed = pressed;

        if (began) active = HitTest(p);

        if (pressed && active == Control.Slider)
            rock.ErosionSpeed = SliderToSpeed(Mathf.InverseLerp(slider.xMin, slider.xMax, p.x));

        if (!ended) return;
        switch (active)
        {
            case Control.Toggle when toggle.Contains(p): open = !open; confirmResetUntil = 0f; break;
            case Control.Close when close.Contains(p): open = false; break;
            case Control.Outside: open = false; break;
            case Control.SpeedOne when speedOne.Contains(p): rock.ErosionSpeed = 1f; break;
            case Control.Reset when reset.Contains(p):
                if (Time.unscaledTime < confirmResetUntil)
                {
                    confirmResetUntil = 0f;
                    onReset?.Invoke();
                }
                else confirmResetUntil = Time.unscaledTime + ConfirmWindow;
                break;
        }
        if (active == Control.Slider || active == Control.SpeedOne)
        {
            PlayerPrefs.SetFloat(SpeedKey, rock.ErosionSpeed);
            PlayerPrefs.Save();
        }
        active = Control.None;
    }

    Control HitTest(Vector2 p)
    {
        if (toggle.Contains(p)) return Control.Toggle;
        if (!open) return Control.None;
        if (slider.Contains(p)) return Control.Slider;
        if (speedOne.Contains(p)) return Control.SpeedOne;
        if (reset.Contains(p)) return Control.Reset;
        if (close.Contains(p)) return Control.Close;
        return panel.Contains(p) ? Control.None : Control.Outside;
    }

    void Layout()
    {
        float u = Mathf.Min(Screen.width, Screen.height) / 100f;
        Rect safe = Screen.safeArea;
        float top = Screen.height - safe.yMax + u * 3f;
        toggle = new Rect(safe.xMax - u * 19f, top, u * 16f, u * 8f);

        float w = Mathf.Min(safe.width - u * 6f, u * 90f);
        float x = safe.x + (safe.width - w) * 0.5f;
        panel = new Rect(x, top + u * 20f, w, u * 72f); // below the HUD stats
        float inner = w - u * 8f;
        slider = new Rect(x + u * 4f, panel.y + u * 22f, inner, u * 9f);
        speedOne = new Rect(x + w - u * 20f, panel.y + u * 11f, u * 16f, u * 7f);
        reset = new Rect(x + u * 4f, panel.y + u * 47f, inner, u * 9f);
        close = new Rect(x + u * 4f, panel.y + u * 59f, inner, u * 9f);
    }

    void OnGUI()
    {
        if (rock == null) return;
        Layout();
        float u = Mathf.Min(Screen.width, Screen.height) / 100f;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.white } };
            title = new GUIStyle(label) { fontStyle = FontStyle.Bold };
            button = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        label.fontSize = Mathf.RoundToInt(u * 3.4f);
        title.fontSize = Mathf.RoundToInt(u * 4f);
        button.fontSize = Mathf.RoundToInt(u * 3.4f);

        Fill(toggle, new Color(0f, 0f, 0f, open ? 0.7f : 0.35f));
        GUI.Label(toggle, "DEV", button);
        if (!open) return;

        Fill(panel, new Color(0.08f, 0.08f, 0.09f, 0.88f));
        float x = panel.x + u * 4f;
        GUI.Label(new Rect(x, panel.y + u * 3f, panel.width, u * 6f), "DEV OPTIONS", title);

        string speed = rock.ErosionSpeed <= 0f ? "OFF" : $"x{rock.ErosionSpeed:0.##}";
        GUI.Label(new Rect(x, panel.y + u * 11f, panel.width, u * 7f), $"Erosion speed  {speed}", label);
        Fill(speedOne, new Color(1f, 1f, 1f, 0.15f));
        GUI.Label(speedOne, "x1", button);

        // Slider: track + knob.
        Rect track = new Rect(slider.x, slider.center.y - u * 0.6f, slider.width, u * 1.2f);
        Fill(track, new Color(1f, 1f, 1f, 0.25f));
        float knobX = Mathf.Lerp(slider.xMin, slider.xMax, SpeedToSlider(rock.ErosionSpeed));
        Fill(new Rect(knobX - u * 2f, slider.center.y - u * 3f, u * 4f, u * 6f), Color.white);

        var e = rock.Erosion;
        GUI.Label(new Rect(x, panel.y + u * 33f, panel.width, u * 6f),
            $"Roundness {e.Roundness * 100f:0.00}%   Chips {rock.Chips}", label);
        GUI.Label(new Rect(x, panel.y + u * 39f, panel.width, u * 6f),
            $"Mass {rock.Body.mass * 1000f:0} g   Collider {(rock.IsSphereCollider ? "sphere" : "hull")}", label);

        bool confirming = Time.unscaledTime < confirmResetUntil;
        Fill(reset, confirming ? new Color(0.75f, 0.2f, 0.15f, 0.9f) : new Color(1f, 1f, 1f, 0.15f));
        GUI.Label(reset, confirming ? "TAP AGAIN TO REPLACE YOUR ROCK" : "NEW ROCK (RESET)", button);
        Fill(close, new Color(1f, 1f, 1f, 0.15f));
        GUI.Label(close, "CLOSE", button);
    }

    // Slider: far left = off, then logarithmic from x0.1 to x1000.
    const float OffZone = 0.04f;
    static float SliderToSpeed(float t) =>
        t < OffZone ? 0f : Mathf.Pow(10f, Mathf.Lerp(-1f, 3f, (t - OffZone) / (1f - OffZone)));

    static float SpeedToSlider(float speed) =>
        speed <= 0f ? 0f : OffZone + (1f - OffZone) * Mathf.InverseLerp(-1f, 3f, Mathf.Log10(speed));

    static Vector2 ToGui(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
