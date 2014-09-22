using UnityEngine;
using System.Collections;

public class LOPCursor : MonoBehaviour {

    private Quaternion rotation;
    private Quaternion initialRotation;
    private bool calibrate = true;

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
            }
        }
    }

    #region NetworkViewSync

    void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info) {
        if (stream.isWriting) {
            rotation = Input.gyro.attitude;
            stream.Serialize(ref rotation);
        } else {
            stream.Serialize(ref rotation);
            if (calibrate) {
                calibrate = false;
                initialRotation = Quaternion.Inverse(rotation);
            }
            rotation = initialRotation * rotation;
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
            GUILayout.Label("Attitude: " + Input.gyro.attitude);
        }
    }
}
