using UnityEngine;

public class RotationVectorProcessor {

    private static RotationVectorProcessor instance = new RotationVectorProcessor();

    private RotationVectorProcessor() { }

    public static RotationVectorProcessor GetInstance() {
        return instance;
    }

    public Quaternion CalculateOrientation() {
        return Quaternion.identity;
    }

}
