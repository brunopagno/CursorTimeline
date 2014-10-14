using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum CursorState {
    Normal,
    Focus
}

public class LOPCursor : MonoBehaviour {

    private Quaternion rotation;
    private Quaternion initialRotation;
    private bool calibrate;
    private Queue<Vector3> smoothingQueue = new Queue<Vector3>(16);
    private CursorState state;
    private CursorState State {
        get {
            return state;
        }
        set {
            state = value;
        }
    }

    public GameObject cursor;
    public Plane referencePlane;
    public int distance = 16;

    void Start() {
        calibrate = true;
        referencePlane = new Plane(Vector3.up, Vector3.zero);

        if (Network.isClient) {
            if (SystemInfo.supportsGyroscope) {
                Input.gyro.enabled = true;
                Input.compass.enabled = true;
            }
        }

        State = CursorState.Normal;
    }

    #region NetworkViewSync

    void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info) {
        if (stream.isWriting) {
            rotation = Input.gyro.attitude;
            stream.Serialize(ref rotation);
        } else {
            stream.Serialize(ref rotation);
            if (calibrate) {
                initialRotation = Quaternion.Inverse(rotation);
                if (initialRotation.x != 0 || initialRotation.y != 0 || initialRotation.z != 0 || initialRotation.w != 0) {
                    calibrate = false;
                }
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
        Focus(string.Empty);
    }

    private void OnDoubleTap(Touch touch) {
    }

    private void OnTouchMoved(Touch touch) {
    }

    private void OnUntap(Touch touch) {
        Unfocus(string.Empty);
    }

    #endregion

    #region Message Exchange

    [RPC]
    public void Calibrate(string message) {
        if (networkView.isMine) {
            networkView.RPC("Calibrate", RPCMode.Others, message);
        } else {
            calibrate = true;
        }
    }

    [RPC]
    public void Focus(string message) {
        if (networkView.isMine) {
            networkView.RPC("Focus", RPCMode.Others, message);
        } else {
            State = CursorState.Focus;
        }
    }

    [RPC]
    public void Unfocus(string message) {
        if (networkView.isMine) {
            networkView.RPC("Unocus", RPCMode.Others, message);
        } else {
            State = CursorState.Normal;
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
        if (networkView.isMine) {
            if (GUILayout.Button("Center cursor")) {
                Calibrate(string.Empty);
            }
        }
    }
}
