using UnityEngine;

// Covenant Phantom dropship: flies in, hovers while it unloads Sangheili, then leaves. Scenery with a purpose; it can't be shot down.
public class Phantom : MonoBehaviour
{
    Vector3 hoverPoint, exitDir;
    float arrivedAt = -1f, nextDrop;
    int dropped;
    const int Troops = 3;

    public static void Spawn(Vector3 hoverPoint)
    {
        var go = new GameObject("Phantom");
        float a = Random.value * Mathf.PI * 2f;
        Vector3 start = hoverPoint + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 110f; start.y = 22f;
        go.transform.position = start;
        Part(go, PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), new Vector3(7f, 2.4f, 12f), new Color(0.35f, 0.3f, 0.45f));
        Part(go, PrimitiveType.Cube, new Vector3(-5.5f, 0.3f, -1f), new Vector3(5f, 0.4f, 6f), new Color(0.3f, 0.26f, 0.4f));
        Part(go, PrimitiveType.Cube, new Vector3(5.5f, 0.3f, -1f), new Vector3(5f, 0.4f, 6f), new Color(0.3f, 0.26f, 0.4f));
        var glow = Part(go, PrimitiveType.Sphere, new Vector3(0f, -1.1f, 0f), new Vector3(3.5f, 0.6f, 5f), new Color(0.5f, 0.4f, 1f));
        glow.material.EnableKeyword("_EMISSION"); glow.material.SetColor("_EmissionColor", new Color(0.5f, 0.4f, 1f) * 3f);
        var light = new GameObject("Glow").AddComponent<Light>();
        light.transform.SetParent(go.transform, false); light.transform.localPosition = new Vector3(0, -2f, 0);
        light.color = new Color(0.55f, 0.4f, 1f); light.range = 20f; light.intensity = 2f;
        go.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(hoverPoint - start, Vector3.up));
        var ph = go.AddComponent<Phantom>();
        ph.hoverPoint = new Vector3(hoverPoint.x, 13f, hoverPoint.z);
        ph.exitDir = (start - hoverPoint).normalized;
        Sfx.Play3D("phantom", hoverPoint + Vector3.up * 13f, 1f);
    }

    static Renderer Part(GameObject parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        var p = GameObject.CreatePrimitive(type);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(parent.transform, false); p.transform.localPosition = pos; p.transform.localScale = scale;
        var r = p.GetComponent<Renderer>(); r.material.color = color;
        return r;
    }

    void Update()
    {
        if (arrivedAt < 0f)
        {
            transform.position = Vector3.MoveTowards(transform.position, hoverPoint, 28f * Time.deltaTime);
            if ((transform.position - hoverPoint).sqrMagnitude < 1f) { arrivedAt = Time.time; nextDrop = Time.time + 1f; }
            return;
        }
        if (dropped < Troops)
        {
            transform.position += Vector3.up * Mathf.Sin(Time.time * 2f) * 0.3f * Time.deltaTime;
            if (Time.time >= nextDrop)
            {
                nextDrop = Time.time + 1.2f; dropped++;
                Vector3 ground = transform.position + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                ground.y = 0.2f;
                GameBootstrap.SpawnDropTrooper(ground);
            }
            return;
        }
        // Done unloading: climb out
        transform.position += (exitDir * 20f + Vector3.up * 6f) * Time.deltaTime;
        if ((transform.position - hoverPoint).magnitude > 130f) Destroy(gameObject);
    }
}
