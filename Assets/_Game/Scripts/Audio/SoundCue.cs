using System;
using UnityEngine;

/// <summary>
/// One game sound: one or more clip variations and how loud they play.
/// Plain class: SoundLibrarySO holds one per sound.
/// </summary>
[Serializable]
public class SoundCue
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [Tooltip("Random pitch change, ± this much, so repeats don't sound identical. 0 = always the same.")]
    [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.05f;

    [NonSerialized] private int lastIndex = -1;

    public int Count => clips == null ? 0 : clips.Length;
    public float Volume => volume;

    /// <summary>A random variation, never the same one twice in a row. Null when empty.</summary>
    public AudioClip Pick()
    {
        if (Count == 0) return null;
        if (Count == 1) return clips[0];

        int index;
        if (lastIndex < 0) index = UnityEngine.Random.Range(0, Count);
        else
        {
            index = UnityEngine.Random.Range(0, Count - 1);
            if (index >= lastIndex) index++; // skips the last one played
        }
        lastIndex = index;
        return clips[index];
    }

    /// <summary>The variation at index, clamped to the last one. For sounds played in order.</summary>
    public AudioClip At(int index) => Count == 0 ? null : clips[Mathf.Clamp(index, 0, Count - 1)];

    public float RandomPitch() => 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
}
