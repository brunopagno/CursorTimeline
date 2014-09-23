using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LOPCursor : MonoBehaviour {

    private Quaternion rotation;
    private Quaternion initialRotation;
    private bool calibrate = true;
    private Queue<Vector3> smoothingQueue = new Queue<Vector3>(16);

    public GameObject rotationMaster;

    public GameObject cursorProj;
    public Plane referencePlaneHorizontal;
    public Plane referencePlaneVertical;
    public int distance = 15;

    private string tapsandstuff = "";
    private string tapsandstuff2 = "";

    void Start() {
        referencePlaneHorizontal = new Plane(Vector3.down, Vector3.zero);
        referencePlaneVertical = new Plane(Vector3.back, Vector3.zero);

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
        tapsandstuff = "tap";
    }

    private void OnDoubleTap(Touch touch) {
        tapsandstuff += " double-tap ";
    }

    private void OnTouchMoved(Touch touch) {
        tapsandstuff2 = touch.position.ToString();
    }

    private void OnUntap(Touch touch) {
        tapsandstuff += " untap ";
    }

    #endregion

    void Update() {
        if (Network.isServer) {
            // TOUCH
            foreach (Touch touch in Input.touches) {
                switch (touch.phase) {
                    case TouchPhase.Began:
                        //bool didDoubleTap;
                        //touching = true;
                        //if (doubleTapTime > 0 && doubleTapTime < 0.3f) {
                        //    doubleTapTime = 0;
                        //    didDoubleTap = true;
                        //    OnDoubleTap(touch);
                        //}
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

            // GYRO
            Vector3 direction = rotation * Vector3.up;
            Ray ray = new Ray(Vector3.down * distance, direction);
            float rayDistance = 0;
            float x = 0;
            if (referencePlaneHorizontal.Raycast(ray, out rayDistance)) {
                x = ray.GetPoint(rayDistance).x;
            }

            direction = rotation * Vector3.forward;
            ray = new Ray(Vector3.back * distance, direction);
            rayDistance = 0;
            float y = 0;
            if (referencePlaneVertical.Raycast(ray, out rayDistance)) {
                y = -ray.GetPoint(rayDistance).y;
            }

            smoothingQueue.Enqueue(new Vector3(x, y, 0));
            if (smoothingQueue.Count >= 8) {
                smoothingQueue.Dequeue();
            }

            cursorProj.transform.position = MediumPosition();
        }
    }

    void OnGUI() {
        if (Network.isClient) {
            if (GUI.Button(new Rect(10, 150, 150, 100), "recalibrate")) {
                calibrate = true;
            }
            GUI.Label(new Rect(10, 270, 150, 40), tapsandstuff);
            GUI.Label(new Rect(10, 310, 150, 40), tapsandstuff2);
        }
    }
}
