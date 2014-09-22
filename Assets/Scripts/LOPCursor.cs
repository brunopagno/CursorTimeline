using UnityEngine;
using System.Collections;

public class LOPCursor : MonoBehaviour {

    private Quaternion rotation;
    private Quaternion initialRotation;

    public GameObject rotationMaster;

    public GameObject cursorProj;
    public Plane referencePlane;
    public int distance = 15;

    void Start() {
        referencePlane = new Plane(Vector3.back, Vector3.zero);

        if (Network.isClient) {
            if (SystemInfo.supportsGyroscope) {
                Input.gyro.enabled = true;
                Input.compass.enabled = true;
                initialRotation = Quaternion.Inverse(Input.gyro.attitude);
            }
        }
    }

    #region NetworkViewSync

    void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info) {
        if (stream.isWriting) {
            rotation = Input.gyro.attitude * initialRotation;
            stream.Serialize(ref rotation);
        } else {
            stream.Serialize(ref rotation);
            rotationMaster.transform.rotation = rotation;
        }
    }

    #endregion

    void Update() {
        if (Network.isServer) {
            Vector3 direction = rotation * Vector3.forward;
            Ray ray = new Ray(Vector3.back * distance, direction);
            float rayDistance = 0;
            if (referencePlane.Raycast(ray, out rayDistance)) {
                Vector3 result = ray.GetPoint(rayDistance);
                cursorProj.transform.position = new Vector3(result.x, result.y, 0);
            }
        }
    }

    void OnGUI() {
        if (Network.isClient) {
            GUILayout.Label("");
            GUILayout.Label("Mag: " + Input.compass.rawVector);
            GUILayout.Label("Acc: " + Input.acceleration);
            GUILayout.Label("Gyro " + Input.gyro.rotationRate);
            GUILayout.Label("Rotation " + rotation);
        }
    }
}
