using UnityEngine;

// The UNSC Pillar of Autumn slips away over the yard at the start: Noble Six is alone. Placed in the scene as a prop.
public class Departure : MonoBehaviour
{
    public float lifetime = 45f;
    float born; Vector3 dir;
    void Start() { born = Time.time; dir = transform.forward; }
    void Update()
    {
        float t = Time.time - born;
        transform.position += dir * (6f + t * t * 0.35f) * Time.deltaTime;
        if (t > lifetime) Destroy(gameObject);
    }
}
