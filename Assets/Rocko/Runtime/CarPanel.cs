using UnityEngine;

namespace Rocko
{
    public sealed class CarPanel : MonoBehaviour
    {
        public string Label { get; private set; }
        public bool IsDoor { get { return Label != null && Label.Contains("door"); } }
        public bool IsOpen { get { return fraction > 0.01f; } }
        public bool ClearForEntry { get { return fraction > 0.6f; } }
        public float OpenFraction { get { return fraction; } }
        public bool Grabbed { get; private set; }
        private Vector3 axis, lever;
        private float angle, fraction, velocity, idleHold;
        private bool targetOpen;
        private BoxCollider walking;

        //
        // @description - Build a hinged panel with independent walking collision for doors
        // @param {Transform} root - Geometry parent
        // @param {Vector3} pivot - Hinge position in car coordinates
        // @param {Vector3} rotationAxis - Local hinge axis
        // @param {float} degrees - Signed maximum opening angle
        // @param {string} label - Interaction name
        // @param {Vector3} center - Closed target center
        // @param {Vector3} size - Target dimensions
        // @return {CarPanel} - Bounded hinge with no force coupling to the vehicle
        //
        public static CarPanel Create(Transform root, Vector3 pivot, Vector3 rotationAxis,
            float degrees, string label, Vector3 center, Vector3 size)
        {
            foreach (Transform child in root) child.localPosition -= pivot;
            root.localPosition = pivot;
            CarPanel panel = root.gameObject.AddComponent<CarPanel>();
            panel.Label = label; panel.axis = rotationAxis; panel.angle = degrees; panel.lever = center - pivot;
            BoxCollider target = root.gameObject.AddComponent<BoxCollider>();
            target.isTrigger = true; target.center = center - pivot; target.size = size;
            if (panel.IsDoor) panel.walking = CarAccess.WalkBox(root, center - pivot, size);
            return panel;
        }

        //
        // @description - Set a test/reset door pose or request a hood/hatch opening animation
        // @param {bool} open - Desired fully open state
        // @return {Void} - Applies the requested panel pose
        //
        public void SetOpen(bool open)
        {
            targetOpen = open;
            if (IsDoor) { fraction = open ? 1 : 0; velocity = 0; ApplyPose(); }
        }

        //
        // @description - Take hold of a door; pull its latch free with a small opening impulse
        // @return {Void} - Captures this door until release
        //
        public void BeginGrab()
        {
            Grabbed = true; idleHold = 0;
            if (fraction < 0.02f) velocity = 0.7f;
        }

        //
        // @description - Drag with camera movement or close by holding still
        // @param {Vector3} cameraRight - Camera horizontal direction
        // @param {float} look - Horizontal look movement this frame
        // @param {float} dt - Scaled frame duration
        // @return {Void} - Changes angular velocity without selecting a fixed open state
        //
        public void Drag(Vector3 cameraRight, float look, float dt)
        {
            if (!Grabbed || dt <= 0) return;
            if (Mathf.Abs(look) > 0.03f)
            {
                idleHold = 0;
                Vector3 tangent = transform.TransformDirection(Vector3.Cross(axis, lever)) * Mathf.Sign(angle);
                float direction = Mathf.Sign(Vector3.Dot(tangent, cameraRight));
                velocity = Mathf.Clamp(look * direction / (Mathf.Abs(angle) * dt), -2.5f, 2.5f);
            }
            else
            {
                idleHold += dt;
                if (idleHold > 0.4f) velocity = -0.65f;
            }
        }

        //
        // @description - Let go while preserving the door's remaining momentum
        // @return {Void} - Releases the grab
        //
        public void EndGrab() { Grabbed = false; idleHold = 0; }

        //
        // @description - Restore the closed latch and stop panel movement during reset
        // @return {Void} - Clears all hinge motion
        //
        public void ResetClosed()
        {
            fraction = velocity = idleHold = 0; targetOpen = Grabbed = false; ApplyPose();
        }

        //
        // @description - Integrate bounded door momentum and gravity; hood and hatch retain assisted travel
        // @return {Void} - Updates the hinge without pushing the player or transferring torque to the car
        //
        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float previous = fraction;
            if (IsDoor)
            {
                if (fraction > 0.01f && !Grabbed)
                {
                    float gravity = Vector3.Dot(Vector3.Cross(transform.TransformDirection(lever), Vector3.down),
                        transform.TransformDirection(axis)) * Mathf.Sign(angle);
                    velocity += gravity * dt;
                }
                fraction = Mathf.Clamp01(fraction + velocity * dt);
                if ((fraction == 0 && velocity < 0) || (fraction == 1 && velocity > 0)) velocity = 0;
                velocity *= Mathf.Exp(-2.2f * dt);
            }
            else fraction = Mathf.MoveTowards(fraction, targetOpen ? 1 : 0, dt / 0.65f);
            ApplyPose();
            if (walking != null && Physics.CheckBox(walking.transform.TransformPoint(walking.center),
                walking.size * 0.48f, walking.transform.rotation, 1 << CarAccess.PlayerLayer, QueryTriggerInteraction.Ignore))
            {
                fraction = previous; velocity = 0; ApplyPose();
            }
        }

        //
        // @description - Apply the continuous hinge angle to geometry and walking collision
        // @return {Void} - Updates local rotation
        //
        private void ApplyPose() { transform.localRotation = Quaternion.AngleAxis(angle * fraction, axis); }
    }
}
