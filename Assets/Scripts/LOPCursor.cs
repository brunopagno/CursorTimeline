﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum CursorState {
    Normal,
    Focus
}

public class LOPCursor : MonoBehaviour {

    private Quaternion rotation;
    private Quaternion initialRotation;
    private Plane referencePlane;
    private int distance = 16;

    private Vector3 position = Vector3.zero;
    public Vector3 Position {
        get {
            return position;
        }
        set {
            // goes from -1 to 1
            position.Set(((value.x / Screen.width) - 0.5f) * 2, ((value.y / Screen.height) - 0.5f) * 2, 0);
        }
    }

    private float zoomFactor = 0.5f;
    private bool calibrate;
    private Queue<Vector3> smoothingCursor = new Queue<Vector3>(16);
    private Queue<Vector3> smoothingPosition = new Queue<Vector3>(16);
    private CursorState state;
    private Vector2 actionArea;

    public GameObject cursor;
    public GameObject cursorArea;

    private float fadeSpeed = .5f;
    private float fadeTime = 1.5f;

    //private string screenMessage;

    void Start() {
        calibrate = true;
        referencePlane = new Plane(Vector3.up, Vector3.zero);

        if (Network.isClient) {
            if (SystemInfo.supportsGyroscope) {
                Input.gyro.enabled = true;
                Input.compass.enabled = true;
            }

            SetupScreenSize(string.Empty);
        }

        state = CursorState.Normal;
    }

    #region NetworkViewSync

    void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info) {
        if (stream.isWriting) {
            rotation = Input.gyro.attitude;
            stream.Serialize(ref rotation);
            stream.Serialize(ref position);
        } else {
            stream.Serialize(ref rotation);
            stream.Serialize(ref position);
            if (calibrate) {
                initialRotation = Quaternion.Inverse(rotation);
                if (initialRotation.x != 0 || initialRotation.y != 0 || initialRotation.z != 0 || initialRotation.w != 0) {
                    calibrate = false;
                }
            }
            rotation = initialRotation * rotation;
        }
    }

    #endregion

    #region TouchEvents

    private void OnTap(Touch touch) {
        state = CursorState.Focus;
        Focus(string.Empty);
    }

    private void OnDoubleTap(Touch touch) {
    }

    private void OnTouchMoved(Touch touch) {
        if (state == CursorState.Focus) {
            this.Position = touch.position;
        }
    }

    private void OnUntap(Touch touch) {
        state = CursorState.Normal;
        Unfocus(string.Empty);
    }

    #endregion

    #region Message Exchange

    [RPC]
    public void SetupScreenSize(string message) {
        if (networkView.isMine) {
            networkView.RPC("SetupScreenSize", RPCMode.Others, "" + Screen.width + "," + Screen.height);
            actionArea = new Vector2(2, 2); // whatever, just make sure it's not null
        } else {
            string[] resolution = message.Split(',');
            float width = 0;
            float height = 0;
            if (float.TryParse(resolution[0], out width) && float.TryParse(resolution[1], out height)) {
                float proportion = width + height;
                actionArea = new Vector2(width / proportion, height / proportion) * 4;
            } else {
                actionArea = new Vector2(2, 2);
            }
            cursorArea.transform.localScale = new Vector3(width / actionArea.x, height / actionArea.y, 1);
            Debug.Log("action_area: " + actionArea);
            Debug.Log("local_scale: " + cursorArea.transform.localScale);
        }
    }

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
            state = CursorState.Focus;
        }
    }

    [RPC]
    public void Unfocus(string message) {
        if (networkView.isMine) {
            networkView.RPC("Unfocus", RPCMode.Others, message);
        } else {
            state = CursorState.Normal;
        }
    }

    #endregion

    void Update() {
        if (Network.isServer) { // ON SERVER
            if (state == CursorState.Normal) {
                Vector3 direction = rotation * Vector3.down;
                Ray ray = new Ray(Vector3.up * distance, direction);
                float rayDistance = 0;
                if (referencePlane.Raycast(ray, out rayDistance)) {
                    Vector3 point = ray.GetPoint(rayDistance);
                    smoothingCursor.Enqueue(new Vector3(-point.x, -point.z, 0));
                }

                if (smoothingCursor.Count >= 8) {
                    smoothingCursor.Dequeue();
                }
            }

            transform.position = SmoothFilter(smoothingCursor.ToArray());

            if (state == CursorState.Focus) {
                smoothingPosition.Enqueue(new Vector3(Position.x * actionArea.x, Position.y * actionArea.y, 0));

                if (smoothingPosition.Count >= 8) {
                    smoothingPosition.Dequeue();
                }
                cursor.transform.position = transform.position + SmoothFilter(smoothingPosition.ToArray());
            }

            // cursor area animation
            if (state == CursorState.Normal) {
                float fade = Mathf.SmoothDamp(0f, 1f, ref fadeSpeed, fadeTime);
                cursorArea.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, fade);
            }
            if (state == CursorState.Focus) {
                float fade = Mathf.SmoothDamp(1f, 0f, ref fadeSpeed, fadeTime);
                cursorArea.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, fade);
            }
        } else { // ON CLIENT
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
            if (Input.touchCount == 2) {
                Touch tZero = Input.GetTouch(0);
                Touch tOne = Input.GetTouch(1);

                Vector2 tZeroPrevPos = tZero.position - tZero.deltaPosition;
                Vector2 tOnePrevPos = tOne.position - tOne.deltaPosition;

                float prevDelta = (tZeroPrevPos - tOnePrevPos).magnitude;
                float curDelta = (tZero.position - tOne.position).magnitude;

                float deltaMagnitudeDiff = prevDelta - curDelta;
                Bounds bounds = cursorArea.GetComponent<SpriteRenderer>().sprite.bounds;
                actionArea.Set(actionArea.x + zoomFactor, actionArea.y + zoomFactor);
                cursorArea.transform.localScale.Set(actionArea.x / bounds.size.x, actionArea.y / bounds.size.y, 1);
            }
        }
    }

    private Vector3 SmoothFilter(Vector3[] array) {
        float x = 0;
        float y = 0;
        foreach (Vector3 v in array) {
            x += v.x;
            y += v.y;
        }

        return new Vector3(x / array.Length, y / array.Length, 0);
    }

    void OnGUI() {
        if (networkView.isMine) {
            if (GUILayout.Button("Center cursor")) {
                Calibrate(string.Empty);
            }
            //GUI.Label(new Rect(10, 280, 444, 333), screenMessage);
        }
    }
}
