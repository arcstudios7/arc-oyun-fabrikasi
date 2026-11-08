using UnityEngine;

namespace Game
{
    /// <summary>
    /// Lives on the courier's Animator object. While riding, Unity's humanoid IK pulls the hands onto the handlebar grips
    /// and the feet onto the footboard, so any seated Mixamo pose looks like riding this bike.
    /// </summary>
    public class RideIK : MonoBehaviour
    {
        public Animator Anim;
        public Transform LeftHand, RightHand, LeftFoot, RightFoot;
        public bool Active;
        float weight;

        void Update()
        {
            weight = Mathf.MoveTowards(weight, Active ? 1f : 0f, Time.deltaTime * 4f);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (Anim == null) return;
            Goal(AvatarIKGoal.LeftHand, LeftHand);
            Goal(AvatarIKGoal.RightHand, RightHand);
            Goal(AvatarIKGoal.LeftFoot, LeftFoot);
            Goal(AvatarIKGoal.RightFoot, RightFoot);
        }

        void Goal(AvatarIKGoal goal, Transform target)
        {
            float w = target != null ? weight : 0f;
            Anim.SetIKPositionWeight(goal, w);
            if (target != null && w > 0f) Anim.SetIKPosition(goal, target.position);
        }
    }
}
