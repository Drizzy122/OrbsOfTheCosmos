using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace Platformer
{
    public class SaveSlot
    {
        private string profileId;
        private VisualElement rootSlotElement;
        
        private VisualElement noDataContent;
        private VisualElement hasDataContent;
        private Label percentageCompleteText;
        private Button clearButton;
        private Label saveDateText;
        private VisualElement thumbnailImage;
        
        public Button saveSlotButton { get; private set; }
        public Button getClearButton { get { return clearButton; } } // Expose for SaveSlotsMenu to bind to
        
        public bool hasData { get; private set; } = false;

        public SaveSlot(VisualElement slotElement, string profileId) 
        {
            this.rootSlotElement = slotElement;
            this.profileId = profileId;

            // Query the specific parts inside this slot
            saveSlotButton = rootSlotElement.Q<Button>("SaveSlotButton");
            noDataContent = rootSlotElement.Q<VisualElement>("NoDataContent");
            hasDataContent = rootSlotElement.Q<VisualElement>("HasDataContent");
            percentageCompleteText = rootSlotElement.Q<Label>("PercentageCompleteText");
            saveDateText = rootSlotElement.Q<Label>("SaveDateText");
            clearButton = rootSlotElement.Q<Button>("ClearButton");
            thumbnailImage = hasDataContent.Q<VisualElement>("ThumbnailImage");
            
        }

        public void SetData(GameData data) 
        {
            if (data == null) 
            {
                hasData = false;
                noDataContent.style.display = DisplayStyle.Flex;
                hasDataContent.style.display = DisplayStyle.None;
                clearButton.style.display = DisplayStyle.None;
                
                
            }
            else 
            {
                hasData = true;
                noDataContent.style.display = DisplayStyle.None;
                hasDataContent.style.display = DisplayStyle.Flex;
                clearButton.style.display = DisplayStyle.Flex;

                percentageCompleteText.text = data.GetPercentageComplete() + "% COMPLETE";
                
                System.DateTime saveTime = System.DateTime.FromBinary(data.lastUpdated);
                saveDateText.text = saveTime.ToString("MM/dd/yyyy HH:mm");
                LoadDynamicThumbnail();
            }
        }
        
        private void LoadDynamicThumbnail()
        {
            
            // The real shot, captured when the player last paused, lives next to the save in
            // persistentDataPath. There is no Resources fallback any more — those could only
            // ever be static placeholders, and a profile that has not been quicksaved simply
            // shows the styled empty frame instead.
            Texture2D loadedTexture = SaveThumbnail.Load(profileId);

            if (loadedTexture != null)
            {
                thumbnailImage.style.backgroundImage = new StyleBackground(loadedTexture);
                thumbnailImage.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            }
            else
            {
                // Cleared rather than left as-is: slots are reused across profiles, so a
                // stale texture would otherwise linger on a save that has no thumbnail.
                thumbnailImage.style.backgroundImage = StyleKeyword.None;
            }
        }

        public string GetProfileId() 
        {
            return this.profileId;
        }

        public void SetInteractable(bool interactable)
        {
            saveSlotButton.SetEnabled(interactable);
            clearButton.SetEnabled(interactable);
        }
    }
}