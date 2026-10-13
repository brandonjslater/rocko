using UnityEngine;

namespace Rocko
{
    public sealed class CarPanel : MonoBehaviour
    {
        public string Label { get; private set; }
        public bool IsOpen { get; private set; }
        public bool ClearForEntry { get { return IsOpen && fraction > 0.85f; } }
        private Vector3 axis;
        private float angle, fraction;

        //
        // @description - Place a hinge without moving its assembled geometry and add a query-only target
        // @param {Transform} root - Panel geometry parent in car coordinates
        // @param {Vector3} pivot - Hinge location in car coordinates
        // @param {Vector3} rotationAxis - Local hinge axis
        // @param {float} degrees - Signed open angle
        // @param {string} label - Interaction name
        // @param {Vector3} center - Closed target center in car coordinates
        // @param {Vector3} size - Target dimensions
        // @return {CarPanel} - Animated panel; no additional chassis collision or mass
        //
        public static CarPanel Create(Transform root, Vector3 pivot, Vector3 rotationAxis,
            float degrees, string label, Vector3 center, Vector3 size)
        {
            foreach (Transform child in root) child.localPosition -= pivot;
            root.localPosition = pivot;
            CarPanel panel = root.gameObject.AddComponent<CarPanel>();
            panel.Label = label; panel.axis = rotationAxis; panel.angle = degrees;
            BoxCollider target = root.gameObject.AddComponent<BoxCollider>();
            target.isTrigger = true; target.center = center - pivot; target.size = size;
            if (label.Contains("door"))
                CarAccess.WalkBox(root, center - pivot, size);
            return panel;
        }

        //
        // @description - Request opening or closing the hinged panel
        // @param {bool} open - Desired state
        // @return {Void} - Starts or reverses the animation
        //
        public void SetOpen(bool open) { IsOpen = open; }

        //
        // @description - Restore a latched panel during vehicle reset
        // @return {Void} - Immediately closes the panel
        //
        public void ResetClosed()
        {
            IsOpen = false; fraction = 0; transform.localRotation = Quaternion.identity;
        }

        //
        // @description - Animate panel travel with a gentle start and stop
        // @return {Void} - Updates the visual hinge and interaction volume
        //
        private void Update()
        {
            fraction = Mathf.MoveTowards(fraction, IsOpen ? 1 : 0, Time.deltaTime / 0.65f);
            transform.localRotation = Quaternion.AngleAxis(angle * Mathf.SmoothStep(0, 1, fraction), axis);
        }
    }
}
