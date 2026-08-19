// Rig setup for LegSolver — strips the joint drives so the solver is the only muscle.
//
// A ConfigurableJoint drive and LegSolver's pose torque both try to hold a bone at a target
// rotation. Run both and they fight, and the drive wins: it is a constraint solved in acceleration
// units with unlimited force, while the solver only gets to add a torque. So the joints keep the
// skeleton together and enforce the angle limits, and nothing else.
using UnityEditor;
using UnityEngine;

public class JointSetup : EditorWindow
{
    // per-axis ranges in degrees. X is the primary axis (ConfigurableJoint.axis), which on these
    // rigs is the bend axis — bones run down local -Z, so local X is the hinge.
    [SerializeField] private Vector2 bendLimit = new Vector2(-60f, 60f);
    [SerializeField] private float twistLimit = 8f;     // angular Y
    [SerializeField] private float splayLimit = 8f;     // angular Z
    [SerializeField] private bool lockLinear = true;
    [SerializeField] private bool clearDrives = true;
    [SerializeField] private bool disableProjection = true;

    [MenuItem("Tools/Procedural/Configure Rig Joints")]
    static void Open()
    {
        GetWindow<JointSetup>(true, "Configure Rig Joints").minSize = new Vector2(340f, 260f);
    }

    void OnGUI()
    {
        GameObject target = Selection.activeGameObject;

        EditorGUILayout.HelpBox(
            "Select a rig root, then apply. Every ConfigurableJoint underneath becomes structural " +
            "only: limits are enforced, drives are off, so LegSolver's torque is the sole actuator.",
            MessageType.Info);

        EditorGUILayout.Space();
        bendLimit = EditorGUILayout.Vector2Field("Bend Limit (angular X)", bendLimit);
        twistLimit = EditorGUILayout.FloatField("Twist Limit (angular Y)", twistLimit);
        splayLimit = EditorGUILayout.FloatField("Splay Limit (angular Z)", splayLimit);

        EditorGUILayout.Space();
        lockLinear = EditorGUILayout.Toggle("Lock Linear Motion", lockLinear);
        clearDrives = EditorGUILayout.Toggle("Clear Drives", clearDrives);
        disableProjection = EditorGUILayout.Toggle("Disable Projection", disableProjection);

        EditorGUILayout.Space();
        if (target == null)
        {
            EditorGUILayout.LabelField("Nothing selected.");
            return;
        }

        ConfigurableJoint[] joints = target.GetComponentsInChildren<ConfigurableJoint>(true);
        EditorGUILayout.LabelField($"{target.name}: {joints.Length} joint(s)");

        using (new EditorGUI.DisabledScope(joints.Length == 0))
        {
            if (GUILayout.Button("Apply")) Apply(joints);
        }
    }

    void Apply(ConfigurableJoint[] joints)
    {
        Undo.RecordObjects(joints, "Configure Rig Joints");

        foreach (ConfigurableJoint joint in joints)
        {
            if (lockLinear)
            {
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
            }

            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            joint.lowAngularXLimit = Limit(joint.lowAngularXLimit, Mathf.Min(bendLimit.x, bendLimit.y));
            joint.highAngularXLimit = Limit(joint.highAngularXLimit, Mathf.Max(bendLimit.x, bendLimit.y));
            joint.angularYLimit = Limit(joint.angularYLimit, Mathf.Abs(twistLimit));
            joint.angularZLimit = Limit(joint.angularZLimit, Mathf.Abs(splayLimit));

            if (clearDrives)
            {
                // spring 0 / damper 0 is what actually hands control to LegSolver
                joint.angularXDrive = Off(joint.angularXDrive);
                joint.angularYZDrive = Off(joint.angularYZDrive);
                joint.slerpDrive = Off(joint.slerpDrive);
                joint.targetRotation = Quaternion.identity;
                joint.targetAngularVelocity = Vector3.zero;
            }

            if (disableProjection) joint.projectionMode = JointProjectionMode.None;

            PrefabUtility.RecordPrefabInstancePropertyModifications(joint);
        }

        Debug.Log($"Configured {joints.Length} joint(s) as structural-only.", Selection.activeGameObject);
    }

    static SoftJointLimit Limit(SoftJointLimit limit, float value)
    {
        limit.limit = value;
        return limit;
    }

    static JointDrive Off(JointDrive drive)
    {
        drive.positionSpring = 0f;
        drive.positionDamper = 0f;
        return drive;
    }
}
