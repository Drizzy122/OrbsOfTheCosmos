using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Required for Image
using System.Collections;
using System.Collections.Generic;

public class IntroSequence : MonoBehaviour
{
    [Header("Image Settings")]
    public Image splashImage;
    public CanvasGroup imageCanvasGroup;
    public float imageDuration = 3.0f; // How long to show the image

    [Header("Video Settings")]
    public VideoPlayer videoPlayer;
    public CanvasGroup videoCanvasGroup;
    public List<VideoClip> introClips; 
    public string nextSceneName; 

    [Header("Transition Settings")]
    public float fadeDuration = 1.0f;

    [Header("Skip")]
    [Tooltip("InputReader asset. Leave empty to disable skipping.")]
    public Platformer.InputReader input;

    private int videoIndex = 0;
    private bool skipRequested;

    void OnEnable()
    {
        if (input != null)
        {
            // The Menu map owns SkipIntro, and the intro scene has no gameplay — enable the
            // whole set so the action is live without the player having touched anything.
            input.EnablePlayerActions();
            input.SkipIntro += RequestSkip;
        }
    }

    void OnDisable()
    {
        if (input != null) input.SkipIntro -= RequestSkip;
    }

    // Each press raises the flag for exactly one segment; FullSequence consumes it, so
    // holding or mashing advances one clip at a time rather than skipping the sequence.
    void RequestSkip() => skipRequested = true;

    void ConsumeSkip() => skipRequested = false;

    void Start()
    {
        StartCoroutine(FullSequence());
    }

    IEnumerator FullSequence()
    {
        // --- PART 1: THE IMAGE ---
        if (splashImage != null)
        {
            yield return StartCoroutine(Fade(imageCanvasGroup, 0, 1));
            yield return WaitOrSkip(imageDuration);

            // One press advances one segment, so the flag is consumed here rather than left
            // set - otherwise a single press would tear through the whole sequence.
            ConsumeSkip();
            yield return StartCoroutine(Fade(imageCanvasGroup, 1, 0));
        }

        // --- PART 2: THE VIDEOS ---
        while (videoIndex < introClips.Count)
        {
            videoPlayer.clip = introClips[videoIndex];
            videoPlayer.Prepare();

            while (!videoPlayer.isPrepared) yield return null;

            // Restored every iteration. The loop fades this to 0 after each clip, so without
            // this the second and later videos would play fully transparent.
            videoCanvasGroup.alpha = 1;
            videoPlayer.Play();

            // Wait until the video is almost done (minus fade time)
            float waitTime = (float)videoPlayer.length - fadeDuration;
            yield return WaitOrSkip(Mathf.Max(0, waitTime));

            // Skipped mid-clip: stop it, consume the press, and fall through to the same
            // fade the clip would have got on its own.
            if (skipRequested)
            {
                videoPlayer.Stop();
                ConsumeSkip();
            }

            yield return StartCoroutine(Fade(videoCanvasGroup, 1, 0));

            videoIndex++;
        }



        // --- PART 3: LOAD SCENE ---
        SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>WaitForSeconds cannot be interrupted, so skipping would still sit through the
    /// rest of a clip's runtime. This polls the flag instead.</summary>
    IEnumerator WaitOrSkip(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator Fade(CanvasGroup cg, float start, float end)
    {
        float elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, end, elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = end;
    }
}