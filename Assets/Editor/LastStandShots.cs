using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders still screenshots of the scene from fixed vantage points (no Play mode needed) so the look can be reviewed.
// Menu: Tools > Last Stand > Capture Screenshots. Output: ./Screenshots/*.png (git-ignored).
// Headless: Unity -batchmode -projectPath <p> -executeMethod LastStandShots.CaptureAll   (needs a GPU, so no -nographics)
public static class LastStandShots
{
    const int W = 1920, H = 1080, TW = 640, TH = 360;
    static readonly System.Collections.Generic.List<Texture2D> thumbs = new System.Collections.Generic.List<Texture2D>();
    static readonly System.Collections.Generic.List<string> thumbNames = new System.Collections.Generic.List<string>();

    // SHOTS="01,08,11_view_dmr" limits which shots are rendered (prefix match); unset = all
    static bool Wanted(string name)
    {
        var f = System.Environment.GetEnvironmentVariable("SHOTS");
        if (string.IsNullOrEmpty(f)) return true;
        foreach (var k in f.Split(',')) if (name.StartsWith(k.Trim())) return true;
        return false;
    }

    // Stitches the rendered shots into 3x3 contact sheets (640x360 tiles = one 1920x1080 image per 9 shots)
    static void WriteSheets(string dir)
    {
        for (int sheet = 0; sheet * 9 < thumbs.Count; sheet++)
        {
            var tex = new Texture2D(TW * 3, TH * 3, TextureFormat.RGB24, false);
            for (int i = 0; i < 9 && sheet * 9 + i < thumbs.Count; i++)
                tex.SetPixels((i % 3) * TW, (2 - i / 3) * TH, TW, TH, thumbs[sheet * 9 + i].GetPixels());
            File.WriteAllBytes(Path.Combine(dir, "sheet_" + (char)('A' + sheet) + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        File.WriteAllText(Path.Combine(dir, "sheets.txt"), string.Join("\n", thumbNames));
    }

    // Rebuilds the scene with the current builder, then captures (used for headless review runs)
    public static void BuildAndCapture() { LastStandBuilder.BuildAll(); CaptureAll(); }

    [MenuItem("Tools/Last Stand/Capture Screenshots")]
    public static void CaptureAll()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/LastStand.unity");
        Physics.SyncTransforms();
        foreach (var ps in Object.FindObjectsOfType<ParticleSystem>()) ps.Simulate(10f, true, true);   // so smoke / flames / ash appear in still frames
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots"));
        Directory.CreateDirectory(dir);

        // Wide shots of the yard
        Shot(dir, "01_start_view", new Vector3(0, 2.9f, -16f), new Vector3(0, 3.5f, 20f), 80f);
        Shot(dir, "02_yard_west", new Vector3(-6f, 2.9f, -10f), new Vector3(-30f, 3f, 12f), 80f);
        Shot(dir, "03_warehouse_door", new Vector3(0, 1.7f, -4f), new Vector3(0, 2.5f, 14f), 80f);
        Shot(dir, "04_inside_crate_room", new Vector3(-8f, 1.7f, 14f), new Vector3(-12f, 1f, 18f), 80f);
        Shot(dir, "05_roof_nest", new Vector3(0, 8.6f, 4f), new Vector3(0, 5f, -30f), 75f);
        Shot(dir, "06_sky_horizon", new Vector3(0, 2f, -16f), new Vector3(-30f, 14f, 60f), 90f);
        Shot(dir, "07_stairs_east", new Vector3(30f, 3f, -2f), new Vector3(18f, 3f, 10f), 80f);

        // Enemy lineup: a row of each kind in front of a camera
        var kinds = new[] { Enemy.Kind.Grunt, Enemy.Kind.Elite, Enemy.Kind.Ranger, Enemy.Kind.General, Enemy.Kind.Zealot };
        var spawned = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < kinds.Length; i++)
        {
            var prefab = Resources.Load<GameObject>("Enemies/" + kinds[i]);
            if (!prefab) continue;
            var e = (GameObject)Object.Instantiate(prefab, new Vector3(-8f + i * 4f, 0f, -4f), Quaternion.Euler(0, 180f, 0));
            foreach (var cc in e.GetComponentsInChildren<CharacterController>()) cc.enabled = false;
            spawned.Add(e);
        }
        Shot(dir, "08_enemy_lineup", new Vector3(0f, 1.8f, -14f), new Vector3(0f, 1.7f, -4f), 50f);
        Shot(dir, "09_elite_closeup", new Vector3(-3f, 1.9f, -9f), new Vector3(-4f, 1.7f, -4f), 45f);
        foreach (var s in spawned) Object.DestroyImmediate(s);

        var wraith = (GameObject)Object.Instantiate(Resources.Load<GameObject>("Enemies/Wraith"), new Vector3(0, 0, 14f), Quaternion.Euler(0, 180f, 0));
        Shot(dir, "10_wraith", new Vector3(-6f, 3f, -4f), new Vector3(0f, 2f, 14f), 55f);
        Object.DestroyImmediate(wraith);

        // First-person view through the real Player prefab (forearms + weapon model in the Gun holder)
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        foreach (var id in new[] { "dmr", "ar", "shotgun", "sword", "sniper", "plasma", "concussion", "turret" })
        {
            var pl = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            pl.transform.position = new Vector3(0f, 1.0f, -16f);
            var gun = pl.transform.Find("Camera/Gun");
            var model = (GameObject)Object.Instantiate(Resources.Load<GameObject>("Weapons/" + id), gun);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            var camT = pl.transform.Find("Camera");
            Shot(dir, "11_view_" + id, camT.position, camT.position + Vector3.forward * 20f + Vector3.up * 0.3f, 80f);
            Object.DestroyImmediate(pl);
        }
        WriteSheets(dir);
        Debug.Log("LastStandShots: wrote screenshots to " + dir);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // Debug: average brightness of the sky in 8 horizontal directions (finds where the sun glow sits in the panorama)
    public static void SkyProbe()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/LastStand.unity");
        var sb = new System.Text.StringBuilder("SKYPROBE ");
        float rot = float.Parse(System.Environment.GetEnvironmentVariable("SKYROT") ?? "0");
        RenderSettings.skybox.SetFloat("_Rotation", rot); sb.Append("rot=" + rot + " ");
        for (int yaw = 0; yaw < 360; yaw += 45)
        {
            var go = new GameObject("p"); var cam = go.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 40f, 0); cam.transform.rotation = Quaternion.Euler(-6f, yaw, 0f); cam.fieldOfView = 30f;
            var rt = new RenderTexture(64, 64, 24); cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(64, 64, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            float sum = 0; foreach (var c in tex.GetPixels()) sum += (c.r + c.g + c.b) / 3f;
            sb.Append("yaw" + yaw + "=" + (sum / (64 * 64)).ToString("0.00") + " ");
            RenderTexture.active = prev; Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }
        Debug.Log(sb.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Shot(string dir, string name, Vector3 pos, Vector3 look, float fov)
    {
        if (!Wanted(name)) return;
        var go = new GameObject("ShotCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = pos; cam.transform.LookAt(look);
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f; cam.clearFlags = CameraClearFlags.Skybox;
        if (System.Environment.GetEnvironmentVariable("NOFX") == null) LastStandBuilder.AttachPostFx(cam);
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        var small = new RenderTexture(TW, TH, 0); Graphics.Blit(rt, small);
        RenderTexture.active = small;
        var thumb = new Texture2D(TW, TH, TextureFormat.RGB24, false); thumb.ReadPixels(new Rect(0, 0, TW, TH), 0, 0); thumb.Apply();
        thumbs.Add(thumb); thumbNames.Add(name); RenderTexture.active = rt; Object.DestroyImmediate(small);
        RenderTexture.active = prev;
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }
}
