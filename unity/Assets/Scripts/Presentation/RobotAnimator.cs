using System.Collections.Generic;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>Marks a model-instance child with its POF submodel index.</summary>
    public class SubmodelTag : MonoBehaviour
    {
        public int Index;
    }

    /// <summary>
    /// Lerps submodel joints between the robot's five named poses
    /// (rest/alert/fire/recoil/flinch) like ai_frame_animation — constant
    /// approach rate, no timing data in the game files.
    /// </summary>
    public class RobotAnimator : MonoBehaviour
    {
        public Quaternion[][] Poses; // [state 0..4][submodel]
        public int TargetState;

        readonly List<(Transform transform, int index)> joints = new List<(Transform, int)>();

        void Start()
        {
            foreach (var tag in GetComponentsInChildren<SubmodelTag>())
                joints.Add((tag.transform, tag.Index));
        }

        void Update()
        {
            if (Poses == null || TargetState < 0 || TargetState >= Poses.Length)
                return;
            var pose = Poses[TargetState];
            float step = 5f * Time.deltaTime;
            foreach (var (t, index) in joints)
            {
                if (index <= 0 || index >= pose.Length) // submodel 0 (body) never animates
                    continue;
                t.localRotation = Quaternion.Slerp(t.localRotation, pose[index], step);
            }
        }
    }
}
