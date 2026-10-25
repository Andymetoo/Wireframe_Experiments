using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestAnxiety
{
    /// <summary>Scene-owned procedural acoustics; scenario events own all discrete dramatic sounds.</summary>
    public sealed class TestAnxietyAudio : MonoBehaviour
    {
        readonly List<AudioClip> clips = new List<AudioClip>();
        readonly List<AudioSource> sources = new List<AudioSource>();
        readonly Dictionary<AudioSource, float> sourceLevels = new Dictionary<AudioSource, float>();
        readonly Dictionary<string, AudioClip[]> cues = new Dictionary<string, AudioClip[]>(StringComparer.OrdinalIgnoreCase);
        readonly List<AudioSource> eventVoices = new List<AudioSource>();
        readonly List<AudioClip> footsteps = new List<AudioClip>();
        AudioSource hum, rearBallast, ventilation, exterior, clock, desk, motor, resonance;
        AudioClip paper, pencil, feed, returnPaper, tick, tock, contradiction;
        float clockElapsed, ambienceTime, mood, moodTarget, fade = 1, masterVolume = .85f;
        bool silent, paused, built, moodExplicit;
        int footstepIndex, eventIndex, clockBeat;

        public float MasterVolume { get { return masterVolume; } set { SetVolume(value); } }

        public void Build(Transform parent)
        {
            if (built) return;
            built = true;
            transform.SetParent(parent, false);
            hum = Source("Front fluorescent ballast", new Vector3(-1.6f, 2.95f, 1.5f), .10f, .45f);
            rearBallast = Source("Rear fluorescent ballast", new Vector3(2.4f, 2.95f, -3.4f), .064f, .8f);
            ventilation = Source("Rear wall ventilation", new Vector3(3.8f, 2.8f, -5.85f), .115f, 1);
            exterior = Source("Courtyard air beyond the windows", new Vector3(-5.35f, 1.9f, .7f), .085f, 1);
            clock = Source("Clock above the blackboard", new Vector3(3.10f, 2.62f, 5.78f), .11f, 1);
            desk = Source("Paper and pencil at desk 014", new Vector3(0, .8f, -2), .34f, .35f);
            motor = Source("Optical reader motors", new Vector3(3.5f, 1.1f, 4.5f), .30f, .85f);
            resonance = Source("Low room resonance", new Vector3(0, .8f, -4.3f), .17f, 1);
            for (int i = 0; i < 8; i++) eventVoices.Add(Source("Spatial room event " + (i + 1), Vector3.zero, .25f, 1));
            hum.clip = Generate("Front ballast / 60 Hz and cabinet vibration", 8, 10, (t, n) =>
                (.20f * Sin(60, t) + .045f * Sin(120, t) + .022f * Sin(180, t)) * (.9f + .1f * Sin(.125f, t)) + .018f * n, true);
            rearBallast.clip = Generate("Rear ballast / loose cover", 11, 21, (t, n) =>
                .13f * Sin(60, t) + .045f * Sin(119.91f, t) + .023f * n * (.5f + .5f * Sin(.37f, t)), true);
            ventilation.clip = Generate("Air handling / duct turbulence", 13, 11, (t, n) =>
                (.16f * n + .018f * Sin(43, t) + .014f * Sin(86, t)) * (.77f + .11f * Sin(.077f, t) + .06f * Sin(.23f, t)), true, .08f);
            exterior.clip = Generate("Courtyard / soft overcast wind", 17, 25, (t, n) =>
                .19f * n * (.55f + .18f * Sin(.118f, t) + .14f * Sin(.294f, t)), true, .045f);
            foreach (AudioSource loop in new[] { hum, rearBallast, ventilation, exterior }) { loop.loop = true; loop.Play(); }
            paper = Generate("Paper lifted and folded", .63f, 12, (t, n) =>
                n * (.10f + .22f * Mathf.Pow(Mathf.Sin(t * 39), 2)) * Envelope(t, .63f));
            pencil = Generate("Graphite fills a bubble", .19f, 13, (t, n) =>
                n * .21f * (.3f + .7f * Mathf.Abs(Mathf.Sin(t * 240))) * Envelope(t, .19f));
            feed = Generate("Reader feed and optical head", 2.8f, 14, (t, n) =>
                (.12f * Mathf.Sin(2 * Mathf.PI * (105 * t + 11 * Mathf.Sin(t * 2))) + n * .07f + .02f * Sin(720, t)) * Envelope(t, 2.8f));
            returnPaper = Generate("Reader return rollers", 1.7f, 15, (t, n) =>
                (.13f * Mathf.Sin(2 * Mathf.PI * (145 * t - 8 * t * t)) + n * .13f) * Envelope(t, 1.7f));
            tick = Generate("Clock escapement tick", .065f, 17, (t, n) =>
                (.20f * n + .09f * Sin(1350, t)) * Mathf.Exp(-t * 90));
            tock = Generate("Clock escapement tock", .075f, 27, (t, n) =>
                (.17f * n + .10f * Sin(1040, t)) * Mathf.Exp(-t * 74));
            contradiction = Generate("Quiet cabinet resonance", 1.8f, 18, (t, n) =>
                (.12f * Sin(47, t) + .04f * Sin(71, t) + .017f * n) * Envelope(t, 1.8f));
            for (int i = 0; i < 6; i++)
            {
                int variant = i;
                footsteps.Add(Generate("Shoe on waxed vinyl " + (i + 1), .34f + i * .009f, 70 + i, (t, n) =>
                {
                    float heel = Pulse(t, .012f, 31), sole = Pulse(t, .087f + variant * .003f, 23);
                    return .13f * Sin(76 + variant * 7, t) * heel + n * (.23f * heel + .16f * sole);
                }));
            }
            cues["knock"] = new[] { Generate("Three muted knocks on classroom door", 1.48f, 40, (t, n) =>
                Knock(t, .055f, n, 1) + Knock(t, .43f, n, .86f) + Knock(t, .96f, n, .93f)) };
            cues["chair"] = new[] {
                Generate("Chair leg shifts across vinyl", 1.05f, 41, (t, n) =>
                    (.09f * Mathf.Sin(2 * Mathf.PI * (230 * t + 12 * Mathf.Sin(t * 8))) + .14f * n) * Envelope(t, 1.05f) * (.4f + .6f * Mathf.Sin(Mathf.PI * t / 1.05f))),
                Generate("Chair frame settles", .78f, 42, (t, n) =>
                    (.10f * Sin(189 + 20 * t, t) + .08f * n) * Envelope(t, .78f)) };
            cues["relay"] = new[] { Generate("Electrical contactor opens", .32f, 43, (t, n) =>
                (.18f * n + .10f * Sin(730, t)) * Pulse(t, .02f, 65) + .09f * n * Pulse(t, .14f, 80)) };
            cues["bell"] = new[] { Generate("School bell in a distant corridor", 3.3f, 44, (t, n) =>
                (.12f * Sin(1291, t) + .072f * Sin(1779, t) + .038f * Sin(2501, t)) *
                (Pulse(t, .02f, 1.35f) + .5f * Pulse(t, .38f, 1.7f)) * Envelope(t, 3.3f)) };
            cues["intercom"] = new[] { Generate("PA contact and filtered air", 2.75f, 45, (t, n) =>
            {
                float breath = Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - .38f) / 1.83f)));
                return n * (.075f + .045f * Sin(19, t)) * breath + .018f * Sin(180, t) * breath +
                    n * (.19f * Pulse(t, .03f, 90) + .17f * Pulse(t, 2.48f, 85));
            }, false, .18f) };
            cues["paperstorm"] = new[] { Generate("Reader briefly shuffles an unseen stack", 1.45f, 46, (t, n) =>
                (n * .24f + .065f * Sin(170, t)) * (.35f + .65f * Mathf.Abs(Mathf.Sin(t * 23))) * Envelope(t, 1.45f)) };
            cues["latch"] = new[] { Generate("Door latch disengages", .67f, 47, (t, n) =>
                (n * .18f + .11f * Sin(460, t)) * Pulse(t, .03f, 30) + (.16f * n + .06f * Sin(870, t)) * Pulse(t, .33f, 45)) };
            cues["creak"] = new[] {
                Generate("Exterior window frame flexes", 1.27f, 48, (t, n) =>
                    (.065f * Sin(95 + t * 16, t) + .08f * n) * Envelope(t, 1.27f)),
                Generate("Courtyard drain settles", .85f, 49, (t, n) =>
                    (.07f * Sin(360, t) + .06f * Sin(571, t) + .08f * n) * Pulse(t, .06f, 7)) };
            if (paused) foreach (AudioSource source in sources) source.Pause();
        }

        public void Tick(float deltaTime, float currentTension)
        {
            if (!built || paused || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            ambienceTime += deltaTime;
            mood = Mathf.MoveTowards(mood, moodExplicit ? moodTarget : Mathf.Clamp01(currentTension), deltaTime * .22f);
            fade = Mathf.MoveTowards(fade, silent ? 0 : 1, deltaTime * .8f);
            SetLevel(hum, (.10f + mood * .025f) * fade * (.95f + .05f * Mathf.Sin(ambienceTime * .19f)));
            SetLevel(rearBallast, (.064f + mood * .014f) * fade);
            SetLevel(ventilation, .115f * (1 - mood * .47f) * fade);
            SetLevel(exterior, .085f * (1 - mood * .32f) * fade);
            if (silent) return;
            clockElapsed += deltaTime;
            if (clockElapsed >= 1)
            {
                clockElapsed %= 1;
                OneShot(clock, (clockBeat++ & 1) == 0 ? tick : tock, .68f);
            }
            // No timer-driven chair scare: the controller owns the intent and position of each cue.
        }

        public void SetMood(float phase)
        {
            if (float.IsNaN(phase) || float.IsInfinity(phase)) return;
            moodExplicit = true;
            moodTarget = Mathf.Clamp01(phase);
        }

        public void SetVolume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume)) return;
            masterVolume = Mathf.Clamp01(volume);
            foreach (AudioSource source in sources) if (source != null) source.volume = sourceLevels[source] * masterVolume;
        }

        public void PlayFootstep(Vector3 position)
        {
            if (!built || paused) return;
            AudioSource voice = EventVoice(position + Vector3.up * .07f);
            SetLevel(voice, .30f);
            int index = FootstepVariant();
            voice.pitch = .96f + (footstepIndex % 5) * .017f;
            OneShot(voice, footsteps[index], .83f);
        }

        /// <summary>knock, chair, relay, bell, intercom, paperstorm, latch, creak, or step. Positions are world coordinates.</summary>
        public void PlayCue(string cue, Vector3 position)
        {
            if (!built || paused || string.IsNullOrWhiteSpace(cue)) return;
            bool step = string.Equals(cue, "step", StringComparison.OrdinalIgnoreCase);
            AudioClip[] variations;
            if (!step && !cues.TryGetValue(cue, out variations)) return;
            AudioSource voice = EventVoice(position);
            if (step)
            {
                SetLevel(voice, .21f);
                voice.pitch = .89f + (footstepIndex % 4) * .028f;
                OneShot(voice, footsteps[FootstepVariant()], .8f);
                return;
            }
            variations = cues[cue];
            float level = .24f;
            if (string.Equals(cue, "bell", StringComparison.OrdinalIgnoreCase)) level = .16f;
            else if (string.Equals(cue, "intercom", StringComparison.OrdinalIgnoreCase)) level = .30f;
            else if (string.Equals(cue, "paperstorm", StringComparison.OrdinalIgnoreCase)) level = .29f;
            else if (string.Equals(cue, "creak", StringComparison.OrdinalIgnoreCase)) level = .18f;
            voice.pitch = .97f + (eventIndex % 4) * .018f;
            SetLevel(voice, level);
            OneShot(voice, variations[eventIndex % variations.Length], 1);
        }

        public void PlayPaper() { OneShot(desk, paper, .8f); }
        public void PlayPencil() { OneShot(desk, pencil, .75f); }
        public void PlayScanner() { OneShot(motor, feed, .85f); }
        public void PlayReturn() { OneShot(motor, returnPaper, .8f); }
        public void PlayContradiction() { OneShot(resonance, contradiction, .65f); }
        public void SetSilent(bool value) { silent = value; }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            foreach (AudioSource source in sources)
            {
                if (source == null) continue;
                if (paused) source.Pause(); else source.UnPause();
            }
        }

        int FootstepVariant()
        {
            int index = (footstepIndex * 5 + footstepIndex / 6) % footsteps.Count;
            footstepIndex++;
            return index;
        }

        AudioSource EventVoice(Vector3 position)
        {
            eventIndex++;
            AudioSource voice = eventVoices[eventIndex % eventVoices.Count];
            foreach (AudioSource candidate in eventVoices) if (!candidate.isPlaying) { voice = candidate; break; }
            if (voice.isPlaying) voice.Stop();
            voice.transform.position = position;
            return voice;
        }

        void OneShot(AudioSource source, AudioClip clip, float volume)
        { if (source != null && clip != null && !paused) source.PlayOneShot(clip, volume); }

        void SetLevel(AudioSource source, float level)
        {
            sourceLevels[source] = level;
            source.volume = level * masterVolume;
        }

        AudioSource Source(string name, Vector3 position, float volume, float spatialBlend)
        {
            var node = new GameObject(name);
            node.transform.SetParent(transform, false);
            node.transform.localPosition = position;
            AudioSource source = node.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.1f;
            source.maxDistance = 17;
            source.dopplerLevel = 0;
            source.reverbZoneMix = 0;
            sources.Add(source);
            SetLevel(source, volume);
            return source;
        }

        AudioClip Generate(string name, float duration, int seed, Func<float, float, float> sample, bool loop = false, float noiseSmoothing = .35f)
        {
            const int rate = 22050;
            const int crossfade = 768;
            int count = Mathf.CeilToInt(duration * rate);
            var raw = new float[count + (loop ? crossfade : 0)];
            var values = new float[count];
            var random = new System.Random(seed);
            float filteredNoise = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                filteredNoise = Mathf.Lerp(filteredNoise, (float)random.NextDouble() * 2 - 1, noiseSmoothing);
                raw[i] = Mathf.Clamp(sample((float)i / rate, filteredNoise), -.4f, .4f);
            }
            Array.Copy(raw, values, count);
            if (loop)
            {
                // Stitch the continuation of the tail into the start instead of inserting a silent seam.
                for (int i = 0; i < crossfade; i++)
                    values[i] = Mathf.Lerp(raw[count + i], raw[i], Mathf.SmoothStep(0, 1, (float)i / crossfade));
            }
            else
            {
                int ramp = Mathf.Min(88, count / 4);
                for (int i = 0; i < ramp; i++)
                {
                    float weight = (float)i / ramp;
                    values[i] *= weight;
                    values[count - i - 1] *= weight;
                }
            }
            AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(values, 0);
            clips.Add(clip);
            return clip;
        }

        static float Sin(float frequency, float time) { return Mathf.Sin(2 * Mathf.PI * frequency * time); }
        static float Pulse(float time, float onset, float decay)
        { float age = time - onset; return age < 0 ? 0 : Mathf.Clamp01(age / .004f) * Mathf.Exp(-age * decay); }
        static float Knock(float time, float onset, float noise, float strength)
        { float age = Mathf.Max(0, time - onset); return strength * (.16f * Sin(132, age) + .08f * Sin(241, age) + .11f * noise) * Pulse(time, onset, 21); }
        static float Envelope(float time, float length)
        { return Mathf.Clamp01(time / .04f) * Mathf.Clamp01((length - time) / .13f); }

        void OnDestroy()
        {
            foreach (AudioSource source in sources) if (source != null) source.Stop();
            foreach (AudioClip clip in clips) if (clip != null) Destroy(clip);
        }
    }
}
