using UnityEngine;

/// <summary>
/// Sonidos de feedback generados por codigo (ondas sinusoidales), sin archivos de audio.
///
/// En VR el sonido no es decoracion: confirma acciones que la vista no siempre
/// alcanza a mostrar (agarrar una pieza, soltarla, equivocarse). Al ir en un
/// AudioSource 3D posicionado en el mundo, ademas ayuda a ubicar donde paso.
/// </summary>
public class AudioCues : MonoBehaviour
{
    AudioSource src;

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;   // 3D: se ubica en el espacio
        src.minDistance = 1f;
        src.maxDistance = 12f;
    }

    public void Grab()    => Play(660f, 0.07f);
    public void Release() => Play(440f, 0.07f);
    public void Success() => Play(880f, 0.18f);
    public void Error()   => Play(180f, 0.30f);
    public void Finish()  => Play(1046f, 0.45f);

    void Play(float hz, float seconds)
    {
        src.PlayOneShot(Tone(hz, seconds), 0.5f);
    }

    /// <summary>Genera un tono con envolvente para que no chasquee al cortar.</summary>
    static AudioClip Tone(float hz, float seconds)
    {
        const int rate = 44100;
        int n = Mathf.Max(1, Mathf.RoundToInt(rate * seconds));
        var data = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Min(1f, i / (rate * 0.01f))                 // ataque
                      * Mathf.Min(1f, (n - i) / (rate * 0.05f));          // caida
            data[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * env;
        }

        var clip = AudioClip.Create($"tone{(int)hz}", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
