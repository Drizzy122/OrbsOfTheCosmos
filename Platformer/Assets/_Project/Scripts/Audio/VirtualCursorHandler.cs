using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem.UI;

public class UIToolkitCursorSync : MonoBehaviour
{
    [SerializeField] private PanelRenderer uiDocument;
    [SerializeField] private VirtualMouseInput virtualMouseInput;

    private VisualElement cursorElement;

    void OnEnable()
    {
        if (uiDocument != null) uiDocument.RegisterUIReloadCallback(OnUIReload);
    }

    void OnDisable()
    {
        if (uiDocument != null) uiDocument.UnregisterUIReloadCallback(OnUIReload);
    }

    void OnUIReload(PanelRenderer _, VisualElement root)
    {
        // Grab the cursor we made in UI Builder
        cursorElement = root.Q<VisualElement>("VirtualCursor");
    }

    
    
    void Update()
    {
        if (cursorElement == null || virtualMouseInput == null) return;

        // 1. Get the virtual mouse position from the component
        Vector2 mousePos = virtualMouseInput.virtualMouse.position.ReadValue();

        // 2. Convert screen coordinates to UI Toolkit coordinates
        // UI Toolkit (0,0) is top-left. Screen (0,0) is bottom-left.
        float uiX = mousePos.x;
        float uiY = Screen.height - mousePos.y;

        // 3. Update the UI element position
        cursorElement.style.left = uiX;
        cursorElement.style.top = uiY;
    }
}