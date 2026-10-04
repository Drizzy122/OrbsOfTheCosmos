using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static PlayerInputActions;

namespace Platformer
{
    public interface IInputReader {
        Vector2 Direction { get; }
        void EnablePlayerActions();
    }

    [CreateAssetMenu(fileName = "InputReader", menuName = "Platformer/InputReader")]
    public class InputReader : ScriptableObject, IPlayerActions, IMenuActions
    {
        [field: Header("Player Actions Events")]
        public event UnityAction<Vector2> Move = delegate { };
        public event UnityAction<Vector2> Look = delegate { };
        public event UnityAction<bool> Sprint = delegate { };
        public event UnityAction<bool> Jump = delegate { };
        public event UnityAction<bool> Dodge = delegate { };
        public event UnityAction<bool> SonarPulse = delegate { };
        public event UnityAction<bool> Wallclimb = delegate { };
        public event UnityAction<bool> Glide = delegate { };
        public event UnityAction LightAttack = delegate { };
        public event UnityAction BlastAttack = delegate { };
        public event UnityAction SwapWeapon = delegate { };
        public event UnityAction<bool> interact = delegate { };
        public event UnityAction<bool> Aim = delegate { };
        public event UnityAction<bool> submit = delegate { };
        public event UnityAction Paused = delegate { };
        public event UnityAction SkipIntro = delegate { };
        public event UnityAction CharacterMenu = delegate { };
        public event UnityAction PreviousTab = delegate { };
        public event UnityAction NextTab = delegate { };
        public event UnityAction<RaycastHit> Click = delegate { };
        public event UnityAction Counter = delegate { };
      
        PlayerInputActions inputActions;
        
        //public bool IsJumpKeyPressed() => inputActions.Player.Jump.IsPressed();
        
        public bool IsJumpKeyPressed = false;
        
        
        public Vector2 Direction => inputActions.Player.Move.ReadValue<Vector2>();
        public bool CounterPressed { get; set; }
        
        // 1. Add this variable right under your other public variables at the top
        public bool IsUsingMouse { get; private set; }
        
        public void EnablePlayerActions()
        {
            if (inputActions == null)
            {
                inputActions = new PlayerInputActions();
                inputActions.Player.SetCallbacks(this);
                inputActions.Menu.SetCallbacks(this);
            }
            inputActions.Enable();
        }

        public void EnablePlayerMap()
        {
            if (inputActions != null) inputActions.Player.Enable();
        }

        public void DisablePlayerMap()
        {
            if (inputActions != null) inputActions.Player.Disable();
        }
        public void OnMove(InputAction.CallbackContext context)
        {
            Move.Invoke(context.ReadValue<Vector2>());
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            if (context.control != null && context.control.device != null)
            {
                IsUsingMouse = context.control.device is Mouse;
            }

            Look.Invoke(context.ReadValue<Vector2>());
        }

        public void OnLightAttack(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
            {
                LightAttack.Invoke();
            }
        }

        public void OnBlastAttack(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
            {
                BlastAttack.Invoke();
            }
        }

        public void OnSwapWeapon(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
            {
                SwapWeapon.Invoke();
            }
        }
        public void OnJump(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    IsJumpKeyPressed = true; // 2. Set to TRUE when pressed
                    Jump.Invoke(true);
                    break;
                case InputActionPhase.Canceled:
                    IsJumpKeyPressed = false; // 3. Set to FALSE when released
                    Jump.Invoke(false);
                    break;
            }
        }
        public void OnDodge(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    Dodge.Invoke(true);
                    break;
                case InputActionPhase.Canceled:
                    Dodge.Invoke(false);
                    break;
            }
        }

        public void OnSonarPulse(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    SonarPulse.Invoke(true);
                    break;
                case InputActionPhase.Canceled:
                    SonarPulse.Invoke(false);
                    break;
            }
        }
        public void OnWallclimb(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    Wallclimb.Invoke(true);
                    break;
                case InputActionPhase.Canceled:
                    Wallclimb.Invoke(false);
                    break;
            }
        }
        public void OnGlide(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    Glide.Invoke(true);
                    break;
                case InputActionPhase.Canceled:
                    Glide.Invoke(false);
                    break;
            }
        }
        public void OnInteract(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    interact.Invoke(true);
                    break;
                case InputActionPhase.Canceled:
                    interact.Invoke(false);
                    break;
            }
        }
        // Menu-map actions routed into the global event bus. They live on the
        // Menu map (always enabled) so submit keeps working while dialogue
        // disables the Player map. InputEvents stamps the current context.
        public void OnSubmit(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started && GameEventsManager.instance != null)
            {
                GameEventsManager.instance.inputEvents.SubmitPressed();
            }
        }

        public void OnQuestLogToggle(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started && GameEventsManager.instance != null)
            {
                GameEventsManager.instance.inputEvents.QuestLogTogglePressed();
            }
        }


        void OnEnable()
        {
            if (inputActions == null)
            {
                inputActions = new PlayerInputActions();
                inputActions.Player.SetCallbacks(this);
                inputActions.Menu.SetCallbacks(this);
            }
            EnablePlayerActions();
        }
        void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Disable();
            }
        }
        // Generated from the Menu map. IMenuActions requires this once SkipIntro exists in
        // the asset, so it has to land in the same change or nothing compiles.
        public void OnSkipIntro(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                SkipIntro.Invoke();
            }
        }

        // Opens the tabbed hub. Separate from Pause so the system menu and the gameplay
        // screen no longer fight over one button.
        public void OnCharacterMenu(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                CharacterMenu.Invoke();
            }
        }

        public void OnPause(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                Paused.Invoke();
            }
        }

        public void OnPreviousTab(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                PreviousTab.Invoke();
        }

        public void OnNextTab(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                NextTab.Invoke();
        }
        
        public void OnCounter(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
            {
                Counter.Invoke();
                CounterPressed = true;
            }
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    Sprint.Invoke(true);
                    break;
                case InputActionPhase.Canceled:
                    Sprint.Invoke(false);
                    break;
            }
        }

        public void OnAim(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    Aim.Invoke(true);
                    break;
                case InputActionPhase.Canceled:
                    Aim.Invoke(false);
                    break;
            }
        }
    }
}