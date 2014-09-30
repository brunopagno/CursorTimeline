using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LOPCursor : MonoBehaviour {

    private Quaternion rotation;
    private Quaternion initialRotation;
    private bool calibrate = true;
    private Queue<Vector3> smoothingQueue = new Queue<Vector3>(16);

    public GameObject cursor;
    public Plane referencePlane;
    public int distance = 16;

    void Start() {
        referencePlane = new Plane(Vector3.up, Vector3.zero);

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
        }
    }

    private Vector3 MediumPosition() {
        float x = 0;
        float y = 0;
        foreach (Vector3 v in smoothingQueue.ToArray()) {
            x += v.x;
            y += v.y;
        }

        return new Vector3(x / smoothingQueue.Count, y / smoothingQueue.Count, 0);
    }

    #endregion

    #region TouchEvents

    private void OnTap(Touch touch) {
        OutMessage("Tap");
    }

    private void OnDoubleTap(Touch touch) {
        OutMessage("DoubleTap");
    }

    private void OnTouchMoved(Touch touch) {
        OutMessage("Move: " + touch.position);
    }

    private void OnUntap(Touch touch) {
        OutMessage("Untap");
    }

    #endregion

    #region Message Exchange

    [RPC]
    public void OutMessage(string message) {
        if (networkView.isMine) {
            networkView.RPC("InMessage", RPCMode.Others, message);
            Debug.Log("SERVER SENT: " + message);
        } else {
            networkView.RPC("InMessage", RPCMode.Server, message);
            Debug.Log("CLIENT SENT: " + message);
        }
    }

    [RPC]
    void InMessage(string message) {
        if (networkView.isMine) {
            Debug.Log("SERVER RECEIVED: " + message);
        } else {
            Debug.Log("CLIENT RECEIVED: " + message);
        }
    }

    #endregion

    void Update() {
        if (Network.isServer) { // ON SERVER
            // GYRO
            Vector3 direction = rotation * Vector3.down;
            Ray ray = new Ray(Vector3.up * distance, direction);
            float rayDistance = 0;
            if (referencePlane.Raycast(ray, out rayDistance)) {
                Vector3 point = ray.GetPoint(rayDistance);
                smoothingQueue.Enqueue(new Vector3(-point.x, -point.z, 0));
            }

            if (smoothingQueue.Count >= 8) {
                smoothingQueue.Dequeue();
            }

            cursor.transform.position = MediumPosition();
        } else { // ON CLIENT
            // TOUCH
            foreach (Touch touch in Input.touches) {
                switch (touch.phase) {
                    case TouchPhase.Began:
                        if (touch.tapCount > 1) {
                            OnDoubleTap(touch);
                        } else {
                            OnTap(touch);
                        }
                        break;
                    case TouchPhase.Moved:
                        OnTouchMoved(touch);
                        break;
                    case TouchPhase.Ended:
                        OnUntap(touch);
                        break;
                }
            }
        }
    }

    void OnGUI() {
        if (Network.isClient) {
            if (GUI.Button(new Rect(10, 150, 150, 100), "recalibrate")) {
                calibrate = true;
            }
        }
    }
}
