using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx;
using UnityEngine;

namespace TinyRexPlaytime
{
    [BepInPlugin("brandcreative.tinyrex.playtime", "Chompy Playtime", "0.6.1")]
    public sealed partial class PlaytimePlugin : BaseUnityPlugin
    {
        private readonly List<RexState> geese = new List<RexState>();
        private readonly ControlsCore controls = new ControlsCore();
        private readonly string[] sizeNames = { "Tiny", "Normal", "Big" };
        private float nextScan;
        private bool showedHelp;
        private string message;
        private float messageUntil;
        private Texture2D flameTexture;
        private Material flameMaterial;
        private bool shaderWarning;
        private GUIStyle messageStyle;
        private Material dustMaterial;
        private FMOD.Sound fartSound;
        private FMOD.Channel fartChannel;
        private float lastFart = -100f;
        private bool pendingFart;
        private bool fartAudioWarning;
        private readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

        private void Awake()
        {
            StartCoroutine(FlushEffectsAfterAnimation());
            Logger.LogInfo("Chompy Playtime v0.6.1 loaded. F9 / right-stick click=fart.  F6=size, F7=fire on honk, F8=reset. Controller: hold LB+RB, then Y / X / B.");
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup >= nextScan)
            {
                nextScan = Time.realtimeSinceStartup + 0.75f;
                DiscoverGeese();
            }
            Commands command = Commands.None;
            if (Input.GetKeyDown(KeyCode.F6)) command |= Commands.CycleSize;
            if (Input.GetKeyDown(KeyCode.F7)) command |= Commands.ToggleFire;
            if (Input.GetKeyDown(KeyCode.F8)) command |= Commands.Reset;
            if (Input.GetKeyDown(KeyCode.F9)) command |= Commands.Fart;
            for (uint i = 0; i < 4; i++)
            {
                ushort current;
                bool connected = ControllerInput.Read(i, out current);
                command |= controls.PollPad((int)i, connected, current);
            }
            // Poll even when paused so a held combination cannot queue an action on unpause.
            if (Time.timeScale <= 0.001f || !HasActiveGoose()) return;
            Commands applied = controls.Apply(command);
            if ((applied & Commands.Reset) != 0)
            {
                pendingFart = false;
                foreach (RexState s in geese) { s.PendingFire = false; s.FlameUntil = 0f; if (s.Dust) s.Dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
                ApplySize();
                foreach (RexState s in geese) if (s.Flames) s.Flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ShowMessage("Normal size - fire OFF");
            }
            else if ((applied & Commands.CycleSize) != 0)
            {
                ApplySize();
                ShowMessage(sizeNames[controls.SizeIndex] + " T-Rex");
            }
            else if ((applied & Commands.ToggleFire) != 0)
            {
                if (!controls.FireEnabled) foreach (RexState s in geese) { s.PendingFire = false; s.FlameUntil = 0f; if (s.Flames) s.Flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
                ShowMessage("Fire on honk: " + (controls.FireEnabled ? "ON" : "OFF"));
            }
            if ((applied & Commands.Fart) != 0 && Time.time - lastFart >= 0.95f) { lastFart = Time.time; pendingFart = true; }
        }

        private bool HasActiveGoose()
        {
            foreach (RexState s in geese)
                if (s.Goose && s.Goose.isActiveAndEnabled && s.Goose.player != null) return true;
            return false;
        }

        private void DiscoverGeese()
        {
            for (int i = geese.Count - 1; i >= 0; i--)
            {
                if (geese[i].Goose) continue;
                DestroyTinyArms(geese[i]);
                if (geese[i].Flames) Destroy(geese[i].Flames.gameObject);
                if (geese[i].Dust) Destroy(geese[i].Dust.gameObject);
                geese.RemoveAt(i);
            }
            foreach (Goose goose in UnityEngine.Object.FindObjectsOfType<Goose>())
            {
                if (!goose.isActiveAndEnabled || goose.player == null || goose.bigGoseScalar <= 0f) continue;
                bool found = false;
                foreach (RexState s in geese) if (s.Goose == goose) { found = true; break; }
                if (found) continue;
                RexState state = new RexState(goose);
                geese.Add(state);
                SetSize(state);
                Logger.LogInfo("Attached playtime controls to goose player " + goose.playerNumber);
                if (!showedHelp)
                {
                    showedHelp = true;
                    ShowMessage("Chompy ready! Honk = roar + fire. Flap = tiny arms.\nRight-stick click = fart. LB + RB: Y = size, X = fire, B = reset\nKeyboard: F6/F7/F8; F9 = fart", 9f);
                }
            }
        }

        private void ApplySize()
        {
            foreach (RexState state in geese) if (state.Goose) SetSize(state);
        }

        private void SetSize(RexState state)
        {
            // Absolute baseline values prevent cumulative drift after repeated toggles.
            state.Goose.transform.localScale = state.BaseScale * controls.Factor;
            state.Goose.bigGoseScalar = state.BaseScalar * controls.Factor;
        }

        private void LateUpdate()
        {
            if (fartChannel.hasHandle()) fartChannel.setPaused(Time.timeScale <= 0.001f);
            foreach (RexState state in geese)
            {
                Goose goose = state.Goose;
                if (!goose || !goose.isActiveAndEnabled || goose.gooseHonker == null) continue;
                if (state.Flames && Time.timeScale <= 0.001f)
                    state.Flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                // Read the game's completed honk event; do not synthesize or replace audio/input.
                float honkTime = goose.gooseHonker.timeAtLastHonk;
                bool newHonk = goose.gooseHonker.justQuacked && honkTime != state.LastHonkTime;
                if (!newHonk) continue;
                state.LastHonkTime = honkTime;
                if (!controls.FireEnabled || Time.timeScale <= 0.001f) continue;
                if (Time.time - state.LastPuff < 0.16f) continue;
                state.LastPuff = Time.time;
                state.PendingFire = true;
            }
        }

        private Material GetFlameMaterial()
        {
            if (flameMaterial) return flameMaterial;
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (!shader) shader = Shader.Find("Particles/Additive");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("UI/Default");
            if (!shader)
            {
                if (!shaderWarning) { Logger.LogWarning("No compatible fire shader was available. Size controls remain enabled."); shaderWarning = true; }
                return null;
            }
            flameTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            flameTexture.name = "TinyRexSoftFlame";
            flameTexture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float dx = (x - 15.5f) / 15.5f;
                float dy = (y - 15.5f) / 15.5f;
                float alpha = Mathf.Clamp01(1f - dx * dx - dy * dy);
                alpha = alpha * alpha;
                flameTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            flameTexture.Apply(false, true);
            flameMaterial = new Material(shader);
            flameMaterial.name = "TinyRexFlameMaterial";
            flameMaterial.mainTexture = flameTexture;
            if (flameMaterial.HasProperty("_Color")) flameMaterial.SetColor("_Color", Color.white);
            // The sprites/additive/UI shaders already blend. Configure Standard Unlit if selected.
            if (shader.name == "Particles/Standard Unlit")
            {
                flameMaterial.SetFloat("_Mode", 2f);
                flameMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                flameMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                flameMaterial.SetFloat("_ZWrite", 0f);
                flameMaterial.EnableKeyword("_ALPHABLEND_ON");
                flameMaterial.renderQueue = 3000;
            }
            return flameMaterial;
        }

        private void EmitFire(RexState state, int count)
        {
            Goose goose = state.Goose;
            if (!state.UpperBeak || !state.HeadBone) state.ResolveBones();
            Transform mouth = state.UpperBeak ? state.UpperBeak : goose.gooseHonker.honkParticleSystem ? goose.gooseHonker.honkParticleSystem.transform : goose.holdParent;
            if (!mouth) return;
            Material material = GetFlameMaterial();
            if (!material) return;
            if (!state.Flames)
            {
                GameObject obj = new GameObject("TinyRex_FirePuff_Cosmetic");
                state.Flames = obj.AddComponent<ParticleSystem>();
                ParticleSystem ps = state.Flames;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.7f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 160;
                main.gravityModifier = -0.05f;
                var emission = ps.emission; emission.enabled = false;
                var shape = ps.shape; shape.enabled = false;
                var collision = ps.collision; collision.enabled = false;
                var lights = ps.lights; lights.enabled = false;
                var color = ps.colorOverLifetime; color.enabled = true;
                Gradient gradient = new Gradient();
                gradient.SetKeys(new[] {
                    new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0f),
                    new GradientColorKey(new Color(1f, 0.42f, 0.06f), 0.35f),
                    new GradientColorKey(new Color(0.65f, 0.12f, 0.01f), 1f)
                }, new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
                color.color = gradient;
                var size = ps.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f)));
                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Vector3 direction = MouthDirection(state);
            float scale = controls.Factor;
            Vector3 position = mouth.position + direction * ((state.UpperBeak ? 0.06f : 0.02f) * scale);
            state.Flames.transform.position = position;
            state.Flames.Play();
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams();
                puff.position = position + UnityEngine.Random.insideUnitSphere * (0.018f * scale);
                puff.velocity = (direction * UnityEngine.Random.Range(1.5f, 2.6f) + UnityEngine.Random.insideUnitSphere * 0.3f + Vector3.up * 0.18f) * scale;
                puff.startLifetime = UnityEngine.Random.Range(0.25f, 0.48f);
                puff.startSize = UnityEngine.Random.Range(0.07f, 0.13f) * scale;
                puff.startColor = Color.white;
                state.Flames.Emit(puff, 1);
            }
        }

        private Vector3 MouthDirection(RexState state)
        {
            // Both points are animated skeleton bones, not gameplay aim / grab targets.
            if (state.UpperBeak && state.HeadBone)
            {
                Vector3 forward = state.UpperBeak.position - state.HeadBone.position;
                if (forward.sqrMagnitude > 0.000001f) return forward.normalized;
            }
            ParticleSystem native = state.Goose.gooseHonker.honkParticleSystem;
            if (native)
            {
                Vector3 localDirection = Quaternion.Euler(native.shape.rotation) * Vector3.forward;
                return native.transform.TransformDirection(localDirection).normalized;
            }
            return state.Goose.transform.forward;
        }

        private IEnumerator FlushEffectsAfterAnimation()
        {
            while (true)
            {
                yield return endOfFrame; // All LateUpdate / IK bone movement is complete.
                if (Time.timeScale <= 0.001f)
                {
                    pendingFart = false;
                    foreach (RexState state in geese) { state.PendingFire = false; state.FlameUntil = 0f; }
                    continue;
                }
                bool fart = pendingFart;
                pendingFart = false;
                bool emittedFart = false;
                foreach (RexState state in geese)
                {
                    if (!state.Goose || !state.Goose.isActiveAndEnabled) continue;
                    UpdateTinyArms(state);
                    if (state.PendingFire)
                    {
                        state.PendingFire = false;
                        if (controls.FireEnabled) { state.FlameUntil = Time.time + 0.18f; state.FireCredit = 4f; }
                    }
                    // A short stream, rather than a fixed world-facing burst, tracks head turns.
                    if (controls.FireEnabled && Time.time < state.FlameUntil)
                    {
                        state.FireCredit += Time.deltaTime * 140f;
                        int count = Mathf.Clamp(Mathf.FloorToInt(state.FireCredit), 0, 12);
                        state.FireCredit -= count;
                        if (count > 0) EmitFire(state, count);
                    }
                    if (fart) { EmitFart(state); emittedFart = true; }
                }
                if (emittedFart) PlayFartSound();
            }
        }

        private Material GetDustMaterial()
        {
            if (dustMaterial) return dustMaterial;
            Material baseMaterial = GetFlameMaterial();
            if (!baseMaterial) return null;
            Shader alpha = Shader.Find("Sprites/Default");
            if (!alpha) alpha = Shader.Find("UI/Default");
            if (!alpha) alpha = Shader.Find("Particles/Standard Unlit");
            dustMaterial = alpha ? new Material(alpha) : new Material(baseMaterial);
            dustMaterial.name = "ChompyBrownDustMaterial";
            dustMaterial.mainTexture = flameTexture;
            if (dustMaterial.HasProperty("_Color")) dustMaterial.SetColor("_Color", Color.white);
            if (dustMaterial.shader.name == "Particles/Standard Unlit")
            {
                dustMaterial.SetFloat("_Mode", 2f);
                dustMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dustMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                dustMaterial.SetFloat("_ZWrite", 0f);
                dustMaterial.EnableKeyword("_ALPHABLEND_ON");
                dustMaterial.renderQueue = 3000;
            }
            return dustMaterial;
        }

        private void EmitFart(RexState state)
        {
            if (!state.TailBone || !state.ChestBone) state.ResolveBones();
            Vector3 rear = state.TailBone && state.ChestBone ? state.TailBone.position - state.ChestBone.position : -state.Goose.transform.forward;
            rear = Vector3.ProjectOnPlane(rear, Vector3.up);
            if (rear.sqrMagnitude < 0.000001f) rear = -state.Goose.transform.forward;
            rear.Normalize();
            float scale = controls.Factor;
            Vector3 origin = state.TailBone ? state.TailBone.position : state.Goose.transform.position + Vector3.up * (0.25f * scale);
            origin += rear * (0.06f * scale) - Vector3.up * (0.075f * scale); // Below the tail, not inside it.
            Material material = GetDustMaterial();
            if (material)
            {
                if (!state.Dust)
                {
                    GameObject obj = new GameObject("Chompy_BrownDust");
                    ParticleSystem ps = obj.AddComponent<ParticleSystem>();
                    state.Dust = ps;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = 1f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 80; main.gravityModifier = -0.015f;
                    var emission = ps.emission; emission.enabled = false;
                    var shape = ps.shape; shape.enabled = false;
                    var collision = ps.collision; collision.enabled = false;
                    var color = ps.colorOverLifetime; color.enabled = true;
                    Gradient gradient = new Gradient();
                    gradient.SetKeys(new[] {
                        new GradientColorKey(new Color(0.42f,0.24f,0.10f),0f),
                        new GradientColorKey(new Color(0.58f,0.40f,0.23f),0.5f),
                        new GradientColorKey(new Color(0.48f,0.34f,0.20f),1f)
                    }, new[] {new GradientAlphaKey(0.7f,0f),new GradientAlphaKey(0.45f,0.5f),new GradientAlphaKey(0f,1f)});
                    color.color = gradient;
                    var size = ps.sizeOverLifetime; size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0f,0.4f),new Keyframe(0.5f,1f),new Keyframe(1f,0.7f)));
                    var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                state.Dust.transform.position = origin;
                state.Dust.Play();
                for (int i = 0; i < 24; i++)
                {
                    var puff = new ParticleSystem.EmitParams();
                    puff.position = origin + UnityEngine.Random.insideUnitSphere * (0.025f * scale);
                    puff.velocity = (rear * UnityEngine.Random.Range(0.6f,1.15f) + UnityEngine.Random.insideUnitSphere * 0.35f + Vector3.up * 0.13f) * scale;
                    puff.startLifetime = UnityEngine.Random.Range(0.45f,0.85f);
                    puff.startSize = UnityEngine.Random.Range(0.12f,0.25f) * scale;
                    puff.startColor = Color.white;
                    state.Dust.Emit(puff,1);
                }
            }
            // Reuse the actual native honk-line effect, without triggering a honk or a roar.
            ParticleSystem native = state.Goose.gooseHonker.honkParticleSystem;
            if (native)
            {
                Quaternion rotateToRear = Quaternion.FromToRotation(MouthDirection(state), rear);
                ParticleSystem lines = UnityEngine.Object.Instantiate(native, origin, rotateToRear * native.transform.rotation);
                lines.name = "Chompy_FartSoundLines";
                lines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var nativeMain = native.main;
                lines.transform.localScale = nativeMain.scalingMode == ParticleSystemScalingMode.Local ? native.transform.localScale : native.transform.lossyScale;
                var main = lines.main;
                main.loop = false; main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = nativeMain.scalingMode;
                main.stopAction = ParticleSystemStopAction.Destroy;
                lines.Play(true);
                Destroy(lines.gameObject,3f);
            }
        }

        private void PlayFartSound()
        {
            try
            {
                if (!FMODUnity.RuntimeManager.IsInitialized) return;
                FMOD.System system = FMODUnity.RuntimeManager.LowlevelSystem;
                if (!fartSound.hasHandle())
                {
                    string path = Path.Combine(Paths.PluginPath,"TinyRexPlaytime","Chompy-Fart.wav");
                    FMOD.RESULT loaded = system.createSound(path,FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE | FMOD.MODE.LOOP_OFF,out fartSound);
                    if (loaded != FMOD.RESULT.OK)
                    {
                        if (!fartAudioWarning) { Logger.LogWarning("Could not load fart WAV: " + loaded); fartAudioWarning = true; }
                        return;
                    }
                }
                FMOD.ChannelGroup group = new FMOD.ChannelGroup();
                try
                {
                    FMOD.Studio.Bus master = FMODUnity.RuntimeManager.GetBus("bus:/");
                    if (master.getChannelGroup(out group) != FMOD.RESULT.OK) system.getMasterChannelGroup(out group);
                }
                catch { system.getMasterChannelGroup(out group); }
                if (fartChannel.hasHandle()) fartChannel.stop();
                FMOD.RESULT played = system.playSound(fartSound,group,true,out fartChannel);
                if (played == FMOD.RESULT.OK) { fartChannel.setVolume(0.7f); fartChannel.setPaused(false); }
                else if (!fartAudioWarning) { Logger.LogWarning("Could not play fart: " + played); fartAudioWarning = true; }
            }
            catch (Exception error)
            {
                if (!fartAudioWarning) { Logger.LogWarning("Fart audio unavailable: " + error.Message); fartAudioWarning = true; }
            }
        }

        #pragma warning disable 0649 // Data fields are populated by EmbeddedArms().
        [Serializable]
        private sealed class ArmsPackage { public ArmSpec[] arms; }
        [Serializable]
        private sealed class ArmSpec
        {
            public string name, bone;
            public float[] pivot, vertices, normals, foldedWing, flapAxis;
            public int[] triangles, colors;
        }
        #pragma warning restore 0649
        private sealed class TinyArm
        {
            public Transform Root, Wing;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public Vector3 Pivot;
            public Vector3[] Vertices, Normals, DrawVertices, DrawNormals;
            public int[] Triangles, ReversedTriangles;
            public bool Reversed;
            public Quaternion Swing = Quaternion.identity;
            public Quaternion Folded;
            public Vector3 FlapAxis;
            public float Phase;
        }
        private ArmsPackage armPackage;
        private bool armLoadFailed;
        private Material armBodyMaterial;

        private void EnsureTinyArms(RexState state)
        {
            if (state.TinyArms != null || armLoadFailed) return;
            if (!state.ChestBone || !state.BodyRenderer) state.ResolveBones();
            Material material=state.BodyRenderer?state.BodyRenderer.sharedMaterial:armBodyMaterial;
            if(!material)
            {
                foreach(Material candidate in Resources.FindObjectsOfTypeAll<Material>())
                    if(candidate && candidate.name.ToLowerInvariant().Contains("vertexcolorxraylead")){material=candidate;armBodyMaterial=candidate;break;}
            }
            if (!state.ChestBone || !material)
            {
                if (!state.ReportedMissingArmAnchor) { state.ReportedMissingArmAnchor=true; Logger.LogWarning("Chompy arms waiting for chest/material; collecting a diagnostic report."); SaveArmDiagnostics(state,"waiting for chest/material"); }
                return;
            }
            try
            {
                if (armPackage == null)
                {
                    armPackage = EmbeddedArms();
                    if (armPackage == null || armPackage.arms == null || armPackage.arms.Length != 2) throw new InvalidDataException("Expected two tiny arms");
                }
                var created = new List<TinyArm>();
                foreach (ArmSpec spec in armPackage.arms)
                {
                    int count = spec.vertices.Length / 3;
                    if (count == 0 || spec.normals.Length != count * 3 || spec.colors.Length != count * 4) throw new InvalidDataException("Invalid arm mesh data");
                    var vertices = new Vector3[count]; var normals = new Vector3[count]; var colors = new Color32[count];
                    for (int i=0;i<count;i++)
                    {
                        vertices[i] = new Vector3(spec.vertices[i*3],spec.vertices[i*3+1],spec.vertices[i*3+2]);
                        normals[i] = new Vector3(spec.normals[i*3],spec.normals[i*3+1],spec.normals[i*3+2]);
                        colors[i] = new Color32((byte)spec.colors[i*4],(byte)spec.colors[i*4+1],(byte)spec.colors[i*4+2],(byte)spec.colors[i*4+3]);
                    }
                    Mesh mesh = new Mesh(); mesh.name = "Chompy_TinyArm_" + spec.name;
                    mesh.MarkDynamic();
                    mesh.vertices = vertices; mesh.normals = normals; mesh.colors32 = colors; mesh.triangles = spec.triangles;
                    mesh.uv = new Vector2[count];
                    var tangents = new Vector4[count]; for(int i=0;i<count;i++) tangents[i]=new Vector4(1f,0f,0f,-1f); mesh.tangents=tangents;
                    mesh.RecalculateBounds();
                    GameObject obj = new GameObject(mesh.name);
                    obj.layer = state.BodyRenderer ? state.BodyRenderer.gameObject.layer : 0;
                    obj.transform.SetParent(state.Goose.transform,false);
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.localRotation = Quaternion.identity;
                    obj.transform.localScale = Vector3.one; // Never inherit the wing's folded scale.
                    obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    if(state.BodyRenderer)
                    {
                        renderer.shadowCastingMode = state.BodyRenderer.shadowCastingMode;
                        renderer.receiveShadows = state.BodyRenderer.receiveShadows;
                        renderer.sortingLayerID = state.BodyRenderer.sortingLayerID;
                        renderer.sortingOrder = state.BodyRenderer.sortingOrder;
                    }
                    var reversed = (int[])spec.triangles.Clone();
                    for(int i=0;i<reversed.Length;i+=3){int temp=reversed[i+1];reversed[i+1]=reversed[i+2];reversed[i+2]=temp;}
                    created.Add(new TinyArm {
                        Root=obj.transform, Mesh=mesh, Renderer=renderer,
                        Pivot=new Vector3(spec.pivot[0],spec.pivot[1],spec.pivot[2]),
                        Vertices=vertices, Normals=normals, DrawVertices=new Vector3[count], DrawNormals=new Vector3[count],
                        Triangles=spec.triangles, ReversedTriangles=reversed,
                        Wing=spec.bone=="upperwing_l" ? state.LeftWing : state.RightWing,
                        Folded=new Quaternion(spec.foldedWing[0],spec.foldedWing[1],spec.foldedWing[2],spec.foldedWing[3]),
                        FlapAxis=new Vector3(spec.flapAxis[0],spec.flapAxis[1],spec.flapAxis[2]),
                        Phase=spec.name=="Left" ? 0f : 1.6f
                    });
                }
                state.TinyArms = created.ToArray();
                state.ArmsCreatedAt=Time.realtimeSinceStartup;
                Logger.LogInfo("Chompy v0.6.1 arms created from embedded geometry on the active character.");
                UpdateArmGeometry(state);
                SaveArmDiagnostics(state,"created");
            }
            catch (Exception error)
            {
                armLoadFailed = true;
                Logger.LogError("Could not build Chompy arms: " + error);
                SaveArmDiagnostics(state,"creation error: "+error);
            }
        }

        private void UpdateTinyArms(RexState state)
        {
            EnsureTinyArms(state);
            if (state.TinyArms == null || !state.ChestBone) return;
            bool shown = state.Goose.isActiveAndEnabled && state.Goose.gameObject.activeInHierarchy;
            bool flapping = state.Goose.gooseWings != null && state.Goose.gooseWings.isFlapping;
            foreach (TinyArm arm in state.TinyArms)
            {
                if (!arm.Root) continue;
                arm.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                int layer = state.BodyRenderer ? state.BodyRenderer.gameObject.layer : 0;
                Camera camera = Camera.main;
                if(camera && (camera.cullingMask & (1<<layer))==0)
                {
                    if((camera.cullingMask & 1)!=0) layer=0;
                    else for(int candidate=0;candidate<32;candidate++)if((camera.cullingMask&(1<<candidate))!=0){layer=candidate;break;}
                }
                arm.Root.gameObject.layer=layer;
                Quaternion delta = Quaternion.identity;
                if (flapping && arm.Wing) delta = (Quaternion.Inverse(state.ChestBone.rotation) * arm.Wing.rotation) * Quaternion.Inverse(arm.Folded);
                Quaternion target = Quaternion.RotateTowards(Quaternion.identity,delta,45f);
                if (flapping) target = target * Quaternion.AngleAxis(Mathf.Sin(Time.time*20f+arm.Phase)*10f,arm.FlapAxis);
                arm.Swing = Quaternion.Slerp(arm.Swing,target,1f-Mathf.Exp(-Time.deltaTime*20f));
            }
            UpdateArmGeometry(state);
            if(!state.ReportedArmsAfterRender && Time.realtimeSinceStartup-state.ArmsCreatedAt>1f)
            {
                state.ReportedArmsAfterRender=true;SaveArmDiagnostics(state,"one second after attachment");
            }
        }

        private void UpdateArmGeometry(RexState state)
        {
            if (state.TinyArms == null || !state.ChestBone || !state.Goose) return;
            foreach(TinyArm arm in state.TinyArms)
            {
                if (!arm.Root || !arm.Mesh) continue;
                // Keep renderer under the active actor. Bake the bone's full affine transform,
                // including reflected/scaled rigs, into this tiny dynamic mesh.
                Matrix4x4 matrix = state.Goose.transform.worldToLocalMatrix * state.ChestBone.localToWorldMatrix * Matrix4x4.TRS(arm.Pivot,arm.Swing,Vector3.one);
                Matrix4x4 normals = matrix.inverse.transpose;
                for(int i=0;i<arm.Vertices.Length;i++)
                {
                    arm.DrawVertices[i] = matrix.MultiplyPoint3x4(arm.Vertices[i]);
                    arm.DrawNormals[i] = normals.MultiplyVector(arm.Normals[i]).normalized;
                }
                bool reversed = matrix.determinant < 0f;
                if(reversed != arm.Reversed){arm.Mesh.triangles=reversed?arm.ReversedTriangles:arm.Triangles;arm.Reversed=reversed;}
                arm.Mesh.vertices=arm.DrawVertices;arm.Mesh.normals=arm.DrawNormals;arm.Mesh.RecalculateBounds();
                if(state.BodyRenderer){state.BodyRenderer.GetPropertyBlock(state.ArmProperties);arm.Renderer.SetPropertyBlock(state.ArmProperties);}
            }
        }

        private void SaveArmDiagnostics(RexState state,string phase)
        {
            try
            {
                var text = new System.Text.StringBuilder();
                text.AppendLine("Chompy arm report v0.6.1: "+phase+" / player "+state.Goose.playerNumber);
                text.AppendLine("Chest found="+(bool)state.ChestBone+"; left/right wing="+(bool)state.LeftWing+"/"+(bool)state.RightWing);
                if(state.ChestBone)text.AppendLine("Chest active="+state.ChestBone.gameObject.activeInHierarchy+" scale="+state.ChestBone.lossyScale);
                text.AppendLine("Animator root="+(state.Goose.gooseEngine?state.Goose.gooseEngine.name:"none"));
                var list=new List<Renderer>(state.Goose.GetComponentsInChildren<Renderer>(true));
                if(state.Goose.gooseEngine)list.AddRange(state.Goose.gooseEngine.GetComponentsInChildren<Renderer>(true));
                foreach(Renderer renderer in list)
                    if(!(renderer is ParticleSystemRenderer))text.AppendLine(renderer.name+" type="+renderer.GetType().Name+" enabled="+renderer.enabled+" active="+renderer.gameObject.activeInHierarchy+" layer="+renderer.gameObject.layer+" material="+(renderer.sharedMaterial?renderer.sharedMaterial.name:"none")+" shader="+(renderer.sharedMaterial&&renderer.sharedMaterial.shader?renderer.sharedMaterial.shader.name:"none"));
                if(Camera.main)text.AppendLine("Camera="+Camera.main.name+" mask="+Camera.main.cullingMask);
                if(state.TinyArms!=null)foreach(TinyArm arm in state.TinyArms)text.AppendLine("Arm "+arm.Root.name+" vertices="+arm.Mesh.vertexCount+" bounds="+arm.Renderer.bounds+" parent="+arm.Root.parent.name);
                File.AppendAllText(Path.Combine(Paths.GameRootPath,"Chompy-Arms-Diagnostics.txt"),text.ToString()+Environment.NewLine);
            }catch(Exception error){Logger.LogWarning("Arm report unavailable: "+error.Message);}
        }

        private void DestroyTinyArms(RexState state)
        {
            if (state.TinyArms == null) return;
            foreach (TinyArm arm in state.TinyArms)
            {
                if (arm.Root) Destroy(arm.Root.gameObject);
                if (arm.Mesh) Destroy(arm.Mesh);
            }
            state.TinyArms = null;
        }

        private void ShowMessage(string text, float seconds = 2.2f)
        {
            message = text;
            messageUntil = Time.realtimeSinceStartup + seconds;
        }

        private void OnGUI()
        {
            if (Time.realtimeSinceStartup > messageUntil || string.IsNullOrEmpty(message) || !HasActiveGoose()) return;
            if (messageStyle == null)
            {
                messageStyle = new GUIStyle(GUI.skin.box);
                messageStyle.fontSize = 15;
                messageStyle.alignment = TextAnchor.MiddleLeft;
                messageStyle.padding = new RectOffset(12, 12, 8, 8);
                messageStyle.normal.textColor = Color.white;
            }
            float width = Mathf.Min(610f, Screen.width - 24f);
            float height = messageStyle.CalcHeight(new GUIContent(message), width);
            GUI.Box(new Rect(12f, 12f, width, height), message, messageStyle);
        }

        private void OnDestroy()
        {
            foreach (RexState s in geese)
            {
                if (s.Goose) { s.Goose.transform.localScale = s.BaseScale; s.Goose.bigGoseScalar = s.BaseScalar; }
                DestroyTinyArms(s);
                if (s.Flames) Destroy(s.Flames.gameObject);
                if (s.Dust) Destroy(s.Dust.gameObject);
            }
            try { if (fartChannel.hasHandle()) fartChannel.stop(); if (fartSound.hasHandle()) fartSound.release(); } catch { }
            if (dustMaterial) Destroy(dustMaterial);
            if (flameMaterial) Destroy(flameMaterial);
            if (flameTexture) Destroy(flameTexture);
        }

        private sealed class RexState
        {
            public readonly Goose Goose;
            public readonly Vector3 BaseScale;
            public readonly float BaseScalar;
            public float LastHonkTime;
            public float LastPuff = -100f;
            public ParticleSystem Flames;
            public ParticleSystem Dust;
            public Transform HeadBone, UpperBeak, TailBone, ChestBone;
            public Transform LeftWing, RightWing;
            public Renderer BodyRenderer;
            public bool ReportedMissingArmAnchor, ReportedArmsAfterRender;
            public float ArmsCreatedAt;
            public readonly MaterialPropertyBlock ArmProperties = new MaterialPropertyBlock();
            public TinyArm[] TinyArms;
            public bool PendingFire;
            public float FlameUntil, FireCredit;
            public void ResolveBones()
            {
                var meshes = new List<SkinnedMeshRenderer>(Goose.GetComponentsInChildren<SkinnedMeshRenderer>(true));
                if(Goose.gooseEngine)meshes.AddRange(Goose.gooseEngine.GetComponentsInChildren<SkinnedMeshRenderer>(true));
                foreach (SkinnedMeshRenderer renderer in meshes)
                {
                    foreach (Transform bone in renderer.bones)
                    {
                        if (!bone) continue;
                        string name = bone.name.ToLowerInvariant();
                        if (name == "head") HeadBone = bone;
                        else if (name == "beakupper") UpperBeak = bone;
                        else if (name == "tailbone") TailBone = bone;
                        else if (name == "chest") ChestBone = bone;
                        else if (name == "upperwing_l") LeftWing = bone;
                        else if (name == "upperwing_r") RightWing = bone;
                    }
                }
                if(Goose.gooseEngine)
                {
                    foreach(Transform bone in Goose.gooseEngine.GetComponentsInChildren<Transform>(true))
                    {
                        string name=bone.name.ToLowerInvariant();
                        if(name=="chest"&&!ChestBone)ChestBone=bone;
                        else if(name=="head"&&!HeadBone)HeadBone=bone;
                        else if(name=="beakupper"&&!UpperBeak)UpperBeak=bone;
                        else if(name=="tailbone"&&!TailBone)TailBone=bone;
                        else if(name=="upperwing_l"&&!LeftWing)LeftWing=bone;
                        else if(name=="upperwing_r"&&!RightWing)RightWing=bone;
                    }
                }
                Renderer best=null;int bestScore=int.MinValue;
                var renderers = new List<Renderer>(Goose.GetComponentsInChildren<Renderer>(true));
                if(Goose.gooseEngine)renderers.AddRange(Goose.gooseEngine.GetComponentsInChildren<Renderer>(true));
                foreach(Renderer renderer in renderers)
                {
                    if(renderer is ParticleSystemRenderer || !renderer.sharedMaterial || renderer.name.StartsWith("Chompy_"))continue;
                    string label=renderer.name.ToLowerInvariant();
                    if(label.Contains("highlight")||label.Contains("xray")||label.Contains("target"))continue;
                    int score=(renderer.enabled&&renderer.gameObject.activeInHierarchy?200:0)+(renderer is SkinnedMeshRenderer?40:0)+(label.Contains("goosemesh")?60:0);
                    SkinnedMeshRenderer skin=renderer as SkinnedMeshRenderer;
                    if(skin&&ChestBone)foreach(Transform bone in skin.bones)if(bone==ChestBone){score+=1000;break;}
                    if(renderer.sharedMaterial.name.ToLowerInvariant().Contains("vertexcolor"))score+=20;
                    if(score>bestScore){bestScore=score;best=renderer;}
                }
                if(best)BodyRenderer=best;
            }
            public RexState(Goose goose)
            {
                Goose = goose;
                BaseScale = goose.transform.localScale;
                BaseScalar = goose.bigGoseScalar;
                ResolveBones();
                LastHonkTime = goose.gooseHonker == null ? 0f : goose.gooseHonker.timeAtLastHonk;
            }
        }

        private static class ControllerInput
        {
            [StructLayout(LayoutKind.Sequential)]
            private struct Gamepad { public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LX, LY, RX, RY; }
            [StructLayout(LayoutKind.Sequential)]
            private struct State { public uint Packet; public Gamepad Pad; }
            [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] private static extern uint Read14(uint index, out State state);
            [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")] private static extern uint Read13(uint index, out State state);
            [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")] private static extern uint Read910(uint index, out State state);
            private static int library;
            public static bool Read(uint index, out ushort buttons)
            {
                buttons = 0;
                State state;
                if (library == -1) return false;
                if (library == 0)
                {
                    try { uint result = Read14(index, out state); library = 14; buttons = state.Pad.Buttons; return result == 0; }
                    catch (DllNotFoundException) { }
                    catch (EntryPointNotFoundException) { }
                    try { uint result = Read13(index, out state); library = 13; buttons = state.Pad.Buttons; return result == 0; }
                    catch (DllNotFoundException) { }
                    catch (EntryPointNotFoundException) { }
                    try { uint result = Read910(index, out state); library = 910; buttons = state.Pad.Buttons; return result == 0; }
                    catch (DllNotFoundException) { library = -1; return false; }
                    catch (EntryPointNotFoundException) { library = -1; return false; }
                }
                uint status = library == 14 ? Read14(index, out state) : library == 13 ? Read13(index, out state) : Read910(index, out state);
                if (status != 0) return false;
                buttons = state.Pad.Buttons;
                return true;
            }
        }
    }
}
