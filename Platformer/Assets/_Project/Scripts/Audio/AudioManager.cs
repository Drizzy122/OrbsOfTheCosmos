using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using UnityEngine.UIElements;

namespace Platformer
{
    public class AudioManager : MonoBehaviour
    {
        [field: Header("Game Volume")]
        [SerializeField] PanelRenderer settingsUIDocument;
        [field: Range(0, 1)] public float masterVolume = 1;
        [field: Range(0, 1)] public float musicVolume = 1;
        [field: Range(0, 1)] public float ambienceVolume = 1;
        [field: Range(0, 1)] public float SFXVolume = 1;
        
        private Bus masterBus;
        private Bus musicBus;
        private Bus ambienceBus;
        private Bus sfxBus;
        
        private List<EventInstance> eventInstances;
        private List<StudioEventEmitter> eventEmitters;
        
        private EventInstance ambienceEventInstance;
        private EventInstance musicEventInstance;

        public static AudioManager instance { get; private set; }

        private void Awake()
        {
            if (instance != null)
            {
                Debug.LogError("Found more than one AudioManager in the scene");
            }
            instance = this;
            eventInstances = new List<EventInstance>();
            eventEmitters  = new List<StudioEventEmitter>();
            
            masterBus   = RuntimeManager.GetBus("bus:/");
            musicBus    = RuntimeManager.GetBus("bus:/Music");
            ambienceBus = RuntimeManager.GetBus("bus:/Ambience");
            sfxBus      = RuntimeManager.GetBus("bus:/SFX");
        }

        private void Start()
        {
            InitiializeAmbience(FMODEvents.instance.ambience);
            IntializeMusic(FMODEvents.instance.music);
            SetupUIToolkit();
        }

        // -------------------------------------------------------------------------
        // Footsteps — fire and forget, NOT added to the tracked list
        // -------------------------------------------------------------------------
        public void PlayFootstep(EventReference eventReference, Vector3 worldPos, int terrain, int walkRun)
        {
            EventInstance instance = RuntimeManager.CreateInstance(eventReference);
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(worldPos));
            instance.setParameterByName("Terrain", terrain);
            instance.setParameterByName("WalkRun", walkRun);
            instance.start();
            instance.release(); // FMOD cleans this up automatically after it finishes
        }

        // -------------------------------------------------------------------------
        // One-shot (no parameters needed)
        // -------------------------------------------------------------------------
        public void PlayOneShot(EventReference sound, Vector3 worldPos)
        {
            RuntimeManager.PlayOneShot(sound, worldPos);
        }
        public void RegisterButtonAudio(Button button, bool isCloseAction = false)
        {
            button.RegisterCallback<PointerEnterEvent>(evt =>
                PlayOneShot(FMODEvents.instance.ui, Vector3.zero));

            button.clicked += () =>
                PlayOneShot(isCloseAction ? FMODEvents.instance.uiclose : FMODEvents.instance.uiopen, Vector3.zero);
        }

        // -------------------------------------------------------------------------
        // Long-lived instances (music, ambience, etc.) — tracked for cleanup
        // -------------------------------------------------------------------------
        public EventInstance CreateEventInstance(EventReference eventReference)
        {
            EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);
            eventInstances.Add(eventInstance);
            return eventInstance;   
        }

        public StudioEventEmitter InitializeEventEmitter(EventReference eventReference, GameObject emitterGameObject)
        {
            StudioEventEmitter emitter = emitterGameObject.GetComponent<StudioEventEmitter>();
            emitter.EventReference = eventReference;
            eventEmitters.Add(emitter);
            return emitter;
        }

        // -------------------------------------------------------------------------
        // Ambience
        // -------------------------------------------------------------------------
        private void InitiializeAmbience(EventReference ambienceEventReference)
        {
            ambienceEventInstance = CreateEventInstance(ambienceEventReference);
            ambienceEventInstance.start();
        }

        public void SetAmbienceParameter(string parameterName, float parameterValue)
        {
            ambienceEventInstance.setParameterByName(parameterName, parameterValue);
        }

        // -------------------------------------------------------------------------
        // Music
        // -------------------------------------------------------------------------
        private void IntializeMusic(EventReference musicEventReference)
        {
            musicEventInstance = CreateEventInstance(musicEventReference);
            musicEventInstance.start();
        }

        public void SetMusicArea(MusicArea area)
        {
            musicEventInstance.setParameterByName("Area", (float)area);
        }

        public void SetMusicParameter(string parameterName, float parameterValue)
        {
            musicEventInstance.setParameterByName(parameterName, parameterValue);
        }

        public void StopMusic()
        {
            if (musicEventInstance.isValid())
                musicEventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        // -------------------------------------------------------------------------
        // Cleanup
        // -------------------------------------------------------------------------
        private void CleanUp()
        {
            foreach (EventInstance eventInstance in eventInstances)
            {
                eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                eventInstance.release();
            }
            foreach (StudioEventEmitter emitter in eventEmitters)
            {
                emitter.Stop();
            }
        }

        private void OnDestroy()
        {
            if (settingsUIDocument != null) settingsUIDocument.UnregisterUIReloadCallback(OnSettingsUIReload);
            CleanUp();
        }

        // -------------------------------------------------------------------------
        // UI Volume Sliders
        // -------------------------------------------------------------------------
        private void SetupUIToolkit()
        {
            if (settingsUIDocument == null) return;
            settingsUIDocument.RegisterUIReloadCallback(OnSettingsUIReload);
        }

        private void OnSettingsUIReload(PanelRenderer _, VisualElement root)
        {
            Slider masterSlider   = root.Q<Slider>("MasterVolumeSlider");
            Slider musicSlider    = root.Q<Slider>("MusicVolumeSlider");
            Slider sfxSlider      = root.Q<Slider>("SFXVolumeSlider");
            Slider ambienceSlider = root.Q<Slider>("AmbienceVolumeSlider");

            if (masterSlider != null)   masterSlider.value   = masterVolume;
            if (musicSlider != null)    musicSlider.value    = musicVolume;
            if (sfxSlider != null)      sfxSlider.value      = SFXVolume;
            if (ambienceSlider != null) ambienceSlider.value = ambienceVolume;

            masterSlider?.RegisterValueChangedCallback(evt => {
                masterVolume = evt.newValue;
                masterBus.setVolume(masterVolume);
            });

            musicSlider?.RegisterValueChangedCallback(evt => {
                musicVolume = evt.newValue;
                musicBus.setVolume(musicVolume);
            });

            sfxSlider?.RegisterValueChangedCallback(evt => {
                SFXVolume = evt.newValue;
                sfxBus.setVolume(SFXVolume);
            });

            ambienceSlider?.RegisterValueChangedCallback(evt => {
                ambienceVolume = evt.newValue;
                ambienceBus.setVolume(ambienceVolume);
            });
        }
    }
}