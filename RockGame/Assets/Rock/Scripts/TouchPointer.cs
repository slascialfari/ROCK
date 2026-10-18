using UnityEngine;

// The one place that reads the finger (or mouse in the editor). Screen pixels, origin bottom-left.
public static class TouchPointer
{
    public static bool Read(out bool pressed, out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        var pointer = UnityEngine.InputSystem.Pointer.current;
        pressed = false;
        position = default;
        if (pointer == null) return false;
        pressed = pointer.press.isPressed;
        position = pointer.position.ReadValue();
        return true;
#else
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            pressed = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            position = touch.position;
            return true;
        }
        pressed = Input.GetMouseButton(0);
        position = Input.mousePosition;
        return true;
#endif
    }
}
