using UnityEngine;

/// <summary>
/// Plays sounds; knows nothing about the game. A few 2D AudioSources taken in turn (so overlapping
/// sounds don't cut each other off) and one more for a looping sound.
/// Volume / mute is the AudioListener's (GameSettings), not this class's.
/// </summary>
public class AudioPlayer : MonoBehaviour
{
    [Tooltip("How many sounds can play at once. The oldest one is cut when all are busy.")]
    [SerializeField, Min(1)] private int voices = 12;

    private AudioSource[] sources;
    private AudioSource loopSource;
    private int nextSource;

    private void Awake()
    {
        sources = new AudioSource[voices];
        for (int i = 0; i < voices; i++) sources[i] = CreateSource(false);
        loopSource = CreateSource(true);
    }

    /// <summary>A random variation of the cue.</summary>
    public void Play(SoundCue cue)
    {
        if (cue == null) return;
        Play(cue.Pick(), cue.Volume, cue.RandomPitch());
    }

    /// <summary>The cue's variation at index, at its own pitch. For sounds played in order.</summary>
    public void PlayAt(SoundCue cue, int index)
    {
        if (cue == null) return;
        Play(cue.At(index), cue.Volume, 1f);
    }

    /// <summary>
    /// A piece of the cue's first clip: from start (seconds) for length seconds, or to the clip's end when
    /// length is 0. For one clip holding several sounds in a row (the countdown's beats).
    /// </summary>
    public void PlaySegment(SoundCue cue, float start, float length)
    {
        AudioClip clip = cue?.At(0);
        if (clip == null || start >= clip.length) return;

        AudioSource source = NextSource();
        source.clip = clip;
        source.volume = cue.Volume;
        source.pitch = 1f;
        source.time = start;

        double now = AudioSettings.dspTime;
        source.PlayScheduled(now);
        source.SetScheduledEndTime(now + (length > 0f ? length : clip.length - start));
    }

    /// <summary>Starts looping the cue's first clip. Does nothing if that loop is already playing.</summary>
    public void StartLoop(SoundCue cue)
    {
        AudioClip clip = cue?.At(0);
        if (clip == null || (loopSource.isPlaying && loopSource.clip == clip)) return;

        loopSource.clip = clip;
        loopSource.volume = cue.Volume;
        loopSource.Play();
    }

    public void StopLoop()
    {
        if (loopSource != null) loopSource.Stop();
    }

    private void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;

        AudioSource source = NextSource();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();
    }

    // Round robin: a free source if there is one, otherwise the one that started longest ago.
    private AudioSource NextSource()
    {
        AudioSource source = sources[nextSource];
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource candidate = sources[(nextSource + i) % sources.Length];
            if (!candidate.isPlaying) { source = candidate; break; }
        }
        nextSource = (System.Array.IndexOf(sources, source) + 1) % sources.Length;
        return source;
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D: the camera is always next to the player
        return source;
    }
}
