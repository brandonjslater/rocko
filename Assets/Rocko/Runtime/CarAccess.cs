using System;
using UnityEngine;

namespace Rocko
{
    public sealed class CarAccess : MonoBehaviour
    {
        public const int PlayerLayer = 30;
        public RockoVehicle car;
        public CarPanel driverDoor, passengerDoor, hood, hatch;
        public Collider seatTarget;
        public RockoIgnition ignition;

        //
        // @description - Require a stopped car, a player already inside at the seat, independently of door angle
        // @param {Vector3} feet - Walking player position
        // @return {bool} - Whether a seat interaction may begin
        //
        public bool CanEnter(Vector3 feet)
        {
            Vector3 local = transform.InverseTransformPoint(feet);
            return !car.occupied && car.SpeedMPH < 1.1f &&
                local.x > -0.67f && local.x < -0.08f && local.z > -0.58f && local.z < 0.15f &&
                local.y > 0.18f && local.y < 0.72f;
        }

        //
        // @description - Find the nearest visible interaction, respecting external walls and player reach
        // @param {Camera} view - First-person camera
        // @param {Transform} player - Walking character to ignore
        // @return {Collider} - Reachable panel or driver seat, otherwise null
        //
        public Collider FindTarget(Camera view, Transform player)
        {
            RaycastHit[] hits = Physics.RaycastAll(view.transform.position, view.transform.forward,
                2.3f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(player)) continue;
                Vector3 localEye = transform.InverseTransformPoint(view.transform.position);
                if (ignition != null && hit.collider == ignition.Target) return car.occupied ? hit.collider : null;
                if (hit.collider == seatTarget) return localEye.x < 0 ? seatTarget : null;
                CarPanel panel = hit.collider.GetComponentInParent<CarPanel>();
                if (panel != null && panel.transform.IsChildOf(transform))
                {
                    bool reachable = panel == driverDoor ? car.occupied || localEye.x < 0
                        : panel == passengerDoor ? car.occupied || localEye.x > 0
                        : panel == hood ? !car.occupied && localEye.z > 0.7f
                        : !car.occupied && localEye.z < -0.85f;
                    return reachable ? hit.collider : null;
                }
                // The simplified chassis encloses the cabin; it is not an interaction occluder.
                if (hit.collider.GetComponentInParent<RockoVehicle>() == car) continue;
                if (!hit.collider.isTrigger) return null;
            }
            return null;
        }

        //
        // @description - Identify the original vehicle-only hull and wheel colliders
        // @param {Collider} collider - Candidate collider
        // @return {bool} - Whether walking and posture checks should ignore it
        //
        public bool IsChassis(Collider collider)
        {
            return collider.transform == transform || collider is WheelCollider;
        }

        //
        // @description - Add independent kinematic walking collision so player contacts cannot push the dynamic car
        // @param {Transform} parent - Geometry parent
        // @param {Vector3} center - Local center
        // @param {Vector3} size - Box dimensions
        // @return {BoxCollider} - Walking collision shape
        //
        public static BoxCollider WalkBox(Transform parent, Vector3 center, Vector3 size)
        {
            GameObject box = new GameObject("Walking collision");
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            Rigidbody proxy = box.AddComponent<Rigidbody>();
            proxy.isKinematic = true; proxy.useGravity = false;
            BoxCollider collider = box.AddComponent<BoxCollider>();
            collider.center = Vector3.zero; collider.size = size;
            collider.excludeLayers = ~(1 << PlayerLayer);
            collider.layerOverridePriority = 10;
            return collider;
        }

        //
        // @description - Close all panels when recovering or staging the vehicle
        // @return {Void} - Restores all four closed poses
        //
        public void ResetClosed()
        {
            driverDoor.ResetClosed(); passengerDoor.ResetClosed(); hood.ResetClosed(); hatch.ResetClosed();
        }
    }
}
