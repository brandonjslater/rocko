using UnityEngine;

namespace Rocko
{
    public sealed class EngineAudio : MonoBehaviour
    {
        public RockoVehicle car;
        private AudioSource source;

        //
        // @description - Generate a temporary four-cylinder-like harmonic loop without licensed samples
        // @return {Void} - Creates a quiet spatial engine sound
        //
        private void Start()
        {
            source = gameObject.AddComponent<AudioSource>();
            const int rate = 44100;
            float[] samples = new float[rate];
            for (int i = 0; i < samples.Length; i++)
            {
                float phase = i * 2f * Mathf.PI * 100f / rate;
                samples[i] = 0.35f * Mathf.Sin(phase) + 0.16f * Mathf.Sin(phase * 2) + 0.06f * Mathf.Sin(phase * 4);
            }
            AudioClip clip = AudioClip.Create("Synthesized engine placeholder", rate, 1, rate, false);
            clip.SetData(samples, 0); source.clip = clip; source.loop = true;
            source.spatialBlend = 0.8f; source.minDistance = 2; source.maxDistance = 35;
            source.dopplerLevel = 0; source.volume = 0.15f; source.Play();
        }

        //
        // @description - Follow simulated engine speed and pause state
        // @return {Void} - Adjusts loop pitch and loudness
        //
        private void Update()
        {
            source.pitch = Mathf.Clamp(car.RPM / 3000f, 0.15f, 3f);
            RockoIgnition ignition = car.GetComponent<CarAccess>()?.ignition;
            if (ignition != null && ignition.StarterEngaged && Time.timeScale > 0)
            { source.pitch = 0.22f; source.volume = 0.09f; return; }
            source.volume = car.EngineRunning && Time.timeScale > 0 ? 0.08f + Mathf.Abs(car.CrankTorqueNm) / 165f * 0.13f : 0;
        }

        //
        // @description - Release the generated audio buffer
        // @return {Void} - Disposes the runtime clip
        //
        private void OnDestroy()
        {
            if (source != null && source.clip != null) Destroy(source.clip);
        }
    }
}
