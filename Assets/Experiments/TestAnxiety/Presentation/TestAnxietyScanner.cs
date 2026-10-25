using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestAnxiety
{
    /// <summary>A scene-owned optical reader. The controller advances its clock, so pause freezes paper and motors together.</summary>
    public sealed class TestAnxietyScanner : MonoBehaviour
    {
        public bool Busy { get; private set; }
        public bool Ready { get; private set; }
        public bool ProcessingHeld { get; set; }
        public bool Powered { get; private set; } = true;
        public Transform InteractionFocus { get; private set; }
        public Transform ReviewView { get; private set; }
        public Action<string> OnSound;

        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Transform> rollers = new List<Transform>();
        readonly List<GameObject> pencilMarks = new List<GameObject>();
        readonly List<GameObject> redMarks = new List<GameObject>();
        Transform machineRoot, paper;
        Material paperMaterial, graphite, red, lamp;
        TextMesh status;
        Action onReady, onProcessed;
        float elapsed;
        int phase;
        int[] selected = new int[9];
        bool[] incorrect = new bool[9];
        bool graded, holdAnnounced;
        string automaticStatus = "READY", displayOverride, lastVisibleStatus;

        public void Build(Transform anchor, Material litTemplate = null)
        {
            if (machineRoot != null) return;
            machineRoot = new GameObject("OMR-98 optical examination reader").transform;
            machineRoot.SetParent(transform, false);
            machineRoot.SetPositionAndRotation(anchor.position, anchor.rotation);
            Material housing = MakeMaterial(litTemplate, "Aged ivory machine housing", new Color(.57f, .56f, .48f), .18f);
            Material dark = MakeMaterial(litTemplate, "Charcoal molded plastic", new Color(.065f, .078f, .073f), .25f);
            Material metal = MakeMaterial(litTemplate, "Brushed roller steel", new Color(.29f, .31f, .28f), .5f, .5f);
            Material rubber = MakeMaterial(litTemplate, "Feed rubber", new Color(.028f, .032f, .03f), .08f);
            paperMaterial = MakeMaterial(litTemplate, "Examination stock", new Color(.86f, .84f, .73f), .04f);
            graphite = MakeMaterial(litTemplate, "Pencil graphite", new Color(.12f, .13f, .12f), .08f);
            red = MakeMaterial(litTemplate, "Red grading ink", new Color(.55f, .035f, .025f), .04f);
            lamp = MakeMaterial(litTemplate, "Reader status light", new Color(.34f, .61f, .35f), .2f);
            lamp.EnableKeyword("_EMISSION");
            lamp.SetColor("_EmissionColor", new Color(.12f, .38f, .15f));

            Cube("Heavy lower chassis", new Vector3(0, .09f, 0), new Vector3(1.1f, .18f, .76f), dark, true);
            Cube("Main scanner housing", new Vector3(0, .39f, .055f), new Vector3(1.05f, .4f, .65f), housing, true);
            Cube("Raised optical head", new Vector3(.1f, .59f, .11f), new Vector3(.77f, .065f, .48f), housing);
            Cube("Case seam", new Vector3(0, .49f, -.275f), new Vector3(1.06f, .012f, .012f), dark);
            Cube("Input aperture", new Vector3(-.12f, .355f, -.279f), new Vector3(.42f, .032f, .012f), dark);
            Cube("Output aperture", new Vector3(-.12f, .141f, -.386f), new Vector3(.42f, .028f, .015f), rubber);
            Cube("Input guide tray", new Vector3(-.12f, .327f, -.48f), new Vector3(.43f, .018f, .41f), housing);
            Cube("Input left rail", new Vector3(-.336f, .35f, -.46f), new Vector3(.016f, .04f, .38f), dark);
            Cube("Input right rail", new Vector3(.096f, .35f, -.46f), new Vector3(.016f, .04f, .38f), dark);
            Cube("Output collection tray", new Vector3(-.12f, .112f, -.75f), new Vector3(.45f, .025f, .74f), dark);
            Cube("Output lip", new Vector3(-.12f, .128f, -1.12f), new Vector3(.45f, .04f, .018f), housing);
            Cube("Control fascia", new Vector3(.354f, .32f, -.283f), new Vector3(.25f, .22f, .018f), dark);
            Cube("Indicator lens", new Vector3(.285f, .37f, -.298f), new Vector3(.038f, .018f, .01f), lamp);
            Cube("Power switch", new Vector3(.42f, .265f, -.302f), new Vector3(.035f, .045f, .023f), rubber);
            for (int i = 0; i < 9; i++)
                Cube("Cooling vent", new Vector3(.529f, .26f + i * .027f, .11f), new Vector3(.005f, .01f, .3f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Cube("Rubber mounting foot", new Vector3(side * .43f, -.014f, .24f), new Vector3(.14f, .045f, .13f), rubber);
                Cube("Rubber mounting foot", new Vector3(side * .43f, -.014f, -.24f), new Vector3(.14f, .045f, .13f), rubber);
                Sphere("Fascia screw", new Vector3(side * .47f, .46f, -.278f), new Vector3(.012f, .012f, .006f), metal);
            }
            for (int i = 0; i < 3; i++)
            {
                Transform roller = Cylinder("Visible feed roller", new Vector3(-.24f + i * .12f, .35f, -.30f), new Vector3(.038f, .038f, .038f), rubber);
                roller.localRotation = Quaternion.Euler(0, 0, 90);
                rollers.Add(roller);
            }
            Label("Brand plate", "SCANTRONICS", new Vector3(-.45f, .555f, -.278f), .045f, dark.color);
            Label("Model plate", "OMR / 98", new Vector3(.19f, .535f, -.278f), .025f, dark.color);
            Label("Input instruction", "FACE UP   /   ONE SHEET", new Vector3(-.31f, .41f, -.282f), .025f, dark.color);
            status = Label("Status display", "READY", new Vector3(.255f, .333f, -.298f), .025f, new Color(.64f, .77f, .52f));
            Label("Property sticker", "DISTRICT PROPERTY\nROOM 107   -   08/99", new Vector3(-.45f, .26f, -.282f), .012f, dark.color);
            Label("Output instruction", "RETURNED FORMS", new Vector3(-.28f, .07f, -.388f), .012f, paperMaterial.color);

            // These targets are child transforms so callers need no knowledge of local machine dimensions.
            InteractionFocus = Target("Reader interaction", new Vector3(0, .3f, -.80f));
            ReviewView = Target("Returned paper inspection", new Vector3(-.12f, .95f, -1.30f));
            ReviewView.LookAt(machineRoot.TransformPoint(new Vector3(-.12f, .16f, -.85f)));
            BuildPaper();
            ResetMachine();
        }

        public void SetSheetMarks(int[] answers, bool isGraded)
        {
            for (int i = 0; i < selected.Length; i++) selected[i] = answers != null && i < answers.Length ? answers[i] : -1;
            graded = isGraded;
            RefreshMarks();
        }

        /// <summary>Optional accurate world-space red marks; the readable result UI uses the same grading result.</summary>
        public void SetGradingMarks(bool[] wrongAnswers)
        {
            for (int i = 0; i < incorrect.Length; i++) incorrect[i] = wrongAnswers != null && i < wrongAnswers.Length && wrongAnswers[i];
            RefreshMarks();
        }

        /// <param name="processed">Optional mark preparation callback, invoked before the return sheet becomes visible.</param>
        public void BeginScan(Action completed, Action processed = null)
        {
            if (Busy || Ready || paper == null || !Powered) return;
            Busy = true;
            Ready = false;
            elapsed = 0;
            phase = 0;
            holdAnnounced = false;
            displayOverride = null;
            onReady = completed;
            onProcessed = processed;
            graded = false;
            Array.Clear(incorrect, 0, incorrect.Length);
            RefreshMarks();
            paper.gameObject.SetActive(true);
            paper.localPosition = new Vector3(-.12f, .348f, -.61f);
            ShowStatus("FEED");
            SetLamp(new Color(.73f, .43f, .08f));
            OnSound?.Invoke("feed");
        }

        public void Tick(float deltaTime)
        {
            if (!Busy || !Powered || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            float next = elapsed + deltaTime;
            // Finish accepting the physical sheet before waiting. A large frame must never skip this gate.
            if (ProcessingHeld && phase < 2) next = Mathf.Min(next, Mathf.Max(2.05f, elapsed));
            float motionTime = next - elapsed;
            elapsed = next;
            foreach (Transform roller in rollers) roller.Rotate(Vector3.up, Mathf.Min(motionTime, .1f) * 390, Space.Self);
            if (elapsed >= 1.65f && phase < 1)
            {
                phase = 1;
                paper.gameObject.SetActive(false);
                displayOverride = null;
                ShowStatus("READING");
                OnSound?.Invoke("process");
            }
            if (phase == 1 && ProcessingHeld && elapsed >= 2.05f)
            {
                ShowStatus("PLEASE RETURN\nTO YOUR DESK");
                if (!holdAnnounced) { holdAnnounced = true; OnSound?.Invoke("hold"); }
            }
            else if (holdAnnounced && !ProcessingHeld)
            {
                holdAnnounced = false;
                displayOverride = null;
                ShowStatus("READING");
                OnSound?.Invoke("resume");
            }
            if (elapsed >= 4.45f && phase < 2 && !ProcessingHeld)
            {
                phase = 2;
                Action processedCallback = onProcessed;
                onProcessed = null;
                processedCallback?.Invoke();
                graded = true;
                RefreshMarks();
                paper.gameObject.SetActive(true);
                displayOverride = null;
                ShowStatus("RETURN");
                OnSound?.Invoke("return");
            }
            if (elapsed < 1.65f)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / 1.65f);
                paper.localPosition = Vector3.Lerp(new Vector3(-.12f, .348f, -.61f), new Vector3(-.12f, .348f, .07f), t);
            }
            else if (elapsed < 4.45f)
            {
                float glow = .45f + Mathf.Sin(elapsed * 8f) * .15f;
                SetLamp(new Color(glow, glow * .67f, .06f));
            }
            else if (elapsed < 6.15f)
            {
                float t = Mathf.SmoothStep(0, 1, (elapsed - 4.45f) / 1.7f);
                paper.localPosition = Vector3.Lerp(new Vector3(-.12f, .144f, -.14f), new Vector3(-.12f, .144f, -.85f), t);
            }
            else
            {
                paper.localPosition = new Vector3(-.12f, .144f, -.85f);
                paper.gameObject.SetActive(true);
                Busy = false;
                Ready = true;
                displayOverride = null;
                ShowStatus("COLLECT");
                SetLamp(new Color(.18f, .52f, .24f));
                Action callback = onReady;
                onReady = null;
                callback?.Invoke();
            }
        }

        public void Retrieve()
        {
            if (!Ready) return;
            paper.gameObject.SetActive(false);
            Ready = false;
            displayOverride = null;
            ShowStatus("READY");
            OnSound?.Invoke("retrieve");
        }

        public void ResetMachine()
        {
            Busy = Ready = false;
            ProcessingHeld = false;
            holdAnnounced = false;
            elapsed = 0;
            phase = 0;
            onReady = null;
            onProcessed = null;
            if (paper != null) paper.gameObject.SetActive(false);
            displayOverride = null;
            ShowStatus("READY");
            if (lamp != null) SetLamp(Powered ? new Color(.18f, .52f, .24f) : Color.black);
        }

        /// <summary>Temporary physical display hint, cleared at the next machine phase. Null restores its normal status.</summary>
        public void SetDisplay(string text)
        {
            displayOverride = string.IsNullOrWhiteSpace(text) ? null : text;
            ShowStatus(automaticStatus);
        }

        /// <summary>Power loss freezes any active paper cycle without discarding its callbacks; restoring power resumes it.</summary>
        public void SetPowered(bool value)
        {
            Powered = value;
            if (lamp != null) SetLamp(value ? Busy ? new Color(.55f, .35f, .06f) : new Color(.18f, .52f, .24f) : Color.black);
            ShowStatus(automaticStatus);
        }

        void ShowStatus(string text)
        {
            automaticStatus = text;
            if (status == null) return;
            string visible = !Powered ? "OFF" : displayOverride ?? automaticStatus;
            if (visible == lastVisibleStatus) return;
            lastVisibleStatus = visible;
            // Keep an instruction on the small physical display instead of letting it spill across the case.
            string[] words = visible.Replace("\n", " \n ").Split(' ');
            var lines = new List<string>();
            string line = "";
            foreach (string word in words)
            {
                if (word.Length == 0) continue;
                if (word == "\n") { lines.Add(line); line = ""; continue; }
                if (line.Length > 0 && line.Length + word.Length + 1 > 15) { lines.Add(line); line = ""; }
                line += (line.Length == 0 ? "" : " ") + word;
            }
            if (line.Length > 0) lines.Add(line);
            status.text = string.Join("\n", lines.GetRange(0, Mathf.Min(3, lines.Count)));
        }

        void BuildPaper()
        {
            paper = new GameObject("Physical submitted answer sheet").transform;
            paper.SetParent(machineRoot, false);
            Cube("Answer sheet stock", Vector3.zero, new Vector3(.285f, .002f, .37f), paperMaterial, false, paper);
            TextMesh heading = Label("Form heading", "TEST ANXIETY\nANSWER RECORD", new Vector3(-.12f, .002f, .16f), .012f, graphite.color, paper);
            heading.transform.localEulerAngles = new Vector3(90, 0, 0);
            for (int row = 0; row < 9; row++)
            {
                float z = .065f - row * .024f;
                TextMesh number = Label("Question number", (row + 1).ToString("00"), new Vector3(-.113f, .0021f, z + .004f), .009f, graphite.color, paper);
                number.transform.localEulerAngles = new Vector3(90, 0, 0);
                for (int col = 0; col < 4; col++)
                {
                    float x = -.052f + col * .037f;
                    Transform bubble = Cylinder("Printed answer bubble", new Vector3(x, .0017f, z), new Vector3(.012f, .00015f, .012f), graphite, paper);
                    Cylinder("Empty bubble center", new Vector3(x, .0019f, z), new Vector3(.009f, .00015f, .009f), paperMaterial, paper);
                    Transform mark = Cylinder("Pencil selection", new Vector3(x, .0023f, z), new Vector3(.009f, .00015f, .009f), graphite, paper);
                    pencilMarks.Add(mark.gameObject);
                }
                Transform cross = new GameObject("Red correction " + (row + 1)).transform;
                cross.SetParent(paper, false);
                cross.localPosition = new Vector3(.113f, .0025f, z);
                Transform slash = Cube("Red correction stroke", Vector3.zero, new Vector3(.018f, .0004f, .002f), red, false, cross);
                slash.localRotation = Quaternion.Euler(0, 45, 0);
                slash = Cube("Red correction stroke", Vector3.zero, new Vector3(.018f, .0004f, .002f), red, false, cross);
                slash.localRotation = Quaternion.Euler(0, -45, 0);
                redMarks.Add(cross.gameObject);
            }
            RefreshMarks();
        }

        void RefreshMarks()
        {
            for (int i = 0; i < pencilMarks.Count; i++) pencilMarks[i].SetActive(selected[i / 4] == i % 4);
            for (int i = 0; i < redMarks.Count; i++) redMarks[i].SetActive(graded && incorrect[i]);
        }

        void SetLamp(Color color)
        {
            if (!Powered) color = Color.black;
            lamp.color = color;
            lamp.SetColor("_EmissionColor", color * .8f);
        }

        Material MakeMaterial(Material template, string name, Color color, float smoothness, float metallic = 0)
        {
            Shader shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("TEST ANXIETY requires the project's URP Lit shader.");
            var material = template != null ? new Material(template) : new Material(shader);
            material.name = name;
            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            ownedMaterials.Add(material);
            return material;
        }

        Transform Cube(string name, Vector3 position, Vector3 scale, Material material, bool solid = false, Transform parent = null)
        { return Shape(PrimitiveType.Cube, name, position, scale, material, solid, parent); }
        Transform Cylinder(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        { return Shape(PrimitiveType.Cylinder, name, position, scale, material, false, parent); }
        Transform Sphere(string name, Vector3 position, Vector3 scale, Material material)
        { return Shape(PrimitiveType.Sphere, name, position, scale, material, false, null); }

        Transform Shape(PrimitiveType shape, string name, Vector3 position, Vector3 scale, Material material, bool solid, Transform parent)
        {
            GameObject item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.transform.SetParent(parent != null ? parent : machineRoot, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = item.GetComponent<Collider>();
            if (!solid && collider != null) { collider.enabled = false; Destroy(collider); }
            return item.transform;
        }

        Transform Target(string name, Vector3 position)
        {
            Transform item = new GameObject(name).transform;
            item.SetParent(machineRoot, false);
            item.localPosition = position;
            return item;
        }

        TextMesh Label(string name, string value, Vector3 position, float size, Color color, Transform parent = null)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent != null ? parent : machineRoot, false);
            item.transform.localPosition = position;
            var mesh = item.AddComponent<TextMesh>();
            mesh.text = value;
            // Native TextMesh scales glyph pixels by characterSize / 10. Keep size in world-space em units.
            mesh.characterSize = size * 10f / 64f;
            mesh.fontSize = 64;
            mesh.color = color;
            mesh.anchor = TextAnchor.UpperLeft;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            item.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            return mesh;
        }

        void OnDestroy()
        {
            onReady = null;
            onProcessed = null;
            foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}
