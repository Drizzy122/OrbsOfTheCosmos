using System;
using System.Collections.Generic;
using KBCore.Refs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Platformer
{
    [RequireComponent(typeof(PanelRenderer))]
    public class SaveSlotsMenu : MonoBehaviour
    {
        [field: Header("Components")] 
        [field: SerializeField, Self] PanelRenderer saveSlotsRenderer;
        [field: SerializeField, Anywhere] ConfirmationPopUpMenu confirmationPopupMenu;
     
        [Header("Scene")]
        [Tooltip("Scene Name to load to.")]
        [field: SerializeField] string gameSceneName = "Game";
     
        [field: Header("UI Elements")]
        private VisualElement rootContainer;
        private Button backButton;

        private SaveSlot[] saveSlots;
        private bool isLoadingGame = false;

        // This stores the instruction on how to go back to whoever opened this menu
        private Action onBackAction;

        private void Awake()
        {
            saveSlotsRenderer = GetComponent<PanelRenderer>();
            saveSlotsRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDestroy()
        {
            if (saveSlotsRenderer != null) saveSlotsRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            rootContainer = root.Q<VisualElement>("SaveSlotsMenuContainer");
            if (rootContainer == null) return;

            backButton = rootContainer.Q<Button>("BackButton");
            backButton.clicked += OnBackClicked;

            saveSlots = new SaveSlot[3];
            saveSlots[0] = new SaveSlot(rootContainer.Q<VisualElement>("SaveSlot1"), "profile1");
            saveSlots[1] = new SaveSlot(rootContainer.Q<VisualElement>("SaveSlot2"), "profile2");
            saveSlots[2] = new SaveSlot(rootContainer.Q<VisualElement>("SaveSlot3"), "profile3");

            foreach (SaveSlot slot in saveSlots)
            {
                SaveSlot currentSlot = slot; 
                currentSlot.saveSlotButton.clicked += () => OnSaveSlotClicked(currentSlot);
                currentSlot.getClearButton.clicked += () => OnClearClicked(currentSlot);
            }

            backButton.RegisterCallback<NavigationMoveEvent>(evt => 
            {
                if (evt.direction == NavigationMoveEvent.Direction.Down)
                {
                    // Check if the slot is interactable before focusing
                    if (saveSlots[0].saveSlotButton.enabledSelf) 
                    {
                        saveSlots[0].saveSlotButton.Focus();
                        evt.StopPropagation(); // Stop Unity from trying to auto-navigate
                    }
                }
            });

// 2. Tell the first Save Slot to go UP to the Back Button
            saveSlots[0].saveSlotButton.RegisterCallback<NavigationMoveEvent>(evt =>
            {
                if (evt.direction == NavigationMoveEvent.Direction.Up)
                {
                    backButton.Focus();
                    evt.StopPropagation(); // Stop Unity from trying to auto-navigate
                }
            });
            
            DeactivateMenu();
        }

        public void OnSaveSlotClicked(SaveSlot saveSlot)
        {
            DisableMenuButtons();

            if (isLoadingGame)
            {
                DataPersistenceManager.instance.ChangeSelectedProfileId(saveSlot.GetProfileId());
                SaveGameAndLoadScene();
            }
            else if (saveSlot.hasData)
            {
                confirmationPopupMenu.ActivateMenu(
                    "Starting a New Game with this slot will override the currently saved data. Are you sure?",
                    () => {
                        DataPersistenceManager.instance.ChangeSelectedProfileId(saveSlot.GetProfileId());
                        DataPersistenceManager.instance.NewGame();
                        SaveGameAndLoadScene();
                    },
                    // We pass this.onBackAction so the menu remembers where it came from!
                    () => { this.ActivateMenu(isLoadingGame, this.onBackAction); } 
                );
            }
            else
            {
                DataPersistenceManager.instance.ChangeSelectedProfileId(saveSlot.GetProfileId());
                DataPersistenceManager.instance.NewGame();
                SaveGameAndLoadScene();
            }
        }

        private void SaveGameAndLoadScene()
        {
            DataPersistenceManager.instance.SaveGame();
            SceneLoader.Load(gameSceneName);
        }

        public void OnClearClicked(SaveSlot saveSlot)
        {
            DisableMenuButtons();

            confirmationPopupMenu.ActivateMenu(
                "Are you sure you want to delete this saved data?",
                () => {
                    DataPersistenceManager.instance.DeleteProfileData(saveSlot.GetProfileId());
                    ActivateMenu(isLoadingGame, this.onBackAction);
                },
                () => { ActivateMenu(isLoadingGame, this.onBackAction); }
            );
        }

        public void OnBackClicked()
        {
            this.DeactivateMenu();
            // This triggers the action (either opening PauseMenu or MainMenu)
            onBackAction?.Invoke(); 
        }

        // Updated to require the Action
        public void ActivateMenu(bool isLoadingGame, Action onBackAction)
        {
            if (rootContainer == null) return;   // UI not built yet
            this.onBackAction = onBackAction;

            rootContainer.style.display = DisplayStyle.Flex;
            this.isLoadingGame = isLoadingGame;

            Dictionary<string, GameData> profilesGameData = DataPersistenceManager.instance.GetAllProfilesGameData();
            backButton.SetEnabled(true);

            foreach (SaveSlot saveSlot in saveSlots)
            {
                GameData profileData = null;
                profilesGameData.TryGetValue(saveSlot.GetProfileId(), out profileData);
                saveSlot.SetData(profileData);

                if (profileData == null && isLoadingGame)
                {
                    saveSlot.SetInteractable(false);
                }
                else
                {
                    saveSlot.SetInteractable(true);
                }
            }
            
            backButton.Focus(); 
        }

        public void DeactivateMenu()
        {
            if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
            if (confirmationPopupMenu != null)
            {
                confirmationPopupMenu.DeactivateMenu();
            }
        }

        private void DisableMenuButtons()
        {
            foreach (SaveSlot saveSlot in saveSlots)
            {
                saveSlot.SetInteractable(false);
            }
            backButton.SetEnabled(false);
        }
    }
}