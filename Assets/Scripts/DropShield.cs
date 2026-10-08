using System.Collections.Generic;
using UnityEngine;

// Deployable dome (Reach's Drop Shield): inside it incoming damage is heavily reduced. Lasts a short time.
public class DropShield : MonoBehaviour
{
    public const float Radius = 4f, Duration = 18f;
    static readonly List<DropShield> active = new List<DropShield>();
    float born;

    public static void Deploy(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "DropShield"; go.transform.position = pos; go.transform.localScale = Vector3.one * Radius * 2f;
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>(); r.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.3f, 0.8f, 1f, 0.22f) };
        var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(go.transform, false);
        l.color = new Color(0.4f, 0.8f, 1f); l.range = 10f; l.intensity = 1.5f;
        go.AddComponent<DropShield>();
        Sfx.Play2D("armorlock", 0.6f);
    }

    public static bool Covers(Vector3 p)
    {
        foreach (var d in active) if (d && (d.transform.position - p).sqrMagnitude < Radius * Radius) return true;
        return false;
    }

    void OnEnable() { active.Add(this); born = Time.time; }
    void OnDisable() { active.Remove(this); }
    void Update() { if (Time.time - born > Duration) Destroy(gameObject); }
}
