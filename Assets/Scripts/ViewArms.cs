using UnityEngine;

// Poses the first-person arms onto the current weapon: the right glove sits on the weapon's GripR, the left on GripL
// (empties inside each weapon model), and the forearms stretch from the bottom corners of the view to the gloves.
public static class ViewArms
{
    static readonly Vector3 ShoulderR = new Vector3(0.22f, -0.5f, -0.75f), ShoulderL = new Vector3(-0.45f, -0.5f, -0.6f);   // start behind the camera so the forearms enter from off-screen

    public static void Pose(Transform gun, Transform model)
    {
        PoseArm(gun, model, "R", model.Find("GripR"), ShoulderR);
        PoseArm(gun, model, "L", model.Find("GripL"), ShoulderL);
    }

    static void PoseArm(Transform gun, Transform model, string side, Transform grip, Vector3 shoulder)
    {
        var forearm = gun.Find("Forearm" + side); var glove = gun.Find("Glove" + side); var vambrace = gun.Find("Vambrace" + side);
        bool on = grip != null;
        if (forearm) forearm.gameObject.SetActive(on);
        if (glove) glove.gameObject.SetActive(on);
        if (vambrace) vambrace.gameObject.SetActive(on);
        if (!on) return;
        Vector3 hand = gun.InverseTransformPoint(grip.position);
        if (glove) { glove.localPosition = hand + new Vector3(side == "R" ? 0.04f : -0.04f, 0.0f, 0f); glove.localRotation = Quaternion.identity; glove.localScale = Vector3.one * 1.9f; }
        // the wrist sits just behind the glove
        Vector3 wrist = hand + new Vector3(side == "R" ? 0.05f : -0.05f, -0.01f, -0.08f);
        Vector3 d = wrist - shoulder;
        if (forearm)
        {
            forearm.localPosition = (shoulder + wrist) * 0.5f;
            forearm.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            forearm.localScale = new Vector3(0.072f, d.magnitude * 0.5f, 0.072f);
        }
        if (vambrace)
        {
            vambrace.localPosition = Vector3.Lerp(shoulder, wrist, 0.62f) + Vector3.up * 0.022f;
            vambrace.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            vambrace.localScale = new Vector3(0.1f, 0.06f, 0.22f);
        }
    }
}
