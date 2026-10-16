using System;

namespace Platformer
{
    public class PlayerEvents
    {
        public event Action onDisablePlayerMovement;
        public void DisablePlayerMovement()
        {
            if (onDisablePlayerMovement != null) 
            {
                onDisablePlayerMovement();
            }
        }

        public event Action onEnablePlayerMovement;
        public void EnablePlayerMovement()
        {
            if (onEnablePlayerMovement != null) 
            {
                onEnablePlayerMovement();
            }
        }

        public event Action<int> onExperienceGained;
        public void ExperienceGained(int experience)
        {
            if (onExperienceGained != null)
            {
                onExperienceGained(experience);
            }
        }
        
        public event Action<int> onPlayerLevelChange;
        public void PlayerLevelChange(int level) 
        {
            if (onPlayerLevelChange != null) 
            {
                onPlayerLevelChange(level);
            }
        }

        public event Action<int> onPlayerExperienceChange;
        public void PlayerExperienceChange(int experience)
        {
            if (onPlayerExperienceChange != null)
            {
                onPlayerExperienceChange(experience);
            }
        }

        public event Action<int> onLuminChargesChanged;
        public void LuminChargesChanged(int charges) => onLuminChargesChanged?.Invoke(charges);

        public event Action<int> onComboChanged;
        public void ComboChanged(int combo) => onComboChanged?.Invoke(combo);

        /// <summary>Current and max health in hit points, for the HUD readout.</summary>
        public event Action<float, float> onPlayerHealthChanged;
        public void PlayerHealthChanged(float current, float max) => onPlayerHealthChanged?.Invoke(current, max);

        public event Action<float> onPlayerHit;
        public void PlayerHit(float knockback)
        {
            if (onPlayerHit != null)
            {
                onPlayerHit?.Invoke(knockback);
            }
        }

        public event Action onPlayerDeath;
        public void PlayerDeath()
        {
            if (onPlayerDeath != null)
            {
                onPlayerDeath?.Invoke();
            }
        }
    }
}